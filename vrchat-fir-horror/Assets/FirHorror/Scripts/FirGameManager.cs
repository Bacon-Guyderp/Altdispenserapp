using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

/// <summary>
/// Fir Warning's game manager (remade).
///
/// Design rules, so it works in any VRChat project that runs GeoQuest:
///  * Only manually-synced variables + RequestSerialization/OnDeserialization. No network events.
///    Everything everyone must see (gags, skeletons, the closet, shared scares, the carol, lights-out,
///    toilet stuns) is a synced counter here; each player plays it when the counter changes.
///  * No bitwise operators: sets of items are plain sums of powers of two (see Has/With/Minus).
///  * No global shader values: darkness and brightness are set on the world's materials.
///  * Teleporting never depends on this script: FirTeleporter moves the player first.
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
    [UdonSynced] public int carolCount;
    [UdonSynced] public int lightsCount;
    [UdonSynced] public int stunCount;
    [UdonSynced] public int scareCount;   // shared scares: how many so far, and the latest one
    [UdonSynced] public int scareIndex;
    [UdonSynced] public int gagCount;     // gags / skeleton routines / the closet
    [UdonSynced] public int gagKind;      // 0 gag, 1 skeleton routine, 2 closet
    [UdonSynced] public int gagIndex;

    // ------------------------------------------------------------ scene (wired by the builder)
    public int ValveChapter = 9;
    public Transform lobbySpawn;
    public Transform[] checkpoints;          // [0..5] chapters 1..6, [6] exit room
    public GameObject[] doors;
    public GameObject[] items;
    public int[] itemChapter;
    public VRCObjectSync[] carrySyncs;
    public GameObject[] hatsOnTrees;         // [socket * 3 + colour]
    public GameObject[] hatPickups;          // by colour
    public FirAnimatronic[] animatronics;
    public FirAnimatronic[] carolTrees;
    public FirJumpscare[] jumpscares;
    public FirGag[] gags;
    public FirSkeletonGag[] skeletonGags;
    public FirCloset closet;
    public FirStalker stalker;
    public TextMeshPro[] memoTexts;
    public string[] countNames;
    public int[] countValues;
    public TextMeshPro tapeBoard;
    public TextMeshPro tapeCard;
    public TextMeshPro hatClue;
    public TextMeshPro[] glowDigits;
    public GameObject[] glowOnly;
    public GameObject furnaceFire;
    public TextMeshPro lobbyStatus;
    public TextMeshPro endingBoard;
    public TextMeshPro optionsText;
    public Transform hud;
    public TextMeshPro hudObjective;
    public TextMeshPro hudSubtitle;
    public AudioSource sfx;
    public AudioSource music;
    public AudioClip[] chapterMusic;
    public AudioClip[] screams;
    public AudioClip clipPickup, clipUnlock, clipWrong, clipTalk, clipLaugh, clipStatic, clipHit, clipWhoosh, clipHeartbeat, clipWhisper, clipSwitch;
    public Transform scareRig;
    public FirAnimatronic scareAnim;
    public Material[] worldMaterials;        // every lantern-lit material: darkness and brightness are set on these

    public float lightsOutUntil;             // read by the stalker: nobody can see it while the lights are out
    public int heartbeat;                    // goes up every frame; FirWatchdog shows a stopped manager
    public string step = "not started";      // what it is doing right now (shown by FirWatchdog if it stops)

    // ------------------------------------------------------------ local
    bool inited, synced, inGame, glowShown;
    VRCPlayerApi local;
    int lastRound, lastPhase, seenItems, seenHats, seenCarol, seenLights, seenStun, seenScare, seenGag;
    int pendingItems, pendingHats;
    int[] chapterMask;
    int[] pow2;
    float blackoutUntil, flickerUntil, subtitleUntil, hintAt, nextHud, resetArmedUntil, lightsCooldown;
    int brightness = 1;
    bool flashing = true;
    bool reducedScares;
    float appliedDark = -1f;
    float appliedReach = -1f;
    float appliedAmb = -1f;
    int appliedRoom = -1;
    int appliedBrightness = -1;
    Color[] roomAmb;
    Color[] roomLan;
    int carolStep = -1;
    float carolNext;
    int scareState;
    float scareNext, scareLen;
    int scareClip;
    bool behindActive;
    float behindTimeout;
    float teleportAfterScareAt = -1f;
    VRCPlayerApi[] countBuf;
    float nextCount;
    int cachedInGame = 1;
    string[] colourNames, sessionNames, hatColourNames, familyNames, ordinals, roomNames;

    // ============================================================ setup

    /// <summary>One-time setup, from whichever comes first (VRChat can deliver OnDeserialization before Start).</summary>
    void EnsureInit()
    {
        if (inited) return;
        inited = true;
        local = Networking.LocalPlayer;
        pow2 = new int[31];
        pow2[0] = 1;
        for (int k = 1; k < 31; k++) pow2[k] = pow2[k - 1] * 2;
        chapterMask = new int[10];
        for (int i = 0; i < itemChapter.Length; i++)
        {
            if (i >= 30) break;
            int ch = itemChapter[i];
            chapterMask[ch] = With(chapterMask[ch], i);
        }
        colourNames = new string[] { "<color=#ff4040>RED</color>", "<color=#40c0ff>BLUE</color>", "<color=#50ff60>GREEN</color>", "<color=#ffd030>GOLD</color>" };
        sessionNames = new string[] { "\"JINGLE\"", "\"GREETING\"", "\"LAUGHTER\"", "\"GOODNIGHT\"" };
        hatColourNames = new string[] { "<color=#c00000>red</color>", "<color=#007a10>green</color>", "<color=#1040c0>blue</color>" };
        familyNames = new string[] { "Junior", "Mama", "Grandpa" };
        ordinals = new string[] { "1st", "2nd", "3rd", "4th" };
        roomNames = new string[] { "", "The Showroom", "The Stockroom", "The Voice Lab", "The Offices", "The Living Room", "The Recall Floor" };
        countBuf = new VRCPlayerApi[100];
        // each room's mood: 0 lobby, 1 showroom (red), 2 stockroom (sickly green), 3 voice lab (purple),
        // 4 offices (cold blue), 5 living room (fireplace amber), 6 recall floor (furnace orange), 7 exit
        roomAmb = new Color[8];
        roomAmb[0] = new Color(0.06f, 0.05f, 0.04f, 1f);
        roomAmb[1] = new Color(0.07f, 0.012f, 0.012f, 1f);
        roomAmb[2] = new Color(0.015f, 0.05f, 0.025f, 1f);
        roomAmb[3] = new Color(0.04f, 0.014f, 0.07f, 1f);
        roomAmb[4] = new Color(0.012f, 0.03f, 0.07f, 1f);
        roomAmb[5] = new Color(0.07f, 0.04f, 0.01f, 1f);
        roomAmb[6] = new Color(0.09f, 0.03f, 0.005f, 1f);
        roomAmb[7] = new Color(0.05f, 0.05f, 0.05f, 1f);
        roomLan = new Color[8];
        roomLan[0] = new Color(1f, 0.85f, 0.65f, 1f);
        roomLan[1] = new Color(1f, 0.82f, 0.62f, 1f);
        roomLan[2] = new Color(0.95f, 0.88f, 0.65f, 1f);
        roomLan[3] = new Color(1f, 0.8f, 0.75f, 1f);
        roomLan[4] = new Color(0.85f, 0.92f, 1f, 1f);
        roomLan[5] = new Color(1f, 0.8f, 0.55f, 1f);
        roomLan[6] = new Color(1f, 0.7f, 0.45f, 1f);
        roomLan[7] = new Color(1f, 0.9f, 0.75f, 1f);
    }

    void Start()
    {
        step = "Start: setup";
        EnsureInit();
        step = "Start: hiding the scare rig";
        if (scareRig != null) scareRig.gameObject.SetActive(false);
        for (int g = 0; g < glowOnly.Length; g++) if (glowOnly[g] != null) glowOnly[g].SetActive(false);
        step = "Start: first sync";
        if (Networking.IsOwner(gameObject))
        {
            if (seed == 0) seed = Random.Range(1, 1073741823);
            MarkSynced();
            RequestSerialization();
        }
        step = "Start: applying the game state";
        ApplyState();
        step = "Start: music and texts";
        PlayMusic(0);
        UpdateOptionsText();
        if (lobbyStatus != null) lobbyStatus.text = LobbyText();
        step = "running";
    }

    /// <summary>Adopt the current synced state as already seen, so nothing old replays.</summary>
    void MarkSynced()
    {
        if (synced) return;
        synced = true;
        lastRound = round;
        lastPhase = phase;
        seenItems = itemMask;
        seenHats = hatMask;
        seenCarol = carolCount;
        seenLights = lightsCount;
        seenStun = stunCount;
        seenScare = scareCount;
        seenGag = gagCount;
    }

    public override void OnDeserialization()
    {
        step = "receiving the game state";
        EnsureInit();
        bool first = !synced;
        MarkSynced();
        ApplyState();
        step = "running";
        if (first) return;
        // Two players collecting at the same moment both write the whole set; the last write wins.
        // Put back anything of ours that was overwritten.
        int lostItems = Minus(pendingItems, itemMask);
        int lostHats = Minus(pendingHats, hatMask);
        pendingItems = lostItems;
        pendingHats = lostHats;
        if (lostItems == 0 && lostHats == 0) return;
        if (phase < 1 || phase > 6) { pendingItems = 0; pendingHats = 0; return; }
        TakeOwnership();
        itemMask = Union(itemMask, lostItems);
        hatMask = Union(hatMask, lostHats);
        CheckAdvance();
        Commit();
    }

    // ============================================================ lobby / room buttons (teleporters call these after moving you)

    /// <summary>START. The START teleporter has already moved the presser into the Showroom.</summary>
    public void StartGame()
    {
        step = "StartGame";
        EnsureInit();
        MarkSynced();
        if (phase != 0 && phase != 7) { JoinGame(); return; }
        TakeOwnership();
        phase = 1;
        itemMask = 0;
        hatMask = 0;
        pendingItems = 0;
        pendingHats = 0;
        seed = Random.Range(1, 1073741823);
        startMs = Networking.GetServerTimeInMilliseconds();
        finishMs = 0;
        round = round + 1;
        Commit();
        RespawnCarryItems();
    }

    /// <summary>Join game. The teleporter already took you to the Showroom; this moves you on to the current room.</summary>
    public void JoinGame()
    {
        step = "JoinGame";
        EnsureInit();
        MarkSynced();
        if (phase < 1) { Say("Nobody is playing yet. Press <b>START</b>.", 4f); return; }
        inGame = true;
        TeleportToCheckpoint();
        PlayMusic(phase);
        Say(ChapterIntro(phase), 9f);
    }

    /// <summary>Called by the "Back to the lobby" teleporters after they've moved you.</summary>
    public void LeftGame()
    {
        step = "LeftGame";
        EnsureInit();
        inGame = false;
        PlayMusic(0);
    }

    /// <summary>Called by the Room Select teleporters after they've moved you (practice / testing).</summary>
    public void EnteredRoom()
    {
        step = "EnteredRoom";
        EnsureInit();
        inGame = true;
    }

    public void ResetGame()
    {
        step = "ResetGame";
        EnsureInit();
        if (Time.time > resetArmedUntil)
        {
            resetArmedUntil = Time.time + 4f;
            Say("Press <b>Reset</b> again to send everyone back to the lobby.", 4f);
            return;
        }
        resetArmedUntil = 0f;
        MarkSynced();
        TakeOwnership();
        phase = 0;
        itemMask = 0;
        hatMask = 0;
        finishMs = 0;
        pendingItems = 0;
        pendingHats = 0;
        round = round + 1;
        Commit();
        RespawnCarryItems();
    }

    public void ToggleBrightness() { EnsureInit(); brightness = (brightness + 1) % 3; UpdateOptionsText(); }
    public void ToggleFlashing() { EnsureInit(); flashing = !flashing; UpdateOptionsText(); }
    public void ToggleReducedScares() { EnsureInit(); reducedScares = !reducedScares; UpdateOptionsText(); }

    void UpdateOptionsText()
    {
        if (optionsText == null) return;
        string b = brightness == 0 ? "Dark (hard)" : (brightness == 1 ? "Normal" : "Bright (easier)");
        optionsText.text = "<b>Your options</b> (only for you)\nBrightness: " + b +
                           "\nFlashing lights: " + (flashing ? "on" : "off") +
                           "\nJumpscares: " + (reducedScares ? "sound only" : "full");
    }

    // ============================================================ objectives (called by props)

    public void CollectItem(int index)
    {
        step = "CollectItem";
        EnsureInit();
        if (Has(itemMask, index)) return;
        int ch = itemChapter[index];
        bool allowed = ch == phase || (ch == ValveChapter && phase == 6);
        if (!allowed) { Say("<i>It won't come loose. Not yet.</i>", 3f); return; }
        MarkSynced();
        TakeOwnership();
        itemMask = With(itemMask, index);
        pendingItems = With(pendingItems, index);
        PlaySfx(clipPickup);
        CheckAdvance();
        Commit();
    }

    public void FillSocket(int socket)
    {
        step = "FillSocket";
        EnsureInit();
        if (Has(hatMask, socket) || phase != 5) return;
        MarkSynced();
        TakeOwnership();
        hatMask = With(hatMask, socket);
        pendingHats = With(pendingHats, socket);
        PlaySfx(clipPickup);
        CheckAdvance();
        Commit();
    }

    void CheckAdvance()
    {
        if (phase == 5 && hatMask == 7) Advance();
        if (phase == 6 && ChapterItemsDone(6) && ChapterItemsDone(ValveChapter)) Advance();
    }

    public void StationPower()
    {
        step = "StationPower";
        EnsureInit();
        if (phase > 1) { Say("It's already running.", 2f); return; }
        if (phase < 1) return;
        if (!ChapterItemsDone(1))
        {
            PlaySfx(clipWrong);
            Say("It needs " + (ChapterItemsNeed(1) - ChapterItemsHave(1)) + " more batteries.", 3f);
            return;
        }
        MarkSynced();
        TakeOwnership();
        carolCount = carolCount + 1;
        Commit();
    }

    public void StationLights()
    {
        step = "StationLights";
        EnsureInit();
        if (phase != 2) { PlaySfx(clipSwitch); Say("<i>Click. Nothing.</i>", 2f); return; }
        if (Time.time < lightsCooldown) { Say("<i>The switch is still warm. Wait a moment.</i>", 2f); return; }
        lightsCooldown = Time.time + 20f;
        MarkSynced();
        TakeOwnership();
        lightsCount = lightsCount + 1;
        Commit();
    }

    /// <summary>A thrown toilet hit the hunting tree.</summary>
    public void StunStalker()
    {
        step = "StunStalker";
        EnsureInit();
        MarkSynced();
        TakeOwnership();
        stunCount = stunCount + 1;
        Commit();
    }

    /// <summary>A shared scare went off for the player who walked into it: everyone else in the game gets it too.</summary>
    public void ShareScare(int index)
    {
        step = "ShareScare";
        EnsureInit();
        MarkSynced();
        TakeOwnership();
        scareIndex = index;
        scareCount = scareCount + 1;
        seenScare = scareCount;   // the player who set it off has already played it
        Commit();
    }

    /// <summary>A gag (kind 0), skeleton routine (1) or the closet (2) was pressed: everyone else sees it too.</summary>
    public void ShareGag(int kind, int index)
    {
        step = "ShareGag";
        EnsureInit();
        MarkSynced();
        TakeOwnership();
        gagKind = kind;
        gagIndex = index;
        gagCount = gagCount + 1;
        seenGag = gagCount;       // the presser has already started it
        Commit();
    }

    public void SolveLock(int ch)
    {
        step = "SolveLock";
        EnsureInit();
        if (phase != ch) return;
        MarkSynced();
        TakeOwnership();
        Advance();
        Commit();
    }

    public void WrongCode(int ch)
    {
        step = "WrongCode";
        EnsureInit();
        PlaySfx(clipWrong);
        if (ch == 1) { FaceScare(0, 1.0f); Say("<color=#ff5555>\"WRONG NOTE.\"</color>", 3f); }
        else if (ch == 3) { GroupMode(3, 4, 2f); Blackout(1.2f); Say("<color=#ff5555>\"THAT'S NOT HOW I SOUND.\"</color>", 3f); }
        else if (ch == 2) { Flicker(1.5f); BehindScare(); }
        else FaceScare(1, 1.0f);
    }

    public bool ChapterItemsDone(int ch) { EnsureInit(); return Minus(chapterMask[ch], itemMask) == 0; }
    public int ChapterItemsHave(int ch) { EnsureInit(); return CountBits(chapterMask[ch]) - CountBits(Minus(chapterMask[ch], itemMask)); }
    public int ChapterItemsNeed(int ch) { EnsureInit(); return CountBits(chapterMask[ch]); }
    public bool ValvesOpen() { return ChapterItemsDone(ValveChapter); }

    void Advance()
    {
        phase = phase + 1;
        if (phase == 7) finishMs = Networking.GetServerTimeInMilliseconds();
    }

    // ============================================================ puzzle answers (from the synced seed)

    /// <summary>
    /// ch1: carol note i (0-3, a tree, never the same twice running), ch2: glow digit i, ch3: tape colour at
    /// log position i, ch4: door digit i, ch5: hat colour for socket i, ch30: session name of tape colour i.
    /// Verified by tools/verify/check_puzzles.py over 300,000 seeds.
    /// </summary>
    public int CodeAt(int ch, int i)
    {
        EnsureInit();
        if (ch == 1)
        {
            int v = Mix(seed, 1, 0) % 4;
            for (int k = 1; k <= i; k++) v = (v + 1 + Mix(seed, 1, k) % 3) % 4;
            return v;
        }
        if (ch == 2) return Mix(seed, 2, i) % 10;
        if (ch == 3) return Perm(3, 4, i);
        if (ch == 30) return Perm(30, 4, i);
        if (ch == 4) return (seed / Pow10(i)) % 10;
        if (ch == 5) return Perm(5, 3, i);
        return 0;
    }

    /// <summary>Integer hash with quadratic mixing, plain arithmetic; max intermediate 2,146,145,274 (fits an int).</summary>
    int Mix(int x, int salt, int i)
    {
        int h = (x % 46309 + (x / 46309) % 46309 + salt * 7919 + i * 104729 + 1) % 46309;
        for (int k = 0; k < 4; k++) h = (h * h + h * 37 + salt * 31 + i * 7 + k * 13 + 17) % 46309;
        return h;
    }

    int Perm(int salt, int n, int i)
    {
        int[] order = new int[] { 0, 1, 2, 3 };
        int s = Mix(seed, salt, 0);
        for (int k = n - 1; k > 0; k--)
        {
            s = Mix(s, salt, k);
            int j = s % (k + 1);
            int tmp = order[k];
            order[k] = order[j];
            order[j] = tmp;
        }
        return order[i];
    }

    // ============================================================ applying the synced state

    void ApplyState()
    {
        string caller = step;
        step = caller + " > items";
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;
            bool got = Has(itemMask, i);
            if (items[i].activeSelf == got) items[i].SetActive(!got);
        }
        int usedColours = 0;
        for (int s = 0; s < 3; s++)
        {
            bool on = Has(hatMask, s);
            int c = CodeAt(5, s);
            if (on) usedColours = With(usedColours, c);
            for (int k = 0; k < 3; k++)
            {
                GameObject h = hatsOnTrees[s * 3 + k];
                if (h != null) h.SetActive(on && k == c);
            }
        }
        for (int k = 0; k < hatPickups.Length; k++)
        {
            bool used = Has(usedColours, k);
            if (hatPickups[k] != null && hatPickups[k].activeSelf == used) hatPickups[k].SetActive(!used);
        }
        step = caller + " > furnace and doors";
        if (furnaceFire != null) furnaceFire.SetActive(phase >= 6 && ValvesOpen());
        for (int d = 0; d < doors.Length; d++) if (doors[d] != null) doors[d].SetActive(phase < d + 2);
        step = caller + " > texts";
        RefreshTexts();
        step = caller + " > round and chapter";
        if (!synced) { step = caller; return; }

        if (round != lastRound)
        {
            lastRound = round;
            lastPhase = phase;
            seenItems = itemMask;
            seenHats = hatMask;
            carolStep = -1;
            for (int j = 0; j < jumpscares.Length; j++) if (jumpscares[j] != null) jumpscares[j].Rearm();
            if (phase == 0)
            {
                if (inGame) { inGame = false; local.TeleportTo(lobbySpawn.position, lobbySpawn.rotation); PlayMusic(0); }
            }
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

        // moments that follow from what changed
        step = caller + " > shared moments";
        if (itemMask != seenItems)
        {
            int added = Minus(itemMask, seenItems);
            seenItems = itemMask;
            if (inGame && added != 0) ItemMoments(added);
        }
        if (hatMask != seenHats)
        {
            seenHats = hatMask;
            if (inGame && phase == 5)
            {
                GroupMode(5, 1, 6f);
                PlaySfx(clipWhoosh);
                if (hatMask != 0 && hatMask != 7) Say("<i>Every tree in the room turns to look at you.</i>", 3f);
            }
        }
        if (carolCount != seenCarol)
        {
            seenCarol = carolCount;
            if (inGame && phase == 1)
            {
                carolStep = 0;
                carolNext = Time.time + 1.2f;
                Say("\"Listen carefully. Then <b>touch us</b> in the same order.\"", 4f);
            }
        }
        if (lightsCount != seenLights)
        {
            seenLights = lightsCount;
            float len = SmallGroup() ? 6f : 8f;
            lightsOutUntil = Time.time + len;
            if (inGame) { PlaySfx(clipSwitch); Blackout(len); }
        }
        if (stunCount != seenStun)
        {
            seenStun = stunCount;
            if (stalker != null) stalker.Stun();
        }
        if (scareCount != seenScare)
        {
            seenScare = scareCount;
            if (inGame && scareIndex >= 0 && scareIndex < jumpscares.Length && jumpscares[scareIndex] != null) jumpscares[scareIndex].FireShared();
        }
        if (gagCount != seenGag)
        {
            seenGag = gagCount;
            step = caller + " > playing a gag";
            PlayGagLocally(gagKind, gagIndex);
        }
        step = caller;
    }

    void PlayGagLocally(int kind, int index)
    {
        if (kind == 0 && index >= 0 && index < gags.Length && gags[index] != null) gags[index].Play();
        else if (kind == 1 && index >= 0 && index < skeletonGags.Length && skeletonGags[index] != null) skeletonGags[index].Play();
        else if (kind == 2 && closet != null) closet.Play();
    }

    void ItemMoments(int added)
    {
        if (phase == 1 && Overlaps(added, chapterMask[1]) && ChapterItemsDone(1))
        { Blackout(2.5f); GroupMode(1, 4, 2.5f); PlaySfx(screams[0]); Say("\"<b>Thank you!</b> Now turn on the panel. We want to <b>sing</b> for you.\"", 5f); }
        else if (phase == 2 && Overlaps(added, chapterMask[2]) && ChapterItemsDone(2))
        { PlaySfx(clipLaugh); Say("\"Four little fuses. The fuse box wants a <b>code</b>. It's written where only the <b>dark</b> can read it.\"", 6f); }
        else if (phase == 3 && Overlaps(added, chapterMask[3]) && ChapterItemsDone(3))
        { GroupMode(3, 2, 3f); Say("\"Play them for me. In the <b>right</b> order.\"", 4f); }
        if (Overlaps(added, chapterMask[ValveChapter]) && ValvesOpen())
        { Blackout(1.5f); PlaySfx(clipHit); Say("<color=#ffaa55>The furnace roars into life.</color> \"You'd burn your own <b>family</b>?\"", 5f); }
    }

    void EnterPhase(int p)
    {
        hintAt = Time.time + (SmallGroup() ? 210f : 300f);
        PlayMusic(p);
        Say(ChapterIntro(p), 10f);
        if (p == 2) { Blackout(1.5f); PlaySfx(screams[1]); }
        if (p == 4) Flicker(3f);
        if (p == 6) { Blackout(2.5f); GroupMode(5, 4, 2.5f); PlaySfx(screams[2]); }
        if (p == 7) PlaySfx(clipLaugh);
    }

    string ChapterIntro(int p)
    {
        if (p == 1) return "\"Ho ho ho! Welcome to the <b>Showroom</b>! The power's out, friends. Find <b>three batteries</b> for the panel by the far door.\"";
        if (p == 2) return "\"Bravo! The <b>stockroom</b> lost its <b>four fuses</b>. Oh, and one of my brothers got out of his box. <b>Keep your eyes on him.</b>\"";
        if (p == 3) return "\"The <b>Voice Lab</b>! Where we learned to talk. Find the <b>four voice tapes</b> and play them in the order on the recording log.\"";
        if (p == 4) return "\"Management locked the door. The code is in their <b>memos</b>. Management liked <b>counting</b> things. Don't mind the staff. They're... resting.\"";
        if (p == 5) return "\"Just like the catalogue photo! But my family is so cold. Read our <b>Christmas card</b> and give everyone the right hat.\"";
        if (p == 6) return "<color=#ff4444>\"You put hats on them. You think they're REAL. THEY ARE IMITATIONS. Light the furnace. Burn all <b>five</b>. I am the ORIGINAL. I will be watching.\"</color>";
        if (p == 7) return "\"Thank you for choosing the ORIGINAL. <b>Play it safe.</b>\"";
        return "";
    }

    string Hint(int p)
    {
        if (p == 1) return ChapterItemsDone(1) ? "Hint: press the power panel to hear the carol again. Touch the table trees in the order they sang."
                                              : "Hint: look inside the presents, behind the window curtain and under the long red table.";
        if (p == 2) return ChapterItemsDone(2) ? "Hint: the light switch is by the entrance. In the dark, look for green writing on the walls."
                                              : "Hint: the fuses are at the ends of the shelf aisles and on crates. Have someone watch the tree.";
        if (p == 3) return "Hint: the card in the booth gives each tape colour's session name. The whiteboard gives the order of the names.";
        if (p == 4) return "Hint: walk every aisle and count carefully. Add the memo's number and keep only the last digit.";
        if (p == 5) return "Hint: the card is on the mantle. One clue says who wears what; the other rules a colour out. Plaques give names.";
        if (p == 6) return ValvesOpen() ? "Hint: drop the little trees into the furnace's glowing mouth." : "Hint: three gas valves glint along the walls. Open all three.";
        return "";
    }

    void RefreshTexts()
    {
        string[] who = new string[] { "R. Pine, Quality Control", "D. Spruce, Voice Department", "H. Holly, Shipping", "M. Balsam, Plant Manager" };
        for (int i = 0; i < memoTexts.Length; i++)
        {
            if (memoTexts[i] == null || i >= countNames.Length || i >= 4) continue;
            int d = CodeAt(4, i);
            int add = ((d - countValues[i] % 10) % 10 + 10) % 10;
            memoTexts[i].text = "<b>MEMO</b>\nFrom: " + who[i] + "\n\nThe <b>" + ordinals[i] + "</b> digit of the door code:\ncount the <b>" + countNames[i] +
                                "</b> in the Offices, then add <b>" + add + "</b>.\n(If it's 10 or more, keep only the last digit.)\n\n<i>If a unit asks you for the code, do not answer it.</i>";
        }
        if (tapeBoard != null)
            tapeBoard.text = "<b>RECORDING LOG - DF-96 voice</b>\nPlay the sessions in this order:\n\n1. " + sessionNames[CodeAt(30, CodeAt(3, 0))] +
                             "\n2. " + sessionNames[CodeAt(30, CodeAt(3, 1))] + "\n3. " + sessionNames[CodeAt(30, CodeAt(3, 2))] +
                             "\n4. " + sessionNames[CodeAt(30, CodeAt(3, 3))] + "\n\n<size=70%>never play session 5</size>";
        if (tapeCard != null)
            tapeCard.text = "<b>TAPE LABELS</b>\n" + colourNames[0] + " = " + sessionNames[CodeAt(30, 0)] + "\n" + colourNames[1] + " = " + sessionNames[CodeAt(30, 1)] +
                            "\n" + colourNames[2] + " = " + sessionNames[CodeAt(30, 2)] + "\n" + colourNames[3] + " = " + sessionNames[CodeAt(30, 3)];
        for (int i = 0; i < glowDigits.Length; i++)
            if (glowDigits[i] != null && i < 4) glowDigits[i].text = ordinals[i] + " digit\n<size=200%>" + CodeAt(2, i) + "</size>";
        if (hatClue != null)
        {
            int r = (seed / 7) % 3;
            int b = (r + 1) % 3;
            int c = (r + 2) % 3;
            hatClue.text = "<i>Merry Christmas from the Fir family!</i>\n\n" + familyNames[r] + " always wears " + hatColourNames[CodeAt(5, r)] + ".\n" +
                           familyNames[b] + " would <b>never</b> wear " + hatColourNames[CodeAt(5, c)] + ".\n\n<i>We'll be watching you open your presents.</i>";
        }
        if (endingBoard != null)
        {
            if (phase == 7)
                endingBoard.text = "<b>YOU ESCAPED</b>\n\nTime: " + Clock(finishMs - startMs) + "\n\nThe Original is still in the showroom.\nIt is still smiling.\n\n<size=60%>Fir Warning - a fan-made horror world. Not affiliated with any toy company.</size>";
            else endingBoard.text = "";
        }
    }

    // ============================================================ per frame

    void Update()
    {
        heartbeat = heartbeat + 1;
        step = "Update: start";
        EnsureInit();
        if (!Utilities.IsValid(local)) return;
        float t = Time.time;
        step = "Update: lighting";

        // darkness and brightness, set on the world's materials only when they change
        float dark = 0f;
        if (t < blackoutUntil) dark = 1f;
        else if (t < flickerUntil && flashing) dark = Random.value < 0.35f ? 0.9f : 0f;
        if (scareState == 2) dark = 0f;
        float reach = 6f;
        float amb = 0.004f;
        if (brightness == 0) { reach = 4.5f; amb = 0f; }
        if (brightness == 2) { reach = 8.5f; amb = 0.04f; }
        if (phase == 7 || !inGame) { reach = 14f; amb = amb + 0.12f; }
        if (scareState == 2 && flashing) amb = amb + 0.1f;
        ApplyLook(dark, reach, amb);
        ApplyMood(RoomAt(local.GetPosition()));

        step = "Update: glow paint";
        bool glow = dark > 0.5f && inGame;
        if (glow != glowShown)
        {
            glowShown = glow;
            for (int g = 0; g < glowOnly.Length; g++) if (glowOnly[g] != null) glowOnly[g].SetActive(glow);
        }

        step = "Update: HUD";
        VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        if (hud != null)
        {
            Vector3 want = head.position + head.rotation * new Vector3(0f, -0.18f, 0.9f);
            float f = Mathf.Min(1f, Time.deltaTime * 8f);
            hud.position = Vector3.Lerp(hud.position, want, f);
            hud.rotation = Quaternion.Slerp(hud.rotation, head.rotation, f);
        }
        if (t > nextHud)
        {
            nextHud = t + 0.5f;
            if (hudObjective != null) hudObjective.text = ObjectiveText();
            if (hudSubtitle != null && t > subtitleUntil) hudSubtitle.text = "";
            if (lobbyStatus != null) lobbyStatus.text = LobbyText();
            if (inGame && phase >= 1 && phase <= 6 && t > hintAt)
            {
                hintAt = t + (SmallGroup() ? 180f : 240f);
                Say("<color=#aaaaaa>" + Hint(phase) + "</color>", 9f);
            }
        }

        step = "Update: carol";
        UpdateCarol();
        step = "Update: face scare";
        UpdateFaceScare(head);
        step = "Update: behind-you scare";
        UpdateBehindScare(head);
        step = "running";
        if (teleportAfterScareAt > 0f && t > teleportAfterScareAt) { teleportAfterScareAt = -1f; TeleportToCheckpoint(); }
    }

    void ApplyLook(float dark, float reach, float amb)
    {
        bool darkChanged = Mathf.Abs(dark - appliedDark) > 0.01f;
        bool reachChanged = Mathf.Abs(reach - appliedReach) > 0.01f;
        bool ambChanged = Mathf.Abs(amb - appliedAmb) > 0.001f;
        if (!darkChanged && !reachChanged && !ambChanged) return;
        for (int i = 0; i < worldMaterials.Length; i++)
        {
            Material m = worldMaterials[i];
            if (m == null) continue;
            if (darkChanged) m.SetFloat("_Dark", dark);
            if (reachChanged) m.SetFloat("_Reach", reach);
            if (ambChanged) m.SetFloat("_Ambient", amb);
        }
        appliedDark = dark;
        appliedReach = reach;
        appliedAmb = amb;
    }

    /// <summary>Which room a position is in (0 lobby, 1-6 chapters, 7 exit), from the factory layout along +z.</summary>
    int RoomAt(Vector3 p)
    {
        if (p.x < -30f) return 0;
        if (p.z < 20f) return 1;
        if (p.z < 56f) return 2;
        if (p.z < 80f) return 3;
        if (p.z < 104f) return 4;
        if (p.z < 120f) return 5;
        if (p.z < 150f) return 6;
        return 7;
    }

    /// <summary>Sets the room's mood colours on the world's materials when you walk into another room.</summary>
    void ApplyMood(int room)
    {
        if (room == appliedRoom && brightness == appliedBrightness) return;
        appliedRoom = room;
        appliedBrightness = brightness;
        float scale = 1f;
        if (brightness == 0) scale = 0.6f;
        if (brightness == 2) scale = 1.6f;
        Color a = roomAmb[room];
        Color amb = new Color(a.r * scale, a.g * scale, a.b * scale, 1f);
        Color lan = roomLan[room];
        for (int i = 0; i < worldMaterials.Length; i++)
        {
            Material m = worldMaterials[i];
            if (m == null) continue;
            m.SetColor("_AmbColor", amb);
            m.SetColor("_LanColor", lan);
        }
    }

    void UpdateCarol()
    {
        if (carolStep < 0 || Time.time < carolNext) return;
        if (carolStep >= 5)
        {
            carolStep = -1;
            if (Random.value < 0.5f) { Blackout(1.2f); PlaySfx(clipLaugh); }
            return;
        }
        int tree = CodeAt(1, carolStep);
        if (tree < carolTrees.Length && carolTrees[tree] != null) carolTrees[tree].Note();
        carolStep = carolStep + 1;
        carolNext = Time.time + 1.1f;
    }

    string ObjectiveText()
    {
        if (!inGame || phase < 1 || phase > 6) return "";
        if (phase == 1) return ChapterItemsDone(1) ? "Power the panel, then repeat the carol on the table trees" : "Batteries: " + ChapterItemsHave(1) + "/3";
        if (phase == 2) return ChapterItemsDone(2) ? "Find the 3 glow-paint digits (lights off!) and enter them at the fuse box" : "Fuses: " + ChapterItemsHave(2) + "/4";
        if (phase == 3) return ChapterItemsDone(3) ? "Play the tapes on the deck in the log's order" : "Voice tapes: " + ChapterItemsHave(3) + "/4";
        if (phase == 4) return "Solve the 4 memos, then enter the door code";
        if (phase == 5) return "Santa hats: " + CountBits(hatMask) + "/3  -  read the Christmas card";
        if (!ValvesOpen()) return "Gas valves: " + ChapterItemsHave(ValveChapter) + "/" + ChapterItemsNeed(ValveChapter);
        return "Imitations burned: " + ChapterItemsHave(6) + "/" + ChapterItemsNeed(6);
    }

    string LobbyText()
    {
        if (phase == 0) return "Nobody is playing.\nPress <b>START</b> when everyone is here.\n<size=70%>Plays well with 1-8. With 1 or 2 players the hunting tree is slower.</size>";
        if (phase == 7) return "The last group escaped in " + Clock(finishMs - startMs) + ".\nPress <b>START</b> to play again.";
        int now = Networking.GetServerTimeInMilliseconds();
        return "Game in progress: chapter " + phase + "/6, " + roomNames[phase] + "\n" + Clock(now - startMs) + " elapsed\nPress <b>Join game</b> to catch up.";
    }

    // ============================================================ scares (local)

    public void Say(string text, float seconds)
    {
        if (hudSubtitle == null) return;
        hudSubtitle.text = text;
        subtitleUntil = Time.time + seconds;
        nextHud = 0f;
    }

    public void PlaySfx(AudioClip c) { if (c != null && sfx != null) sfx.PlayOneShot(c); }
    public void Blackout(float seconds) { blackoutUntil = Mathf.Max(blackoutUntil, Time.time + seconds); }
    public void Flicker(float seconds) { flickerUntil = Mathf.Max(flickerUntil, Time.time + seconds); }
    public bool IsInGame() { return inGame; }

    public void GroupMode(int chapter, int mode, float seconds)
    {
        for (int i = 0; i < animatronics.Length; i++)
            if (animatronics[i] != null && animatronics[i].chapter == chapter) animatronics[i].SetMode(mode, seconds);
    }

    public void FaceScare(int screamIndex, float seconds)
    {
        if (scareState != 0 || behindActive) return;
        if (reducedScares) { PlaySfx(screams[screamIndex % screams.Length]); Flicker(0.6f); return; }
        scareClip = screamIndex;
        scareLen = seconds;
        scareState = 1;
        scareNext = Time.time + 0.18f;
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
            scareState = 2;
            scareNext = t + scareLen;
            scareRig.gameObject.SetActive(true);
            if (scareClip >= 0)
            {
                if (scareAnim != null) scareAnim.SetMode(4, scareLen);
                PlaySfx(screams[scareClip % screams.Length]);
                PlaySfx(clipHit);
            }
        }
        else if (scareState == 2)
        {
            scareState = 3;
            scareNext = t + 0.7f;
            scareRig.gameObject.SetActive(false);
            Blackout(0.7f);
        }
        else scareState = 0;
    }

    public void Subliminal()
    {
        if (scareState != 0 || behindActive || reducedScares) return;
        scareClip = -1;
        scareLen = 0.12f;
        scareState = 1;
        scareNext = Time.time + 0.05f;
    }

    public void Glitch()
    {
        string[] lines = new string[] { "Y O U   C A N ' T   L E A V E", "BUY ONLY THE ORIGINAL", "it is behind the next door",
                                        "I SEE YOU", "PLAY IT SAFE PLAY IT SAFE PLAY IT SAFE", "who is standing next to you?" };
        Say("<color=#ff2020><b>" + lines[Random.Range(0, lines.Length)] + "</b></color>", 1.4f);
        PlaySfx(clipStatic);
        Flicker(0.6f);
    }

    public void BehindScare()
    {
        if (scareState != 0 || behindActive || scareRig == null) return;
        if (reducedScares) { PlaySfx(clipWhisper); return; }
        VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 b = head.rotation * Vector3.back;
        Vector3 back = new Vector3(b.x, 0f, b.z).normalized;
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

    public void Caught()
    {
        if (!inGame) return;
        FaceScare(phase == 6 ? 2 : 0, 1.1f);
        Say("<color=#ff5555>It got you.</color> Back to the start of the room.", 4f);
        teleportAfterScareAt = Time.time + 1.4f;
    }

    // ============================================================ players

    public int PlayersInGame()
    {
        EnsureInit();
        if (Time.time < nextCount) return cachedInGame;
        nextCount = Time.time + 1f;
        int n = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi.GetPlayers(countBuf);
        int c = 0;
        for (int i = 0; i < n && i < countBuf.Length; i++)
        {
            VRCPlayerApi p = countBuf[i];
            if (Utilities.IsValid(p) && Vector3.Distance(p.GetPosition(), lobbySpawn.position) > 25f) c = c + 1;
        }
        cachedInGame = Mathf.Max(1, c);
        return cachedInGame;
    }

    public bool SmallGroup() { return PlayersInGame() <= 2; }

    public float NearestOtherPlayer(Vector3 from)
    {
        EnsureInit();
        int n = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi.GetPlayers(countBuf);
        float best = 999f;
        for (int i = 0; i < n && i < countBuf.Length; i++)
        {
            VRCPlayerApi p = countBuf[i];
            if (!Utilities.IsValid(p) || p.isLocal) continue;
            if (Vector3.Distance(p.GetPosition(), lobbySpawn.position) < 25f) continue;
            float d = Vector3.Distance(p.GetPosition(), from);
            if (d < best) best = d;
        }
        return best;
    }

    // ============================================================ helpers

    void TeleportToCheckpoint()
    {
        if (checkpoints.Length == 0) return;
        int i = Mathf.Clamp(phase - 1, 0, checkpoints.Length - 1);
        if (checkpoints[i] == null) return;
        local.TeleportTo(checkpoints[i].position, checkpoints[i].rotation);
    }

    void PlayMusic(int p)
    {
        if (music == null || p < 0 || p >= chapterMusic.Length) return;
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
    int Pow10(int i) { int r = 1; for (int k = 0; k < i; k++) r = r * 10; return r; }

    string Clock(int ms)
    {
        if (ms < 0) ms = 0;
        int s = ms / 1000;
        int m = s / 60;
        s = s % 60;
        return m + ":" + (s < 10 ? "0" : "") + s;
    }

    // ---- sets of small numbers (0-29) as sums of powers of two: plain arithmetic, no bitwise operators

    bool Has(int set, int i) { return (set / pow2[i]) % 2 == 1; }
    int With(int set, int i) { return Has(set, i) ? set : set + pow2[i]; }

    int CountBits(int set)
    {
        int c = 0;
        for (int i = 0; i < 30; i++) if (Has(set, i)) c = c + 1;
        return c;
    }

    int Minus(int a, int b)
    {
        int r = 0;
        for (int i = 0; i < 30; i++) if (Has(a, i) && !Has(b, i)) r = r + pow2[i];
        return r;
    }

    int Union(int a, int b) { return a + Minus(b, a); }
    bool Overlaps(int a, int b) { return CountBits(a) > CountBits(Minus(a, b)); }
}
