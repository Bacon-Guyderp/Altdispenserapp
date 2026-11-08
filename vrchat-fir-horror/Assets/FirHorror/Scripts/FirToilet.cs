using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

/// <summary>
/// A throwable toilet. Pick it up, press Use to flush it, throw it at things. It clunks when
/// it lands, and a hit on the hunting tree stuns it for a few seconds.
/// The thrower (the owner) does the hit check and tells everyone.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirToilet : UdonSharpBehaviour
{
    public VRC_Pickup pickup;
    public Rigidbody body;
    public AudioSource audioSrc;
    public AudioClip flushClip, clunkClip;
    public FirStalker stalker;
    public float hitSpeed = 2.5f;
    public float hitRadius = 1.3f;

    float lastSpeed, nextHit, nextClunk;

    public override void OnPickupUseDown()
    {
        if (audioSrc != null && flushClip != null && !audioSrc.isPlaying) { audioSrc.clip = flushClip; audioSrc.Play(); }
    }

    void OnCollisionEnter(Collision c)
    {
        if (lastSpeed < 1.5f || Time.time < nextClunk || audioSrc == null) return;
        nextClunk = Time.time + 0.3f;
        audioSrc.PlayOneShot(clunkClip, Mathf.Clamp01(lastSpeed / 7f));
    }

    void Update()
    {
        if (pickup != null && pickup.IsHeld) { lastSpeed = 0; return; }
        lastSpeed = body != null ? body.velocity.magnitude : 0f;
        if (stalker == null || lastSpeed < hitSpeed || Time.time < nextHit) return;
        if (!Networking.IsOwner(gameObject) || !stalker.IsActive()) return;
        if (Vector3.Distance(transform.position, stalker.transform.position + Vector3.up * 1.1f) > hitRadius) return;
        nextHit = Time.time + 2f;
        stalker.SendCustomNetworkEvent(NetworkEventTarget.All, "NetStun");
    }
}
