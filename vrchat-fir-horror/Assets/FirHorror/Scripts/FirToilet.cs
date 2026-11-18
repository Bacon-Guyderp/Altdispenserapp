using UdonSharp;
using UnityEngine;
using VRC.Udon;
using VRC.SDKBase;

/// <summary>
/// A throwable toilet. Pick it up, press Use to flush it, throw it at things. It clunks when it
/// lands, and a hit on the hunting tree stuns it for a few seconds (for everyone, through the
/// game manager's synced stun counter). No collision callbacks: a sudden stop is a landing.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirToilet : UdonSharpBehaviour
{
    public VRC_Pickup pickup;
    public Rigidbody body;
    public AudioSource audioSrc;
    public AudioClip flushClip, clunkClip;
    public UdonBehaviour stalker;       // FirStalker
    public UdonBehaviour game;          // FirGameManager
    public float hitSpeed = 2.5f;
    public float hitRadius = 1.3f;

    float lastSpeed, nextHit, nextClunk;

    public override void OnPickupUseDown()
    {
        if (audioSrc != null && flushClip != null && !audioSrc.isPlaying) { audioSrc.clip = flushClip; audioSrc.Play(); }
    }

    void Update()
    {
        if (pickup != null && pickup.IsHeld) { lastSpeed = 0f; return; }
        float speed = body != null ? body.velocity.magnitude : 0f;
        // landed: it was flying and has suddenly slowed right down
        if (lastSpeed > 1.5f && speed < lastSpeed * 0.45f && Time.time > nextClunk && audioSrc != null && clunkClip != null)
        {
            nextClunk = Time.time + 0.3f;
            audioSrc.PlayOneShot(clunkClip, Mathf.Clamp01(lastSpeed / 7f));
        }
        lastSpeed = speed;
        if (stalker == null || game == null || speed < hitSpeed || Time.time < nextHit) return;
        if (!Networking.IsOwner(gameObject) || !(bool)stalker.GetProgramVariable("syncActive")) return;
        if (Vector3.Distance(transform.position, stalker.transform.position + Vector3.up * 1.1f) > hitRadius) return;
        nextHit = Time.time + 2f;
        game.SendCustomEvent("StunStalker");
    }
}
