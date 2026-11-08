using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Random scares between the scripted ones, local to each player: whispers and rustles from just
/// behind you, a distant laugh, a phone ringing, and now and then the lights stutter.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirAmbience : UdonSharpBehaviour
{
    public FirGameManager game;
    public AudioSource emitter;          // 3D, moved behind you for each sound
    public AudioClip[] clips;
    public float minGap = 35f, maxGap = 90f;
    float next;

    void Start() { next = Time.time + Random.Range(minGap, maxGap); }

    void Update()
    {
        if (Time.time < next) return;
        next = Time.time + Random.Range(minGap, maxGap);
        if (game.phase < 1 || game.phase > 6 || clips.Length == 0) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        VRCPlayerApi.TrackingData h = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        if (Vector3.Distance(h.position, game.lobbySpawn.position) < 25f) return;
        Vector3 behind = h.rotation * new Vector3(Random.Range(-1.5f, 1.5f), 0, -Random.Range(2f, 5f));
        emitter.transform.position = h.position + behind;
        emitter.PlayOneShot(clips[Random.Range(0, clips.Length)]);
        if (Random.value < 0.3f) game.Flicker(1.2f);
    }
}
