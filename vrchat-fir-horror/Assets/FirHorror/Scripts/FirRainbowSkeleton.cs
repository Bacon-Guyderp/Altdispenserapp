using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.Udon;

/// <summary>
/// One skeleton in the rainbow line-up. Press it and it does its routine and says its line (its jaw flaps
/// while it talks). Self-contained: it only moves its own bones, so it needs nothing else to work.
/// pivots / meshes: 0 root, 1 torso, 2 skull, 3 shoulderL, 4 shoulderR, 5 elbowL, 6 elbowR, 7 legL, 8 legR, 9 jaw.
/// Acts: 0 red (leans out, hands on hips), 1 orange (knocks on thin air, shrugs), 2 yellow (trembles, points at
/// its ribcage), 3 green (woozy wobble), 4 blue (sad slow dance), 5 indigo (huge shrug, scratches its skull),
/// 6 violet (goes to pieces and back), 7 white (shrugs: "A skeleton."), 8 pink (giggles), 9 teal (ghostly), 10 gold (flexes),
/// 11 silver (sad wave), 12 black (sneaks, BOO), 13 brown (rubs its belly), 14 glow (floats), 15 rainbow (disco),
/// 16 tiny (bounces), 17 giant (stretches).
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirRainbowSkeleton : UdonSharpBehaviour
{
    public int act;
    public string colourName = "";
    public Transform[] pivots;
    public Transform[] meshes;
    public AudioSource voice;
    public float voiceDelay = 0.4f;
    public TextMeshPro label;

    Vector3[] restPos, meshPos;
    Quaternion[] restRot, meshRot;
    float startT, length, talkFrom, talkUntil;
    bool running, voiced, ready;

    void Start()
    {
        int n = pivots.Length;
        restPos = new Vector3[n]; restRot = new Quaternion[n];
        meshPos = new Vector3[n]; meshRot = new Quaternion[n];
        for (int i = 0; i < n; i++)
        {
            restPos[i] = pivots[i].localPosition; restRot[i] = pivots[i].localRotation;
            meshPos[i] = meshes[i].localPosition; meshRot[i] = meshes[i].localRotation;
        }
        ready = true;
    }

    public override void Interact()
    {
        if (!ready || running) return;
        Play();
        Share(3);
    }

    public void Play()
    {
        if (!ready || running) return;
        running = true;
        voiced = false;
        startT = Time.time;
        float clip = voice != null && voice.clip != null ? voice.clip.length : 2f;
        length = Mathf.Max(4f, voiceDelay + clip + 1.0f);
        talkFrom = 999f;
        talkUntil = 0f;
        if (label != null) label.text = "<b>" + colourName + "</b>";
    }

    void Update()
    {
        if (!running) return;
        float t = Time.time - startT;
        if (!voiced && t >= voiceDelay)
        {
            voiced = true;
            if (voice != null) voice.Play();
            talkFrom = t;
            talkUntil = t + (voice != null && voice.clip != null ? voice.clip.length - 0.25f : 1.5f);
        }
        Pose(t);
        float flap = t > talkFrom && t < talkUntil ? Mathf.Abs(Mathf.Sin((t - talkFrom) * 13f)) : 0f;
        pivots[9].localRotation = restRot[9] * Quaternion.Euler(28f * flap, 0, 0);
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

    void Pose(float t)
    {
        float L = length;
        if (act == 0)          // red: leans out, then hands on hips
        {
            float lean = Ramp(t, 0.1f, 0.5f, 1.1f, 1.5f);
            float k = Ramp(t, 1.3f, 1.6f, L - 0.6f, L - 0.3f);
            Apply(0, 0, 0, 0, 25f * lean - 5f * k, 0, 15f * lean, -10f * k, 0, -40f * k, 0, 40f * k, 0, 100f * k, 0, -100f * k, 0, 0);
        }
        else if (act == 1)     // orange: knock knock on thin air, then a shrug
        {
            float arm = Ramp(t, 0.1f, 0.35f, 1.6f, 1.9f);
            float knock = t < 1.6f ? Mathf.Max(0f, Mathf.Sin(t * 14f)) : 0f;
            float s = Ramp(t, 1.9f, 2.2f, L - 0.6f, L - 0.3f);
            Apply(0, 0, 0, 0, 0, 0, 0, 10f * s, 0, -12f * s, -70f * arm, 12f * s,
                  -100f * s, -38f * s, -60f * arm * (1f - 0.5f * knock) - 100f * s, 38f * s, 0, 0);
        }
        else if (act == 2)     // yellow: scared stiff, then points at its own empty ribcage
        {
            float shake = Ramp(t, 0.1f, 0.3f, 1.4f, 1.7f), f = Mathf.Sin(t * 40f);
            float p = Ramp(t, 1.7f, 2.0f, L - 0.6f, L - 0.3f);
            Apply(0, 0, 0, 0.01f * f * shake, 10f * shake + 15f * p, 0, 8f * Mathf.Sin(t * 25f) * shake, 0,
                  0, -5f * shake, -40f * p, 5f * shake, -100f * shake, 50f * shake, -100f * shake - 70f * p, -50f * shake, 6f * f * shake, -6f * f * shake);
        }
        else if (act == 3)     // green: woozy, hand on its skull, nearly topples
        {
            float k = Ramp(t, 0.2f, 0.6f, L - 0.7f, L - 0.3f), w = Mathf.Sin(t * 2.6f);
            Apply(0, 0, 0, 0, 8f * w * k, 10f * k, 12f * w * k, 15f * Mathf.Sin(t * 1.9f) * k,
                  15f * w * k, -30f * k, -150f * k, 20f * k, 20f * k, 0, -60f * k, 0, 10f * w * k, -10f * w * k);
        }
        else if (act == 4)     // blue: a slow, sad dance with an invisible partner
        {
            float k = Ramp(t, 0.2f, 0.8f, L - 0.8f, L - 0.3f), sway = Mathf.Sin(t * 1.8f);
            Apply(0.06f * sway * k, 0, 25f * sway * k, 0.01f * Mathf.Abs(sway) * k, 10f * k, 25f * k, 0, 6f * sway * k,
                  -70f * k, -15f * k, -70f * k, 15f * k, -50f * k, 0, -50f * k, 0, 8f * sway * k, -8f * sway * k);
        }
        else if (act == 5)     // indigo: the biggest shrug, then scratches its skull
        {
            float s = Ramp(t, 0.2f, 0.5f, 1.8f, 2.1f);
            float sc = Ramp(t, 2.1f, 2.4f, L - 0.6f, L - 0.3f), r = Mathf.Sin(t * 16f);
            Apply(0, 0, 0, 0.05f * s, -6f * s, 0, 25f * s, 18f * s - 10f * sc, 0, -14f * s, -150f * sc, 14f * s + 20f * sc,
                  -110f * s, -45f * s, -110f * s - 60f * sc + 10f * r * sc, 45f * s, 0, 0);
        }
        else if (act == 6)     // violet: goes to pieces, then puts itself back together
        {
            float shake = Ramp(t, 0f, 0.1f, 0.4f, 0.5f);
            Apply(0, 0, 0, 0, 0, 0, 10f * Mathf.Sin(t * 40f) * shake, 0, 0, -10f * shake, 0, 10f * shake, 0, 0, 0, 0, 0, 0);
            float pile = t < 0.5f ? 0f : t < L - 1.4f ? S((t - 0.5f) / 0.45f) : 1f - S((t - (L - 1.4f)) / 0.8f);
            for (int j = 0; j < 10; j++) Pile(j, pile);
        }
        else if (act == 8)     // pink: doubled over with giggles, slapping its knees
        {
            float k = Ramp(t, 0.2f, 0.5f, L - 0.6f, L - 0.3f), g = Mathf.Sin(t * 18f), slap = Mathf.Abs(Mathf.Sin(t * 7f));
            Apply(0, 0, 0, 0.02f * Mathf.Abs(g) * k, (32f + 6f * g) * k, -15f * k, 12f * Mathf.Sin(t * 9f) * k, 8f * g * k,
                  -(15f + 25f * slap) * k, -8f * k, -(15f + 25f * (1f - slap)) * k, 8f * k, -20f * k, 0, -20f * k, 0, 0, 0);
        }
        else if (act == 9)     // teal: ghostly arms out, swaying ("ooooh")
        {
            float k = Ramp(t, 0.3f, 1.0f, L - 0.8f, L - 0.3f), w = Mathf.Sin(t * 1.6f);
            Apply(0, 0, 15f * w * k, 0.04f * (1f + Mathf.Sin(t * 2.2f)) * k, 6f * k, -10f * k, 10f * w * k, 0,
                  -90f * k, -10f * w * k, -90f * k, -10f * w * k, 35f * k, 0, 35f * k, 0, 0, 0);
        }
        else if (act == 10)    // gold: bodybuilder flex, double biceps
        {
            float k = Ramp(t, 0.2f, 0.5f, L - 0.6f, L - 0.3f), pump = Mathf.Abs(Mathf.Sin(t * 5f));
            Apply(0, 0, 0, 0.015f * pump * k, -8f * k, -10f * k, 0, 0, 0, -85f * k, 0, 85f * k,
                  0, -(100f + 25f * pump) * k, 0, (100f + 25f * pump) * k, -10f * k, 10f * k);
        }
        else if (act == 11)    // silver: hangs its head, then a sad little wave
        {
            float k = Ramp(t, 0.2f, 0.8f, L - 0.6f, L - 0.3f), wv = Ramp(t, 2.2f, 2.5f, 3.6f, 3.9f);
            Apply(0, 0, 0, -0.03f * k, 12f * k, 30f * k - 15f * wv, 0, 0, 5f * k, -5f * k, -110f * wv, 5f * k,
                  0, 0, -40f * wv, 25f * Mathf.Sin(t * 7f) * wv, 0, 0);
        }
        else if (act == 12)    // black: sneaks side to side, then BOO
        {
            float sn = Ramp(t, 0.1f, 0.4f, 2.8f, 3.0f), boo = Ramp(t, 3.0f, 3.15f, L - 0.6f, L - 0.3f);
            float side = Mathf.Sin(t * 3f) * 0.25f * sn;
            float step = Mathf.Max(0f, Mathf.Sin(t * 6f));
            Apply(side, 0.1f * boo, 0, -0.06f * sn + 0.04f * boo, 25f * sn - 10f * boo, 0, -30f * sn, 0,
                  -60f * sn - 140f * boo, -20f * boo, -60f * sn - 140f * boo, 20f * boo, -70f * sn, 0, -70f * sn, 0, -25f * step * sn, -25f * (1f - step) * sn);
        }
        else if (act == 13)    // brown: rubs where its belly would be, then shrugs
        {
            float rub = Ramp(t, 0.2f, 0.5f, 2.6f, 2.9f), s = Ramp(t, 2.9f, 3.2f, L - 0.6f, L - 0.3f), c = Mathf.Sin(t * 6f);
            Apply(0, 0, 0, 0.02f * s, -6f * s, 10f * rub, 0, 15f * s, -40f * rub, -12f * s, -40f * rub + 8f * c * rub, 12f * s + 8f * Mathf.Cos(t * 6f) * rub,
                  -90f * rub - 100f * s, -38f * s, -90f * rub - 100f * s, 38f * s, 0, 0);
        }
        else if (act == 14)    // glow: floats, arms out like a ghost light
        {
            float k = Ramp(t, 0.3f, 1.0f, L - 0.8f, L - 0.3f), w = Mathf.Sin(t * 1.4f);
            Apply(0, 0, 10f * w * k, (0.12f + 0.06f * Mathf.Sin(t * 2f)) * k, 0, -8f * k, 8f * w * k, 6f * w * k,
                  -20f * k, -80f * k, -20f * k, 80f * k, 0, 0, 0, 0, 15f * k, -15f * k);
        }
        else if (act == 15)    // rainbow: disco, pointing up then down across
        {
            float k = Ramp(t, 0.1f, 0.3f, L - 0.6f, L - 0.3f), beat = Mathf.Sin(t * 5f), up = beat > 0f ? 1f : 0f;
            Apply(0.08f * beat * k, 0, 15f * beat * k, 0.02f * Mathf.Abs(beat) * k, 0, 0, 0, 10f * beat * k,
                  0, -40f * k, (-160f * up - 30f * (1f - up)) * k, (20f * up - 40f * (1f - up)) * k, -30f * k, 0, 0, 0, -15f * Mathf.Max(0f, beat) * k, -15f * Mathf.Max(0f, -beat) * k);
        }
        else if (act == 16)    // tiny: bounces with excitement, arms up
        {
            float k = Ramp(t, 0.1f, 0.3f, L - 0.5f, L - 0.2f), j = Mathf.Abs(Mathf.Sin(t * 7f));
            Apply(0, 0, 0, 0.18f * j * k, -5f * k, -10f * k, 0, 10f * Mathf.Sin(t * 7f) * k,
                  -160f * k, -20f * k + 15f * Mathf.Sin(t * 14f) * k, -160f * k, 20f * k - 15f * Mathf.Sin(t * 14f) * k, 0, 0, 0, 0, -20f * j * k, -20f * j * k);
        }
        else if (act == 17)    // giant: a big, slow stretch, then pats its non-existent belly
        {
            float st = Ramp(t, 0.3f, 1.3f, 2.4f, 3.0f), pat = Ramp(t, 3.0f, 3.3f, L - 0.6f, L - 0.3f), p = Mathf.Abs(Mathf.Sin(t * 8f));
            Apply(0, 0, 0, 0.03f * st, -12f * st + 5f * pat, -20f * st, 0, 0, -170f * st - 30f * pat, -15f * st, -170f * st - 40f * pat, 15f * st,
                  -20f * st - 90f * pat, 0, -20f * st - 90f * pat + 15f * p * pat, 0, 0, 0);
        }
        else                   // white: a lazy shrug ("A skeleton.")
        {
            float k = Ramp(t, 0.2f, 0.5f, L - 0.7f, L - 0.4f);
            Apply(0, 0, 0, 0.03f * k, -6f * k, 0, 20f * k, 15f * k, 0, -12f * k, 0, 12f * k, -100f * k, -38f * k, -100f * k, 38f * k, 0, 0);
        }
    }

    /// <summary>Poses the skeleton. Arm angles are given per side (no mirroring), in degrees.</summary>
    void Apply(float x, float z, float yaw, float bob, float lean, float hx, float hy, float hz,
               float sLx, float sLz, float sRx, float sRz, float eLx, float eLz, float eRx, float eRz, float legL, float legR)
    {
        pivots[0].localPosition = restPos[0] + new Vector3(x, bob, z);
        pivots[0].localRotation = restRot[0] * Quaternion.Euler(0, yaw, 0);
        pivots[1].localRotation = restRot[1] * Quaternion.Euler(lean, 0, 0);
        pivots[2].localRotation = restRot[2] * Quaternion.Euler(hx, hy, hz);
        pivots[3].localRotation = restRot[3] * Quaternion.Euler(sLx, 0, sLz);
        pivots[4].localRotation = restRot[4] * Quaternion.Euler(sRx, 0, sRz);
        pivots[5].localRotation = restRot[5] * Quaternion.Euler(eLx, 0, eLz);
        pivots[6].localRotation = restRot[6] * Quaternion.Euler(eRx, 0, eRz);
        pivots[7].localRotation = restRot[7] * Quaternion.Euler(legL, 0, 0);
        pivots[8].localRotation = restRot[8] * Quaternion.Euler(legR, 0, 0);
    }

    /// <summary>Moves one bone's mesh from where it belongs (k = 0) to lying in a pile on the floor (k = 1).</summary>
    void Pile(int j, float k)
    {
        Transform m = meshes[j];
        Transform owner = m.parent;
        Vector3 homePos = owner.TransformPoint(meshPos[j]);
        Quaternion homeRot = owner.rotation * meshRot[j];
        Vector3 rootPos = pivots[0].position;
        Vector3 spot = rootPos + transform.rotation * new Vector3(Mathf.Sin(j * 12.9f) * 0.35f, 0.07f + 0.03f * (j % 3), 0.15f + Mathf.Cos(j * 7.3f) * 0.3f);
        Quaternion lie = transform.rotation * Quaternion.Euler(90f, j * 47f, (j % 2) * 20f);
        m.position = Vector3.Lerp(homePos, spot, k);
        m.rotation = Quaternion.Slerp(homeRot, lie, k);
    }

    float Ramp(float t, float a, float b, float c, float d)
    {
        if (t < a || t > d) return 0f;
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
