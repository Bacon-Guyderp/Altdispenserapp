using UdonSharp;
using UnityEngine;
using UnityEngine.AI;
using VRC.SDKBase;

/// <summary>
/// The tree that only moves when nobody is looking at it. The owner (normally the instance
/// master) runs the NavMeshAgent and checks every player's view; everyone else smoothly follows
/// the synced position. Each client checks for itself whether the tree reached its player.
/// Active in the Stockroom, the Offices and (faster, and it creeps even when watched) the Recall Floor.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class FirStalker : UdonSharpBehaviour
{
    public FirGameManager game;
    public NavMeshAgent agent;
    public GameObject visual;
    public Transform eyes;
    public Transform[] spawnByPhase;      // index = phase; null = not active in that phase
    public Transform parking;
    public float unseenSpeed = 2.2f;
    public float finaleUnseenSpeed = 3.0f;
    public float finaleSeenSpeed = 0.35f;
    public float catchDistance = 1.1f;
    public int sightMask = 1;             // layers that block its view (Default)
    public AudioSource moveAudio;
    public AudioClip[] moveClips;
    public FirAnimatronic anim;

    [UdonSynced] Vector3 syncPos;
    [UdonSynced] float syncYaw;
    [UdonSynced] bool syncActive;

    VRCPlayerApi[] players;
    int lastPhase = -1, lastRound = -1;
    float nextThink, nextCatch, nextCreak, nextBeat;
    Vector3 lastPos;
    bool seen;
    float stunUntil;

    void Start()
    {
        players = new VRCPlayerApi[96];
        lastPos = transform.position;
        syncPos = transform.position;
        if (agent != null) agent.enabled = Networking.IsOwner(gameObject);
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (agent == null) return;
        agent.enabled = Networking.IsOwner(gameObject);
        if (agent.enabled) agent.Warp(transform.position);
    }

    public bool IsActive() { return syncActive; }

    /// <summary>Hit by a thrown toilet: it screams and stops for a few seconds.</summary>
    public void NetStun()
    {
        if (!syncActive) return;
        stunUntil = Time.time + 4f;
        if (anim != null) anim.SetMode(4, 1.2f);
    }

    bool ActiveIn(int p) { return p >= 0 && p < spawnByPhase.Length && spawnByPhase[p] != null; }

    void Update()
    {
        bool owner = Networking.IsOwner(gameObject);
        if (owner) OwnerThink();
        else
        {
            transform.position = Vector3.Lerp(transform.position, syncPos, Mathf.Min(1f, Time.deltaTime * 6f));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, syncYaw, 0), Mathf.Min(1f, Time.deltaTime * 6f));
        }
        if (visual != null && visual.activeSelf != syncActive) visual.SetActive(syncActive);
        if (!syncActive) return;

        // You hear it move. It only moves when you aren't looking.
        float moved = (transform.position - lastPos).magnitude;
        lastPos = transform.position;
        if (moved > 0.01f && Time.time > nextCreak && moveAudio != null && moveClips.Length > 0)
        {
            nextCreak = Time.time + Random.Range(0.5f, 1.1f);
            moveAudio.PlayOneShot(moveClips[Random.Range(0, moveClips.Length)]);
        }

        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        Vector3 me = lp.GetPosition();
        Vector3 flat = transform.position - me; flat.y = 0;
        float d = flat.magnitude;
        if (d < 7f && Time.time > nextBeat) { nextBeat = Time.time + 3.6f; game.PlaySfx(game.clipHeartbeat); }
        if (d < catchDistance && Mathf.Abs(transform.position.y - me.y) < 1.5f && Time.time > nextCatch)
        {
            nextCatch = Time.time + 6f;
            game.Caught();
        }
    }

    void OwnerThink()
    {
        int phase = game.phase;
        if (phase != lastPhase || game.round != lastRound)
        {
            lastPhase = phase; lastRound = game.round;
            Transform at = ActiveIn(phase) ? spawnByPhase[phase] : parking;
            if (agent != null && agent.enabled && at != null) { agent.Warp(at.position); transform.rotation = at.rotation; }
            else if (at != null) transform.SetPositionAndRotation(at.position, at.rotation);
            nextThink = Time.time + 6f;   // a moment's grace when a chapter starts
        }
        syncActive = ActiveIn(phase);
        syncPos = transform.position;
        syncYaw = transform.eulerAngles.y;
        if (!syncActive || agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        if (Time.time < nextThink) { agent.isStopped = true; return; }
        if (Time.time < stunUntil) { agent.isStopped = true; agent.velocity = Vector3.zero; return; }
        seen = AnyoneLooking();
        bool finale = phase == 6;
        float speed = seen ? (finale ? finaleSeenSpeed : 0f) : (finale ? finaleUnseenSpeed : unseenSpeed);
        agent.speed = speed;
        agent.isStopped = speed <= 0f;
        if (speed <= 0f) agent.velocity = Vector3.zero;

        VRCPlayerApi target = Nearest();
        if (target != null && Time.frameCount % 15 == 0) agent.SetDestination(target.GetPosition());
        if (anim != null && !seen && Time.frameCount % 120 == 0) anim.SetMode(1, 3f);
    }

    bool AnyoneLooking()
    {
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
            if (dist > 28f) continue;
            if (Vector3.Dot(h.rotation * Vector3.forward, to / dist) < 0.55f) continue;
            if (Physics.Linecast(h.position, e, sightMask, QueryTriggerInteraction.Ignore)) continue;
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
}
