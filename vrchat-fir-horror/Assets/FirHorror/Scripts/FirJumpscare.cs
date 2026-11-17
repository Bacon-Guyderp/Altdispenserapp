using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

/// <summary>
/// A scare, fired by walking into its zone (or by FirItem/scripts calling Fire()). The zone is a box of
/// `zoneSize` around this object, checked in code ten times a second. There is deliberately no trigger
/// collider: big invisible triggers block VRChat's interact pointer for everything behind them.
///  kind 0  face scare: a screaming tree in your face
///  kind 1  reveal: shows `target` for `duration` seconds with a sound
///  kind 2  sound: plays `sound` (positional) only
///  kind 3  drop: `target` falls from above to `dropTo` and stays
///  kind 4  behind: whispers, and something is standing behind you
///  kind 5  group: every tree in `groupChapter` screams, lights out
///  kind 6  peek: `target` stands there until you look at it, then it's gone
///  kind 7  flicker: the lights stutter and `anim` starts talking
///  kind 8  creeper: `target` steps closer every time you look away, then it gets you
///  kind 9  dark reveal: the lights die, and when they come back `target` is standing there
///  kind 10 charge: `target` sprints at you screaming; look at it or not, it is coming
/// `restore` is re-enabled on a new game (mimic items that vanish when they bite).
/// `global` makes it happen to everyone in the game, not just the player who walked in.
/// </summary>
// NoVariableSync, not None: a None behaviour can't send or receive network events at all.
[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class FirJumpscare : UdonSharpBehaviour
{
    public FirGameManager game;
    public int kind;
    public int minPhase = 1, maxPhase = 6;
    public float delay;
    public float duration = 1.2f;
    public bool once = true;
    public bool global;
    public int screamIndex;
    public int groupChapter;
    public GameObject target;
    public Transform dropTo;
    public AudioSource sound;
    public FirAnimatronic anim;
    public int animMode = 1;
    public Vector3 zoneSize;          // zero = no zone (fired by an item instead)
    public GameObject restore;

    bool fired;
    float nextAllowed;
    int state;           // 1 dropping, 2 peeking, 3 creeping
    float stateT, stepAt, nextZone;
    Vector3 homePos;
    bool homeSet;
    Vector3 dropFrom;

    void Start() { if (target != null && kind != 3) target.SetActive(false); }

    public void Rearm()
    {
        fired = false; nextAllowed = 0; state = 0;
        if (target != null && kind != 3) target.SetActive(false);
        if (target != null && kind == 3 && dropFrom != Vector3.zero) target.transform.position = dropFrom;
        if (target != null && (kind == 8 || kind == 10) && homeSet) target.transform.position = homePos;
        if (restore != null) restore.SetActive(true);
    }

    void CheckZone()
    {
        if (once && fired) return;
        if (Time.time < nextAllowed) return;
        if (game.phase < minPhase || game.phase > maxPhase) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        Vector3 p = transform.InverseTransformPoint(lp.GetPosition() + Vector3.up * 0.9f);
        if (Mathf.Abs(p.x) > zoneSize.x * 0.5f || Mathf.Abs(p.y) > zoneSize.y * 0.5f || Mathf.Abs(p.z) > zoneSize.z * 0.5f) return;
        fired = true;
        nextAllowed = Time.time + 25f;
        if (global) SendCustomNetworkEvent(NetworkEventTarget.All, "NetFire");
        else Fire();
    }

    public void NetFire()
    {
        fired = true;
        if (game.IsInGame()) Fire();   // shared scares skip people waiting in the lobby
    }

    public void Fire()
    {
        if (delay > 0) SendCustomEventDelayedSeconds("_Go", delay);
        else _Go();
    }

    public void _Go()
    {
        if (anim != null) anim.SetMode(animMode, duration + 1f);
        if (kind == 0) game.FaceScare(screamIndex, duration);
        else if (kind == 1)
        {
            if (target != null) target.SetActive(true);
            if (sound != null) sound.Play();
            SendCustomEventDelayedSeconds("_Hide", duration);
        }
        else if (kind == 2) { if (sound != null) sound.Play(); }
        else if (kind == 3)
        {
            if (target == null || dropTo == null) return;
            if (dropFrom == Vector3.zero) dropFrom = target.transform.position;
            target.SetActive(true);
            state = 1; stateT = 0;
            if (sound != null) sound.Play();
        }
        else if (kind == 4) game.BehindScare();
        else if (kind == 5)
        {
            game.GroupMode(groupChapter, 4, duration);
            game.Blackout(duration * 0.6f);
            game.PlaySfx(game.screams[screamIndex % game.screams.Length]);
        }
        else if (kind == 6)
        {
            if (target == null) return;
            target.SetActive(true);
            state = 2; stateT = Time.time + 12f;
        }
        else if (kind == 7)
        {
            game.Flicker(duration);
            if (sound != null) sound.Play();
        }
        else if (kind == 8)
        {
            if (target == null) return;
            if (!homeSet) { homeSet = true; homePos = target.transform.position; }
            target.transform.position = homePos;
            target.SetActive(true);
            state = 3; stateT = Time.time + 45f; stepAt = Time.time + 1.5f;
        }
        else if (kind == 10)
        {
            if (target == null) return;
            if (!homeSet) { homeSet = true; homePos = target.transform.position; }
            target.transform.position = homePos;
            target.SetActive(true);
            if (anim != null) anim.SetMode(4, 6f);
            if (sound != null) sound.Play();
            state = 4; stateT = Time.time + 6f;
        }
        else if (kind == 9)
        {
            game.Blackout(1.6f);
            SendCustomEventDelayedSeconds("_DarkReveal", 1.6f);
        }
    }

    public void _DarkReveal()
    {
        if (target != null) target.SetActive(true);
        if (sound != null) sound.Play();
        if (anim != null) anim.SetMode(4, duration);
        SendCustomEventDelayedSeconds("_Hide", duration);
    }

    public void _Hide() { if (target != null) target.SetActive(false); }

    void Update()
    {
        if (zoneSize.x > 0f && Time.time >= nextZone) { nextZone = Time.time + 0.1f; CheckZone(); }
        if (state == 1)
        {
            stateT += Time.deltaTime / 0.35f;
            float k = Mathf.Clamp01(stateT);
            target.transform.position = Vector3.Lerp(dropFrom, dropTo.position, k * k);
            if (k >= 1f) state = 0;
        }
        else if (state == 2)
        {
            VRCPlayerApi lp = Networking.LocalPlayer;
            if (!Utilities.IsValid(lp)) return;
            VRCPlayerApi.TrackingData h = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 to = (target.transform.position + Vector3.up * 0.8f - h.position).normalized;
            if (Vector3.Dot(h.rotation * Vector3.forward, to) > 0.9f || Time.time > stateT)
            {
                state = 0;
                target.SetActive(false);
                game.PlaySfx(game.clipWhoosh);
            }
        }
        else if (state == 4)
        {
            VRCPlayerApi lp = Networking.LocalPlayer;
            if (!Utilities.IsValid(lp)) return;
            Vector3 me = lp.GetPosition();
            Vector3 tp = target.transform.position;
            Vector3 flat = me - tp; flat.y = 0;
            float dist = flat.magnitude;
            if (dist < 1.3f || Time.time > stateT)
            {
                state = 0;
                target.SetActive(false);
                if (dist < 1.3f) game.FaceScare(screamIndex, 0.9f);
                return;
            }
            Vector3 dir = flat / dist;
            target.transform.position = tp + dir * Mathf.Min(dist, 5f * Time.deltaTime);
            target.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
        else if (state == 3)
        {
            VRCPlayerApi lp = Networking.LocalPlayer;
            if (!Utilities.IsValid(lp)) return;
            VRCPlayerApi.TrackingData h = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 tp = target.transform.position;
            Vector3 flat = h.position - tp; flat.y = 0;
            float dist = flat.magnitude;
            if (Time.time > stateT) { state = 0; target.SetActive(false); return; }
            if (dist < 1.6f)
            {
                state = 0;
                target.SetActive(false);
                game.FaceScare(screamIndex, 1.0f);
                return;
            }
            Vector3 to = (tp + Vector3.up * 1f - h.position).normalized;
            bool looking = Vector3.Dot(h.rotation * Vector3.forward, to) > 0.5f;
            if (!looking && Time.time > stepAt)
            {
                stepAt = Time.time + 0.9f;
                float step = Mathf.Min(1.3f, dist - 1.2f);
                target.transform.position = tp + flat / dist * step;
                target.transform.rotation = Quaternion.LookRotation(flat / dist, Vector3.up);
                if (sound != null) sound.Play();
            }
        }
    }
}
