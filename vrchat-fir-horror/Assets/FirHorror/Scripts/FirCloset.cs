using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

/// <summary>
/// An easter egg: a tribute to the old cartoon gag of the skeleton in the closet ("RED SKELETON").
/// Open the Living Room closet and, for everyone at once, the door creaks open, a skeleton steps
/// out through green mist, shrugs with its palms up, and steps back in.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirCloset : UdonSharpBehaviour
{
    public FirGameManager game;
    public Transform hinge;          // the door swings round this (yaw 0 shut, 100 open)
    public Transform skeleton;       // faces out of the closet; slides along its parent's +X
    public Transform armL, armR;     // shoulder pivots
    public Transform foreL, foreR;   // elbow pivots
    public Transform skull;
    public GameObject mist;
    public AudioSource creak;
    public float stepOut = 0.75f;

    float startT = -100f, busyUntil;
    bool running;
    Vector3 skelRest;

    void Start()
    {
        skelRest = skeleton.localPosition;
        skeleton.gameObject.SetActive(false);
        if (mist != null) mist.SetActive(false);
    }

    public override void Interact()
    {
        if (Time.time < busyUntil) return;
        busyUntil = Time.time + 5f;
        SendCustomNetworkEvent(NetworkEventTarget.All, "NetOpen");
    }

    public void NetOpen()
    {
        if (running) return;
        running = true;
        startT = Time.time;
        skeleton.gameObject.SetActive(true);
        if (mist != null) mist.SetActive(true);
        if (creak != null) creak.Play();
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (Utilities.IsValid(lp) && Vector3.Distance(lp.GetPosition(), transform.position) < 14f)
            game.Say("<color=#ff3030><b>RED SKELETON</b></color>  <i>*shrugs*</i>", 3.2f);
    }

    void Update()
    {
        if (!running) return;
        float t = Time.time - startT;
        // door: open, hold, shut
        float door = t < 0.4f ? Smooth(t / 0.4f) : t < 3.2f ? 1f : 1f - Smooth((t - 3.2f) / 0.4f);
        hinge.localRotation = Quaternion.Euler(0, 100f * door, 0);
        // step out, wait, step back
        float step = t < 0.4f ? 0f : t < 1.0f ? Smooth((t - 0.4f) / 0.6f) : t < 2.6f ? 1f : t < 3.2f ? 1f - Smooth((t - 2.6f) / 0.6f) : 0f;
        float bob = (step > 0f && step < 1f) ? Mathf.Abs(Mathf.Sin(t * 14f)) * 0.03f : 0f;
        // the shrug
        float shrug = t < 1.1f ? 0f : t < 1.4f ? Smooth((t - 1.1f) / 0.3f) : t < 2.2f ? 1f : t < 2.5f ? 1f - Smooth((t - 2.2f) / 0.3f) : 0f;
        skeleton.localPosition = skelRest + new Vector3(stepOut * step, bob + 0.04f * shrug, 0);
        // elbows stay by the ribs, forearms come forward and out, palms up
        armL.localRotation = Quaternion.Euler(0, 0, -12f * shrug);
        armR.localRotation = Quaternion.Euler(0, 0, 12f * shrug);
        foreL.localRotation = Quaternion.Euler(-100f * shrug, 0, -38f * shrug);
        foreR.localRotation = Quaternion.Euler(-100f * shrug, 0, 38f * shrug);
        skull.localRotation = Quaternion.Euler(0, 0, 12f * shrug);
        if (t > 3.7f)
        {
            running = false;
            hinge.localRotation = Quaternion.identity;
            skeleton.localPosition = skelRest;
            skeleton.gameObject.SetActive(false);
            if (mist != null) mist.SetActive(false);
        }
    }

    float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
}
