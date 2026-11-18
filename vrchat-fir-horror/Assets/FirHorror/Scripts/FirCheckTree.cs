using TMPro;
using UdonSharp;
using UnityEngine;

/// <summary>Systems check: a talking tree. Press it (or test 8 sends it "Talk"): it sways and its mouth moves.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirCheckTree : UdonSharpBehaviour
{
    public Transform head;
    public Transform mouth;
    public AudioSource voice;
    public TextMeshPro label;

    Quaternion headRest;
    Vector3 mouthRest;
    float until;
    bool ready;

    void Start()
    {
        headRest = head.localRotation;
        mouthRest = mouth.localScale;
        ready = true;
        if (label != null) label.text = "<b>Tree</b>\n<color=#70ff90>its script is running</color>. Press it!";
    }

    public override void Interact() { Talk(); }

    public void Talk()
    {
        if (!ready) return;
        until = Time.time + 3f;
        if (voice != null) voice.Play();
        if (label != null) label.text = "<b>Tree</b>\n<color=#70ff90><b>PASS</b></color> it is talking";
    }

    void Update()
    {
        if (!ready) return;
        float t = Time.time;
        bool talking = t < until;
        float open = talking ? Mathf.Abs(Mathf.Sin(t * 13f)) : 0f;
        mouth.localScale = new Vector3(mouthRest.x, mouthRest.y * (0.2f + 1.6f * open), mouthRest.z);
        float sway = talking ? 8f : 2f;
        head.localRotation = headRest * Quaternion.Euler(Mathf.Sin(t * 1.3f) * sway * 0.4f, Mathf.Sin(t * 0.9f) * sway, 0);
    }
}
