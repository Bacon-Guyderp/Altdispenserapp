using TMPro;
using UdonSharp;
using UnityEngine;

/// <summary>
/// Systems check: your skeleton model. Press it (or test 7 sends it "Act") and it waves and talks.
/// pivots: 0 root, 1 torso, 2 skull, 3 shoulderL, 4 shoulderR, 5 elbowL, 6 elbowR, 7 legL, 8 legR, 9 jaw.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirCheckSkeleton : UdonSharpBehaviour
{
    public Transform[] pivots;
    public AudioSource voice;
    public TextMeshPro label;

    Quaternion[] rest;
    float startT = -100f;
    bool ready;

    void Start()
    {
        rest = new Quaternion[pivots.Length];
        for (int i = 0; i < pivots.Length; i++) rest[i] = pivots[i].localRotation;
        ready = true;
        if (label != null) label.text = "<b>Skeleton</b> (your model)\n<color=#70ff90>its script is running</color>. Press it!";
    }

    public override void Interact() { Act(); }

    public void Act()
    {
        if (!ready) return;
        startT = Time.time;
        if (voice != null) voice.Play();
        if (label != null) label.text = "<b>Skeleton</b> (your model)\n<color=#70ff90><b>PASS</b></color> it is waving";
    }

    void Update()
    {
        if (!ready) return;
        float t = Time.time - startT;
        if (t > 3.5f)
        {
            if (t < 3.6f) for (int i = 0; i < pivots.Length; i++) pivots[i].localRotation = rest[i];
            return;
        }
        float k = Mathf.Clamp01(t * 4f) * Mathf.Clamp01((3.5f - t) * 4f);
        float wave = Mathf.Sin(t * 9f);
        pivots[4].localRotation = rest[4] * Quaternion.Euler(-150f * k, 0, 0);
        pivots[6].localRotation = rest[6] * Quaternion.Euler(0, 0, 35f * wave * k);
        pivots[2].localRotation = rest[2] * Quaternion.Euler(0, 15f * Mathf.Sin(t * 3f) * k, 10f * k);
        pivots[9].localRotation = rest[9] * Quaternion.Euler(25f * Mathf.Abs(Mathf.Sin(t * 12f)) * k, 0, 0);
    }
}
