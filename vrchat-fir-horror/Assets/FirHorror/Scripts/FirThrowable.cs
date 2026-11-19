using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

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

    public UdonBehaviour hunt;       // the horror game: a hit on the tree stuns it
    public Transform stalker;        // the hunting tree

    float lastSpeed, nextLand, nextStun;

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
        // the thrower's client checks for a hit on the hunting tree
        if (stalker != null && hunt != null && speed > 2.5f && Time.time > nextStun && Networking.IsOwner(gameObject))
        {
            Vector3 d = transform.position - (stalker.position + Vector3.up * 1.2f);
            if (Mathf.Abs(d.y) < 1.4f && new Vector2(d.x, d.z).magnitude < 0.9f)
            {
                nextStun = Time.time + 3f;
                hunt.SendCustomEvent("EvStun");
            }
        }
    }
}
