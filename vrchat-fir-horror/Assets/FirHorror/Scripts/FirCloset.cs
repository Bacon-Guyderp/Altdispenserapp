using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

/// <summary>
/// Easter egg: a tribute to the classic cartoon "RED SKELETON" gag. Open the Living Room closet
/// and, for everyone at once:
///   1. a white skeleton steps out and shrugs: "A skeleton."
///   2. it walks off, and a red skeleton leans out of the closet to watch it go
///   3. the red one steps out, hands on hips: "Red skeleton, ha."
///   4. it walks off hunched over, and the door swings shut.
/// Each skeleton is a rig of 10 transforms: root, torso, skull, shoulderL, shoulderR, elbowL,
/// elbowR, legL, legR, jaw. Each says its line out loud (3D voice on its skull) and its jaw flaps. Roots slide along the closet's +X (out) and -Z (off to the side).
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirCloset : UdonSharpBehaviour
{
    public FirGameManager game;
    public Transform hinge;          // yaw 0 shut, -100 open
    public Transform[] white;
    public Transform[] red;
    public GameObject mist;
    public AudioSource creak;
    public AudioSource whiteVoice, redVoice;

    float sceneLength = 9.0f;
    float startT, busyUntil;
    bool running, saidA, saidB;
    float whiteTalkUntil, redTalkUntil;

    void Start()
    {
        white[0].gameObject.SetActive(false);
        red[0].gameObject.SetActive(false);
        if (mist != null) mist.SetActive(false);
    }

    public override void Interact()
    {
        if (Time.time < busyUntil) return;
        busyUntil = Time.time + sceneLength + 1f;
        SendCustomNetworkEvent(NetworkEventTarget.All, "NetOpen");
    }

    public void NetOpen()
    {
        if (running) return;
        running = true; saidA = false; saidB = false;
        startT = Time.time;
        if (mist != null) mist.SetActive(true);
        if (creak != null) creak.Play();
    }

    void Update()
    {
        if (!running) return;
        float t = Time.time - startT;

        float door = t < 0.4f ? S(t / 0.4f) : t < 8.4f ? 1f : 1f - S((t - 8.4f) / 0.4f);
        hinge.localRotation = Quaternion.Euler(0, -100f * door, 0);
        if (t > 8.4f && t - Time.deltaTime <= 8.4f && creak != null) creak.Play();

        // ---- the white skeleton: step out, shrug, turn, stride off
        bool wOn = t < 4.0f;
        if (white[0].gameObject.activeSelf != wOn) white[0].gameObject.SetActive(wOn);
        if (wOn)
        {
            float wx = -0.05f + 0.8f * S((t - 0.4f) / 0.6f);
            float wz = t > 2.8f ? -2.6f * (t - 2.8f) / 1.2f : 0f;
            float yaw = 90f + 90f * S((t - 2.5f) / 0.3f);
            bool walking = (t > 0.4f && t < 1.0f) || t > 2.8f;
            float sw = walking ? Mathf.Sin(t * 10f) : 0f;
            float sh = Ramp(t, 1.1f, 1.4f, 2.2f, 2.5f);
            Apply(white, wx, wz, yaw, walking ? Mathf.Abs(sw) * 0.03f : 0.04f * sh,
                  0f, 0f, 0f, 12f * sh,
                  0f, -12f * sh, -100f * sh, -38f * sh, 25f * sw);
            if (sh > 0.3f && !saidA)
            {
                saidA = true;
                Say("A skeleton.", 2.2f);
                if (whiteVoice != null && whiteVoice.clip != null) { whiteVoice.Play(); whiteTalkUntil = Time.time + whiteVoice.clip.length - 0.25f; }
            }
            Jaw(white, whiteTalkUntil);
        }

        // ---- the red skeleton: lean out, step out, hands on hips, hunch off
        bool rOn = t >= 2.9f && t < 8.4f;
        if (red[0].gameObject.activeSelf != rOn) red[0].gameObject.SetActive(rOn);
        if (rOn)
        {
            float lw = Ramp(t, 3.0f, 3.5f, 4.0f, 4.6f);     // leaning out to watch the white one go
            float hw = Ramp(t, 4.6f, 4.9f, 6.2f, 6.5f);     // hands on hips
            float uw = S((t - 6.2f) / 0.3f);                // hunched
            float rx = -0.05f + 0.35f * S((t - 3.0f) / 0.5f) + 0.45f * S((t - 4.0f) / 0.6f);
            float rz = t > 6.5f ? -3.2f * (t - 6.5f) / 1.9f : 0f;
            float yaw = 90f + 55f * lw + 90f * uw;   // turns to lean out after the white one
            bool walking = (t > 4.0f && t < 4.6f) || t > 6.5f;
            float sw = walking ? Mathf.Sin(t * 8f) : 0f;
            Apply(red, rx, rz, yaw, walking ? Mathf.Abs(sw) * 0.025f : 0f,
                  40f * lw - 5f * hw + 35f * uw, -25f * uw, 25f * lw, -10f * hw,
                  -40f * lw - 25f * uw, -40f * hw, -20f * lw - 30f * uw, 100f * hw,
                  -35f * lw + 20f * sw);
            if (hw > 0.3f && !saidB)
            {
                saidB = true;
                Say("Red skeleton, ha.", 2.4f);
                if (redVoice != null && redVoice.clip != null) { redVoice.Play(); redTalkUntil = Time.time + redVoice.clip.length - 0.25f; }
            }
            Jaw(red, redTalkUntil);
        }

        if (t > sceneLength)
        {
            running = false;
            hinge.localRotation = Quaternion.identity;
            white[0].gameObject.SetActive(false);
            red[0].gameObject.SetActive(false);
            if (mist != null) mist.SetActive(false);
        }
    }

    /// <summary>Poses a rig. Arms mirror left/right; legs (and the arm swing) alternate.</summary>
    void Apply(Transform[] p, float x, float z, float yaw, float bob,
               float lean, float headX, float headY, float headZ,
               float shX, float shZ, float elX, float elZ, float leg)
    {
        p[0].localPosition = new Vector3(x, bob, z);
        p[0].localRotation = Quaternion.Euler(0, yaw, 0);
        p[1].localRotation = Quaternion.Euler(lean, 0, 0);
        p[2].localRotation = Quaternion.Euler(headX, headY, headZ);
        float swing = -0.8f * leg;
        p[3].localRotation = Quaternion.Euler(shX + swing, 0, shZ);
        p[4].localRotation = Quaternion.Euler(shX - swing, 0, -shZ);
        p[5].localRotation = Quaternion.Euler(elX, 0, elZ);
        p[6].localRotation = Quaternion.Euler(elX, 0, -elZ);
        p[7].localRotation = Quaternion.Euler(leg, 0, 0);
        p[8].localRotation = Quaternion.Euler(-leg, 0, 0);
    }

    /// <summary>Flaps the jaw while the line is playing.</summary>
    void Jaw(Transform[] p, float until)
    {
        float open = Time.time < until ? Mathf.Abs(Mathf.Sin(Time.time * 17f)) * (0.6f + 0.4f * Mathf.Sin(Time.time * 5f)) : 0f;
        p[9].localRotation = Quaternion.Euler(28f * open, 0, 0);
    }

    void Say(string line, float seconds)
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (Utilities.IsValid(lp) && Vector3.Distance(lp.GetPosition(), transform.position) < 14f) game.Say(line, seconds);
    }

    /// <summary>0 before a, rises to 1 by b, holds, falls back to 0 between c and d.</summary>
    float Ramp(float t, float a, float b, float c, float d)
    {
        if (t < a || t > d) return 0f;
        if (t < b) return S((t - a) / (b - a));
        if (t < c) return 1f;
        return 1f - S((t - c) / (d - c));
    }

    float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
}
