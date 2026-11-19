using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// A comedy gag (Data/gags.txt). Press it (or walk into it) and its prop moves in, holds while it says its
/// line, and moves back. One script covers every kind by combining a few knobs: slide (moveBy), turn
/// (rotateBy), spin, inflate (scaleBy), wobble, a walking bob, and props hidden until the gag starts.
/// Self-contained: it moves only its own prop, flaps its own tree's mouth, and shows its line in its own
/// speech bubble (turned to face you), so it needs no other script.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirGag : UdonSharpBehaviour
{
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
    public string line = "";
    public Transform mouth;           // a tree's mouth, flapped while the line plays
    public TextMeshPro bubble;        // the speech bubble

    Vector3 p0, s0, mouthScale;
    Quaternion r0;
    float startT, busyUntil, nextWalkIn, nextZone, talkUntil;
    bool running, inited;

    void Start()
    {
        Init();
        if (hideWhenIdle && !running) target.gameObject.SetActive(false);
        if (bubble != null && !running) bubble.gameObject.SetActive(false);
    }

    void Init()
    {
        if (inited) return;
        inited = true;
        p0 = target.localPosition; r0 = target.localRotation; s0 = target.localScale;
        if (mouth != null) mouthScale = mouth.localScale;
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
        Play();
    }

    public void Play()
    {
        Init();
        if (running) return;
        running = true;
        startT = Time.time;
        if (hideWhenIdle) target.gameObject.SetActive(true);
        if (sfx != null) sfx.Play();
        if (voice != null) voice.Play();
        float len = voice != null && voice.clip != null ? voice.clip.length - 0.2f : holdTime;
        talkUntil = Time.time + len;
        if (bubble != null && line != "")
        {
            bubble.text = line;
            bubble.gameObject.SetActive(true);
        }
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
        if (mouth != null)
        {
            float open = Time.time < talkUntil ? Mathf.Abs(Mathf.Sin(t * 13f)) : 0f;
            mouth.localScale = new Vector3(mouthScale.x, mouthScale.y * (0.35f + 1.3f * open), mouthScale.z);
        }
        if (bubble != null && bubble.gameObject.activeSelf)
        {
            VRCPlayerApi lp = Networking.LocalPlayer;
            if (Utilities.IsValid(lp))
            {
                Vector3 head = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
                Vector3 away = bubble.transform.position - head;
                away.y = 0f;
                if (away.magnitude > 0.01f) bubble.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
        }
        if (t >= total)
        {
            running = false;
            target.localPosition = p0; target.localRotation = r0; target.localScale = s0;
            if (mouth != null) mouth.localScale = mouthScale;
            if (hideWhenIdle) target.gameObject.SetActive(false);
            if (bubble != null) bubble.gameObject.SetActive(false);
        }
    }

    float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
}
