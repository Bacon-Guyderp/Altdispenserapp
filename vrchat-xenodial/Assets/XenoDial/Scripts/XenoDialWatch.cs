using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;
using TMPro;

/// <summary>
/// The XenoDial: the local player's alien-transformation watch. One copy in the scene;
/// every client drives it for their own player only.
///
/// VR:      tap the core with your right index finger to open the dial, tap an alien to
///          pick it, tap the core again to transform. Tap the core while transformed to
///          change back. Pull either trigger to use your power.
/// Desktop: Q opens the dial, 1-6 picks an alien, F transforms.
///          While transformed: left-click or F uses your power, Q changes back.
///
/// Transforming changes how you move (speed, jump, gravity and even your size) and
/// gives you a power. The form and powers are synced through your XenoBody so
/// everybody sees your alien. After the time runs out the watch needs to recharge.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class XenoDialWatch : UdonSharpBehaviour
{
    public const int IDLE = 0, DIAL = 1, ALIEN = 2, RECHARGE = 3;
    public const int FIRE = 0, STONE = 1, SPEED = 2, WING = 3, TINY = 4, CRYSTAL = 5;  // same as XenoBody
    private const float MODEL_EYE_HEIGHT = 1.6f;

    [Header("Scene")]
    public XenoBody bodyTemplate;
    public Transform watchRoot;          // local watch (face normal = local +Y)
    public Transform core;               // the tappable core on the face
    public Transform dialRoot;           // hologram ring, shown while choosing
    public Transform[] icons;            // one mini alien per form, children of dialRoot
    public Transform selector;           // halo behind the chosen icon
    public Renderer faceRenderer;
    public Renderer coreRenderer;
    public TextMeshPro label;
    public XenoTarget[] targets;
    public TextMeshPro scoreText;
    public AudioSource ui;
    public AudioClip clickClip, openClip, beepClip;

    [Header("Aliens (index = form)")]
    public string[] formNames;
    public string[] formPowers;
    public Color[] formColors;
    public float[] walkSpeed, runSpeed, jumpImpulse, gravity, eyeHeight, cooldown;

    [Header("Rules")]
    public float transformSeconds = 45f;
    public float rechargeSeconds = 8f;
    public float fireRange = 45f;
    public float slamRadius = 9f;

    [Header("Desktop keys")]
    public KeyCode dialKey = KeyCode.Q;
    public KeyCode actionKey = KeyCode.F;
    public KeyCode[] pickKeys;

    [Header("VR")]
    public Vector3 handPointerEuler = Vector3.zero;  // tweak if fireballs don't leave along your hand
    public float touchRadius = 0.035f;

    private VRCPlayerApi _lp;
    private XenoBody _body;
    private bool _hasWatch;
    private int _state = IDLE;
    private int _selected;
    private int _form = -1;
    private float _endTime, _rechargeEnd, _nextPower, _dialOpenedAt, _nextBeep;
    private bool _coreArmed = true, _iconArmed = true;
    private int _smashed;

    private float _defWalk, _defRun, _defStrafe, _defJump, _defGravity, _defEye;
    private bool _eyeChanged;

    private Color _idleColor = new Color(0.15f, 1f, 0.8f, 1f);
    private Color _redColor = new Color(1f, 0.15f, 0.1f, 1f);

    void Start()
    {
        _lp = Networking.LocalPlayer;
        if (pickKeys == null || pickKeys.Length == 0)
            pickKeys = new KeyCode[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6 };
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

    // ------------------------------------------------------------------ state changes

    private void OpenDial()
    {
        _state = DIAL;
        _dialOpenedAt = Time.time;
        dialRoot.gameObject.SetActive(true);
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

    private void Select(int i)
    {
        if (i < 0 || i >= formNames.Length) return;
        _selected = i;
        _dialOpenedAt = Time.time;
        Play(clickClip);
        Buzz(0.3f);
        Refresh();
    }

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
            for (int i = 0; i < pickKeys.Length && i < formNames.Length; i++)
                if (Input.GetKeyDown(pickKeys[i])) Select(i);
            if (Input.GetKeyDown(KeyCode.RightArrow)) Select((_selected + 1) % formNames.Length);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Select((_selected + formNames.Length - 1) % formNames.Length);
            if (Input.GetKeyDown(actionKey)) Shift();
            else if (Time.time - _dialOpenedAt > 15f) CloseDial();
        }
        else if (_state == ALIEN)
        {
            if (Input.GetKeyDown(actionKey)) Power(false);
            float left = _endTime - Time.time;
            if (left <= 0f) { Revert(true); return; }
            if (Time.time >= _nextBeep) { Play(beepClip); _nextBeep += left < 2f ? 0.5f : 1f; }
            if (_form == WING && !_lp.IsPlayerGrounded())
            {
                Vector3 v = _lp.GetVelocity();
                if (v.y < -1.6f) _lp.SetVelocity(new Vector3(v.x, -1.6f, v.z));  // glide
            }
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
        if (!value || !_hasWatch || _state != ALIEN) return;
        Power(args.handType == HandType.LEFT);
    }

    public override void InputJump(bool value, UdonInputEventArgs args)
    {
        // Skyfin flaps when you jump in mid-air.
        if (!value || _state != ALIEN || _form != WING || _lp == null || _lp.IsPlayerGrounded()) return;
        Power(false);
    }

    // ------------------------------------------------------------------ powers

    private void Power(bool leftHand)
    {
        if (Time.time < _nextPower || _form < 0) return;
        _nextPower = Time.time + cooldown[_form];
        XenoBody b = Body();

        VRCPlayerApi.TrackingData head = _lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 feet = _lp.GetPosition();
        Vector3 fwd = head.rotation * Vector3.forward;
        Vector3 flat = new Vector3(fwd.x, 0f, fwd.z);
        flat = flat.sqrMagnitude > 0.001f ? flat.normalized : (_lp.GetRotation() * Vector3.forward);
        float s = Scale();

        if (_form == FIRE)
        {
            Vector3 origin, dir;
            if (_lp.IsUserInVR())
            {
                VRCPlayerApi.TrackingData hand = _lp.GetTrackingData(leftHand
                    ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand);
                origin = hand.position;
                dir = hand.rotation * Quaternion.Euler(handPointerEuler) * Vector3.forward;
            }
            else
            {
                dir = fwd;
                origin = head.position + fwd * 0.4f - head.rotation * Vector3.up * 0.15f;
            }
            float len = fireRange;
            RaycastHit hit;
            if (Physics.Raycast(origin, dir, out hit, fireRange, 1, QueryTriggerInteraction.Ignore)) len = hit.distance;
            XenoTarget best = null;
            float bestAlong = len + 1f;
            for (int i = 0; i < targets.Length; i++)
            {
                XenoTarget t = targets[i];
                if (t == null || t.IsBroken()) continue;
                Vector3 to = t.transform.position - origin;
                float along = Vector3.Dot(to, dir);
                if (along < 0f || along > bestAlong) continue;
                if ((to - dir * along).magnitude > t.hitRadius) continue;
                best = t;
                bestAlong = along;
            }
            if (best != null) len = bestAlong;
            if (b != null) b.UsePower(FIRE, origin, dir * len);
            if (best != null) SmashTarget(best);
        }
        else if (_form == STONE)
        {
            float r = slamRadius;
            for (int i = 0; i < targets.Length; i++)
            {
                XenoTarget t = targets[i];
                if (t == null || t.IsBroken()) continue;
                Vector3 d = t.transform.position - feet;
                if (new Vector2(d.x, d.z).magnitude <= r && Mathf.Abs(d.y) < 8f) SmashTarget(t);
            }
            Vector3 v = _lp.GetVelocity();
            _lp.SetVelocity(new Vector3(v.x, Mathf.Max(v.y, 3f), v.z));
            if (b != null) b.UsePower(STONE, feet, Vector3.forward * r);
        }
        else if (_form == SPEED)
        {
            Vector3 from = feet + Vector3.up * 0.9f * s;
            _lp.SetVelocity(flat * 28f + Vector3.up * 2f);
            for (int i = 0; i < targets.Length; i++)
            {
                XenoTarget t = targets[i];
                if (t == null || t.IsBroken()) continue;
                Vector3 to = t.transform.position - from;
                float along = Vector3.Dot(to, flat);
                if (along < 0f || along > 9f) continue;
                if ((to - flat * along).magnitude < 2.2f) SmashTarget(t);
            }
            if (b != null) b.UsePower(SPEED, from, -flat * 5f);
        }
        else if (_form == WING)
        {
            Vector3 v = _lp.GetVelocity();
            _lp.SetVelocity(new Vector3(v.x, 6.5f, v.z) + flat * 1.5f);
            if (b != null) b.UsePower(WING, feet, Vector3.forward * 1.3f);
        }
        else if (_form == TINY)
        {
            _lp.SetVelocity(flat * 3f + Vector3.up * 4.5f);
            if (b != null) b.UsePower(TINY, feet, Vector3.forward * 0.35f);
        }
        else if (_form == CRYSTAL)
        {
            // On the ground: a step ahead of you. In the air: a platform right under your feet.
            Vector3 at = _lp.IsPlayerGrounded()
                ? feet + flat * 1.3f + Vector3.up * 0.3f
                : feet + Vector3.down * 0.15f;
            if (b != null) b.UsePower(CRYSTAL, at, flat);
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
            for (int i = 0; i < icons.Length; i++)
            {
                icons[i].rotation = Quaternion.LookRotation(head.position - icons[i].position, headUp);
                float k = i == _selected ? 1.35f + 0.08f * Mathf.Sin(Time.time * 8f) : 1f;
                icons[i].localScale = Vector3.one * k;
            }
            if (_selected >= 0 && _selected < icons.Length)
                selector.position = icons[_selected].position - (head.position - icons[_selected].position).normalized * 0.01f * s;
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
        for (int i = 0; i < icons.Length; i++)
        {
            if (Vector3.Distance(tip, icons[i].position) < r * 0.9f)
            {
                near = true;
                if (_iconArmed && i != _selected) { _iconArmed = false; Select(i); }
            }
        }
        if (!near) _iconArmed = true;
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
            if (_selected < icons.Length) selector.gameObject.SetActive(true);
            label.text = "<b>" + formNames[_selected] + "</b>\n<size=70%>" + formPowers[_selected] + "</size>\n<size=60%>" +
                         (vr ? "Tap an alien, then tap the core" : "1-6 or arrows to pick, F to transform, Q to close") + "</size>";
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
            label.text = "<b>" + formNames[_form] + "</b>  " + secs + "s\n<size=60%>" +
                         (vr ? "Trigger: " : "Click or F: ") + formPowers[_form] + "\n" +
                         (vr ? "Tap the core to change back" : "Q to change back") + "</size>";
        else if (_state == RECHARGE)
            label.text = "<color=#ff5544><b>Recharging</b>  " + secs + "s</color>";
    }
}
