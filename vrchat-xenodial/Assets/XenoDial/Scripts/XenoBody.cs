using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// One copy per player (spawned by VRCPlayerObject) and owned by that player.
/// Holds the synced alien form and the last power used, and draws them for everyone:
/// the alien body follows its owner, and powers (fireballs, shockwaves, crystals...)
/// play on every client. The owner sees their own alien only in mirrors.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class XenoBody : UdonSharpBehaviour
{
    public const int FIRE = 0, STONE = 1, SPEED = 2, WING = 3, TINY = 4, CRYSTAL = 5;
    private const float MODEL_EYE_HEIGHT = 1.6f;   // the alien models are built for this eye height
    private const int MIRROR_LAYER = 18;           // MirrorReflection: hidden from your own camera, shown in mirrors

    [UdonSynced] public int form = -1;
    [UdonSynced] public bool hasWatch;
    [UdonSynced] public int fxSeq;
    [UdonSynced] public int fxKind;
    [UdonSynced] public Vector3 fxPos;
    [UdonSynced] public Vector3 fxDir;

    [Header("Built by XenoDial > Build World")]
    public Transform modelRoot;
    public GameObject[] formModels;
    public Color[] formColors;
    public Transform[] swingA;          // limbs that swing forward while the other set swings back
    public Transform[] swingB;
    public Transform[] pulse;           // flames / crystals that pulse
    public Transform wristWatch;        // the watch other people see on your wrist
    public Renderer flash;              // transform flash (sphere)
    public Transform fireball;
    public Renderer burst;
    public Transform ring;              // shockwave / flap ring
    public Transform trail;             // dash streak
    public Transform[] crystals;        // crystal platforms (with colliders)
    public AudioSource voice;           // 3D, follows the body
    public AudioClip[] powerClips;      // per form
    public AudioClip shiftClip, revertClip;
    public float crystalLifetime = 30f;

    private VRCPlayerApi _owner;
    private bool _isMine;
    private int _shownForm = -1;
    private int _lastFx;
    private bool _gotFirst;
    private float _walkPhase;
    private Quaternion[] _restA, _restB;
    private Vector3[] _pulseScale;

    private float _flashT = 99f;
    private float _fireT = 99f, _fireLen;
    private Vector3 _fireFrom, _fireDir;
    private float _burstT = 99f;
    private float _ringT = 99f, _ringSize;
    private float _trailT = 99f;
    private float[] _crystalBorn;

    void Start()
    {
        _restA = new Quaternion[swingA.Length];
        for (int i = 0; i < swingA.Length; i++) _restA[i] = swingA[i].localRotation;
        _restB = new Quaternion[swingB.Length];
        for (int i = 0; i < swingB.Length; i++) _restB[i] = swingB[i].localRotation;
        _pulseScale = new Vector3[pulse.Length];
        for (int i = 0; i < pulse.Length; i++) _pulseScale[i] = pulse[i].localScale;
        _crystalBorn = new float[crystals.Length];
        for (int i = 0; i < crystals.Length; i++) { crystals[i].gameObject.SetActive(false); _crystalBorn[i] = -999f; }
        ShowForm(-1);
        HideFx();
    }

    private bool ResolveOwner()
    {
        if (_owner != null && Utilities.IsValid(_owner)) return true;
        _owner = Networking.GetOwner(gameObject);
        if (_owner == null || !Utilities.IsValid(_owner)) return false;
        _isMine = _owner.isLocal;
        if (_isMine) SetLayer(modelRoot, MIRROR_LAYER);
        return true;
    }

    private void SetLayer(Transform t, int layer)
    {
        Transform[] all = t.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++) all[i].gameObject.layer = layer;
    }

    // ------------------------------------------------------------------ called by the owner's watch

    public void SetForm(int f)
    {
        if (!Networking.IsOwner(gameObject)) return;
        form = f;
        RequestSerialization();
        ApplyForm();
    }

    public void SetHasWatch(bool on)
    {
        if (!Networking.IsOwner(gameObject)) return;
        hasWatch = on;
        RequestSerialization();
    }

    public void UsePower(int kind, Vector3 pos, Vector3 dir)
    {
        if (!Networking.IsOwner(gameObject)) return;
        fxSeq++;
        fxKind = kind;
        fxPos = pos;
        fxDir = dir;
        RequestSerialization();
        PlayFx(kind, pos, dir, fxSeq);
    }

    // ------------------------------------------------------------------ network

    public override void OnDeserialization()
    {
        ApplyForm();
        if (!_gotFirst) { _gotFirst = true; _lastFx = fxSeq; return; }  // late joiner: don't replay old powers
        if (fxSeq != _lastFx)
        {
            _lastFx = fxSeq;
            PlayFx(fxKind, fxPos, fxDir, fxSeq);
        }
    }

    private void ApplyForm()
    {
        if (form == _shownForm) return;
        bool turningBack = form < 0;
        Color c = (!turningBack && form < formColors.Length) ? formColors[form] : new Color(0.2f, 1f, 0.85f);
        ShowForm(form);
        if (ResolveOwner())
        {
            Vector3 at = _owner.GetPosition() + Vector3.up * _owner.GetAvatarEyeHeightAsMeters() * 0.6f;
            flash.transform.position = at;
            flash.material.SetColor("_Color", new Color(c.r, c.g, c.b, 0.9f));
            flash.gameObject.SetActive(true);
            _flashT = 0f;
        }
        Play(turningBack ? revertClip : shiftClip);
    }

    private void ShowForm(int f)
    {
        _shownForm = f;
        for (int i = 0; i < formModels.Length; i++) formModels[i].SetActive(i == f);
    }

    private void Play(AudioClip clip)
    {
        if (voice == null || clip == null) return;
        voice.PlayOneShot(clip);
    }

    // ------------------------------------------------------------------ powers

    private void PlayFx(int kind, Vector3 pos, Vector3 dir, int seq)
    {
        if (kind >= 0 && kind < powerClips.Length) Play(powerClips[kind]);
        Color c = (kind >= 0 && kind < formColors.Length) ? formColors[kind] : Color.white;
        if (kind == FIRE)
        {
            _fireFrom = pos;
            _fireLen = dir.magnitude;
            _fireDir = _fireLen > 0.001f ? dir / _fireLen : Vector3.forward;
            _fireT = 0f;
            fireball.position = pos;
            fireball.gameObject.SetActive(true);
        }
        else if (kind == STONE)
        {
            StartRing(pos, dir.magnitude, c);
        }
        else if (kind == SPEED)
        {
            float len = Mathf.Max(0.5f, dir.magnitude);
            trail.position = pos + dir * 0.5f;
            trail.rotation = Quaternion.LookRotation(dir.normalized) * Quaternion.Euler(90f, 0f, 0f);
            trail.localScale = new Vector3(0.5f, len * 0.5f, 0.5f);
            trail.gameObject.SetActive(true);
            _trailT = 0f;
        }
        else if (kind == WING || kind == TINY)
        {
            StartRing(pos, Mathf.Max(0.4f, dir.magnitude), c);
        }
        else if (kind == CRYSTAL && crystals.Length > 0)
        {
            int i = seq % crystals.Length;
            Transform cr = crystals[i];
            cr.position = pos;
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            cr.rotation = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat) : Quaternion.identity;
            cr.gameObject.SetActive(true);
            _crystalBorn[i] = Time.time;
        }
    }

    private void StartRing(Vector3 pos, float size, Color c)
    {
        ring.position = pos + Vector3.up * 0.05f;
        _ringSize = size;
        _ringT = 0f;
        Renderer r = ring.GetComponent<Renderer>();
        if (r != null) r.material.SetColor("_Color", new Color(c.r, c.g, c.b, 0.8f));
        ring.gameObject.SetActive(true);
    }

    private void HideFx()
    {
        flash.gameObject.SetActive(false);
        fireball.gameObject.SetActive(false);
        burst.gameObject.SetActive(false);
        ring.gameObject.SetActive(false);
        trail.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ per frame

    public override void PostLateUpdate()
    {
        if (!ResolveOwner()) return;
        float dt = Time.deltaTime;
        float eye = _owner.GetAvatarEyeHeightAsMeters();
        if (eye <= 0.01f) eye = MODEL_EYE_HEIGHT;

        // body follows its owner
        if (_shownForm >= 0)
        {
            modelRoot.position = _owner.GetPosition();
            Quaternion r = _owner.GetRotation();
            modelRoot.rotation = Quaternion.Euler(0f, r.eulerAngles.y, 0f);
            modelRoot.localScale = Vector3.one * (eye / MODEL_EYE_HEIGHT);

            Vector3 v = _owner.GetVelocity();
            float speed = new Vector2(v.x, v.z).magnitude;
            _walkPhase += dt * Mathf.Clamp(speed * 2.2f / Mathf.Max(0.2f, eye), 0f, 30f);
            float amp = Mathf.Clamp01(speed / 2f) * 35f;
            float s = Mathf.Sin(_walkPhase) * amp;
            for (int i = 0; i < swingA.Length; i++) swingA[i].localRotation = _restA[i] * Quaternion.Euler(s, 0f, 0f);
            for (int i = 0; i < swingB.Length; i++) swingB[i].localRotation = _restB[i] * Quaternion.Euler(-s, 0f, 0f);
            float p = 1f + 0.15f * Mathf.Sin(Time.time * 9f);
            for (int i = 0; i < pulse.Length; i++) pulse[i].localScale = _pulseScale[i] * p;
        }

        // the watch other people see on your wrist (you see your own, interactive one)
        bool showWrist = hasWatch && !_isMine && _shownForm < 0;
        if (wristWatch.gameObject.activeSelf != showWrist) wristWatch.gameObject.SetActive(showWrist);
        if (showWrist)
        {
            Vector3 hand = _owner.GetBonePosition(HumanBodyBones.LeftHand);
            Vector3 arm = _owner.GetBonePosition(HumanBodyBones.LeftLowerArm);
            if (hand.sqrMagnitude < 0.0001f) { wristWatch.gameObject.SetActive(false); }
            else
            {
                Vector3 along = hand - arm;
                if (along.sqrMagnitude < 0.0001f) along = _owner.GetRotation() * Vector3.forward;
                wristWatch.position = Vector3.Lerp(arm, hand, 0.82f);
                Vector3 head = _owner.GetBonePosition(HumanBodyBones.Head);
                Vector3 up = Vector3.ProjectOnPlane(head - wristWatch.position, along.normalized);
                if (up.sqrMagnitude < 0.0001f) up = Vector3.up;
                wristWatch.rotation = Quaternion.LookRotation(along.normalized, up.normalized);
                wristWatch.localScale = Vector3.one * (eye / MODEL_EYE_HEIGHT);
            }
        }

        // effects
        if (_flashT < 0.6f)
        {
            _flashT += dt;
            float k = _flashT / 0.6f;
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 3.2f, k) * (eye / MODEL_EYE_HEIGHT);
            if (_flashT >= 0.6f) flash.gameObject.SetActive(false);
        }
        if (_fireT < 50f)
        {
            _fireT += dt;
            float d = _fireT * 32f;
            if (d >= _fireLen)
            {
                fireball.gameObject.SetActive(false);
                _fireT = 99f;
                burst.transform.position = _fireFrom + _fireDir * _fireLen;
                burst.gameObject.SetActive(true);
                _burstT = 0f;
            }
            else fireball.position = _fireFrom + _fireDir * d;
        }
        if (_burstT < 0.35f)
        {
            _burstT += dt;
            burst.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 2.2f, _burstT / 0.35f);
            if (_burstT >= 0.35f) burst.gameObject.SetActive(false);
        }
        if (_ringT < 0.55f)
        {
            _ringT += dt;
            float k = _ringT / 0.55f;
            float w = Mathf.Lerp(0.2f, _ringSize * 2f, k);
            ring.localScale = new Vector3(w, 0.02f + 0.3f * (1f - k), w);
            if (_ringT >= 0.55f) ring.gameObject.SetActive(false);
        }
        if (_trailT < 0.35f)
        {
            _trailT += dt;
            Vector3 ls = trail.localScale;
            float th = Mathf.Lerp(0.5f, 0.02f, _trailT / 0.35f);
            trail.localScale = new Vector3(th, ls.y, th);
            if (_trailT >= 0.35f) trail.gameObject.SetActive(false);
        }
        for (int i = 0; i < crystals.Length; i++)
        {
            if (crystals[i].gameObject.activeSelf && Time.time - _crystalBorn[i] > crystalLifetime)
                crystals[i].gameObject.SetActive(false);
        }
    }
}
