using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Something you can pick up and throw (a VRCPickup with a Rigidbody and a VRCObjectSync on the same object,
/// so everyone sees where it lands). It whooshes when grabbed and thuds when it lands. Self-contained.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirThrowable : UdonSharpBehaviour
{
    public VRC_Pickup pickup;
    public Rigidbody body;
    public AudioSource audioSrc;
    public AudioClip grabClip, landClip;

    float lastSpeed, nextLand;

    public override void OnPickup()
    {
        if (audioSrc != null && grabClip != null) audioSrc.PlayOneShot(grabClip, 0.6f);
    }

    void Update()
    {
        if (pickup != null && pickup.IsHeld) { lastSpeed = 0f; return; }
        float speed = body != null ? body.velocity.magnitude : 0f;
        // landed: it was flying and has suddenly slowed right down
        if (lastSpeed > 1.5f && speed < lastSpeed * 0.45f && Time.time > nextLand && audioSrc != null && landClip != null)
        {
            nextLand = Time.time + 0.3f;
            audioSrc.PlayOneShot(landClip, Mathf.Clamp01(lastSpeed / 7f));
        }
        lastSpeed = speed;
    }
}
