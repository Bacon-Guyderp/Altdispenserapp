using UdonSharp;
using UnityEngine;
using VRC.Udon;
using VRC.SDKBase;

/// <summary>
/// The scare director: random scares between the scripted ones, local to each player. Sounds
/// from just behind you, stuttering lights, and bigger moments (something standing behind you,
/// a blackout with laughter, whispered subtitles). It gets more frequent and nastier each
/// chapter, and it goes after whoever has wandered away from the group or is playing alone.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirAmbience : UdonSharpBehaviour
{
    public UdonBehaviour game;          // FirGameManager
    public Transform lobbySpawn;
    public AudioSource emitter;          // 3D, moved behind you for each sound
    public AudioClip[] clips;
    public AudioClip laughClip, whisperClip;
    public float minGap = 18f, maxGap = 45f;
    public float separatedDistance = 10f;
    float next, nextBig;
    string[] lonely;

    void Start()
    {
        next = Time.time + Random.Range(minGap, maxGap);
        nextBig = Time.time + 90f;
        lonely = new string[]
        {
            "<i>...where did your friend go?</i>",
            "<i>...it's just us now.</i>",
            "<i>...they left you here.</i>",
            "<i>...don't turn around.</i>",
            "<i>...I can hear your heart.</i>",
            "<i>...one of you is an imitation.</i>",
        };
    }

    void Update()
    {
        if (Time.time < next) return;
        if (game == null) { next = Time.time + 5f; return; }
        int phase = GameInt("phase");
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) { next = Time.time + 5f; return; }
        VRCPlayerApi.TrackingData h = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        bool inFactory = phase >= 1 && phase <= 6 && (lobbySpawn == null || Vector3.Distance(h.position, lobbySpawn.position) > 25f);
        bool alone = inFactory && (float)game.GetProgramVariable("nearestOther") > separatedDistance;

        // more often in later chapters, and much more often when you're on your own
        float gap = Random.Range(minGap, maxGap) * Mathf.Max(0.55f, 1.1f - phase * 0.08f);
        if (alone) gap *= 0.6f;
        next = Time.time + gap;
        if (!inFactory || clips.Length == 0) return;

        float roll = Random.value;
        if (Time.time > nextBig && roll < 0.14f + phase * 0.025f + (alone ? 0.1f : 0f))
        {
            nextBig = Time.time + Random.Range(70f, 120f);
            float pick = Random.value;
            if (pick < 0.35f) GameEv("BehindScare");
            else if (pick < 0.55f) GameEv("Subliminal");
            else if (pick < 0.7f) GameEv("Glitch");
            else
            {
                GameEvF("EvBlackout", 1.8f);
                PlayBehind(h, laughClip);
                if (alone) GameSay(lonely[Random.Range(0, lonely.Length)], 3f);
            }
            return;
        }
        PlayBehind(h, clips[Random.Range(0, clips.Length)]);
        if (Random.value < 0.06f + phase * 0.01f) GameEv("Subliminal");
        if (alone && Random.value < 0.45f) { PlayBehind(h, whisperClip); GameSay(lonely[Random.Range(0, lonely.Length)], 3f); }
        if (Random.value < 0.3f + phase * 0.03f) GameEvF("EvFlicker", 1.2f);
    }

    void PlayBehind(VRCPlayerApi.TrackingData h, AudioClip c)
    {
        if (c == null) return;
        Vector3 behind = h.rotation * new Vector3(Random.Range(-1.5f, 1.5f), 0, -Random.Range(1.5f, 4.5f));
        emitter.transform.position = h.position + behind;
        emitter.PlayOneShot(c);
    }

    // ---- calls to other scripts: plain Udon events (see FirGameManager's "public API")
    void GameEv(string ev) { if (game != null) game.SendCustomEvent(ev); }
    void GameEvA(string ev, int a) { if (game == null) return; game.SetProgramVariable("argA", a); game.SendCustomEvent(ev); }
    void GameEvAB(string ev, int a, int b) { if (game == null) return; game.SetProgramVariable("argA", a); game.SetProgramVariable("argB", b); game.SendCustomEvent(ev); }
    void GameEvF(string ev, float f) { if (game == null) return; game.SetProgramVariable("argF", f); game.SendCustomEvent(ev); }
    void GameEvAF(string ev, int a, float f) { if (game == null) return; game.SetProgramVariable("argA", a); game.SetProgramVariable("argF", f); game.SendCustomEvent(ev); }
    void GameSay(string text, float seconds) { if (game == null) return; game.SetProgramVariable("argText", text); game.SetProgramVariable("argF", seconds); game.SendCustomEvent("EvSay"); }
    int GameInt(string name) { return game != null ? (int)game.GetProgramVariable(name) : 0; }
}
