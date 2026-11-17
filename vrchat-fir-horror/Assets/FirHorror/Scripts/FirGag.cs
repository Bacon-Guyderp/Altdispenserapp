using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

/// <summary>
/// A comedy gag: press it (or walk into it) and, for everyone at once, its prop moves in, holds
/// while it says its line, and moves back. One script covers every kind in Data/gags.txt by
/// combining a few knobs: slide (moveBy), turn (rotateBy), spin, inflate (scaleBy), wobble,
/// a walking bob, and props that are hidden until the gag starts.
/// </summary>
// NoVariableSync, not None: a None behaviour can't send or receive network events at all.
[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class FirGag : UdonSharpBehaviour
{
    public FirGameManager game;
    public Transform target;          // the prop that moves
    public bool hideWhenIdle;         // pop-ups, peeks and waddlers only appear during the gag
    public bool walkIn;               // set off by walking into its zone instead of pressing
    public Vector3 zoneCenter, zoneSize;   // the walk-in zone, local to this object (checked in code, no collider)
    public Vector3 moveBy;
    public Vector3 rotateBy;
    public float scaleBy = 1f;
    public float spin;                // degrees per second about Y while holding
    public float wobble;              // shake, in degrees
    public float bob;                 // walking bob, in metres
    public float inTime = 0.35f, holdTime = 2f, outTime = 0.35f;
    public AudioSource voice;
    public AudioSource sfx;
    public string line;
    public FirAnimatronic anim;       // a tree whose mouth flaps while it talks

    Vector3 p0, s0;
    Quaternion r0;
    float startT, busyUntil, nextWalkIn, nextZone;
    bool running;

    void Start()
    {
        p0 = target.localPosition; r0 = target.localRotation; s0 = target.localScale;
        if (hideWhenIdle) target.gameObject.SetActive(false);
    }

    public override void Interact() { Trigger(); }

    void CheckZone()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        Vector3 p = transform.InverseTransformPoint(lp.GetPosition() + Vector3.up * 0.9f) - zoneCenter;
        if (Mathf.Abs(p.x) > zoneSize.x * 0.5f || Mathf.Abs(p.y) > zoneSize.y * 0.5f || Mathf.Abs(p.z) > zoneSize.z * 0.5f) return;
        nextWalkIn = Time.time + 30f;
        Trigger();
    }

    void Trigger()
    {
        if (Time.time < busyUntil) return;
        busyUntil = Time.time + inTime + holdTime + outTime + 0.5f;
        SendCustomNetworkEvent(NetworkEventTarget.All, "NetPlay");
    }

    public void NetPlay()
    {
        if (running) return;
        running = true;
        startT = Time.time;
        if (hideWhenIdle) target.gameObject.SetActive(true);
        if (sfx != null) sfx.Play();
        if (voice != null) voice.Play();
        if (anim != null) anim.SetMode(2, inTime + holdTime);
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (game != null && line != "" && Utilities.IsValid(lp) && Vector3.Distance(lp.GetPosition(), transform.position) < 12f)
            game.Say(line, holdTime + inTime);
    }

    void Update()
    {
        if (walkIn && !running && Time.time >= nextZone && Time.time >= nextWalkIn) { nextZone = Time.time + 0.1f; CheckZone(); }
        if (!running) return;
        float t = Time.time - startT;
        float total = inTime + holdTime + outTime;
        float k = t < inTime ? S(t / inTime) : t < inTime + holdTime ? 1f : 1f - S((t - inTime - holdTime) / outTime);
        float held = Mathf.Clamp(t - inTime, 0f, holdTime);
        Vector3 pos = p0 + moveBy * k;
        if (bob > 0f) pos.y += Mathf.Abs(Mathf.Sin(t * 12f)) * bob * Mathf.Min(1f, k * 4f);
        target.localPosition = pos;
        Quaternion shake = wobble > 0f ? Quaternion.Euler(Mathf.Sin(t * 31f) * wobble * k, 0, Mathf.Cos(t * 27f) * wobble * k) : Quaternion.identity;
        target.localRotation = r0 * Quaternion.Euler(rotateBy * k) * Quaternion.Euler(0, spin * held, 0) * shake;
        target.localScale = s0 * Mathf.Lerp(1f, scaleBy, k);
        if (t >= total)
        {
            running = false;
            target.localPosition = p0; target.localRotation = r0; target.localScale = s0;
            if (hideWhenIdle) target.gameObject.SetActive(false);
        }
    }

    float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
}
