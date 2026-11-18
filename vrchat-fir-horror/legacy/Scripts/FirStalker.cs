using UdonSharp;
using UnityEngine;
using VRC.Udon;
using VRC.SDKBase;

/// <summary>
/// The tree that only moves when nobody is looking at it. No NavMesh: the owner (normally the
/// instance master) walks it straight at the nearest player, and when a shelf or wall is in the
/// way it heads for whichever of the room's waypoints gets it round. Its position is shared with
/// manual sync five times a second; everyone else glides after it. Each client checks for itself
/// whether the tree reached its own player.
/// Active in the Stockroom, the Offices and (faster, and it creeps even when watched) the Recall Floor.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FirStalker : UdonSharpBehaviour
{
    public UdonBehaviour game;          // FirGameManager
    public GameObject visual;
    public Transform eyes;
    public Transform[] spawnByPhase;      // index = phase; null = not active in that phase
    public Transform parking;
    public Transform[] waypoints;         // every room's waypoints
    public int[] waypointPhase;           // which phase each waypoint belongs to
    public float[] roomMinX, roomMaxX, roomMinZ, roomMaxZ;   // by phase: it never leaves the room
    public float unseenSpeed = 2.2f;
    public float finaleUnseenSpeed = 3.0f;
    public float finaleSeenSpeed = 0.35f;
    public float catchDistance = 1.1f;
    public int sightMask = 1;             // layers that block sight and movement (Default)
    public AudioSource moveAudio;
    public AudioClip[] moveClips;
    public UdonBehaviour anim;

    [UdonSynced] public Vector3 syncPos;
    [UdonSynced] public float syncYaw;
    [UdonSynced] public bool syncActive;

    VRCPlayerApi[] players;
    bool inited;
    int lastPhase = -1;
    int lastRound = -1;
    float nextThink, nextCatch, nextCreak, nextBeat, nextWhisper, nextSend, nextLook, nextPath, nextGrumble;
    float stunUntil;
    bool seen;
    Vector3 lastPos;
    Vector3 goal;
    VRCPlayerApi chasing;

    void EnsureInit()
    {
        if (inited) return;
        inited = true;
        players = new VRCPlayerApi[100];
        lastPos = transform.position;
        syncPos = transform.position;
        syncYaw = transform.eulerAngles.y;
        goal = transform.position;
    }

    void Start() { EnsureInit(); }

    public override void OnDeserialization() { EnsureInit(); }

    public bool IsActive() { return syncActive; }

    /// <summary>Hit by a thrown toilet (the game manager calls this on everyone): it screams and stops.</summary>
    public void Stun()
    {
        EnsureInit();
        if (!syncActive) return;
        stunUntil = Time.time + 4f;
        AnimMode(anim, 4, 1.2f);
    }

    bool ActiveIn(int p) { return p >= 0 && p < spawnByPhase.Length && spawnByPhase[p] != null; }

    void Update()
    {
        EnsureInit();
        if (game == null) return;
        bool owner = Networking.IsOwner(gameObject);
        if (owner) OwnerThink();
        else
        {
            // a big jump (new chapter) snaps instead of gliding through walls
            if (Vector3.Distance(syncPos, transform.position) > 5f) transform.position = syncPos;
            else transform.position = Vector3.Lerp(transform.position, syncPos, Mathf.Min(1f, Time.deltaTime * 6f));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, syncYaw, 0f), Mathf.Min(1f, Time.deltaTime * 6f));
        }
        if (visual != null && visual.activeSelf != syncActive) visual.SetActive(syncActive);
        if (!syncActive) return;

        // You hear it move. It only moves when you aren't looking.
        float moved = Vector3.Distance(transform.position, lastPos);
        lastPos = transform.position;
        if (moved > 0.01f && Time.time > nextCreak && moveAudio != null && moveClips.Length > 0)
        {
            nextCreak = Time.time + Random.Range(0.5f, 1.1f);
            moveAudio.PlayOneShot(moveClips[Random.Range(0, moveClips.Length)]);
        }

        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        Vector3 me = lp.GetPosition();
        Vector3 flat = transform.position - me;
        flat.y = 0f;
        float d = flat.magnitude;
        if (d < 7f && Time.time > nextBeat) { nextBeat = Time.time + 3.6f; GameEv("EvSfxHeartbeat"); }
        if (d < 4.5f && Time.time > nextWhisper && anim != null)
        {
            nextWhisper = Time.time + Random.Range(7f, 12f);
            AnimMode(anim, 2, 2f);   // it talks to you when it's close
        }
        float reach = GameBool("smallGroup") ? catchDistance * 0.85f : catchDistance;
        if (Time.time > stunUntil && d < reach && Mathf.Abs(transform.position.y - me.y) < 1.5f && Time.time > nextCatch)
        {
            nextCatch = Time.time + 6f;
            GameEv("Caught");
        }
    }

    void OwnerThink()
    {
        int phase = GameInt("phase");
        int round = GameInt("round");
        if (phase != lastPhase || round != lastRound)
        {
            lastPhase = phase;
            lastRound = round;
            Transform at = ActiveIn(phase) ? spawnByPhase[phase] : parking;
            if (at != null) { transform.position = at.position; transform.rotation = at.rotation; }
            goal = transform.position;
            nextThink = Time.time + 6f;   // a moment's grace when a chapter starts
            syncActive = ActiveIn(phase);
            Send(true);
        }
        syncActive = ActiveIn(phase);
        if (syncActive && Time.time >= nextThink && Time.time >= stunUntil) Move(phase);
        Send(false);
    }

    void Send(bool now)
    {
        if (!now && Time.time < nextSend) return;
        nextSend = Time.time + 0.2f;
        syncPos = transform.position;
        syncYaw = transform.eulerAngles.y;
        RequestSerialization();
    }

    void Move(int phase)
    {
        if (Time.time > nextLook) { nextLook = Time.time + 0.15f; seen = AnyoneLooking(); }
        bool finale = phase == 6;
        // ease off for small groups: one player can't watch it and search at the same time
        int group = GameInt("playersInGame");
        float scale = group <= 1 ? 0.55f : (group == 2 ? 0.75f : 1f);
        if (Time.time < GameFloat("lightsOutUntil") && group <= 2) scale = scale * 0.6f;
        float seenFinale = group <= 1 ? 0f : (group == 2 ? 0.18f : finaleSeenSpeed);
        float speed = seen ? (finale ? seenFinale : 0f) : (finale ? finaleUnseenSpeed : unseenSpeed) * scale;
        if (speed <= 0f) return;

        if (Time.time > nextPath) { nextPath = Time.time + 0.5f; PickGoal(phase); }
        Vector3 pos = transform.position;
        Vector3 to = goal - pos;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist < 0.05f) return;
        Vector3 dir = to / dist;
        Vector3 next = pos + dir * Mathf.Min(dist, speed * Time.deltaTime);
        if (phase < roomMinX.Length)
        {
            next.x = Mathf.Clamp(next.x, roomMinX[phase], roomMaxX[phase]);
            next.z = Mathf.Clamp(next.z, roomMinZ[phase], roomMaxZ[phase]);
        }
        transform.position = next;
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        if (anim != null && !seen && Time.time > nextGrumble) { nextGrumble = Time.time + 4f; AnimMode(anim, 1, 3f); }
    }

    /// <summary>Straight at the nearest player if nothing is in the way, else via the best waypoint it can see.</summary>
    void PickGoal(int phase)
    {
        chasing = Nearest();
        if (chasing == null) { goal = transform.position; return; }
        Vector3 target = chasing.GetPosition();
        target.y = transform.position.y;
        if (Clear(transform.position, target)) { goal = target; return; }
        float best = 9999f;
        Vector3 pick = transform.position;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null || i >= waypointPhase.Length || waypointPhase[i] != phase) continue;
            Vector3 w = waypoints[i].position;
            w.y = transform.position.y;
            float here = Vector3.Distance(transform.position, w);
            if (here < 0.4f) continue;
            if (!Clear(transform.position, w)) continue;
            float score = here + Vector3.Distance(w, target) * 1.3f;
            if (!Clear(w, target)) score = score + 6f;
            if (score < best) { best = score; pick = w; }
        }
        goal = pick;
    }

    bool Clear(Vector3 a, Vector3 b)
    {
        Vector3 o = a + Vector3.up * 0.6f;
        Vector3 d = b - a;
        d.y = 0f;
        float len = d.magnitude;
        if (len < 0.01f) return true;
        RaycastHit hit;
        return !Physics.Raycast(o, d / len, out hit, len, sightMask, QueryTriggerInteraction.Ignore);
    }

    bool AnyoneLooking()
    {
        if (Time.time < GameFloat("lightsOutUntil")) return false;   // nobody can see it in the dark
        int n = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi.GetPlayers(players);
        Vector3 e = eyes != null ? eyes.position : transform.position + Vector3.up;
        for (int i = 0; i < n && i < players.Length; i++)
        {
            VRCPlayerApi p = players[i];
            if (!Utilities.IsValid(p)) continue;
            VRCPlayerApi.TrackingData h = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 to = e - h.position;
            float dist = to.magnitude;
            if (dist > 28f || dist < 0.01f) continue;
            Vector3 dir = to / dist;
            if (Vector3.Dot(h.rotation * Vector3.forward, dir) < 0.55f) continue;
            RaycastHit hit;
            if (Physics.Raycast(h.position, dir, out hit, dist - 0.3f, sightMask, QueryTriggerInteraction.Ignore)) continue;
            return true;
        }
        return false;
    }

    VRCPlayerApi Nearest()
    {
        int n = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi.GetPlayers(players);
        VRCPlayerApi best = null;
        float bestD = 40f;
        for (int i = 0; i < n && i < players.Length; i++)
        {
            VRCPlayerApi p = players[i];
            if (!Utilities.IsValid(p)) continue;
            float d = Vector3.Distance(p.GetPosition(), transform.position);
            if (d < bestD) { bestD = d; best = p; }
        }
        return best;
    }

    // ---- calls to other scripts: plain Udon events (see FirGameManager's "public API")
    void GameEv(string ev) { if (game != null) game.SendCustomEvent(ev); }
    void GameEvA(string ev, int a) { if (game == null) return; game.SetProgramVariable("argA", a); game.SendCustomEvent(ev); }
    void GameEvF(string ev, float f) { if (game == null) return; game.SetProgramVariable("argF", f); game.SendCustomEvent(ev); }
    void GameEvAF(string ev, int a, float f) { if (game == null) return; game.SetProgramVariable("argA", a); game.SetProgramVariable("argF", f); game.SendCustomEvent(ev); }
    void GameEvABF(string ev, int a, int b, float f) { if (game == null) return; game.SetProgramVariable("argA", a); game.SetProgramVariable("argB", b); game.SetProgramVariable("argF", f); game.SendCustomEvent(ev); }
    void GameSay(string text, float seconds) { if (game == null) return; game.SetProgramVariable("argText", text); game.SetProgramVariable("argF", seconds); game.SendCustomEvent("EvSay"); }
    int GameInt(string name) { return game != null ? (int)game.GetProgramVariable(name) : 0; }
    float GameFloat(string name) { return game != null ? (float)game.GetProgramVariable(name) : 0f; }
    bool GameBool(string name) { return game != null && (bool)game.GetProgramVariable(name); }
    void AnimMode(UdonBehaviour a, int mode, float seconds)
    {
        if (a == null) return;
        a.SetProgramVariable("argMode", mode);
        a.SetProgramVariable("argSeconds", seconds);
        a.SendCustomEvent("EvSetMode");
    }
}
