using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// The horror game. Until someone presses START the map is a lit, open gag museum. START pulls everyone into
/// the Gift Wrap Hall: the lights go out (each player's own lantern), every room's exit is locked until its
/// puzzle is solved, scare zones arm, and the hunting tree (FirStalker) comes for whoever isn't looking at it.
///
/// Rooms and puzzles (stage = how many rooms are solved; the door into room i opens at stage i):
///   0 Gift Wrap Hall: find 5 batteries, then press the power box by the exit
///   1 Rainbow Hall:   4 glowing numbers hidden in the hall; type them in rainbow order (red, yellow, green, blue)
///   2 Toy Workshop:   4 notes give the order of 4 toys; press the toys on the exit panel in that order
///   3 Chimney Lounge: find 4 ornaments, then hang them on the bare tree
///   4 Talent Show:    the stage manager's memo: performers on stage, then seats in the house
///   5 Bone Yard:      open 3 gas valves, then pull the furnace lever: the tree is pulled into the fire. You win.
///
/// Networking: everything shared is a handful of synced ints (manual sync). Whoever presses something takes
/// ownership, changes them and sends them; every client then applies the state with Apply(). Puzzle codes come
/// from the synced round number, so every client works out the same codes without sending them.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FirHunt : UdonSharpBehaviour
{
    [UdonSynced] public bool running;
    [UdonSynced] public int round;
    [UdonSynced] public int stage;
    [UdonSynced] public int foundMask;     // collected items, one bit each (worked out with arithmetic, not bit operators)
    [UdonSynced] public int stunCount;
    [UdonSynced] public int caughtCount;

    // ---- set up by the builder
    public Material[] darkMats;            // map materials that go dark (lantern lit) during the game
    public GameObject[] doors;             // doors[i] blocks the doorway into room i (doors[0] is unused)
    public GameObject[] items;             // batteries, ornaments, valves: index = item id
    public GameObject[] itemDone;          // optional "done" look for an item (a turned valve), or null
    public int[] itemRoom;                 // the room each item belongs to
    public Transform[] checkpoints;        // each room's entrance (where you start, and where you go when caught)
    public GameObject[] huntOnly;          // shown only during the game (clues, digits, notes)
    public GameObject[] freeOnly;          // shown only outside the game
    public GameObject[] stageShow;         // shown once stage reaches stageShowAt (hung ornaments, the roaring furnace)
    public int[] stageShowAt;
    public TextMeshPro[] roomBoards;       // each room's task board
    public TextMeshPro lobbyBoard;
    public TextMeshPro[] rainbowDigits;    // red, yellow, green, blue
    public TextMeshPro rainbowDisplay;
    public TextMeshPro talentDisplay;
    public TextMeshPro[] toyNotes;
    public TextMeshPro toyDisplay;
    public string[] toyNames;              // the five toys on the Toy Workshop's panel
    public string talentCode = "";
    public UdonBehaviour stalker;
    public Transform stalkerBody;          // where the tree is (for catching and the heartbeat)
    public AudioSource sfx;                // 2D, for this player's one-shots
    public AudioClip startClip, unlockClip, wrongClip, pickupClip, switchClip, endingClip, screamClip, stunClip;
    public AudioSource ambient;            // moved behind you for random whispers and rustles
    public AudioClip[] ambientClips;
    public AudioSource heartbeat;          // looping, louder as the tree gets close
    public Transform scareFace;            // a tree face that fills your view when you're caught
    public float catchDistance = 1.1f;

    // ---- arguments (FirHuntButton sets these, then sends EvButton)
    public int argAction;
    public int argValue;

    // ---- this player only
    int lastRound = -1, lastStage = -1, lastStun, lastCaught;
    bool darkNow, darkSet;
    string typed1 = "", typed4 = "";
    int seqPos;
    float nextAmbient, scareUntil, teleportAt, catchReady, boardsAt;
    bool teleportPending;
    int[] pow;
    int[] toyOrder;
    int[] rainbowCode;

    void Start()
    {
        loadedAt = Time.time;
        pow = new int[30];
        int p = 1;
        for (int i = 0; i < 30; i++) { pow[i] = p; p = p * 2; }
        toyOrder = new int[4];
        rainbowCode = new int[4];
        if (scareFace != null) scareFace.gameObject.SetActive(false);
        Apply(false);
    }

    // =========================================================== shared actions

    public void EvButton()
    {
        int a = argAction, v = argValue;
        if (a == 1) Begin();
        else if (a == 2) ResetGame();
        else if (a == 3) Collect(v);
        else if (a == 4) Panel(v);
        else if (a == 5) Key(v / 100, v % 100);
        else if (a == 6) Toy(v % 100);
    }

    void Begin()
    {
        if (running && stage < 6)
        {
            // a game is on: this is "join in": go to the furthest room
            Teleport(stage);
            return;
        }
        Take();
        running = true;
        round = Random.Range(1, 60000);
        stage = 0;
        foundMask = 0;
        Commit();
    }

    void ResetGame()
    {
        Take();
        running = false;
        stage = 0;
        foundMask = 0;
        Commit();
    }

    void Collect(int item)
    {
        if (!running || item < 0 || item >= items.Length || Found(item)) return;
        if (itemRoom[item] > stage) return;
        Take();
        foundMask = foundMask + pow[item];
        Commit();
        Play(pickupClip);
    }

    void Panel(int room)
    {
        if (!running) { Play(wrongClip); return; }
        if (room != stage) return;
        int missing = Missing(room);
        if (missing > 0) { Play(wrongClip); return; }
        Solve(room);
    }

    void Key(int room, int key)
    {
        if (!running || room != stage) { Play(wrongClip); return; }
        string t = room == 1 ? typed1 : typed4;
        if (key <= 9)
        {
            if (t.Length < 4) t = t + key.ToString();
            Play(switchClip);
        }
        else if (key == 10) t = "";
        else if (key == 11)
        {
            if (t == Code(room)) { t = ""; Solve(room); }
            else { t = ""; Wrong(); }
        }
        if (room == 1) typed1 = t; else typed4 = t;
        UpdateDisplays();
    }

    void Toy(int toy)
    {
        if (!running || stage != 2) { Play(wrongClip); return; }
        if (toy == toyOrder[seqPos])
        {
            seqPos++;
            Play(switchClip);
            if (seqPos >= 4) { seqPos = 0; Solve(2); }
        }
        else { seqPos = 0; Wrong(); }
        UpdateDisplays();
    }

    void Solve(int room)
    {
        if (stage != room) return;
        Take();
        stage = room + 1;
        Commit();
    }

    /// <summary>A thrown toilet (or baby) hit the tree.</summary>
    public void EvStun()
    {
        if (!running) return;
        Take();
        stunCount++;
        Commit();
    }

    /// <summary>The tree caught this player (sent by this player's own client).</summary>
    public void EvCaught()
    {
        Take();
        caughtCount++;
        Commit();
    }

    void Take() { if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject); }
    void Commit() { synced = true; RequestSerialization(); Apply(true); }

    public override void OnDeserialization()
    {
        // the first state a player receives (on joining) is applied quietly: no teleports, no sounds
        // (a state that arrives long after this player loaded in is news, even if it's the first one)
        bool first = !synced && Time.time < loadedAt + 10f;
        synced = true;
        Apply(!first);
    }
    bool synced;
    float loadedAt;

    // =========================================================== applying the shared state on this client

    void Apply(bool announce)
    {
        if (pow == null) return;   // not started yet; Start applies
        bool newRound = running && round != lastRound;
        if (newRound)
        {
            MakeCodes();
            typed1 = ""; typed4 = ""; seqPos = 0;
            // pull everyone in when a game starts, but not a player who just joined into one already running
            if (announce) { Teleport(0); Play(startClip); }
            lastStage = stage;
            catchReady = Time.time + 6f;
        }
        else if (running && stage != lastStage)
        {
            if (announce && stage > lastStage) Play(stage >= 6 ? endingClip : unlockClip);
            lastStage = stage;
        }
        if (running) lastRound = round;
        if (!running) { lastRound = -1; lastStage = -1; }

        bool stun = announce && running && stunCount != lastStun;
        bool retreat = announce && caughtCount != lastCaught;
        lastStun = stunCount;
        lastCaught = caughtCount;
        if (stun) Play(stunClip);

        bool live = running && stage < 6;
        for (int i = 1; i < doors.Length; i++) if (doors[i] != null) doors[i].SetActive(live && stage < i);
        for (int i = 0; i < items.Length; i++)
        {
            bool f = Found(i);
            if (items[i] != null) items[i].SetActive(live && !f);
            if (itemDone != null && i < itemDone.Length && itemDone[i] != null) itemDone[i].SetActive(running && f);
        }
        for (int i = 0; i < huntOnly.Length; i++) if (huntOnly[i] != null) huntOnly[i].SetActive(running);
        for (int i = 0; i < freeOnly.Length; i++) if (freeOnly[i] != null) freeOnly[i].SetActive(!running);
        for (int i = 0; i < stageShow.Length; i++) if (stageShow[i] != null) stageShow[i].SetActive(running && stage >= stageShowAt[i]);
        SetDark(live);

        if (stalker != null)
        {
            stalker.SetProgramVariable("active", live);
            stalker.SetProgramVariable("stage", stage);
            stalker.SetProgramVariable("round", running ? round : 0);
            stalker.SetProgramVariable("won", running && stage >= 6);
            stalker.SendCustomEvent("Refresh");
            if (stun) stalker.SendCustomEvent("Stun");
            if (retreat) stalker.SendCustomEvent("Retreat");
        }
        UpdateDisplays();
        UpdateBoards();
    }

    void SetDark(bool dark)
    {
        if (darkSet && dark == darkNow) return;
        darkSet = true;
        darkNow = dark;
        for (int i = 0; i < darkMats.Length; i++)
        {
            Material m = darkMats[i];
            if (m == null) continue;
            m.SetFloat("_Fullbright", dark ? 0f : 1f);
            m.SetFloat("_Reach", dark ? 8f : 14f);
            m.SetFloat("_Ambient", dark ? 0.03f : 0.35f);
        }
    }

    bool Found(int i) { return pow != null && i >= 0 && i < 30 && (foundMask / pow[i]) % 2 == 1; }

    int Missing(int room)
    {
        int n = 0;
        for (int i = 0; i < items.Length; i++) if (itemRoom[i] == room && !Found(i)) n++;
        return n;
    }

    int Count(int room)
    {
        int n = 0;
        for (int i = 0; i < items.Length; i++) if (itemRoom[i] == room) n++;
        return n;
    }

    // ---- codes, the same on every client (worked out from the round number)

    int Rng(int s) { return (s * 75 + 74) % 65537; }

    void MakeCodes()
    {
        int s = Rng(round);
        for (int k = 0; k < 4; k++) { s = Rng(s); rainbowCode[k] = s % 10; }
        int[] pool = new int[5];
        for (int k = 0; k < 5; k++) pool[k] = k;
        s = Rng(round + 7);
        for (int k = 4; k > 0; k--)
        {
            s = Rng(s);
            int j = s % (k + 1);
            int tmp = pool[k]; pool[k] = pool[j]; pool[j] = tmp;
        }
        for (int k = 0; k < 4; k++) toyOrder[k] = pool[k];
        for (int k = 0; k < rainbowDigits.Length && k < 4; k++) if (rainbowDigits[k] != null) rainbowDigits[k].text = rainbowCode[k].ToString();
        for (int k = 0; k < toyNotes.Length && k < 4; k++)
            if (toyNotes[k] != null) toyNotes[k].text = "<b>#" + (k + 1) + "</b>\n" + toyNames[toyOrder[k]];
    }

    string Code(int room)
    {
        if (room == 4) return talentCode;
        string c = "";
        for (int k = 0; k < 4; k++) c = c + rainbowCode[k].ToString();
        return c;
    }

    /// <summary>The rainbow code, for tests and the systems check.</summary>
    public string RainbowCode() { return Code(1); }

    void UpdateDisplays()
    {
        if (rainbowDisplay != null) rainbowDisplay.text = Typed(typed1, stage > 1);
        if (talentDisplay != null) talentDisplay.text = Typed(typed4, stage > 4);
        if (toyDisplay != null)
        {
            string d = "";
            for (int k = 0; k < 4; k++) d = d + (stage > 2 || k < seqPos ? "<color=#70ff90>*</color> " : "<color=#666666>*</color> ");
            toyDisplay.text = d;
        }
    }

    string Typed(string t, bool solved)
    {
        if (!running) return "<color=#666666>- - - -</color>";
        if (solved) return "<color=#70ff90>OPEN</color>";
        string d = "";
        for (int k = 0; k < 4; k++) d = d + (k < t.Length ? t.Substring(k, 1) : "_") + (k < 3 ? " " : "");
        return d;
    }

    void UpdateBoards()
    {
        boardsAt = Time.time + 1f;
        for (int r = 0; r < roomBoards.Length; r++)
        {
            if (roomBoards[r] == null) continue;
            roomBoards[r].text = Board(r);
        }
        if (lobbyBoard != null)
        {
            if (!running) lobbyBoard.text = "<b>No game running.</b>\nPress <color=#ffd060>START</color> to begin. Everyone here is pulled in.";
            else if (stage >= 6) lobbyBoard.text = "<color=#70ff90><b>YOU ESCAPED!</b></color>\nThe tree went into the furnace. Press <color=#ffd060>RESET</color> to play again.";
            else lobbyBoard.text = "<b>Game on:</b> room " + (stage + 1) + " of 6\nCaught " + caughtCount + " times. Toilets thrown home: " + stunCount +
                                   "\nPress <color=#ffd060>START</color> to join in.";
        }
    }

    string Board(int r)
    {
        string title = r == 0 ? "THE POWER IS OUT" : r == 1 ? "LIGHTS OUT, RAINBOW" : r == 2 ? "THE TOY LOCK" : r == 3 ? "DECORATE THE TREE" : r == 4 ? "THE STAGE DOOR" : "THE FURNACE";
        string task = r == 0 ? "Find the 5 batteries hidden in the hall, then press the power box by the exit."
                    : r == 1 ? "The rainbow hid 4 glowing numbers in here. Type them on the exit keypad in rainbow order:\n<color=#ff3030>red</color>, <color=#ffe030>yellow</color>, <color=#40ff50>green</color>, <color=#4080ff>blue</color>."
                    : r == 2 ? "The elves left 4 numbered notes. Press the toys on the exit panel in their order. A wrong toy starts you over."
                    : r == 3 ? "Find the 4 ornaments, then hang them on the bare tree by the far wall."
                    : r == 4 ? "Read the stage manager's memo, then type the code on the keypad by the exit (east side)."
                    : "Open the 3 gas valves, then pull the furnace lever. Lure the tree close first...";
        if (!running) return "<b>" + title + "</b>\n<size=75%><color=#aaaaaa>" + task + "\n(Press START in the lobby to play.)</color></size>";
        if (stage > r) return "<b>" + title + "</b>\n<color=#70ff90>SOLVED: the way on is open.</color>";
        if (stage < r) return "<b>" + title + "</b>\n<color=#aaaaaa>Locked.</color>";
        int c = Count(r);
        string prog = c > 0 ? "\n<color=#ffd060>" + (c - Missing(r)) + " of " + c + " found</color>" : "";
        return "<b>" + title + "</b>\n<size=80%>" + task + "</size>" + prog;
    }

    void Teleport(int room)
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp) || checkpoints.Length == 0) return;
        int r = Mathf.Clamp(room, 0, checkpoints.Length - 1);
        if (checkpoints[r] == null) return;
        lp.TeleportTo(checkpoints[r].position, checkpoints[r].rotation);
    }

    void Play(AudioClip c) { if (sfx != null && c != null) sfx.PlayOneShot(c, 1f); }

    void Wrong()
    {
        Play(wrongClip);
        Scare(0.6f);
    }

    /// <summary>A tree face right in front of you, with a scream.</summary>
    void Scare(float seconds)
    {
        scareUntil = Time.time + seconds;
        if (scareFace != null) scareFace.gameObject.SetActive(true);
        Play(screamClip);
        PlaceFace();
    }

    void PlaceFace()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (scareFace == null || !Utilities.IsValid(lp)) return;
        VRCPlayerApi.TrackingData h = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        scareFace.position = h.position + h.rotation * new Vector3(0f, -0.05f, 0.45f);
        scareFace.rotation = h.rotation;
    }

    // =========================================================== this player's horror: catching, heartbeat, whispers

    void Update()
    {
        if (scareFace != null && scareFace.gameObject.activeSelf)
        {
            if (Time.time > scareUntil) scareFace.gameObject.SetActive(false);
            else PlaceFace();
        }
        if (teleportPending && Time.time >= teleportAt) { teleportPending = false; Teleport(stage); }
        if (Time.time > boardsAt && running) UpdateBoards();

        bool live = running && stage < 6;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!live || !Utilities.IsValid(lp))
        {
            if (heartbeat != null && heartbeat.isPlaying) heartbeat.Stop();
            return;
        }
        Vector3 me = lp.GetPosition();
        bool inMap = me.z < -6.2f;
        float d = 99f;
        if (stalkerBody != null && stalkerBody.gameObject.activeInHierarchy)
        {
            Vector3 dv = stalkerBody.position - me;
            dv.y = 0f;
            d = dv.magnitude;
        }
        // caught
        if (inMap && d < catchDistance && Time.time > catchReady && !teleportPending)
        {
            catchReady = Time.time + 5f;
            Scare(1.3f);
            teleportPending = true;
            teleportAt = Time.time + 1.2f;
            EvCaught();
        }
        // heartbeat
        if (heartbeat != null)
        {
            float vol = inMap ? Mathf.Clamp01(1f - (d - 1.5f) / 9f) : 0f;
            heartbeat.volume = vol;
            if (vol > 0.02f && !heartbeat.isPlaying) heartbeat.Play();
            if (vol <= 0.02f && heartbeat.isPlaying) heartbeat.Stop();
        }
        // whispers and rustles somewhere behind you
        if (inMap && Time.time > nextAmbient)
        {
            nextAmbient = Time.time + Random.Range(16f, 34f);
            if (ambient != null && ambientClips.Length > 0)
            {
                VRCPlayerApi.TrackingData h = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                ambient.transform.position = h.position + h.rotation * new Vector3(Random.Range(-2f, 2f), 0f, -3f);
                ambient.clip = ambientClips[Random.Range(0, ambientClips.Length)];
                ambient.Play();
            }
        }
    }
}
