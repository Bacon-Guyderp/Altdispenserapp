using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// The speed zone: boost pads and springs fling you (each player's own client sets their velocity), and rings
/// you run through are collected into your own counter (they come back after a while). Nothing is synced.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirBoost : UdonSharpBehaviour
{
    public Transform[] pads;          // each pad's centre; its forward and up set the push
    public Vector3[] padVelocity;     // in the pad's own frame (z forward, y up)
    public float padRadius = 1.2f;
    public Transform[] rings;
    public float ringRadius = 1.0f;
    public TextMeshPro counter;
    public AudioSource sfx;
    public AudioClip boostClip, springClip, ringClip;

    float[] padReady, ringBack;
    int count;
    bool inited;

    void Start() { Init(); }

    void Init()
    {
        if (inited) return;
        inited = true;
        padReady = new float[pads.Length];
        ringBack = new float[rings.Length];
        Show();
    }

    void Update()
    {
        Init();
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        Vector3 p = lp.GetPosition();
        for (int i = 0; i < pads.Length; i++)
        {
            if (pads[i] == null || Time.time < padReady[i]) continue;
            Vector3 d = p - pads[i].position;
            if (Mathf.Abs(d.y) > 0.8f) continue;
            d.y = 0f;
            if (d.magnitude > padRadius) continue;
            padReady[i] = Time.time + 0.6f;
            Vector3 v = pads[i].rotation * padVelocity[i];
            lp.SetVelocity(v);
            if (sfx != null) { AudioClip c = padVelocity[i].y > padVelocity[i].z ? springClip : boostClip; if (c != null) sfx.PlayOneShot(c, 0.8f); }
        }
        for (int i = 0; i < rings.Length; i++)
        {
            if (rings[i] == null) continue;
            if (!rings[i].gameObject.activeSelf)
            {
                if (Time.time > ringBack[i]) rings[i].gameObject.SetActive(true);
                continue;
            }
            if ((rings[i].position - (p + Vector3.up * 0.9f)).magnitude > ringRadius) continue;
            rings[i].gameObject.SetActive(false);
            ringBack[i] = Time.time + 20f;
            count++;
            if (sfx != null && ringClip != null) sfx.PlayOneShot(ringClip, 0.7f);
            Show();
        }
        for (int i = 0; i < rings.Length; i++) if (rings[i] != null && rings[i].gameObject.activeSelf) rings[i].localRotation = rings[i].localRotation * Quaternion.Euler(0, 180f * Time.deltaTime, 0);
    }

    void Show() { if (counter != null) counter.text = "<b>RINGS: " + count + "</b>"; }

    public int Count() { return count; }
}
