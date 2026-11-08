using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

/// <summary>
/// A scare, fired by walking into this trigger (or by FirItem/scripts calling Fire()).
///  kind 0  face scare: a screaming tree in your face
///  kind 1  reveal: shows `target` for `duration` seconds with a sound
///  kind 2  sound: plays `sound` (positional) only
///  kind 3  drop: `target` falls from above to `dropTo` and stays
///  kind 4  behind: whispers, and something is standing behind you
///  kind 5  group: every tree in `groupChapter` screams, lights out
///  kind 6  peek: `target` stands there until you look at it, then it's gone
///  kind 7  flicker: the lights stutter and `anim` starts talking
/// `global` makes it happen to everyone in the game, not just the player who walked in.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
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

    bool fired;
    float nextAllowed;
    int state;           // 1 dropping, 2 peeking
    float stateT;
    Vector3 dropFrom;

    void Start() { if (target != null && kind != 3) target.SetActive(false); }

    public void Rearm()
    {
        fired = false; nextAllowed = 0; state = 0;
        if (target != null && kind != 3) target.SetActive(false);
        if (target != null && kind == 3 && dropFrom != Vector3.zero) target.transform.position = dropFrom;
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        if (game.phase < minPhase || game.phase > maxPhase) return;
        if (once && fired) return;
        if (Time.time < nextAllowed) return;
        fired = true;
        nextAllowed = Time.time + 25f;
        if (global) SendCustomNetworkEvent(NetworkEventTarget.All, "NetFire");
        else Fire();
    }

    public void NetFire() { fired = true; Fire(); }

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
    }

    public void _Hide() { if (target != null) target.SetActive(false); }

    void Update()
    {
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
    }
}
