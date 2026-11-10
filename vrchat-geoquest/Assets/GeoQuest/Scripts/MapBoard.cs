using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.SDK3.Persistence;
using TMPro;

/// <summary>
/// The guessing map. Point at it (VR: controller laser, desktop: screen centre)
/// and pull the trigger / click to drop your pin. Zoom and pan with the buttons.
/// The map texture is a linear lat/lon box: u = (lon-minLon)/(maxLon-minLon), same for v.
/// Defaults cover North Carolina; set -180/180/-90/90 for a world map.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MapBoard : UdonSharpBehaviour
{
    public GeoGameManager game;
    public Renderer mapRenderer;          // quad, local size 1x1, scaled to width x height
    public Collider mapCollider;          // box collider on that quad
    public Transform pinRoot;             // unscaled, same pose as the quad
    public float width = 2f, height = 1f;

    [Header("Area shown by the map texture")]
    public float minLon = -84.7f, maxLon = -75.1f, minLat = 33.25f, maxLat = 37.15f;

    public Transform myPin, answerPin, hoverDot;
    public Transform[] otherPins;
    public TextMeshPro[] otherLabels;
    public LineRenderer myLine;
    public LineRenderer[] otherLines;
    public LineRenderer laser;
    public TextMeshPro zoomText;

    [Tooltip("Rotation applied to the VR hand's tracking rotation to get the pointing direction.")]
    public Vector3 handPointerEuler = new Vector3(0f, 0f, 0f);
    [Tooltip("Red target shown while calibrating the laser (Fix laser aim button).")]
    public GameObject calibrationTarget;
    [Tooltip("Small text that shows the calibrated aim angles, so they can be made the default.")]
    public TextMeshPro aimReadout;

    private Quaternion _aimR = Quaternion.identity, _aimL = Quaternion.identity;
    private bool _calibrating;
    private float _calibrateFrom;
    private const string AimKey = "geoquest.aim.";
    public float maxPointerDistance = 6f;

    [HideInInspector] public bool hasPin;
    [HideInInspector] public float pinLat, pinLon;

    private float _zoom = 1f, _cu = 0.5f, _cv = 0.5f;
    private Material _mat;
    private bool _answerShown;
    private float _answerLat, _answerLon;
    private float[] _otherLat, _otherLon;
    private bool[] _otherOn;
    private bool _useLeftHand;
    private bool _hovering;
    private Vector2 _hoverUv;
    private int _mask;

    void Start()
    {
        SetAim(Quaternion.Euler(handPointerEuler));
        if (calibrationTarget != null) calibrationTarget.SetActive(false);
        _mat = mapRenderer.material;
        int n = otherPins.Length;
        _otherLat = new float[n];
        _otherLon = new float[n];
        _otherOn = new bool[n];
        _mask = ~((1 << 9) | (1 << 10) | (1 << 18)); // ignore Player, PlayerLocal, MirrorReflection
        ApplyView();
    }

    // ---------------------------------------------------------------- pointer

    void Update()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return;
        Vector3 origin, dir;
        GetRay(lp, _useLeftHand, out origin, out dir);
        _hovering = Raycast(origin, dir, out _hoverUv);
        bool canPlace = game != null && game.CanPlacePin();

        if (hoverDot != null)
        {
            hoverDot.gameObject.SetActive(_hovering && canPlace);
            if (_hovering) hoverDot.localPosition = UvToLocal(_hoverUv, -0.012f);
        }
        if (laser != null)
        {
            bool show = lp.IsUserInVR() && _hovering && canPlace;
            laser.enabled = show;
            if (show)
            {
                laser.SetPosition(0, origin);
                laser.SetPosition(1, pinRoot.TransformPoint(UvToLocal(_hoverUv, -0.012f)));
            }
        }
    }

    // ---------------------------------------------------------------- laser calibration

    /// <summary>Right-hand aim offset; the left hand uses its mirror image.</summary>
    private void SetAim(Quaternion right)
    {
        if (aimReadout != null)
        {
            Vector3 e = right.eulerAngles;
            aimReadout.text = "aim " + Mathf.RoundToInt(e.x) + ", " + Mathf.RoundToInt(e.y) + ", " + Mathf.RoundToInt(e.z);
        }
        _aimR = right;
        _aimL = new Quaternion(right.x, -right.y, -right.z, right.w);
    }

    /// <summary>"Fix laser aim" button: point at the red target and pull the trigger.</summary>
    public void StartCalibration()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null || !lp.IsUserInVR() || calibrationTarget == null) return;
        _calibrating = !_calibrating;
        _calibrateFrom = Time.time + 0.6f;   // ignore the trigger pull that pressed the button
        calibrationTarget.SetActive(_calibrating);
    }

    private void FinishCalibration(VRCPlayerApi lp, bool left)
    {
        VRCPlayerApi.TrackingData td = lp.GetTrackingData(left
            ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand);
        Vector3 want = calibrationTarget.transform.position - td.position;
        if (want.sqrMagnitude < 0.0001f) return;
        // The offset that turns this controller's tracked rotation into "pointing at the target".
        Quaternion off = Quaternion.Inverse(td.rotation) * Quaternion.LookRotation(want.normalized, td.rotation * Vector3.up);
        if (left) off = new Quaternion(off.x, -off.y, -off.z, off.w);
        SetAim(off);
        PlayerData.SetFloat(AimKey + "x", off.x);
        PlayerData.SetFloat(AimKey + "y", off.y);
        PlayerData.SetFloat(AimKey + "z", off.z);
        PlayerData.SetFloat(AimKey + "w", off.w);
        _calibrating = false;
        calibrationTarget.SetActive(false);
    }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        float x, y, z, w;
        if (PlayerData.TryGetFloat(player, AimKey + "x", out x) && PlayerData.TryGetFloat(player, AimKey + "y", out y) &&
            PlayerData.TryGetFloat(player, AimKey + "z", out z) && PlayerData.TryGetFloat(player, AimKey + "w", out w) &&
            (x * x + y * y + z * z + w * w) > 0.5f)
            SetAim(new Quaternion(x, y, z, w));
    }

    public override void InputUse(bool value, UdonInputEventArgs args)
    {
        if (!value) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (_calibrating && lp != null && lp.IsUserInVR())
        {
            if (Time.time >= _calibrateFrom) FinishCalibration(lp, args.handType == HandType.LEFT);
            return;
        }
        if (lp == null || game == null || !game.CanPlacePin()) return;
        if (lp.IsUserInVR()) _useLeftHand = args.handType == HandType.LEFT;
        Vector3 origin, dir;
        GetRay(lp, _useLeftHand, out origin, out dir);
        Vector2 uv;
        if (!Raycast(origin, dir, out uv)) return;
        Vector2 tex = ViewToTex(uv);
        pinLon = minLon + tex.x * (maxLon - minLon);
        pinLat = minLat + tex.y * (maxLat - minLat);
        hasPin = true;
        RefreshPins();
        game.OnPinPlaced();
    }

    private void GetRay(VRCPlayerApi lp, bool left, out Vector3 origin, out Vector3 dir)
    {
        if (lp.IsUserInVR())
        {
            VRCPlayerApi.TrackingData td = lp.GetTrackingData(left
                ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand);
            origin = td.position;
            dir = td.rotation * (left ? _aimL : _aimR) * Vector3.forward;
        }
        else
        {
            VRCPlayerApi.TrackingData td = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            origin = td.position;
            dir = td.rotation * Vector3.forward;
        }
    }

    private bool Raycast(Vector3 origin, Vector3 dir, out Vector2 uv)
    {
        uv = Vector2.zero;
        RaycastHit hit;
        if (!Physics.Raycast(origin, dir, out hit, maxPointerDistance, _mask, QueryTriggerInteraction.Collide)) return false;
        if (hit.collider != mapCollider) return false;
        Vector3 p = pinRoot.InverseTransformPoint(hit.point);
        uv = new Vector2(p.x / width + 0.5f, p.y / height + 0.5f);
        return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
    }

    /// <summary>Set your pin from somewhere else (e.g. a pin dropped on the floor map).</summary>
    public void PlacePinAt(float lat, float lon)
    {
        if (game == null || !game.CanPlacePin()) return;
        pinLat = Mathf.Clamp(lat, minLat, maxLat);
        pinLon = Mathf.Clamp(lon, minLon, maxLon);
        hasPin = true;
        RefreshPins();
        game.OnPinPlaced();
    }

    // ---------------------------------------------------------------- view maths

    private Vector2 ViewToTex(Vector2 uv)
    {
        return new Vector2(_cu + (uv.x - 0.5f) / _zoom, _cv + (uv.y - 0.5f) / _zoom);
    }

    private Vector2 LatLonToView(float lat, float lon)
    {
        float tu = (lon - minLon) / (maxLon - minLon), tv = (lat - minLat) / (maxLat - minLat);
        return new Vector2((tu - _cu) * _zoom + 0.5f, (tv - _cv) * _zoom + 0.5f);
    }

    private Vector3 UvToLocal(Vector2 uv, float z)
    {
        return new Vector3((uv.x - 0.5f) * width, (uv.y - 0.5f) * height, z);
    }

    private bool Place(Transform t, float lat, float lon, bool on)
    {
        if (t == null) return false;
        Vector2 uv = LatLonToView(lat, lon);
        bool vis = on && uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        t.gameObject.SetActive(vis);
        if (vis) t.localPosition = UvToLocal(uv, -0.015f);
        return vis;
    }

    private void Line(LineRenderer l, bool on, float lat, float lon)
    {
        if (l == null) return;
        bool show = on && _answerShown;
        if (show)
        {
            Vector2 a = LatLonToView(lat, lon), b = LatLonToView(_answerLat, _answerLon);
            a = new Vector2(Mathf.Clamp01(a.x), Mathf.Clamp01(a.y));
            b = new Vector2(Mathf.Clamp01(b.x), Mathf.Clamp01(b.y));
            l.SetPosition(0, UvToLocal(a, -0.01f));
            l.SetPosition(1, UvToLocal(b, -0.01f));
        }
        l.enabled = show;
    }

    private void ApplyView()
    {
        float half = 0.5f / _zoom;
        _cu = Mathf.Clamp(_cu, half, 1f - half);
        _cv = Mathf.Clamp(_cv, half, 1f - half);
        if (_mat != null)
        {
            _mat.mainTextureScale = new Vector2(1f / _zoom, 1f / _zoom);
            _mat.mainTextureOffset = new Vector2(_cu - half, _cv - half);
        }
        if (zoomText != null) zoomText.text = "x" + Mathf.RoundToInt(_zoom);
        RefreshPins();
    }

    public void RefreshPins()
    {
        Place(myPin, pinLat, pinLon, hasPin);
        Line(myLine, hasPin, pinLat, pinLon);
        Place(answerPin, _answerLat, _answerLon, _answerShown);
        if (_otherOn == null) return;
        for (int i = 0; i < otherPins.Length; i++)
        {
            bool vis = Place(otherPins[i], _otherLat[i], _otherLon[i], _otherOn[i]);
            if (otherLabels != null && i < otherLabels.Length && otherLabels[i] != null)
                otherLabels[i].gameObject.SetActive(vis);
            if (otherLines != null && i < otherLines.Length) Line(otherLines[i], _otherOn[i], _otherLat[i], _otherLon[i]);
        }
    }

    // ---------------------------------------------------------------- buttons

    public void ZoomIn()
    {
        if (_zoom >= 32f) return;
        if (hasPin && !_answerShown) CenterOn(pinLat, pinLon);
        _zoom *= 2f;
        ApplyView();
    }

    public void ZoomOut()
    {
        if (_zoom <= 1f) return;
        _zoom *= 0.5f;
        ApplyView();
    }

    public void PanLeft() { _cu -= 0.25f / _zoom; ApplyView(); }
    public void PanRight() { _cu += 0.25f / _zoom; ApplyView(); }
    public void PanUp() { _cv += 0.25f / _zoom; ApplyView(); }
    public void PanDown() { _cv -= 0.25f / _zoom; ApplyView(); }

    private void CenterOn(float lat, float lon)
    {
        _cu = (lon - minLon) / (maxLon - minLon);
        _cv = (lat - minLat) / (maxLat - minLat);
    }

    // ---------------------------------------------------------------- game hooks

    public void ResetForRound()
    {
        hasPin = false;
        _answerShown = false;
        for (int i = 0; i < otherPins.Length; i++) _otherOn[i] = false;
        _zoom = 1f; _cu = 0.5f; _cv = 0.5f;
        ApplyView();
    }

    public void ShowAnswer(float lat, float lon)
    {
        bool first = !_answerShown;
        _answerShown = true;
        _answerLat = lat;
        _answerLon = lon;
        if (first) FitAnswer();
        RefreshPins();
    }

    /// <summary>Zoom so the answer (and your pin, if any) are both on screen.</summary>
    private void FitAnswer()
    {
        float lat0 = _answerLat, lat1 = _answerLat, lon0 = _answerLon, lon1 = _answerLon;
        if (hasPin)
        {
            lat0 = Mathf.Min(lat0, pinLat); lat1 = Mathf.Max(lat1, pinLat);
            lon0 = Mathf.Min(lon0, pinLon); lon1 = Mathf.Max(lon1, pinLon);
        }
        float spanU = (lon1 - lon0) / (maxLon - minLon) * 1.4f + 0.02f;
        float spanV = (lat1 - lat0) / (maxLat - minLat) * 1.4f + 0.04f;
        float z = 1f;
        while (z < 16f && spanU * z * 2f <= 1f && spanV * z * 2f <= 1f) z *= 2f;
        _zoom = z;
        CenterOn((lat0 + lat1) * 0.5f, (lon0 + lon1) * 0.5f);
        ApplyView();
    }

    public void SetOtherPin(int i, bool on, float lat, float lon, string label)
    {
        if (_otherOn == null || i >= otherPins.Length) return;
        _otherOn[i] = on;
        _otherLat[i] = lat;
        _otherLon[i] = lon;
        if (otherLabels != null && i < otherLabels.Length && otherLabels[i] != null) otherLabels[i].text = label;
    }

    public int OtherPinCount() { return otherPins == null ? 0 : otherPins.Length; }
}
