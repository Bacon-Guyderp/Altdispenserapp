using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// A harmless Bonetown skeleton that walks (and on some stretches runs) round a loop of points, forever.
/// Where it is comes from the server clock, so every player sees it in the same place without any syncing:
/// position = the point on its loop at (server time + its head start - the time it has spent chatting).
/// Press it and it stops, turns to you, waves and says its line; the press is shared (FirShare, kind 5),
/// so it stops for everyone, and afterwards it carries on from where it stopped.
/// Bones: 0 root, 1 torso, 2 skull, 3/4 upper arms, 5/6 forearms, 7/8 legs, 9 jaw.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirWanderer : UdonSharpBehaviour
{
    public Transform[] pivots;
    public Vector3[] route;            // world points (y = 0), walked in order and back to the first
    public float[] pace;               // per stretch (route[i] to route[i + 1]): 1 = walk, about 2.5 = run
    public float walkSpeed = 1.1f;
    public int headStartMs;            // where on its loop it is at server time 0 (spreads them out)
    public AudioSource voice;
    public string line = "";
    public TextMeshPro bubble;
    public GameObject bubbleObject;
    public Transform bubbleTransform;

    float[] segStart, segTime;
    int periodMs;
    int pausedMs;                      // time spent chatting, taken off the clock
    Quaternion[] restRot;
    Vector3 restPos0;
    bool inited, chatting;
    float chatStart, chatLength, talkUntil, distance;
    Vector3 lastPos;
    Quaternion facing;
    int curSeg;
    float voiceAt;
    bool voicePending;

    void Start() { Init(); }

    void Init()
    {
        if (inited) return;
        inited = true;
        int n = route.Length;
        segStart = new float[n];
        segTime = new float[n];
        float t = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector3 a = route[i], b = route[(i + 1) % n];
            float p = i < pace.Length && pace[i] > 0.1f ? pace[i] : 1f;
            segStart[i] = t;
            segTime[i] = Vector3.Distance(a, b) / (walkSpeed * p);
            t += segTime[i];
        }
        periodMs = Mathf.Max(1000, (int)(t * 1000f));
        restRot = new Quaternion[pivots.Length];
        for (int i = 0; i < pivots.Length; i++) restRot[i] = pivots[i].localRotation;
        restPos0 = pivots[0].localPosition;
        if (bubbleObject != null) bubbleObject.SetActive(false);
        Vector3 here = Where(Clock());
        transform.position = here;
        lastPos = here;
        facing = transform.rotation;
    }

    float Clock()
    {
        int ms = Networking.GetServerTimeInMilliseconds() + headStartMs - pausedMs;
        int m = ms % periodMs;
        if (m < 0) m += periodMs;
        return m / 1000f;
    }

    /// <summary>Where on the loop it is at loop time t (also sets curSeg).</summary>
    Vector3 Where(float t)
    {
        int n = route.Length;
        int seg = n - 1;
        for (int i = 0; i < n; i++) if (t < segStart[i] + segTime[i]) { seg = i; break; }
        curSeg = seg;
        float k = segTime[seg] > 0.001f ? Mathf.Clamp01((t - segStart[seg]) / segTime[seg]) : 1f;
        return Vector3.Lerp(route[seg], route[(seg + 1) % n], k);
    }

    public override void Interact()
    {
        if (chatting) return;
        Play();
        Share(5);
    }

    /// <summary>Stop, turn to the (local) player, wave and talk. Also sent by FirShare for a press by someone else.</summary>
    public void Play()
    {
        Init();
        if (chatting) return;
        chatting = true;
        chatStart = Time.time;
        float len = voice != null && voice.clip != null ? voice.clip.length : 2f;
        chatLength = len + 1.4f;
        talkUntil = Time.time + 0.3f + len - 0.2f;
        voiceAt = Time.time + 0.3f;
        voicePending = true;
        if (bubble != null && bubbleObject != null && line != "") { bubble.text = line; bubbleObject.SetActive(true); }
    }

    void Update()
    {
        if (!inited) return;
        if (chatting)
        {
            float t = Time.time - chatStart;
            if (voicePending && Time.time >= voiceAt) { voicePending = false; if (voice != null) voice.Play(); }
            VRCPlayerApi lp = Networking.LocalPlayer;
            if (Utilities.IsValid(lp))
            {
                Vector3 d = lp.GetPosition() - transform.position;
                d.y = 0f;
                if (d.magnitude > 0.05f) facing = Quaternion.LookRotation(d.normalized, Vector3.up);
                if (bubbleObject != null && bubbleObject.activeSelf)
                {
                    Vector3 away = bubbleTransform.position - lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
                    away.y = 0f;
                    if (away.magnitude > 0.01f) bubbleTransform.rotation = Quaternion.LookRotation(away, Vector3.up);
                }
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 360f * Time.deltaTime);
            float wave = Ramp(t, 0.1f, 0.4f, chatLength - 0.5f, chatLength - 0.2f);
            float w = Mathf.Sin(t * 9f);
            Pose(0f, 0f, 0f, wave, w);
            float flap = Time.time < talkUntil && t > 0.3f ? Mathf.Abs(Mathf.Sin(t * 13f)) : 0f;
            pivots[9].localRotation = restRot[9] * Quaternion.Euler(28f * flap, 0, 0);
            if (t >= chatLength)
            {
                chatting = false;
                pausedMs += (int)(chatLength * 1000f);   // the same on every client, so they stay together
                if (bubbleObject != null) bubbleObject.SetActive(false);
                pivots[9].localRotation = restRot[9];
            }
            return;
        }
        Vector3 p = Where(Clock());
        int seg = curSeg;
        Vector3 step = p - transform.position;
        step.y = 0f;
        float moved = step.magnitude;
        if (moved > 3f) { transform.position = p; moved = 0f; }   // far behind (just loaded): jump there
        else transform.position = p;
        Vector3 dir = route[(seg + 1) % route.Length] - route[seg];
        dir.y = 0f;
        if (dir.magnitude > 0.01f) facing = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 300f * Time.deltaTime);
        float pace0 = seg < pace.Length && pace[seg] > 0.1f ? pace[seg] : 1f;
        float run = Mathf.Clamp01((pace0 - 1.2f) / 1.0f);
        distance += moved;
        Pose(Mathf.Sin(distance * (run > 0.5f ? 3.6f : 4.6f)), run, 1f, 0f, 0f);
    }

    /// <summary>s = stride phase (-1..1), run = 0 walk .. 1 run, moving = 1 while walking, wave = 0..1 (right arm up), w = wave phase.</summary>
    void Pose(float s, float run, float moving, float wave, float w)
    {
        float legAmp = Mathf.Lerp(26f, 48f, run) * moving;
        float armAmp = Mathf.Lerp(22f, 50f, run) * moving;
        float bob = Mathf.Abs(s) * Mathf.Lerp(0.03f, 0.07f, run) * moving;
        pivots[0].localPosition = restPos0 + new Vector3(0, bob, 0);
        pivots[1].localRotation = restRot[1] * Quaternion.Euler(14f * run * moving + 4f * wave, 0, 0);
        pivots[2].localRotation = restRot[2] * Quaternion.Euler(-6f * run * moving, 10f * wave, 0);
        pivots[3].localRotation = restRot[3] * Quaternion.Euler(armAmp * s, 0, -8f);
        pivots[4].localRotation = restRot[4] * Quaternion.Euler(-armAmp * s * (1f - wave) - 160f * wave, 0, 8f + 10f * wave);
        pivots[5].localRotation = restRot[5] * Quaternion.Euler(-70f * run * moving - 15f * moving, 0, 0);
        pivots[6].localRotation = restRot[6] * Quaternion.Euler(-70f * run * moving * (1f - wave) - 15f * moving * (1f - wave), 0, 25f * w * wave);
        pivots[7].localRotation = restRot[7] * Quaternion.Euler(-legAmp * s, 0, 0);
        pivots[8].localRotation = restRot[8] * Quaternion.Euler(legAmp * s, 0, 0);
    }

    float Ramp(float t, float a, float b, float c, float d)
    {
        if (t <= a || t >= d) return 0f;
        if (t < b) return S((t - a) / (b - a));
        if (t > c) return 1f - S((t - c) / (d - c));
        return 1f;
    }

    float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    // ---- playing for everyone: FirShare sends the press to the other players
    public UdonBehaviour share;
    public int shareIndex;
    void Share(int kind)
    {
        if (share == null) return;
        share.SetProgramVariable("argKind", kind);
        share.SetProgramVariable("argIndex", shareIndex);
        share.SendCustomEvent("EvShare");
    }
}
