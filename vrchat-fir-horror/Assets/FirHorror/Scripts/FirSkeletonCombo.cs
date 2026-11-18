using TMPro;
using UdonSharp;
using UnityEngine;

/// <summary>
/// A combination act: several gag skeletons perform together and trade lines. Press its button on the
/// Combo Acts console. It moves the skeletons' bones itself (Transform references only, no calls into
/// other scripts), so it needs nothing else to work.
/// pivots: participant r's bones are pivots[r * 10 + j]: 0 root, 1 torso, 2 skull, 3 shoulderL, 4 shoulderR,
/// 5 elbowL, 6 elbowR, 7 legL, 8 legR, 9 jaw. lines[i] starts at lineStart[i]; lineSpeaker[i] is the participant
/// whose jaw moves (-1 = everyone, a chorus).
/// Combos: 0 traffic lights (green, yellow, red), 1 knock knock (orange, yellow), 2 the Bone-Tones (blue, indigo,
/// violet), 3 medal ceremony (gold, silver, brown), 4 upsy-daisy (tiny, giant), 5 grand finale (everyone).
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirSkeletonCombo : UdonSharpBehaviour
{
    public int combo;
    public string title = "";
    public Transform[] pivots;
    public AudioSource[] lines;
    public float[] lineStart;
    public int[] lineSpeaker;
    public float length = 10f;
    public TextMeshPro label;

    Vector3[] restPos;
    Quaternion[] restRot;
    float[] talkUntil;
    bool[] played;
    int riders;
    float startT;
    bool running, ready;

    void Start()
    {
        int n = pivots.Length;
        riders = n / 10;
        restPos = new Vector3[n];
        restRot = new Quaternion[n];
        for (int i = 0; i < n; i++) { restPos[i] = pivots[i].localPosition; restRot[i] = pivots[i].localRotation; }
        talkUntil = new float[riders];
        played = new bool[lines.Length];
        ready = true;
        Show("<color=#cccccc>press to play</color>");
    }

    public override void Interact()
    {
        if (!ready || running) return;
        running = true;
        startT = Time.time;
        for (int i = 0; i < played.Length; i++) played[i] = false;
        for (int r = 0; r < riders; r++) talkUntil[r] = 0f;
        Show("<color=#70ff90>playing...</color>");
    }

    void Show(string state)
    {
        if (label != null) label.text = "<b>" + title + "</b>\n" + state;
    }

    void Update()
    {
        if (!running) return;
        float t = Time.time - startT;
        for (int i = 0; i < lines.Length; i++)
        {
            if (played[i] || t < lineStart[i]) continue;
            played[i] = true;
            if (lines[i] != null) lines[i].Play();
            float len = lines[i] != null && lines[i].clip != null ? lines[i].clip.length - 0.25f : 1.5f;
            int who = lineSpeaker[i];
            for (int r = 0; r < riders; r++) if (who < 0 || who == r) talkUntil[r] = t + len;
        }
        for (int r = 0; r < riders; r++)
        {
            Pose(r, t);
            float flap = t < talkUntil[r] ? Mathf.Abs(Mathf.Sin(t * 13f + r)) : 0f;
            pivots[r * 10 + 9].localRotation = restRot[r * 10 + 9] * Quaternion.Euler(28f * flap, 0, 0);
        }
        if (t >= length)
        {
            running = false;
            for (int i = 0; i < pivots.Length; i++) { pivots[i].localPosition = restPos[i]; pivots[i].localRotation = restRot[i]; }
            Show("<color=#cccccc>press to play again</color>");
        }
    }

    float Line(int i) { return i < lineStart.Length ? lineStart[i] : length; }

    void Pose(int r, float t)
    {
        float L = length;
        float k = Ramp(t, 0.1f, 0.5f, L - 0.7f, L - 0.3f);
        if (combo == 0)            // traffic lights: green, yellow, red each step forward on their line
        {
            float s = Ramp(t, Line(r) - 0.2f, Line(r) + 0.25f, L - 0.8f, L - 0.4f);
            if (r == 0)      Apply(r, 0, 0.2f * s, 0, 0, -5f * s, -10f * s, 0, 0, 0, -10f * s, -130f * s, 10f * s, 0, 0, -10f * s, 0, 0, 0);
            else if (r == 1) Apply(r, 0, 0.2f * s, 0, 0, 8f * s, 10f * s, 0, 0, -45f * s, 0, -45f * s, 0, (-20f + 20f * Mathf.Sin(t * 6f)) * s, 0, (-20f + 20f * Mathf.Sin(t * 6f)) * s, 0, 0, 0);
            else             Apply(r, 0, 0.2f * s, 0, 0, -6f * s, 0, 15f * s, -10f * s, 0, -40f * s, 0, 40f * s, 0, 100f * s, 0, -100f * s, 0, 0);
        }
        else if (combo == 1)       // knock knock: orange knocks and laughs, yellow asks and facepalms
        {
            if (r == 0)
            {
                float arm = Ramp(t, Line(0) - 0.1f, Line(0) + 0.15f, Line(1) - 0.2f, Line(1));
                float knock = Mathf.Max(0f, Mathf.Sin(t * 14f)) * arm;
                float lg = Ramp(t, Line(4) + 1.6f, Line(4) + 1.9f, L - 0.5f, L - 0.2f), b = Mathf.Sin(t * 14f);
                Apply(r, 0, 0, 0, 0.02f * Mathf.Abs(b) * lg, -15f * lg + 6f * b * lg, 0, 40f * k, 0, 0, 0, -70f * arm, 0,
                      0, 0, -60f * arm * (1f - 0.5f * knock), 0, 0, 0);
            }
            else
            {
                float sh = Ramp(t, Line(1) - 0.1f, Line(1) + 0.2f, Line(1) + 1.1f, Line(1) + 1.4f) + Ramp(t, Line(3) - 0.1f, Line(3) + 0.2f, Line(3) + 1.1f, Line(3) + 1.4f);
                float fp = Ramp(t, Line(5) - 0.3f, Line(5) + 0.1f, L - 0.5f, L - 0.2f);
                Apply(r, 0, 0, 0, 0, 6f * fp, 18f * fp, -40f * k * (1f - fp), 15f * sh, 0, -12f * sh, -70f * fp, 12f * sh,
                      -100f * sh, -38f * sh, -100f * sh - 110f * fp, 38f * sh, 0, 0);
            }
        }
        else if (combo == 2)       // the Bone-Tones: sway and snap in unison, indigo solos, then ta-da
        {
            float sway = Mathf.Sin(t * 3.2f), snap = Mathf.Sin(t * 6.4f);
            float f = Ramp(t, L - 1.6f, L - 1.3f, L - 0.6f, L - 0.3f);
            float solo = r == 1 ? Ramp(t, Line(1) - 0.2f, Line(1) + 0.2f, Line(2) - 0.1f, Line(2) + 0.2f) : 0f;
            float m = k * (1f - f);
            Apply(r, 0.04f * sway * m, 0.2f * solo, 12f * sway * m, 0.015f * Mathf.Abs(sway) * m, 0, 0, 0, 8f * sway * m,
                  -15f * m, -75f * f, -15f * m, 75f * f, -95f * m, 20f * snap * m, -95f * m, -20f * snap * m, 0, 0);
        }
        else if (combo == 3)       // medal ceremony: up onto the podiums; gold flexes, silver sulks, brown waves
        {
            float h = r == 0 ? 0.25f : (r == 1 ? 0.13f : 0.06f);
            float s = Ramp(t, Line(r) - 0.2f, Line(r) + 0.2f, L - 0.8f, L - 0.4f);
            float up = Ramp(t, 0.2f, 0.8f, L - 0.8f, L - 0.3f);
            if (r == 0)
            {
                float pump = Mathf.Abs(Mathf.Sin(t * 5f));
                Apply(r, 0, 0, 0, h * up, -8f * s, -10f * s, 0, 0, 0, -85f * s, 0, 85f * s, 0, -(100f + 25f * pump) * s, 0, (100f + 25f * pump) * s, 0, 0);
            }
            else if (r == 1) Apply(r, 0, 0, 0, h * up, 10f * up, 25f * up, 0, 0, 5f * up, -5f * up, -60f * s, 5f * up, 0, 0, -30f * s, 0, 0, 0);
            else             Apply(r, 0, 0, 0, h * up, 0, -8f * s, 0, 0, 0, 0, -150f * s, 0, 0, 0, 0, 30f * Mathf.Sin(t * 8f) * s, 0, 0);
        }
        else if (combo == 4)       // upsy-daisy: the giant lifts tiny high overhead and puts it back
        {
            float lift = Ramp(t, Line(1) + 0.8f, Line(2) - 0.1f, Line(4) - 0.9f, Line(4) - 0.1f);
            if (r == 0)
            {
                float b = Ramp(t, 0.1f, 0.3f, Line(1), Line(1) + 0.3f), j = Mathf.Abs(Mathf.Sin(t * 7f));
                float kick = Mathf.Sin(t * 9f) * lift;
                // tiny is half size, so its offsets are in its own (scaled) units: up over the giant's head
                Apply(r, 2.05f * lift, 0, 0, 0.18f * j * b + 4.4f * lift, 0, -10f * (b + lift), 0, 10f * Mathf.Sin(t * 7f) * b,
                      -160f * (b + lift), -20f * b, -160f * (b + lift), 20f * b, 0, 0, 0, 0, 25f * kick, -25f * kick);
            }
            else
            {
                float bend = Ramp(t, Line(1) - 0.1f, Line(1) + 0.4f, Line(1) + 0.8f, Line(1) + 1.1f);
                float strain = Ramp(t, Line(3) - 0.1f, Line(3) + 0.2f, Line(3) + 1.6f, Line(3) + 2.0f);
                float shake = Mathf.Sin(t * 30f) * strain;
                Apply(r, 0, 0, -25f * (bend + lift), 0, 35f * bend - 8f * lift + 3f * shake, 20f * bend, -20f * (bend + lift), 4f * shake,
                      -60f * bend - 170f * lift, -10f * lift, -60f * bend - 170f * lift, 10f * lift, -20f * bend, 0, -20f * bend, 0, 0, 0);
            }
        }
        else                       // grand finale: a Mexican wave down the lines, a shimmy, then a bow
        {
            float d = r * 0.12f;
            float w = Ramp(t, 0.3f + d, 0.6f + d, 1.3f + d, 1.7f + d);
            float sh = Ramp(t, 3.0f, 3.4f, L - 1.9f, L - 1.6f), o = Mathf.Sin(t * 9f + r);
            float bow = Ramp(t, L - 1.6f, L - 1.2f, L - 0.6f, L - 0.3f);
            Apply(r, 0.05f * o * sh, 0, 0, 0.02f * Mathf.Abs(o) * sh, 5f * sh + 50f * bow, 10f * bow, 0, 10f * Mathf.Sin(t * 4.5f) * sh,
                  -160f * w + 25f * o * sh, -10f * sh, -160f * w - 25f * o * sh, 10f * sh, -90f * sh, 0, -90f * sh, 0, 10f * o * sh, -10f * o * sh);
        }
    }

    /// <summary>Poses participant r. Arm angles are given per side (no mirroring), in degrees.</summary>
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

    float Ramp(float t, float a, float b, float c, float d)
    {
        if (t < a || t > d) return 0f;
        if (t < b) return S((t - a) / (b - a));
        if (t > c) return 1f - S((t - c) / (d - c));
        return 1f;
    }

    float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
}
