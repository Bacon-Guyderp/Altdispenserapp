using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;
using TMPro;

/// <summary>
/// The XenoDial: the local player's alien-transformation watch. One copy in the scene;
/// every client drives it for their own player only.
///
/// The dial shows ten aliens at a time (a "page"); there are as many pages as aliens / 10.
/// VR:      tap the core with your right index finger to open the dial. Tap an alien to pick
///          it, tap the arrows beside the ring to change page, tap the core to transform.
///          Tap the core while transformed to change back. Pull a trigger to use your power.
/// Desktop: Q opens the dial. 1-9 and 0 pick on the page, left/right step one alien,
///          up/down change page, R picks at random, F transforms.
///          While transformed: left-click or F uses your power, Q changes back.
///
/// Each alien has a power type, a strength, movement stats and a size. The form and
/// powers are synced through your XenoBody so everybody sees them.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class XenoDialWatch : UdonSharpBehaviour
{
    public const int IDLE = 0, DIAL = 1, ALIEN = 2, RECHARGE = 3;
    // Power types. Same numbers in XenoBody.
    public const int FIRE = 0, SLAM = 1, DASH = 2, FLAP = 3, HOP = 4, CRYSTAL = 5, LIGHTNING = 6,
                     BLINK = 7, THRUST = 8, GRAPPLE = 9, LAUNCH = 10, SPIN = 11, BLAST = 12;
    private const float MODEL_EYE_HEIGHT = 1.6f;
    private const int PAGE = 10;

    [Header("Scene")]
    public XenoBody bodyTemplate;
    public Transform watchRoot;          // local watch (face normal = local +Y)
    public Transform core;               // the tappable core on the face
    public Transform dialRoot;           // hologram ring, shown while choosing
    public Transform[] slots;            // ten icon slots around the ring
    public Renderer[] slotDiscs;
    public Transform[] slotMinis;        // holders the mini aliens are copied into
    public Transform prevPage, nextPage; // arrows beside the ring (tap them in VR)
    public Transform selector;           // halo behind the chosen icon
    public Renderer faceRenderer;
    public Renderer coreRenderer;
    public TextMeshPro label;
    public XenoTarget[] targets;
    public TextMeshPro scoreText;
    public AudioSource ui;
    public AudioClip clickClip, openClip, beepClip;

    [Header("Aliens (index = form)")]
    public GameObject[] library;
    public string[] formNames;
    public string[] formPowers;
    public Color[] formColors;
    public int[] powerType;
    public float[] strength;
    public float[] walkSpeed, runSpeed, jumpImpulse, gravity, eyeHeight, cooldown;

    [Header("Rules")]
    public float transformSeconds = 45f;
    public float rechargeSeconds = 8f;
    public float fireRange = 45f;
    public float tinyEyeHeight = 0.45f;   // at or below this you fit through the vault tunnel

    [Header("Desktop keys")]
    public KeyCode dialKey = KeyCode.Q;
    public KeyCode actionKey = KeyCode.F;
    public KeyCode randomKey = KeyCode.R;
    public KeyCode[] pickKeys;

    [Header("VR")]
    public Vector3 handPointerEuler = Vector3.zero;  // tweak if powers don't leave along your hand
    public float touchRadius = 0.035f;

    private VRCPlayerApi _lp;
    private XenoBody _body;
    private bool _hasWatch;
    private int _state = IDLE;
    private int _selected;
    private int _shownPage = -1;
    private int _form = -1;
    private float _endTime, _rechargeEnd, _nextPower, _dialOpenedAt, _nextBeep, _nextThrustFx;
    private bool _coreArmed = true, _iconArmed = true, _pageArmed = true;
    private bool _holdVR, _holdLeft;
    private int _smashed;
    private GameObject[] _minis;

    private float _defWalk, _defRun, _defStrafe, _defJump, _defGravity, _defEye;
    private bool _eyeChanged;

    private Color _idleColor = new Color(0.15f, 1f, 0.8f, 1f);
    private Color _redColor = new Color(1f, 0.15f, 0.1f, 1f);

    void Start()
    {
        _lp = Networking.LocalPlayer;
        if (pickKeys == null || pickKeys.Length == 0)
            pickKeys = new KeyCode[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
                                       KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0 };
        _minis = new GameObject[slots.Length];
        watchRoot.gameObject.SetActive(false);
        dialRoot.gameObject.SetActive(false);
        if (_lp != null)
        {
            // Let the watch make you giant or tiny.
            _lp.SetAvatarEyeHeightMinimumByMeters(0.1f);
            _lp.SetAvatarEyeHeightMaximumByMeters(5f);
        }
        UpdateScore();
    }

    // ------------------------------------------------------------------ public API

    /// <summary>Called by the crashed pod: puts the watch on your wrist.</summary>
    public void Equip()
    {
        if (_hasWatch) return;
        _hasWatch = true;
        watchRoot.gameObject.SetActive(true);
        _state = IDLE;
        Play(openClip);
        XenoBody b = Body();
        if (b != null) b.SetHasWatch(true);
        Refresh();
    }

    public bool HasWatch() { return _hasWatch; }
    public int CurrentForm() { return _form; }
    public bool IsTiny() { return _form >= 0 && eyeHeight[_form] > 0f && eyeHeight[_form] <= tinyEyeHeight; }
    public int AlienCount() { return formNames.Length; }

    // ------------------------------------------------------------------ helpers

    private XenoBody Body()
    {
        if (_body == null && _lp != null && bodyTemplate != null)
            _body = (XenoBody)Networking.FindComponentInPlayerObjects(_lp, bodyTemplate);
        return _body;
    }

    private void Play(AudioClip c)
    {
        if (ui != null && c != null) ui.PlayOneShot(c);
    }

    private void Buzz(float strength)
    {
        if (_lp != null && _lp.IsUserInVR())
            _lp.PlayHapticEventInHand(VRC_Pickup.PickupHand.Right, 0.06f, strength, 220f);
    }

    private float Scale()
    {
        float eye = _lp.GetAvatarEyeHeightAsMeters();
        if (eye <= 0.01f) eye = MODEL_EYE_HEIGHT;
        return Mathf.Clamp(eye / MODEL_EYE_HEIGHT, 0.12f, 3.5f);
    }

    private int PageCount() { return (formNames.Length + PAGE - 1) / PAGE; }

    // ------------------------------------------------------------------ the dial

    private void OpenDial()
    {
        _state = DIAL;
        _dialOpenedAt = Time.time;
        dialRoot.gameObject.SetActive(true);
        ShowPage(_selected / PAGE);
        Play(openClip);
        Buzz(0.4f);
        Refresh();
    }

    private void CloseDial()
    {
        _state = IDLE;
        dialRoot.gameObject.SetActive(false);
        Play(clickClip);
        Refresh();
    }

    /// <summary>Copies this page's aliens from the library into the ring.</summary>
    private void ShowPage(int page)
    {
        if (page == _shownPage) return;
        _shownPage = page;
        for (int i = 0; i < slots.Length; i++)
        {
            if (_minis[i] != null) Destroy(_minis[i]);
            _minis[i] = null;
            int f = page * PAGE + i;
            bool used = f < formNames.Length;
            slots[i].gameObject.SetActive(used);
            if (!used) continue;
            slotDiscs[i].material.SetColor("_Color", new Color(formColors[f].r, formColors[f].g, formColors[f].b, 0.45f));
            if (f < library.Length && library[f] != null)
            {
                GameObject m = Instantiate(library[f]);
                m.transform.SetParent(slotMinis[i], false);
                m.transform.localPosition = Vector3.zero;
                m.transform.localRotation = Quaternion.identity;
                m.transform.localScale = Vector3.one;
                m.SetActive(true);
                _minis[i] = m;
            }
        }
    }

    private void Select(int i)
    {
        int n = formNames.Length;
        if (n == 0) return;
        _selected = ((i % n) + n) % n;
        _dialOpenedAt = Time.time;
        ShowPage(_selected / PAGE);
        Play(clickClip);
        Buzz(0.3f);
        Refresh();
    }

    private void ChangePage(int delta)
    {
        int pages = PageCount();
        int page = ((_selected / PAGE + delta) % pages + pages) % pages;
        Select(page * PAGE);
    }

    // ------------------------------------------------------------------ transforming

    private void Shift()
    {
        if (_lp == null) return;
        int f = _selected;
        if (_form < 0)
        {
            _defWalk = _lp.GetWalkSpeed();
            _defRun = _lp.GetRunSpeed();
            _defStrafe = _lp.GetStrafeSpeed();
            _defJump = _lp.GetJumpImpulse();
            _defGravity = _lp.GetGravityStrength();
            _defEye = _lp.GetAvatarEyeHeightAsMeters();
        }
        _form = f;
        _state = ALIEN;
        _endTime = Time.time + transformSeconds;
        _nextBeep = _endTime - 5f;
        _nextPower = Time.time + 0.4f;
        dialRoot.gameObject.SetActive(false);
        ApplyStats();
        XenoBody b = Body();
        if (b != null) b.SetForm(f);
        Buzz(1f);
        Refresh();
    }

    private void ApplyStats()
    {
        int f = _form;
        _lp.SetWalkSpeed(walkSpeed[f]);
        _lp.SetStrafeSpeed(walkSpeed[f]);
        _lp.SetRunSpeed(runSpeed[f]);
        _lp.SetJumpImpulse(jumpImpulse[f]);
        _lp.SetGravityStrength(gravity[f]);
        if (eyeHeight[f] > 0f)
        {
            _lp.SetAvatarEyeHeightByMeters(eyeHeight[f]);
            _eyeChanged = true;
        }
        else if (_eyeChanged)
        {
            _lp.SetAvatarEyeHeightByMeters(_defEye);
            _eyeChanged = false;
        }
    }

    private void Revert(bool timedOut)
    {
        if (_form < 0) return;
        _form = -1;
        _holdVR = false;
        _lp.SetWalkSpeed(_defWalk);
        _lp.SetStrafeSpeed(_defStrafe);
        _lp.SetRunSpeed(_defRun);
        _lp.SetJumpImpulse(_defJump);
        _lp.SetGravityStrength(_defGravity);
        if (_eyeChanged) { _lp.SetAvatarEyeHeightByMeters(_defEye); _eyeChanged = false; }
        XenoBody b = Body();
        if (b != null) b.SetForm(-1);
        _state = RECHARGE;
        _rechargeEnd = Time.time + (timedOut ? rechargeSeconds : rechargeSeconds * 0.25f);
        Buzz(0.8f);
        Refresh();
    }

    private void CoreTapped()
    {
        if (_state == IDLE) OpenDial();
        else if (_state == DIAL) Shift();
        else if (_state == ALIEN) Revert(false);
        else Play(beepClip);
    }

    public override void OnAvatarChanged(VRCPlayerApi player)
    {
        // A new avatar resets your size. Keep the alien's size and remember the new default.
        if (player == null || !player.isLocal || _form < 0 || eyeHeight[_form] <= 0f) return;
        _defEye = player.GetAvatarEyeHeightAsMeters();
        _lp.SetAvatarEyeHeightByMeters(eyeHeight[_form]);
    }

    // ------------------------------------------------------------------ input

    void Update()
    {
        if (_lp == null || !_hasWatch) return;
        XenoBody body = Body();   // player objects can arrive after you take the watch
        if (body != null && !body.hasWatch) body.SetHasWatch(true);

        if (Input.GetKeyDown(dialKey))
        {
            if (_state == IDLE) OpenDial();
            else if (_state == DIAL) CloseDial();
            else if (_state == ALIEN) Revert(false);
            else Play(beepClip);
        }
        if (_state == DIAL)
        {
            int pageStart = (_selected / PAGE) * PAGE;
            for (int i = 0; i < pickKeys.Length && i < PAGE; i++)
                if (Input.GetKeyDown(pickKeys[i]) && pageStart + i < formNames.Length) Select(pageStart + i);
            if (Input.GetKeyDown(KeyCode.RightArrow)) Select(_selected + 1);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Select(_selected - 1);
            if (Input.GetKeyDown(KeyCode.UpArrow)) ChangePage(1);
            if (Input.GetKeyDown(KeyCode.DownArrow)) ChangePage(-1);
            if (Input.GetKeyDown(randomKey)) Select(Random.Range(0, formNames.Length));
            if (Input.GetKeyDown(actionKey)) Shift();
            else if (Time.time - _dialOpenedAt > 20f) CloseDial();
        }
        else if (_state == ALIEN)
        {
            if (Input.GetKeyDown(actionKey)) Power(false);
            float left = _endTime - Time.time;
            if (left <= 0f) { Revert(true); return; }
            if (Time.time >= _nextBeep) { Play(beepClip); _nextBeep += left < 2f ? 0.5f : 1f; }
            int pt = powerType[_form];
            if (pt == FLAP && !_lp.IsPlayerGrounded())
            {
                Vector3 v = _lp.GetVelocity();
                if (v.y < -1.6f) _lp.SetVelocity(new Vector3(v.x, -1.6f, v.z));  // glide
            }
            if (pt == THRUST && (_holdVR || Input.GetKey(actionKey))) Thrust();
            RefreshTimer(left);
        }
        else if (_state == RECHARGE)
        {
            if (Time.time >= _rechargeEnd) { _state = IDLE; Play(openClip); Refresh(); }
            else RefreshTimer(_rechargeEnd - Time.time);
        }
    }

    public override void InputUse(bool value, UdonInputEventArgs args)
    {
        if (!_hasWatch || _state != ALIEN) { _holdVR = false; return; }
        _holdLeft = args.handType == HandType.LEFT;
        _holdVR = value;
        if (value) Power(_holdLeft);
    }

    public override void InputJump(bool value, UdonInputEventArgs args)
    {
        // Flyers flap when you jump in mid-air.
        if (!value || _state != ALIEN || _lp == null || powerType[_form] != FLAP || _lp.IsPlayerGrounded()) return;
        Power(false);
    }

    // ------------------------------------------------------------------ powers

    private void Aim(bool leftHand, out Vector3 origin, out Vector3 dir)
    {
        VRCPlayerApi.TrackingData head = _lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        if (_lp.IsUserInVR())
        {
            VRCPlayerApi.TrackingData hand = _lp.GetTrackingData(leftHand
                ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand);
            origin = hand.position;
            dir = hand.rotation * Quaternion.Euler(handPointerEuler) * Vector3.forward;
        }
        else
        {
            dir = head.rotation * Vector3.forward;
            origin = head.position + dir * 0.4f - head.rotation * Vector3.up * 0.15f;
        }
    }

    private Vector3 Flat()
    {
        Vector3 fwd = _lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward;
        Vector3 flat = new Vector3(fwd.x, 0f, fwd.z);
        return flat.sqrMagnitude > 0.001f ? flat.normalized : (_lp.GetRotation() * Vector3.forward);
    }

    /// <summary>Distance along the aim to the first wall, or max.</summary>
    private float RayLength(Vector3 origin, Vector3 dir, float max)
    {
        RaycastHit hit;
        if (Physics.Raycast(origin, dir, out hit, max, 1, QueryTriggerInteraction.Ignore)) return hit.distance;
        return max;
    }

    /// <summary>The target nearest along the aim, within its hit radius plus slack.</summary>
    private XenoTarget AimedTarget(Vector3 origin, Vector3 dir, float maxAlong, float slack, out float along)
    {
        XenoTarget best = null;
        along = maxAlong;
        for (int i = 0; i < targets.Length; i++)
        {
            XenoTarget t = targets[i];
            if (t == null || t.IsBroken()) continue;
            Vector3 to = t.transform.position - origin;
            float a = Vector3.Dot(to, dir);
            if (a < 0f || a > along) continue;
            if ((to - dir * a).magnitude > t.hitRadius + slack) continue;
            best = t;
            along = a;
        }
        return best;
    }

    private void SmashAround(Vector3 center, float radius, float height)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            XenoTarget t = targets[i];
            if (t == null || t.IsBroken()) continue;
            Vector3 d = t.transform.position - center;
            if (new Vector2(d.x, d.z).magnitude <= radius && Mathf.Abs(d.y) < height) SmashTarget(t);
        }
    }

    private void Power(bool leftHand)
    {
        if (_form < 0 || Time.time < _nextPower) return;
        int pt = powerType[_form];
        float k = strength[_form];
        _nextPower = Time.time + cooldown[_form];
        XenoBody b = Body();
        Vector3 feet = _lp.GetPosition();
        Vector3 flat = Flat();
        Vector3 v = _lp.GetVelocity();
        float s = Scale();
        Vector3 origin, dir;
        Aim(leftHand, out origin, out dir);
        float along;

        if (pt == FIRE || pt == BLAST)
        {
            float len = RayLength(origin, dir, fireRange);
            XenoTarget t = AimedTarget(origin, dir, len + 1f, 0f, out along);
            if (t != null) len = along;
            if (b != null) b.UsePower(pt, origin, dir * len);
            if (pt == BLAST) SmashAround(origin + dir * len, 4f * k, 5f);
            else if (t != null) SmashTarget(t);
        }
        else if (pt == SLAM)
        {
            SmashAround(feet, 9f * k, 8f);
            _lp.SetVelocity(new Vector3(v.x, Mathf.Max(v.y, 3f), v.z));
            if (b != null) b.UsePower(SLAM, feet, Vector3.forward * 9f * k);
        }
        else if (pt == DASH)
        {
            Vector3 from = feet + Vector3.up * 0.9f * s;
            _lp.SetVelocity(flat * 28f * k + Vector3.up * 2f);
            for (int i = 0; i < targets.Length; i++)
            {
                XenoTarget t = targets[i];
                if (t == null || t.IsBroken()) continue;
                Vector3 to = t.transform.position - from;
                float a = Vector3.Dot(to, flat);
                if (a < 0f || a > 9f * k) continue;
                if ((to - flat * a).magnitude < 2.2f) SmashTarget(t);
            }
            if (b != null) b.UsePower(DASH, from, -flat * 5f);
        }
        else if (pt == FLAP)
        {
            _lp.SetVelocity(new Vector3(v.x, 6.5f * k, v.z) + flat * 1.5f);
            if (b != null) b.UsePower(FLAP, feet, Vector3.forward * 1.3f);
        }
        else if (pt == HOP)
        {
            _lp.SetVelocity(flat * 3f + Vector3.up * 4.5f * k);
            if (b != null) b.UsePower(HOP, feet, Vector3.forward * 0.35f);
        }
        else if (pt == CRYSTAL)
        {
            // On the ground: a step ahead of you. In the air: a platform right under your feet.
            Vector3 at = _lp.IsPlayerGrounded()
                ? feet + flat * 1.3f + Vector3.up * 0.3f
                : feet + Vector3.down * 0.15f;
            if (b != null) b.UsePower(CRYSTAL, at, flat);
        }
        else if (pt == LIGHTNING)
        {
            // Strikes whatever you point near, then arcs to two more targets close by.
            float range = 30f * k;
            float len = RayLength(origin, dir, range);
            XenoTarget first = AimedTarget(origin, dir, len + 1f, 1.8f, out along);
            Vector3 end = origin + dir * len;
            if (first != null)
            {
                end = first.transform.position;
                SmashTarget(first);
                int chained = 0;
                for (int i = 0; i < targets.Length && chained < 2; i++)
                {
                    XenoTarget t = targets[i];
                    if (t == null || t.IsBroken()) continue;
                    if ((t.transform.position - end).magnitude < 7f) { SmashTarget(t); chained++; }
                }
            }
            if (b != null) b.UsePower(LIGHTNING, origin, end - origin);
        }
        else if (pt == BLINK)
        {
            // Teleport forward, stopping short of walls, then find the ground.
            Vector3 eye = feet + Vector3.up * 0.8f * s;
            float d = RayLength(eye, flat, 10f * k);
            Vector3 dest = feet + flat * Mathf.Max(0f, d - 0.6f);
            RaycastHit hit;
            if (Physics.Raycast(dest + Vector3.up * 2f, Vector3.down, out hit, 6f, 1, QueryTriggerInteraction.Ignore)) dest = hit.point;
            if (b != null) b.UsePower(BLINK, feet, dest - feet);
            _lp.TeleportTo(dest, _lp.GetRotation(), VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, true);
        }
        else if (pt == THRUST)
        {
            _nextThrustFx = 0f;   // held: Thrust() runs every frame from Update
        }
        else if (pt == GRAPPLE)
        {
            // Fire a line; if it catches anything, you're yanked toward it.
            RaycastHit hit;
            float range = 40f * k;
            if (Physics.Raycast(origin, dir, out hit, range, 1, QueryTriggerInteraction.Ignore))
            {
                Vector3 pull = (hit.point - feet).normalized;
                _lp.SetVelocity(pull * 18f * k + Vector3.up * 3f);
                XenoTarget t = AimedTarget(origin, dir, hit.distance + 1f, 0f, out along);
                if (t != null) SmashTarget(t);
                if (b != null) b.UsePower(GRAPPLE, origin, hit.point - origin);
            }
            else if (b != null) b.UsePower(GRAPPLE, origin, dir * range);
        }
        else if (pt == LAUNCH)
        {
            _lp.SetVelocity(new Vector3(v.x, 14f * k, v.z));
            if (b != null) b.UsePower(LAUNCH, feet, Vector3.forward * 2.5f);
        }
        else if (pt == SPIN)
        {
            float r = 4.5f * k;
            SmashAround(feet, r, 4f);
            _lp.SetVelocity(new Vector3(v.x, Mathf.Max(v.y, 3f), v.z));
            if (b != null) b.UsePower(SPIN, feet, Vector3.forward * r);
        }
    }

    /// <summary>Jet/rocket/water-jet aliens: while held, push along where you aim.</summary>
    private void Thrust()
    {
        float k = strength[_form];
        Vector3 origin, dir;
        Aim(_holdLeft, out origin, out dir);
        if (!_lp.IsUserInVR()) dir = (dir + Vector3.up * 0.6f).normalized;   // desktop: lean upward so you take off
        Vector3 v = _lp.GetVelocity() + dir * 22f * k * Time.deltaTime + Vector3.up * 9.81f * gravity[_form] * 0.8f * Time.deltaTime;
        float cap = 11f * k;
        if (v.magnitude > cap) v = v.normalized * cap;
        _lp.SetVelocity(v);
        if (Time.time >= _nextThrustFx)
        {
            _nextThrustFx = Time.time + 0.4f;
            XenoBody b = Body();
            if (b != null) b.UsePower(THRUST, _lp.GetPosition() + Vector3.up * 0.8f * Scale(), -dir * 3f);
        }
    }

    private void SmashTarget(XenoTarget t)
    {
        if (!t.Smash()) return;
        _smashed++;
        UpdateScore();
    }

    private void UpdateScore()
    {
        if (scoreText != null) scoreText.text = "Your smashes\n<size=160%>" + _smashed + "</size>";
    }

    // ------------------------------------------------------------------ watch pose and finger taps

    public override void PostLateUpdate()
    {
        if (_lp == null || !_hasWatch) return;
        float s = Scale();
        VRCPlayerApi.TrackingData head = _lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        bool vr = _lp.IsUserInVR();

        if (vr)
        {
            Vector3 hand = _lp.GetBonePosition(HumanBodyBones.LeftHand);
            Vector3 arm = _lp.GetBonePosition(HumanBodyBones.LeftLowerArm);
            Vector3 along;
            Vector3 pos;
            if (hand.sqrMagnitude < 0.0001f || arm.sqrMagnitude < 0.0001f)
            {
                VRCPlayerApi.TrackingData lh = _lp.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand);
                along = lh.rotation * Vector3.forward;
                pos = lh.position - along * 0.06f * s;
            }
            else
            {
                along = (hand - arm).normalized;
                pos = Vector3.Lerp(arm, hand, 0.82f);
            }
            Vector3 up = Vector3.ProjectOnPlane(head.position - pos, along);
            if (up.sqrMagnitude < 0.0001f) up = Vector3.up;
            watchRoot.SetPositionAndRotation(pos + up.normalized * 0.025f * s, Quaternion.LookRotation(along, up.normalized));
        }
        else
        {
            // Desktop: the watch sits at the bottom-left of your view.
            Quaternion hr = head.rotation;
            watchRoot.SetPositionAndRotation(
                head.position + hr * new Vector3(-0.14f, -0.11f, 0.34f) * s,
                Quaternion.LookRotation(hr * Vector3.right, hr * new Vector3(0f, 0.25f, -1f)));
        }
        watchRoot.localScale = Vector3.one * s;

        // Face things toward your eyes.
        Vector3 headUp = head.rotation * Vector3.up;
        if (label != null)
        {
            label.transform.position = watchRoot.position + headUp * 0.13f * s;
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - head.position, headUp);
        }
        if (_state == DIAL)
        {
            int sel = _selected % PAGE;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].rotation = Quaternion.LookRotation(head.position - slots[i].position, headUp);
                float k = i == sel ? 1.35f + 0.08f * Mathf.Sin(Time.time * 8f) : 1f;
                slots[i].localScale = Vector3.one * k;
            }
            if (sel < slots.Length)
                selector.position = slots[sel].position - (head.position - slots[sel].position).normalized * 0.01f * s;
            prevPage.rotation = Quaternion.LookRotation(prevPage.position - head.position, headUp);
            nextPage.rotation = Quaternion.LookRotation(nextPage.position - head.position, headUp);
        }

        // Pulse the face in the last seconds.
        if (_state == ALIEN && _endTime - Time.time < 5f)
        {
            bool on = Mathf.Repeat(Time.time * 4f, 1f) < 0.5f;
            SetFace(on ? _redColor : formColors[_form]);
        }

        if (vr) FingerTaps(s);
    }

    private void FingerTaps(float s)
    {
        Vector3 tip = _lp.GetBonePosition(HumanBodyBones.RightIndexDistal);
        if (tip.sqrMagnitude < 0.0001f) tip = _lp.GetBonePosition(HumanBodyBones.RightHand);
        if (tip.sqrMagnitude < 0.0001f) tip = _lp.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
        float r = touchRadius * s;

        float dCore = Vector3.Distance(tip, core.position);
        if (_coreArmed && dCore < r) { _coreArmed = false; CoreTapped(); }
        else if (!_coreArmed && dCore > r * 2f) _coreArmed = true;

        if (_state != DIAL) return;
        bool near = false;
        int pageStart = (_selected / PAGE) * PAGE;
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].gameObject.activeSelf) continue;
            if (Vector3.Distance(tip, slots[i].position) < r * 0.9f)
            {
                near = true;
                if (_iconArmed && pageStart + i != _selected) { _iconArmed = false; Select(pageStart + i); }
            }
        }
        if (!near) _iconArmed = true;

        float dPrev = Vector3.Distance(tip, prevPage.position), dNext = Vector3.Distance(tip, nextPage.position);
        if (_pageArmed && dPrev < r) { _pageArmed = false; ChangePage(-1); }
        else if (_pageArmed && dNext < r) { _pageArmed = false; ChangePage(1); }
        else if (!_pageArmed && dPrev > r * 2f && dNext > r * 2f) _pageArmed = true;
    }

    // ------------------------------------------------------------------ display

    private void SetFace(Color c)
    {
        if (faceRenderer != null) faceRenderer.material.SetColor("_Color", c);
        if (coreRenderer != null) coreRenderer.material.SetColor("_Color", new Color(c.r, c.g, c.b, 0.9f));
    }

    private void Refresh()
    {
        bool vr = _lp != null && _lp.IsUserInVR();
        if (_state == IDLE)
        {
            SetFace(_idleColor);
            label.text = vr ? "<b>XenoDial</b>\nTap the core" : "<b>XenoDial</b>\nPress Q";
        }
        else if (_state == DIAL)
        {
            SetFace(formColors[_selected]);
            selector.gameObject.SetActive(true);
            label.text = "<size=60%>#" + (_selected + 1) + " of " + formNames.Length + "   page " + (_selected / PAGE + 1) + "/" + PageCount() +
                         "</size>\n<b>" + formNames[_selected] + "</b>\n<size=65%>" + formPowers[_selected] + "</size>\n<size=55%>" +
                         (vr ? "Tap an alien, arrows for more, tap the core to transform"
                             : "1-0 pick, arrows browse, R random, F transform, Q close") + "</size>";
        }
        else if (_state == ALIEN)
        {
            SetFace(formColors[_form]);
            RefreshTimer(_endTime - Time.time);
        }
        else
        {
            SetFace(_redColor);
            RefreshTimer(_rechargeEnd - Time.time);
        }
    }

    private void RefreshTimer(float left)
    {
        int secs = Mathf.CeilToInt(Mathf.Max(0f, left));
        bool vr = _lp != null && _lp.IsUserInVR();
        if (_state == ALIEN)
        {
            bool hold = powerType[_form] == THRUST;
            label.text = "<b>" + formNames[_form] + "</b>  " + secs + "s\n<size=60%>" +
                         (vr ? (hold ? "Hold trigger: " : "Trigger: ") : (hold ? "Hold F: " : "Click or F: ")) + formPowers[_form] + "\n" +
                         (vr ? "Tap the core to change back" : "Q to change back") + "</size>";
        }
        else if (_state == RECHARGE)
            label.text = "<color=#ff5544><b>Recharging</b>  " + secs + "s</color>";
    }
}
