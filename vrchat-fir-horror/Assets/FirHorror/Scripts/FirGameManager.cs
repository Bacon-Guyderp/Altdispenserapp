using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

/// <summary>
/// Runs Fir Warning: the synced story state (which chapter, what's been found), the puzzles'
/// answers (all derived from one synced seed, so every game is different), doors, the
/// head-locked HUD and subtitles, the global "lantern" lighting, and the shared jumpscare rig.
///
/// Chapters (phase): 0 lobby, 1 Showroom, 2 Stockroom, 3 Voice Lab, 4 Offices, 5 Living Room,
/// 6 Recall Floor, 7 escaped. Door i opens once phase >= i + 2.
///
/// Puzzles:
///  1 Carol      find 3 batteries, power the panel, repeat the 5-note carol on the table trees
///  2 Glow paint find 4 fuses, kill the lights to read 3 glow-paint digits, enter them at the fuse box
///  3 Tapes      find 4 tapes; booth card maps colour to session name, whiteboard gives name order
///  4 Memos      each memo is a counting riddle for one digit of the door code
///  5 Hats       a Christmas card of logic clues says which tree wears which colour of hat
///  6 Furnace    open 3 gas valves to light it, then burn 5 imitations while the Original hunts you
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FirGameManager : UdonSharpBehaviour
{
    public const int ValveChapter = 9;   // pseudo-chapter for the furnace valves (collected in phase 6)

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
    public GameObject[] hatsOnTrees;         // [socket * 3 + colour]
    public GameObject[] hatPickups;          // by colour
    public FirAnimatronic[] animatronics;
    public FirAnimatronic[] carolTrees;      // the four Showroom table trees
    public FirJumpscare[] jumpscares;
    public TextMeshPro[] memoTexts;          // office memos, one code digit each
    public string[] countNames;              // what each memo asks you to count
    public int[] countValues;                // how many of them the builder placed
    public TextMeshPro tapeBoard;            // whiteboard: session-name order
    public TextMeshPro tapeCard;             // booth card: colour -> session name
    public TextMeshPro hatClue;              // Christmas card on the mantle
    public TextMeshPro[] glowDigits;         // Stockroom glow paint, one digit each
    public GameObject[] glowOnly;            // everything only visible in the dark
    public GameObject furnaceFire;
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
    public AudioClip clipPickup, clipUnlock, clipWrong, clipTalk, clipLaugh, clipStatic, clipHit, clipWhoosh, clipHeartbeat, clipWhisper, clipSwitch;
    public Transform scareRig;               // a tree whose face sits at the rig's origin
    public FirAnimatronic scareAnim;

    /// <summary>While this is in the future, the lights are out for everyone and nobody can see the stalker.</summary>
    public float lightsOutUntil;

    // ------------------------------------------------------------ local
    VRCPlayerApi local;
    bool synced, inGame, glowShown;
    int lastRound, lastPhase;
    int[] chapterMask;
    float blackoutUntil, flickerUntil, subtitleUntil, hintAt, nextHud, resetArmedUntil, lightsCooldown;
    int brightness = 1;                      // 0 dark, 1 normal, 2 bright
    bool flashing = true, reducedScares;
    int idLight, idDark, idAmbient;
    int idAmbR, idAmbG, idAmbB, idLanR, idLanG, idLanB;
    Vector3[] roomAmb, roomLan;            // each room's mood light and lantern colour
    Vector3 ambNow, lanNow;
    int carolStep = -1; float carolNext;
    int pendingItems, pendingHats;          // what we collected/placed until we see it come back synced
    VRCPlayerApi[] countBuf;
    float nextCount; int cachedInGame = 1;

    // face scare: 0 idle, 1 pre-blink, 2 showing, 3 post-dark
    int scareState; float scareNext; float scareLen; int scareClip;
    // behind scare: tree placed behind you, screams when you turn round
    bool behindActive; float behindTimeout;
    float teleportAfterScareAt = -1;

    string[] colourNames, sessionNames, hatColourNames, familyNames, ordinals;

    void Start()
    {
        local = Networking.LocalPlayer;
        chapterMask = new int[10];
        colourNames = new string[] { "<color=#ff4040>RED</color>", "<color=#40c0ff>BLUE</color>", "<color=#50ff60>GREEN</color>", "<color=#ffd030>GOLD</color>" };
        sessionNames = new string[] { "\"JINGLE\"", "\"GREETING\"", "\"LAUGHTER\"", "\"GOODNIGHT\"" };
        hatColourNames = new string[] { "<color=#c00000>red</color>", "<color=#007a10>green</color>", "<color=#1040c0>blue</color>" };
        familyNames = new string[] { "Junior", "Mama", "Grandpa" };
        ordinals = new string[] { "1st", "2nd", "3rd", "4th" };
        idLight = VRCShader.PropertyToID("_UdonFirLight");
        idDark = VRCShader.PropertyToID("_UdonFirDark");
        idAmbient = VRCShader.PropertyToID("_UdonFirAmbient");
        idAmbR = VRCShader.PropertyToID("_UdonFirAmbR"); idAmbG = VRCShader.PropertyToID("_UdonFirAmbG"); idAmbB = VRCShader.PropertyToID("_UdonFirAmbB");
        idLanR = VRCShader.PropertyToID("_UdonFirLanR"); idLanG = VRCShader.PropertyToID("_UdonFirLanG"); idLanB = VRCShader.PropertyToID("_UdonFirLanB");
        // 0 lobby, 1 showroom (red), 2 stockroom (sickly green), 3 voice lab (purple), 4 offices (cold blue),
        // 5 living room (fireplace amber), 6 recall floor (furnace orange), 7 exit
        roomAmb = new Vector3[] { new Vector3(0.06f, 0.05f, 0.04f), new Vector3(0.07f, 0.012f, 0.012f), new Vector3(0.015f, 0.05f, 0.025f),
                                  new Vector3(0.04f, 0.014f, 0.07f), new Vector3(0.012f, 0.03f, 0.07f), new Vector3(0.07f, 0.04f, 0.01f),
                                  new Vector3(0.09f, 0.03f, 0.005f), new Vector3(0.05f, 0.05f, 0.05f) };
        roomLan = new Vector3[] { new Vector3(1f, 0.85f, 0.65f), new Vector3(1f, 0.82f, 0.62f), new Vector3(0.95f, 0.88f, 0.65f),
                                  new Vector3(1f, 0.8f, 0.75f), new Vector3(0.85f, 0.92f, 1.05f), new Vector3(1.05f, 0.8f, 0.55f),
                                  new Vector3(1.05f, 0.7f, 0.45f), new Vector3(1f, 0.9f, 0.75f) };
        ambNow = roomAmb[0]; lanNow = roomLan[0];
        for (int i = 0; i < itemChapter.Length && i < 31; i++) chapterMask[itemChapter[i]] |= 1 << i;
        if (scareRig != null) scareRig.gameObject.SetActive(false);
        for (int g = 0; g < glowOnly.Length; g++) if (glowOnly[g] != null) glowOnly[g].SetActive(false);
        if (Networking.IsOwner(gameObject))
        {
            if (seed == 0) seed = Random.Range(1, 1 << 30);
            RequestSerialization();
            ApplyState();
        }
        else RefreshTexts();
        PlayMusic(0);
        UpdateOptionsText();
    }

    public override void OnDeserialization()
    {
        ApplyState();
        // Two players collecting at the same moment both write the whole mask, and the last write wins.
        // If something we collected went missing, put it back (and check whether that finished a step).
        int lostItems = pendingItems & ~itemMask, lostHats = pendingHats & ~hatMask;
        pendingItems = lostItems; pendingHats = lostHats;
        if ((lostItems | lostHats) == 0) return;
        if (phase < 1 || phase > 6) { pendingItems = 0; pendingHats = 0; return; }
        TakeOwnership();
        itemMask |= lostItems;
        hatMask |= lostHats;
        bool lit = (lostItems & chapterMask[ValveChapter]) != 0 && ChapterItemsDone(ValveChapter);
        bool done = phase >= 1 && phase <= 3 && (lostItems & chapterMask[phase]) != 0 && ChapterItemsDone(phase);
        if (phase == 6 && ChapterItemsDone(6) && ChapterItemsDone(ValveChapter)) Advance();
        if (phase == 5 && hatMask == 7) Advance();
        Commit();
        if (lit) SendCustomNetworkEvent(NetworkEventTarget.All, "NetFurnaceLit");
        if (done) SendCustomNetworkEvent(NetworkEventTarget.All, "NetChapterItemsDone");
    }

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
        bool allowed = ch == phase || (ch == ValveChapter && phase == 6);
        if (!allowed) { Say("<i>It won't come loose. Not yet.</i>", 3f); return; }
        TakeOwnership();
        itemMask |= bit;
        pendingItems |= bit;
        PlaySfx(clipPickup);
        bool done = ChapterItemsDone(ch);
        if (phase == 6 && ChapterItemsDone(6) && ChapterItemsDone(ValveChapter)) Advance();  // last imitation burned
        Commit();
        if (done) SendCustomNetworkEvent(NetworkEventTarget.All, ch == ValveChapter ? "NetFurnaceLit" : "NetChapterItemsDone");
    }

    public void FillSocket(int socket)
    {
        int bit = 1 << socket;
        if ((hatMask & bit) != 0 || phase != 5) return;
        TakeOwnership();
        hatMask |= bit;
        pendingHats |= bit;
        PlaySfx(clipPickup);
        if (hatMask == 7) Advance();
        Commit();
        SendCustomNetworkEvent(NetworkEventTarget.All, "NetHatPlaced");
    }

    /// <summary>Showroom power panel: once the batteries are in, it makes the trees sing the carol.</summary>
    public void StationPower()
    {
        if (phase > 1) { Say("It's already running.", 2f); return; }
        if (phase < 1) return;
        if (!ChapterItemsDone(1))
        {
            PlaySfx(clipWrong);
            Say("It needs " + (CountBits(chapterMask[1]) - ChapterItemsHave(1)) + " more batteries.", 3f);
            return;
        }
        SendCustomNetworkEvent(NetworkEventTarget.All, "NetPlayCarol");
    }

    /// <summary>Stockroom light switch: lights out for everyone for 8 seconds.</summary>
    public void StationLights()
    {
        if (phase != 2) { PlaySfx(clipSwitch); Say("<i>Click. Nothing.</i>", 2f); return; }
        if (Time.time < lightsCooldown) { Say("<i>The switch is still warm. Wait a moment.</i>", 2f); return; }
        lightsCooldown = Time.time + 20f;
        SendCustomNetworkEvent(NetworkEventTarget.All, "NetLightsOut");
    }

    /// <summary>Every sequence lock (carol, fuse box, tape deck, door keypad) calls this when solved.</summary>
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
        if (ch == 1) { FaceScare(0, 1.0f); Say("<color=#ff5555>\"WRONG NOTE.\"</color>", 3f); }
        else if (ch == 3) { GroupMode(3, 4, 2f); Blackout(1.2f); Say("<color=#ff5555>\"THAT'S NOT HOW I SOUND.\"</color>", 3f); }
        else if (ch == 2) { Flicker(1.5f); BehindScare(); }
        else FaceScare(1, 1.0f);
    }

    public bool ChapterItemsDone(int ch) { return (itemMask & chapterMask[ch]) == chapterMask[ch]; }
    public int ChapterItemsHave(int ch) { return CountBits(itemMask & chapterMask[ch]); }
    public int ChapterItemsNeed(int ch) { return CountBits(chapterMask[ch]); }
    public bool ValvesOpen() { return ChapterItemsDone(ValveChapter); }

    /// <summary>
    /// The answer for each puzzle step, from the synced seed.
    /// ch1: carol note i (0-3, a tree), ch2: digit i, ch3: tape colour at log position i,
    /// ch4: door digit i, ch5: hat colour for socket i (0-2), ch30: session name of tape colour i.
    /// </summary>
    public int CodeAt(int ch, int i)
    {
        if (ch == 1)
        {
            int v = Hash(1, 0) % 4;   // each note moves to a different tree than the last one
            for (int k = 1; k <= i; k++) v = (v + 1 + Hash(1, k) % 3) % 4;
            return v;
        }
        if (ch == 2) return Hash(2, i) % 10;
        if (ch == 3) return Perm(3, 4, i);
        if (ch == 30) return Perm(30, 4, i);
        if (ch == 4) return (seed / Pow10(i)) % 10;
        if (ch == 5) return Perm(5, 3, i);
        return 0;
    }

    int Hash(int salt, int i) { return Mix(seed, salt, i); }

    /// <summary>
    /// A small integer hash with real mixing (xor-shift + multiply), so every puzzle answer is independent.
    /// Stays inside 32-bit ints: h &lt; 46337, so h * 46327 + small &lt;= 2,146,608,832 &lt; 2,147,483,647.
    /// (Checked over 300,000 seeds: all 324 carols occur, digits are uniform, every order occurs.)
    /// </summary>
    int Mix(int x, int salt, int i)
    {
        int h = x ^ (salt * 7919 + i * 104729);
        for (int k = 0; k < 3; k++)
        {
            h = (h ^ (h >> 13)) % 46337;
            h = (h * 46327 + salt * 31 + i * 7 + k) % 2147483647;
        }
        return h;
    }

    int Perm(int salt, int n, int i)
    {
        int[] order = { 0, 1, 2, 3 };
        int s = Mix(seed, salt, 0);
        for (int k = n - 1; k > 0; k--)
        {
            s = Mix(s, salt, k);
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
        if (lastPhase == 1) { Blackout(2.5f); GroupMode(1, 4, 2.5f); PlaySfx(screams[0]); Say("\"<b>Thank you!</b> Now turn on the panel. We want to <b>sing</b> for you.\"", 5f); }
        else if (lastPhase == 2) { PlaySfx(clipLaugh); Say("\"Four little fuses. The fuse box wants a <b>code</b>. It's written where only the <b>dark</b> can read it.\"", 6f); }
        else if (lastPhase == 3) { GroupMode(3, 2, 3f); Say("\"Play them for me. In the <b>right</b> order.\"", 4f); }
        else if (lastPhase == 6) { PlaySfx(clipLaugh); }
    }

    public void NetFurnaceLit()
    {
        if (!inGame) return;
        Blackout(1.5f);
        PlaySfx(clipHit);
        Say("<color=#ffaa55>The furnace roars into life.</color> \"You'd burn your own <b>family</b>?\"", 5f);
    }

    public void NetHatPlaced()
    {
        if (!inGame) return;
        GroupMode(5, 1, 6f);
        PlaySfx(clipWhoosh);
        if (hatMask != 0 && hatMask != 7) Say("<i>Every tree in the room turns to look at you.</i>", 3f);
    }

    public void NetPlayCarol()
    {
        if (!inGame || phase != 1) return;
        carolStep = 0;
        carolNext = Time.time + 1.2f;
        Say("\"Listen carefully. Then <b>touch us</b> in the same order.\"", 4f);
    }

    public void NetLightsOut()
    {
        float len = SmallGroup() ? 6f : 8f;
        lightsOutUntil = Time.time + len;
        if (!inGame) return;
        PlaySfx(clipSwitch);
        Blackout(len);
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
        int usedColours = 0;
        for (int s = 0; s < 3; s++)
        {
            bool on = (hatMask & (1 << s)) != 0;
            int c = CodeAt(5, s);
            if (on) usedColours |= 1 << c;
            for (int k = 0; k < 3; k++)
            {
                GameObject h = hatsOnTrees[s * 3 + k];
                if (h != null) h.SetActive(on && k == c);
            }
        }
        for (int k = 0; k < hatPickups.Length; k++)
        {
            bool used = (usedColours & (1 << k)) != 0;
            if (hatPickups[k] != null && hatPickups[k].activeSelf == used) hatPickups[k].SetActive(!used);
        }
        if (furnaceFire != null) furnaceFire.SetActive(ValvesOpen() && phase >= 6);
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
            carolStep = -1;
            pendingItems = 0; pendingHats = 0;
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
        hintAt = Time.time + (SmallGroup() ? 210f : 300f);   // a hint after 3.5 min (1-2 players) or 5 min stuck
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
        if (p == 1) return ChapterItemsDone(1) ? "Hint: press the power panel to hear the carol again. Touch the table trees in the order they sang. The display shows how far you've got."
                                              : "Hint: look inside the presents, behind the window curtain and under the long red table.";
        if (p == 2) return ChapterItemsDone(2) ? "Hint: the light switch is by the entrance. In the dark, look for green writing on the walls. Someone should keep watching the tree."
                                              : "Hint: the fuses are at the ends of the shelf aisles and on crates. Have someone watch the tree while others search.";
        if (p == 3) return "Hint: the card in the booth tells you each tape colour's session name. The whiteboard gives the order of the names.";
        if (p == 4) return "Hint: walk every aisle and count carefully. Calendars hang inside the cubicles. Add the memo's number and keep only the last digit.";
        if (p == 5) return "Hint: the card is on the mantle. One clue says who wears what. The other rules a colour out. Plaques under the trees give their names.";
        if (p == 6) return ValvesOpen() ? "Hint: drop the little trees into the furnace's glowing mouth." : "Hint: three gas valves glint along the walls. Open all three to light the furnace.";
        return "";
    }

    void RefreshTexts()
    {
        for (int i = 0; i < memoTexts.Length; i++)
        {
            if (memoTexts[i] == null || i >= countNames.Length) continue;
            string[] who = { "R. Pine, Quality Control", "D. Spruce, Voice Department", "H. Holly, Shipping", "M. Balsam, Plant Manager" };
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
            if (glowDigits[i] != null) glowDigits[i].text = ordinals[i] + " digit\n<size=200%>" + CodeAt(2, i) + "</size>";
        if (hatClue != null)
        {
            int r = (seed / 7) % 3;
            int b = (r + 1) % 3, c = (r + 2) % 3;
            hatClue.text = "<i>Merry Christmas from the Fir family!</i>\n\n" + familyNames[r] + " always wears " + hatColourNames[CodeAt(5, r)] + ".\n" +
                           familyNames[b] + " would <b>never</b> wear " + hatColourNames[CodeAt(5, c)] + ".\n\n<i>We'll be watching you open your presents.</i>";
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
        float reach = brightness == 0 ? 4.5f : brightness == 1 ? 6f : 8.5f;
        float amb = brightness == 0 ? 0f : brightness == 1 ? 0.004f : 0.04f;
        if (phase == 7 || !inGame) { reach = 14f; amb += 0.12f; }
        if (scareState == 2 && flashing) amb += 0.1f;
        VRCShader.SetGlobalFloat(idDark, dark);
        VRCShader.SetGlobalFloat(idLight, reach);
        VRCShader.SetGlobalFloat(idAmbient, amb);

        // room mood colours, blended as you walk from room to room
        Vector3 me = local.GetPosition();
        int room = RoomAt(me);
        float moodScale = brightness == 0 ? 0.6f : brightness == 1 ? 1f : 1.6f;
        float blend = Mathf.Min(1f, Time.deltaTime * 2f);
        ambNow = Vector3.Lerp(ambNow, roomAmb[room] * moodScale, blend);
        lanNow = Vector3.Lerp(lanNow, roomLan[room], blend);
        VRCShader.SetGlobalFloat(idAmbR, ambNow.x); VRCShader.SetGlobalFloat(idAmbG, ambNow.y); VRCShader.SetGlobalFloat(idAmbB, ambNow.z);
        VRCShader.SetGlobalFloat(idLanR, lanNow.x); VRCShader.SetGlobalFloat(idLanG, lanNow.y); VRCShader.SetGlobalFloat(idLanB, lanNow.z);

        // glow paint only shows in the dark
        bool glow = dark > 0.5f && inGame;
        if (glow != glowShown)
        {
            glowShown = glow;
            for (int g = 0; g < glowOnly.Length; g++) if (glowOnly[g] != null) glowOnly[g].SetActive(glow);
        }

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
            if (inGame && phase >= 1 && phase <= 6 && t > hintAt) { hintAt = t + (SmallGroup() ? 180f : 240f); Say("<color=#aaaaaa>" + Hint(phase) + "</color>", 9f); }
        }

        UpdateCarol();
        UpdateFaceScare(head);
        UpdateBehindScare(head);
        if (teleportAfterScareAt > 0 && t > teleportAfterScareAt) { teleportAfterScareAt = -1; TeleportToCheckpoint(); }
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
        carolStep++;
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
        if (phase == 0) return "Nobody is playing.\nPress <b>START</b> when everyone is here.\n<size=70%>Plays well with 1-8. With 1 or 2 players the hunting tree is slower and hints come sooner.</size>";
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
            if (scareClip >= 0)
            {
                if (scareAnim != null) scareAnim.SetMode(4, scareLen);
                PlaySfx(screams[scareClip % screams.Length]);
                PlaySfx(clipHit);
            }
        }
        else if (scareState == 2)
        {
            scareState = 3; scareNext = t + 0.7f;
            scareRig.gameObject.SetActive(false);
            Blackout(0.7f);
        }
        else scareState = 0;
    }

    /// <summary>A face for a split second, no sound. Did you see that?</summary>
    public void Subliminal()
    {
        if (scareState != 0 || behindActive || reducedScares) return;
        scareClip = -1; scareLen = 0.12f;
        scareState = 1; scareNext = Time.time + 0.05f;
    }

    /// <summary>The HUD corrupts for a moment.</summary>
    public void Glitch()
    {
        string[] lines = { "Y O U   C A N ' T   L E A V E", "BUY ONLY THE ORIGINAL", "it is behind the next door",
                           "I SEE YOU", "PLAY IT SAFE PLAY IT SAFE PLAY IT SAFE", "who is standing next to you?" };
        Say("<color=#ff2020><b>" + lines[Random.Range(0, lines.Length)] + "</b></color>", 1.4f);
        PlaySfx(clipStatic);
        Flicker(0.6f);
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

    /// <summary>Which room a position is in (0 lobby ... 7 exit), for the mood lighting.</summary>
    int RoomAt(Vector3 p)
    {
        if (p.x < -30f || p.z < 0f) return 0;
        if (p.z < 20f) return 1;
        if (p.z < 56f) return 2;
        if (p.z < 80f) return 3;
        if (p.z < 104f) return 4;
        if (p.z < 120f) return 5;
        if (p.z < 150f) return 6;
        return 7;
    }

    // ============================================================ group size

    /// <summary>Players inside the factory (everyone not in the lobby). At least 1. Refreshed once a second.</summary>
    public int PlayersInGame()
    {
        if (Time.time < nextCount) return cachedInGame;
        nextCount = Time.time + 1f;
        if (countBuf == null) countBuf = new VRCPlayerApi[96];
        int n = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi.GetPlayers(countBuf);
        int c = 0;
        for (int i = 0; i < n && i < countBuf.Length; i++)
        {
            VRCPlayerApi p = countBuf[i];
            if (Utilities.IsValid(p) && Vector3.Distance(p.GetPosition(), lobbySpawn.position) > 25f) c++;
        }
        cachedInGame = Mathf.Max(1, c);
        return cachedInGame;
    }

    /// <summary>True once this player has been taken into the factory (not waiting in the lobby).</summary>
    public bool IsInGame() { return inGame; }

    /// <summary>Solo or duo: the stalker eases off and hints come sooner.</summary>
    public bool SmallGroup() { return PlayersInGame() <= 2; }

    /// <summary>Distance from a point to the nearest other player in the factory (999 if there's nobody else).</summary>
    public float NearestOtherPlayer(Vector3 from)
    {
        if (countBuf == null) countBuf = new VRCPlayerApi[96];
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
