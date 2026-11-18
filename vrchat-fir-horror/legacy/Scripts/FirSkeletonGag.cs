using UdonSharp;
using UnityEngine;
using VRC.Udon;
using VRC.SDKBase;

/// <summary>
/// A skeleton comedy routine (Data/skeleton_gags.txt). Press it and, for everyone at once, one or
/// more skeletons act out a little routine while saying a line, then go back to standing.
///
/// Each rig is 10 pivots, flattened into `pivots` as rig * 10 + j:
///   0 root, 1 torso, 2 skull, 3 shoulderL, 4 shoulderR, 5 elbowL, 6 elbowR, 7 legL, 8 legR, 9 jaw
/// `meshes` holds each pivot's mesh in the same order, so parts can fall off into a pile.
///
/// Acts: 0 salute, 1 shimmy, 2 dance then shrug, 3 collapse into a pile and reassemble,
/// 4 lazy shrug, 5 sing into a mic, 6 point then laugh, 7 on the phone, 8 forearm falls off,
/// 9 hands on hips, 10 tremble, 11 bow.
/// </summary>
// No network events: the game manager's synced gag counter tells everyone else to Play().
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirSkeletonGag : UdonSharpBehaviour
{
    public UdonBehaviour watch;          // diagnostics: reports each press to the lobby board
    public UdonBehaviour game;          // FirGameManager
    public int gagIndex;      // this routine's place in game.skeletonGags (set by the builder)
    public int act;
    public int rigCount = 1;
    public Transform[] pivots;
    public Transform[] meshes;
    public AudioSource voice;
    public float voiceDelay;
    public string line;
    public float baseLength = 4f;

    Vector3[] restPos, meshPos;
    Quaternion[] restRot, meshRot;
    float startT, busyUntil, length, talkFrom, talkUntil;
    bool running, voiced, inited;

    void Start() { Init(); }

    void Init()
    {
        if (inited) return;
        inited = true;
        int n = pivots.Length;
        restPos = new Vector3[n]; restRot = new Quaternion[n];
        meshPos = new Vector3[n]; meshRot = new Quaternion[n];
        for (int i = 0; i < n; i++)
        {
            restPos[i] = pivots[i].localPosition; restRot[i] = pivots[i].localRotation;
            meshPos[i] = meshes[i].localPosition; meshRot[i] = meshes[i].localRotation;
        }
    }

    float ClipLength() { return voice != null && voice.clip != null ? voice.clip.length : 0f; }
    float Length() { return Mathf.Max(baseLength, voiceDelay + ClipLength() + 0.8f); }

    public override void Interact()
    {
        Report();
        if (Time.time < busyUntil) return;
        busyUntil = Time.time + Length() + 0.5f;
        Play();
        GameEvAB("EvShareGag", 1, gagIndex);
    }

    /// <summary>Plays the routine on this client only.</summary>
    public void Play()
    {
        Init();
        if (running) return;
        running = true; voiced = false;
        startT = Time.time;
        length = Length();
        talkFrom = 999f; talkUntil = 0f;
    }

    void Update()
    {
        if (!running) return;
        float t = Time.time - startT;
        if (!voiced && t >= voiceDelay)
        {
            voiced = true;
            if (voice != null) voice.Play();
            talkFrom = t; talkUntil = t + ClipLength() - 0.2f;
            VRCPlayerApi lp = Networking.LocalPlayer;
            if (game != null && Utilities.IsValid(lp) && Vector3.Distance(lp.GetPosition(), transform.position) < 12f)
                GameSay(line, ClipLength() + 0.5f);
        }
        for (int r = 0; r < rigCount; r++)
        {
            // jaw first: Pose may lay the jaw's mesh in the pile, which needs the jaw where it'll stay this frame
            float flap = (t >= talkFrom && t < talkUntil) ? Mathf.Abs(Mathf.Sin(t * 17f + r)) * (0.6f + 0.4f * Mathf.Sin(t * 5f)) : 0f;
            pivots[r * 10 + 9].localRotation = restRot[r * 10 + 9] * Quaternion.Euler(28f * flap, 0, 0);
            Pose(r, t);
        }
        if (t >= length)
        {
            running = false;
            for (int i = 0; i < pivots.Length; i++)
            {
                pivots[i].localPosition = restPos[i]; pivots[i].localRotation = restRot[i];
                meshes[i].localPosition = meshPos[i]; meshes[i].localRotation = meshRot[i];
            }
        }
    }

    void Pose(int r, float t)
    {
        float L = length;
        if (act == 0)            // salute, one after another
        {
            float w = Ramp(t, 0.3f + 0.25f * r, 0.6f + 0.25f * r, L - 1.0f, L - 0.6f);
            Apply(r, 0, 0, 0, 0, 0, -5f * w, 0, 0, 0, 0, -20f * w, 85f * w, 0, 0, 0, 120f * w, 0, 0);
        }
        else if (act == 1)       // shimmy
        {
            float k = Ramp(t, 0.2f, 0.5f, L - 0.8f, L - 0.4f), o = Mathf.Sin(t * 9f);
            Apply(r, 0.05f * o * k, 0, 0, 0.02f * Mathf.Abs(o) * k, 5f * k, 0, 0, 10f * Mathf.Sin(t * 4.5f) * k,
                  25f * o * k, -10f * k, -25f * o * k, 10f * k, -90f * k, 0, -90f * k, 0, 10f * o * k, -10f * o * k);
        }
        else if (act == 2)       // dance like nobody's watching, then shrug
        {
            float d = Ramp(t, 0.1f, 0.4f, 2.2f, 2.5f), s = Ramp(t, 2.5f, 2.8f, L - 0.8f, L - 0.5f), w = Mathf.Sin(t * 8f);
            float kick = Mathf.Sin(t * 6f);
            Apply(r, 0, 0, 0, 0.04f * Mathf.Abs(kick) * d, 0, 0, 0, 12f * s,
                  0, -(150f + 20f * w) * d - 12f * s, 0, (150f - 20f * w) * d + 12f * s,
                  -100f * s, -38f * s, -100f * s, 38f * s, -30f * Mathf.Max(0, kick) * d, -30f * Mathf.Max(0, -kick) * d);
        }
        else if (act == 3)       // fall apart into a pile, then put yourself back together
        {
            float shake = Ramp(t, 0f, 0.1f, 0.4f, 0.5f);
            Apply(r, 0, 0, 0, 0, 0, 0, 10f * Mathf.Sin(t * 40f) * shake, 0, 0, -10f * shake, 0, 10f * shake, 0, 0, 0, 0, 0, 0);
            float pile = t < 0.5f ? 0f : t < L - 1.4f ? S((t - 0.5f) / 0.45f) : 1f - S((t - (L - 1.4f)) / 0.8f);
            for (int j = 0; j < 10; j++) Pile(r, j, pile);
        }
        else if (act == 4)       // can't be bothered
        {
            float k = Ramp(t, 0.2f, 0.5f, L - 0.7f, L - 0.4f);
            Apply(r, 0, 0, 0, 0.03f * k, -6f * k, 0, 20f * k, 15f * k, 0, -12f * k, 0, 12f * k, -100f * k, -38f * k, -100f * k, 38f * k, 0, 0);
        }
        else if (act == 5)       // big finish into the microphone
        {
            float k = Ramp(t, 0.2f, 0.6f, L - 0.6f, L - 0.3f);
            Apply(r, 0, 0, 8f * Mathf.Sin(t * 2f) * k, 0.015f * Mathf.Abs(Mathf.Sin(t * 4f)) * k, -6f * k, -15f * k, 0, 0,
                  -20f * k, -60f * k, -20f * k, 60f * k, -40f * k, 0, -40f * k, 0, 0, 0);
        }
        else if (act == 6)       // point at you, then slap a knee laughing
        {
            float p = Ramp(t, 0.2f, 0.5f, 2.0f, 2.3f), l = Ramp(t, 2.3f, 2.6f, L - 0.6f, L - 0.3f), b = Mathf.Sin(t * 14f);
            Apply(r, 0, 0, 0, 0.02f * Mathf.Abs(b) * l, 25f * l + 6f * b * l, -10f * l, 0, 0,
                  -30f * l, 0, -80f * p - 30f * l, 0, -20f * l, 0, -20f * l, 0, 0, 0);
        }
        else if (act == 7)       // on hold, tapping a foot
        {
            float k = Ramp(t, 0.2f, 0.6f, L - 0.7f, L - 0.3f);
            Apply(r, 0, 0, 0, 0, 0, 0, 10f * k, -15f * k, 0, 0, -10f * k, 35f * k, 0, 0, -150f * k, 0,
                  -10f * Mathf.Max(0, Mathf.Sin(t * 6f)) * k, 0);
        }
        else if (act == 8)       // the right forearm drops off
        {
            float look = Ramp(t, 0.9f, 1.2f, L - 1.0f, L - 0.6f);
            float twitch = Ramp(t, 0.4f, 0.5f, 0.6f, 0.8f);
            Apply(r, 0, 0, 0, 0, 10f * look, 35f * look, 0, 0, 0, 0, -20f * twitch, 20f * twitch, 0, 0, 0, 0, 0, 0);
            float drop = t < 0.5f ? 0f : t < L - 0.9f ? S((t - 0.5f) / 0.35f) : 1f - S((t - (L - 0.9f)) / 0.4f);
            Pile(r, 6, drop);
        }
        else if (act == 9)       // hands on hips
        {
            float k = Ramp(t, 0.2f, 0.5f, L - 0.7f, L - 0.4f);
            Apply(r, 0, 0, 0, 0, -5f * k, 0, 0, -10f * k, 0, -40f * k, 0, 40f * k, 0, 100f * k, 0, -100f * k, 0, 0);
        }
        else if (act == 10)      // scared stiff
        {
            float k = Ramp(t, 0.1f, 0.3f, L - 0.5f, L - 0.2f), f = Mathf.Sin(t * 40f);
            Apply(r, 0, 0, 0, 0.01f * f * k, 10f * k, 0, 8f * Mathf.Sin(t * 25f) * k, 0,
                  0, -5f * k, 0, 5f * k, -100f * k, 50f * k, -100f * k, -50f * k, 6f * f * k, -6f * f * k);
        }
        else if (act == 11)      // take a bow
        {
            float k = Ramp(t, 0.3f + 0.3f * r, 0.8f + 0.3f * r, L - 1.0f, L - 0.6f);
            Apply(r, 0, 0, 0, 0, 55f * k, 10f * k, 0, 0, 0, -50f * k, -40f * k, 0, 0, 0, -80f * k, -40f * k, 0, 0);
        }
    }

    /// <summary>Poses rig r. Arm angles are given per side (no mirroring), in degrees.</summary>
    void Apply(int r, float x, float z, float yaw, float bob, float lean, float hx, float hy, float hz,
               float sLx, float sLz, float sRx, float sRz, float eLx, float eLz, float eRx, float eRz, float legL, float legR)
    {
        int b = r * 10;
        pivots[b].localPosition = restPos[b] + new Vector3(x, bob, z);
        pivots[b].localRotation = restRot[b] * Quaternion.Euler(0, yaw, 0);
        pivots[b + 1].localRotation = restRot[b + 1] * Quaternion.Euler(lean, 0, 0);
        pivots[b + 2].localRotation = restRot[b + 2] * Quaternion.Euler(hx, hy, hz);
        pivots[b + 3].localRotation = restRot[b + 3] * Quaternion.Euler(sLx, 0, sLz);
        pivots[b + 4].localRotation = restRot[b + 4] * Quaternion.Euler(sRx, 0, sRz);
        pivots[b + 5].localRotation = restRot[b + 5] * Quaternion.Euler(eLx, 0, eLz);
        pivots[b + 6].localRotation = restRot[b + 6] * Quaternion.Euler(eRx, 0, eRz);
        pivots[b + 7].localRotation = restRot[b + 7] * Quaternion.Euler(legL, 0, 0);
        pivots[b + 8].localRotation = restRot[b + 8] * Quaternion.Euler(legR, 0, 0);
    }

    /// <summary>Moves one part's mesh from where it belongs (k = 0) to lying on the floor in a pile (k = 1).</summary>
    void Pile(int r, int j, float k)
    {
        int i = r * 10 + j;
        Transform m = meshes[i];
        Transform owner = m.parent;
        Vector3 homePos = owner.TransformPoint(meshPos[i]);
        Quaternion homeRot = owner.rotation * meshRot[i];
        Vector3 rootPos = pivots[r * 10].position;
        Vector3 spot = rootPos + transform.rotation * new Vector3(Mathf.Sin(j * 12.9f) * 0.35f, 0.07f + 0.03f * (j % 3), 0.15f + Mathf.Cos(j * 7.3f) * 0.3f);
        Quaternion lie = transform.rotation * Quaternion.Euler(90f, j * 47f, (j % 2) * 20f);
        m.position = Vector3.Lerp(homePos, spot, k);
        m.rotation = Quaternion.Slerp(homeRot, lie, k);
    }

    float Ramp(float t, float a, float b, float c, float d)
    {
        if (t < a || t > d) return 0f;
        if (t < b) return S((t - a) / (b - a));
        if (t < c) return 1f;
        return 1f - S((t - c) / (d - c));
    }

    float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    // ---- calls to other scripts: plain Udon events (see FirGameManager's "public API")
    void GameEv(string ev) { if (game != null) game.SendCustomEvent(ev); }
    void GameEvA(string ev, int a) { if (game == null) return; game.SetProgramVariable("argA", a); game.SendCustomEvent(ev); }
    void GameEvAB(string ev, int a, int b) { if (game == null) return; game.SetProgramVariable("argA", a); game.SetProgramVariable("argB", b); game.SendCustomEvent(ev); }
    void GameEvF(string ev, float f) { if (game == null) return; game.SetProgramVariable("argF", f); game.SendCustomEvent(ev); }
    void GameEvAF(string ev, int a, float f) { if (game == null) return; game.SetProgramVariable("argA", a); game.SetProgramVariable("argF", f); game.SendCustomEvent(ev); }
    void GameSay(string text, float seconds) { if (game == null) return; game.SetProgramVariable("argText", text); game.SetProgramVariable("argF", seconds); game.SendCustomEvent("EvSay"); }
    int GameInt(string name) { return game != null ? (int)game.GetProgramVariable(name) : 0; }
    void Report() { if (watch == null) return; watch.SetProgramVariable("argWhat", gameObject.name); watch.SendCustomEvent("EvPressed"); }
}
