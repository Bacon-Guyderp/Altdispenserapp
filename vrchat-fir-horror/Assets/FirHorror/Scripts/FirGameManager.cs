using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

/// <summary>
/// Runs Fir Warning: the synced story state (which chapter, what's been found), doors, the
/// head-locked HUD and subtitles, the global "lantern" lighting, and the shared jumpscare rig.
///
/// Chapters (phase): 0 lobby, 1 Showroom, 2 Stockroom, 3 Voice Lab, 4 Offices, 5 Living Room,
/// 6 Recall Floor, 7 escaped. Door i opens once phase >= i + 2.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FirGameManager : UdonSharpBehaviour
{
    // ------------------------------------------------------------ synced
    [UdonSynced] public int phase;
    [UdonSynced] public int itemMask;
    [UdonSynced] public int hatMask;
    [UdonSynced] public int seed;
    [UdonSynced] public int startMs;
    [UdonSynced] public int finishMs;
    [UdonSynced] public int round;

    // ------------------------------------------------------------ scene (wired by the builder)
    public Transform lobbySpawn;
    public Transform[] checkpoints;          // [0..5] chapters 1..6, [6] ending room
    public GameObject[] doors;
    public GameObject[] items;               // by global item index
    public int[] itemChapter;
    public VRCObjectSync[] carrySyncs;       // every pickup, respawned on reset
    public GameObject[] hatsOnTrees;         // by hat socket
    public GameObject[] hatPickups;
    public FirAnimatronic[] animatronics;
    public FirJumpscare[] jumpscares;
    public TextMeshPro[] memoTexts;          // office memos, one code digit each
    public TextMeshPro tapeBoard;
    public TextMeshPro lobbyStatus;
    public TextMeshPro endingBoard;
    public TextMeshPro optionsText;
    public Transform hud;
    public TextMeshPro hudObjective;
    public TextMeshPro hudSubtitle;
    public AudioSource sfx;                  // 2D, one-shots
    public AudioSource music;                // 2D, chapter loops
    public AudioClip[] chapterMusic;         // by phase 0..7
    public AudioClip[] screams;
    public AudioClip clipPickup, clipUnlock, clipWrong, clipTalk, clipLaugh, clipStatic, clipHit, clipWhoosh, clipHeartbeat, clipWhisper;
    public Transform scareRig;               // a tree whose face sits at the rig's origin
    public FirAnimatronic scareAnim;

    // ------------------------------------------------------------ local
    VRCPlayerApi local;
    bool synced, inGame;
    int lastRound, lastPhase;
    int[] chapterMask;
    float blackoutUntil, flickerUntil, subtitleUntil, hintAt, nextHud, phaseEnteredAt, resetArmedUntil;
    int brightness = 1;                      // 0 dark, 1 normal, 2 bright
    bool flashing = true, reducedScares;
    int idLight, idDark, idAmbient;

    // face scare: 0 idle, 1 pre-blink, 2 showing, 3 post-dark
    int scareState; float scareNext; float scareLen; int scareClip;
    // behind scare: tree placed behind you, screams when you turn round
    bool behindActive; float behindTimeout;
    float teleportAfterScareAt = -1;

    string[] objectiveName;

    void Start()
    {
        local = Networking.LocalPlayer;
        chapterMask = new int[8];
        objectiveName = new string[] { "", "Batteries", "Fuses", "Voice tapes", "Door code", "Santa hats", "Imitations burned", "" };
        idLight = VRCShader.PropertyToID("_UdonFirLight");
        idDark = VRCShader.PropertyToID("_UdonFirDark");
        idAmbient = VRCShader.PropertyToID("_UdonFirAmbient");
        for (int i = 0; i < itemChapter.Length && i < 31; i++) chapterMask[itemChapter[i]] |= 1 << i;
        if (scareRig != null) scareRig.gameObject.SetActive(false);
        if (Networking.IsOwner(gameObject))
        {
            if (seed == 0) seed = Random.Range(1, 1 << 30);
            RequestSerialization();
            ApplyState();
        }
        PlayMusic(0);
        UpdateOptionsText();
    }

    public override void OnDeserialization() { ApplyState(); }

    // ============================================================ buttons (lobby + ending)

    public void StartGame()
    {
        if (phase != 0 && phase != 7) { JoinGame(); return; }
        TakeOwnership();
        phase = 1; itemMask = 0; hatMask = 0;
        seed = Random.Range(1, 1 << 30);
        startMs = Networking.GetServerTimeInMilliseconds();
        finishMs = 0;
        round++;
        Commit();
        RespawnCarryItems();
    }

    public void ResetGame()
    {
        if (Time.time > resetArmedUntil)
        {
            resetArmedUntil = Time.time + 4f;
            Say("Press <b>Reset</b> again to send everyone back to the lobby.", 4f);
            return;
        }
        resetArmedUntil = 0;
        TakeOwnership();
        phase = 0; itemMask = 0; hatMask = 0; finishMs = 0;
        round++;
        Commit();
        RespawnCarryItems();
    }

    public void JoinGame()
    {
        if (phase < 1) { Say("Nobody is playing yet. Press <b>START</b>.", 4f); return; }
        TeleportToCheckpoint();
        inGame = true;
        PlayMusic(phase);
        Say(ChapterIntro(phase), 9f);
    }

    public void ReturnToLobby()
    {
        inGame = false;
        local.TeleportTo(lobbySpawn.position, lobbySpawn.rotation);
        PlayMusic(0);
    }

    public void ToggleBrightness() { brightness = (brightness + 1) % 3; UpdateOptionsText(); }
    public void ToggleFlashing() { flashing = !flashing; UpdateOptionsText(); }
    public void ToggleReducedScares() { reducedScares = !reducedScares; UpdateOptionsText(); }

    void UpdateOptionsText()
    {
        if (optionsText == null) return;
        string[] b = { "Dark (hard)", "Normal", "Bright (easier)" };
        optionsText.text = "<b>Your options</b> (only for you)\nBrightness: " + b[brightness] +
                           "\nFlashing lights: " + (flashing ? "on" : "off") +
                           "\nJumpscares: " + (reducedScares ? "sound only" : "full");
    }

    // ============================================================ objectives (called by props)

    public void CollectItem(int index)
    {
        int bit = 1 << index;
        if ((itemMask & bit) != 0) return;
        int ch = itemChapter[index];
        if (ch != phase) { Say("<i>It won't come loose. Not yet.</i>", 3f); return; }
        TakeOwnership();
        itemMask |= bit;
        PlaySfx(clipPickup);
        bool done = (itemMask & chapterMask[ch]) == chapterMask[ch];
        if (done && ch == 6) Advance();  // the last imitation in the furnace ends the game
        Commit();
        if (done) SendCustomNetworkEvent(NetworkEventTarget.All, "NetChapterItemsDone");
    }

    public void FillSocket(int socket)
    {
        int bit = 1 << socket;
        if ((hatMask & bit) != 0 || phase != 5) return;
        TakeOwnership();
        hatMask |= bit;
        PlaySfx(clipPickup);
        if (hatMask == (1 << hatsOnTrees.Length) - 1) Advance();
        Commit();
        SendCustomNetworkEvent(NetworkEventTarget.All, "NetHatPlaced");
    }

    public void StationPower() { Station(1); }
    public void StationFuses() { Station(2); }

    void Station(int ch)
    {
        if (phase > ch) { Say("It's already running.", 2f); return; }
        if (phase < ch) { Say("<i>Dead. No power.</i>", 2f); return; }
        int have = CountBits(itemMask & chapterMask[ch]);
        int need = CountBits(chapterMask[ch]);
        if (have < need)
        {
            PlaySfx(clipWrong);
            Say("It needs " + (need - have) + " more " + objectiveName[ch].ToLower() + ".", 3f);
            return;
        }
        TakeOwnership();
        Advance();
        Commit();
    }

    /// <summary>The tape deck (chapter 3) and keypad (chapter 4) call this when solved.</summary>
    public void SolveLock(int ch)
    {
        if (phase != ch) return;
        TakeOwnership();
        Advance();
        Commit();
    }

    public void WrongCode(int ch)
    {
        PlaySfx(clipWrong);
        if (ch == 3) { GroupMode(3, 4, 2f); Blackout(1.2f); Say("<color=#ff5555>\"THAT'S NOT HOW I SOUND.\"</color>", 3f); }
        else FaceScare(1, 1.0f);
    }

    public bool ChapterItemsDone(int ch) { return (itemMask & chapterMask[ch]) == chapterMask[ch]; }
    public int ChapterItemsHave(int ch) { return CountBits(itemMask & chapterMask[ch]); }

    /// <summary>The solution for a lock. Chapter 3: tape colour order (0-3). Chapter 4: keypad digits.</summary>
    public int CodeAt(int ch, int i)
    {
        if (ch == 4) return (seed / Pow10(i)) % 10;
        int[] order = { 0, 1, 2, 3 };
        int s = seed;
        for (int k = 3; k > 0; k--)
        {
            s = ((s % 100003) * 73 + 41) % 100003;
            int j = s % (k + 1);
            int tmp = order[k]; order[k] = order[j]; order[j] = tmp;
        }
        return order[i];
    }

    void Advance()
    {
        phase++;
        if (phase == 7) finishMs = Networking.GetServerTimeInMilliseconds();
    }

    // ============================================================ network events

    public void NetChapterItemsDone()
    {
        if (!inGame) return;
        if (lastPhase == 1) { Blackout(2.5f); GroupMode(1, 4, 2.5f); PlaySfx(screams[0]); Say("\"<b>Thank you!</b> Now put them in the panel.\"", 4f); }
        else if (lastPhase == 2) { PlaySfx(clipLaugh); Say("\"Four fuses. Four little fuses. <b>Hurry.</b>\"", 4f); }
        else if (lastPhase == 3) { GroupMode(3, 2, 3f); Say("\"Play them for me. In the <b>right</b> order.\"", 4f); }
    }

    public void NetHatPlaced()
    {
        if (!inGame) return;
        GroupMode(5, 1, 6f);
        PlaySfx(clipWhoosh);
        if (hatMask != 0 && hatMask != (1 << hatsOnTrees.Length) - 1) Say("<i>Every tree in the room turns to look at you.</i>", 3f);
    }

    // ============================================================ state

    void ApplyState()
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;
            bool got = (itemMask & (1 << i)) != 0;
            if (items[i].activeSelf == got) items[i].SetActive(!got);
        }
        for (int s = 0; s < hatsOnTrees.Length; s++)
        {
            bool on = (hatMask & (1 << s)) != 0;
            hatsOnTrees[s].SetActive(on);
            if (hatPickups[s] != null && hatPickups[s].activeSelf == on) hatPickups[s].SetActive(!on);
        }
        for (int d = 0; d < doors.Length; d++) if (doors[d] != null) doors[d].SetActive(phase < d + 2);
        RefreshTexts();

        if (!synced)
        {
            synced = true; lastRound = round; lastPhase = phase;
            return;  // late joiners stay in the lobby and use "Join game"
        }
        if (round != lastRound)
        {
            lastRound = round; lastPhase = phase;
            for (int j = 0; j < jumpscares.Length; j++) if (jumpscares[j] != null) jumpscares[j].Rearm();
            if (phase == 0) { if (inGame) ReturnToLobby(); }
            else
            {
                inGame = true;
                TeleportToCheckpoint();
                EnterPhase(phase);
            }
        }
        else if (phase != lastPhase)
        {
            lastPhase = phase;
            if (inGame) { PlaySfx(clipUnlock); EnterPhase(phase); }
        }
    }

    void EnterPhase(int p)
    {
        phaseEnteredAt = Time.time;
        hintAt = Time.time + 360f;   // a hint after six minutes stuck
        PlayMusic(p);
        Say(ChapterIntro(p), 10f);
        if (p == 6) { Blackout(2.5f); GroupMode(5, 4, 2.5f); PlaySfx(screams[2]); }
        if (p == 7) PlaySfx(clipLaugh);
    }

    string ChapterIntro(int p)
    {
        if (p == 1) return "\"Ho ho ho! Welcome to the <b>Showroom</b>! The power's out, friends. Find <b>three batteries</b> for the panel by the far door.\"";
        if (p == 2) return "\"Thank you! The <b>stockroom</b> lost its <b>four fuses</b>. Oh, and one of my brothers got out of his box. <b>Keep your eyes on him.</b>\"";
        if (p == 3) return "\"The <b>Voice Lab</b>! Where we learned to talk. Find the <b>four voice tapes</b>, then play them in the order on the recording log.\"";
        if (p == 4) return "\"Management left the door code on their <b>memos</b>. Four memos, four digits. Don't mind the staff. They're... resting.\"";
        if (p == 5) return "\"Just like the catalogue photo! But my family is so cold. Give each tree the <b>Santa hat that fits</b>.\"";
        if (p == 6) return "<color=#ff4444>\"You put hats on them. You think they're REAL. THEY ARE IMITATIONS. Burn all <b>five</b> in the furnace. I am the ORIGINAL. I will be watching.\"</color>";
        if (p == 7) return "\"Thank you for choosing the ORIGINAL. <b>Play it safe.</b>\"";
        return "";
    }

    string Hint(int p)
    {
        if (p == 1) return "Hint: look inside the gift boxes, behind the window curtain and under the long table.";
        if (p == 2) return "Hint: the fuses are at the ends of the shelf aisles. Make someone watch the tree while others search.";
        if (p == 3) return "Hint: tapes are on the desks, the conveyor and in the booth. The log on the whiteboard gives the order.";
        if (p == 4) return "Hint: each memo is on a different desk and says which digit it is (1st, 2nd...).";
        if (p == 5) return "Hint: small hat, small tree. Hold the hat right on top of the tree's head and let go.";
        if (p == 6) return "Hint: pick up the little trees and drop them into the furnace's glowing mouth.";
        return "";
    }

    void RefreshTexts()
    {
        for (int i = 0; i < memoTexts.Length; i++)
        {
            if (memoTexts[i] == null) continue;
            string[] ord = { "1st", "2nd", "3rd", "4th" };
            string[] who = { "R. Pine, Quality Control", "D. Spruce, Voice Department", "H. Holly, Shipping", "M. Balsam, Plant Manager" };
            memoTexts[i].text = "<b>MEMO</b>\nFrom: " + who[i] + "\n\nThe Living Room door code changed again. The <b>" + ord[i] +
                                "</b> digit is <size=150%><b>" + CodeAt(4, i) + "</b></size>.\n\nIf a unit asks you for the code, <b>do not answer it.</b>";
        }
        if (tapeBoard != null)
        {
            string[] names = { "<color=#ff4040>RED</color>", "<color=#40c0ff>BLUE</color>", "<color=#50ff60>GREEN</color>", "<color=#ffd030>GOLD</color>" };
            tapeBoard.text = "<b>RECORDING LOG - DF-96 voice</b>\nPlay the sessions in order:\n\n1. " + names[CodeAt(3, 0)] + "\n2. " + names[CodeAt(3, 1)] +
                             "\n3. " + names[CodeAt(3, 2)] + "\n4. " + names[CodeAt(3, 3)] + "\n\n<size=70%>never play session 5</size>";
        }
        if (endingBoard != null)
        {
            int ms = finishMs - startMs;
            endingBoard.text = phase == 7
                ? "<b>YOU ESCAPED</b>\n\nTime: " + Clock(ms) + "\n\nThe Original is still in the showroom.\nIt is still smiling.\n\n<size=60%>Fir Warning - a fan-made horror world. Not affiliated with any toy company.</size>"
                : "";
        }
    }

    // ============================================================ per frame

    void Update()
    {
        if (!Utilities.IsValid(local)) return;
        float t = Time.time;

        // lantern lighting
        float dark = 0f;
        if (t < blackoutUntil) dark = 1f;
        else if (t < flickerUntil && flashing) dark = Random.value < 0.35f ? 0.9f : 0f;
        if (scareState == 2) dark = 0f;
        float reach = brightness == 0 ? 5.5f : brightness == 1 ? 7f : 9.5f;
        float amb = brightness == 0 ? 0f : brightness == 1 ? 0.02f : 0.07f;
        if (phase == 7 || !inGame) { reach = 14f; amb += 0.12f; }
        if (scareState == 2 && flashing) amb += 0.5f;
        VRCShader.SetGlobalFloat(idDark, dark);
        VRCShader.SetGlobalFloat(idLight, reach);
        VRCShader.SetGlobalFloat(idAmbient, amb);

        // HUD follows the head
        VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        if (hud != null)
        {
            Vector3 want = head.position + head.rotation * new Vector3(0, -0.18f, 0.9f);
            hud.position = Vector3.Lerp(hud.position, want, Mathf.Min(1f, Time.deltaTime * 8f));
            hud.rotation = Quaternion.Slerp(hud.rotation, head.rotation, Mathf.Min(1f, Time.deltaTime * 8f));
        }
        if (t > nextHud)
        {
            nextHud = t + 0.5f;
            if (hudObjective != null) hudObjective.text = ObjectiveText();
            if (hudSubtitle != null && t > subtitleUntil) hudSubtitle.text = "";
            if (lobbyStatus != null) lobbyStatus.text = LobbyText();
            if (inGame && phase >= 1 && phase <= 6 && t > hintAt) { hintAt = t + 240f; Say("<color=#aaaaaa>" + Hint(phase) + "</color>", 8f); }
        }

        UpdateFaceScare(head);
        UpdateBehindScare(head);
        if (teleportAfterScareAt > 0 && t > teleportAfterScareAt) { teleportAfterScareAt = -1; TeleportToCheckpoint(); }
    }

    string ObjectiveText()
    {
        if (!inGame || phase < 1 || phase > 6) return "";
        if (phase == 4) return "Find the four memos, then enter the code";
        if (phase == 5) return objectiveName[5] + ": " + CountBits(hatMask) + "/" + hatsOnTrees.Length;
        int need = CountBits(chapterMask[phase]);
        string s = objectiveName[phase] + ": " + ChapterItemsHave(phase) + "/" + need;
        if (phase == 1 && ChapterItemsHave(1) == need) s += "  -  use the power panel";
        if (phase == 2 && ChapterItemsHave(2) == need) s += "  -  use the fuse box";
        if (phase == 3 && ChapterItemsHave(3) == need) s += "  -  play them on the tape deck";
        return s;
    }

    string LobbyText()
    {
        if (phase == 0) return "Nobody is playing.\nPress <b>START</b> when everyone is here.";
        int now = Networking.GetServerTimeInMilliseconds();
        if (phase == 7) return "The last group escaped in " + Clock(finishMs - startMs) + ".\nPress <b>START</b> to play again.";
        string[] names = { "", "The Showroom", "The Stockroom", "The Voice Lab", "The Offices", "The Living Room", "The Recall Floor" };
        return "Game in progress: chapter " + phase + "/6, " + names[phase] + "\n" + Clock(now - startMs) + " elapsed\nPress <b>Join game</b> to catch up.";
    }

    // ============================================================ scares (local)

    public void Say(string text, float seconds)
    {
        if (hudSubtitle == null) return;
        hudSubtitle.text = text;
        subtitleUntil = Time.time + seconds;
        nextHud = 0;
    }

    public void PlaySfx(AudioClip c) { if (c != null && sfx != null) sfx.PlayOneShot(c); }

    public void Blackout(float seconds) { blackoutUntil = Mathf.Max(blackoutUntil, Time.time + seconds); }
    public void Flicker(float seconds) { flickerUntil = Mathf.Max(flickerUntil, Time.time + seconds); }

    public void GroupMode(int chapter, int mode, float seconds)
    {
        for (int i = 0; i < animatronics.Length; i++)
            if (animatronics[i] != null && animatronics[i].chapter == chapter) animatronics[i].SetMode(mode, seconds);
    }

    /// <summary>A tree's face right in front of yours, with a scream.</summary>
    public void FaceScare(int screamIndex, float seconds)
    {
        if (scareState != 0 || behindActive) return;
        if (reducedScares) { PlaySfx(screams[screamIndex % screams.Length]); Flicker(0.6f); return; }
        scareClip = screamIndex; scareLen = seconds;
        scareState = 1; scareNext = Time.time + 0.18f;
        blackoutUntil = Time.time + 0.18f;
    }

    void UpdateFaceScare(VRCPlayerApi.TrackingData head)
    {
        if (scareState == 0 || scareRig == null) return;
        float t = Time.time;
        if (scareState == 2)
        {
            Vector3 fwd = head.rotation * Vector3.forward;
            scareRig.position = head.position + fwd * 0.5f + Random.insideUnitSphere * 0.015f;
            scareRig.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
        }
        if (t < scareNext) return;
        if (scareState == 1)
        {
            scareState = 2; scareNext = t + scareLen;
            scareRig.gameObject.SetActive(true);
            if (scareAnim != null) scareAnim.SetMode(4, scareLen);
            PlaySfx(screams[scareClip % screams.Length]);
            PlaySfx(clipHit);
        }
        else if (scareState == 2)
        {
            scareState = 3; scareNext = t + 0.7f;
            scareRig.gameObject.SetActive(false);
            Blackout(0.7f);
        }
        else scareState = 0;
    }

    /// <summary>Whispers, and a tree is standing behind you. It screams when you turn round.</summary>
    public void BehindScare()
    {
        if (scareState != 0 || behindActive || scareRig == null) return;
        if (reducedScares) { PlaySfx(clipWhisper); return; }
        VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 back = head.rotation * Vector3.back; back.y = 0; back.Normalize();
        scareRig.position = head.position + back * 0.9f;
        scareRig.rotation = Quaternion.LookRotation(-back, Vector3.up);
        scareRig.gameObject.SetActive(true);
        if (scareAnim != null) scareAnim.SetMode(1, 10f);
        behindActive = true;
        behindTimeout = Time.time + 9f;
        PlaySfx(clipWhisper);
    }

    void UpdateBehindScare(VRCPlayerApi.TrackingData head)
    {
        if (!behindActive) return;
        Vector3 to = (scareRig.position - head.position).normalized;
        bool seen = Vector3.Dot(head.rotation * Vector3.forward, to) > 0.6f;
        if (seen || Time.time > behindTimeout)
        {
            behindActive = false;
            scareRig.gameObject.SetActive(false);
            if (seen) { scareState = 1; scareNext = Time.time; scareClip = 2; scareLen = 0.8f; }
        }
    }

    /// <summary>The stalker reached the local player.</summary>
    public void Caught()
    {
        if (!inGame) return;
        FaceScare(phase == 6 ? 2 : 0, 1.1f);
        Say("<color=#ff5555>It got you.</color> Back to the start of the room.", 4f);
        teleportAfterScareAt = Time.time + 1.4f;
    }

    // ============================================================ helpers

    void TeleportToCheckpoint()
    {
        int i = Mathf.Clamp(phase - 1, 0, checkpoints.Length - 1);
        local.TeleportTo(checkpoints[i].position, checkpoints[i].rotation);
    }

    void PlayMusic(int p)
    {
        if (music == null || chapterMusic == null || p >= chapterMusic.Length) return;
        AudioClip c = chapterMusic[p];
        if (music.clip == c && music.isPlaying) return;
        music.clip = c;
        if (c != null) music.Play(); else music.Stop();
    }

    void RespawnCarryItems()
    {
        for (int i = 0; i < carrySyncs.Length; i++)
        {
            if (carrySyncs[i] == null) continue;
            Networking.SetOwner(local, carrySyncs[i].gameObject);
            carrySyncs[i].Respawn();
        }
    }

    void TakeOwnership() { if (!Networking.IsOwner(gameObject)) Networking.SetOwner(local, gameObject); }

    void Commit() { RequestSerialization(); ApplyState(); }

    int CountBits(int v) { int c = 0; while (v != 0) { c += v & 1; v = v >> 1; } return c; }

    int Pow10(int i) { int r = 1; for (int k = 0; k < i; k++) r *= 10; return r; }

    string Clock(int ms)
    {
        if (ms < 0) ms = 0;
        int s = ms / 1000;
        int m = s / 60; s %= 60;
        return m + ":" + (s < 10 ? "0" : "") + s;
    }
}
