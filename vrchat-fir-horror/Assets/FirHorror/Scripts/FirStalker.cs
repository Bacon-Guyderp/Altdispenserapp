using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// The hunting tree. It only moves while nobody is looking at it: if any player in the map has it near the
/// middle of their view, with nothing in the way, it freezes. Otherwise it walks (through the rooms' doorways)
/// towards the nearest player, faster in later rooms. A thrown toilet stuns it for a few seconds.
///
/// Its owner (normally the instance master) moves it and sends where it is a few times a second (manual sync);
/// everyone else glides it there. FirHunt pushes the game state in (active, stage, round, won) and sends
/// Refresh, Stun and Retreat. Catching is checked by each player's own FirHunt.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FirStalker : UdonSharpBehaviour
{
    [UdonSynced] public Vector3 syncPos;
    [UdonSynced] public float syncYaw;

    // pushed in by FirHunt
    public bool active;
    public int stage;
    public int round;
    public bool won;

    // set up by the builder
    public GameObject body;            // the tree you see
    public Transform head;             // shakes when stunned
    public Transform mouth;
    public float[] roomZ0, roomZ1, doorX;
    public Transform[] lairs;          // where it waits in each room (the far end)
    public Transform furnace;          // where it ends up when you win
    public AudioSource voice;
    public AudioClip stunClip, wakeClip;
    public float baseSpeed = 1.3f, speedPerRoom = 0.18f, viewAngle = 40f, viewDistance = 20f;

    float stunUntil, waitUntil, nextThink, nextSend;
    int lastRound = -1, lastStage = -1;
    bool watched, hasTarget, inited;
    Vector3 goal, lastPos, mouthScale;
    float speed;
    VRCPlayerApi[] players;

    void Start() { Init(); Refresh(); }

    void Init()
    {
        if (inited) return;
        inited = true;
        players = new VRCPlayerApi[32];
        if (mouth != null) mouthScale = mouth.localScale;
        syncPos = transform.position;
        syncYaw = transform.eulerAngles.y;
        lastPos = transform.position;
    }

    /// <summary>The game state changed (sent on every client).</summary>
    public void Refresh()
    {
        Init();
        if (body != null) body.SetActive(active || won);
        bool owner = Networking.IsOwner(gameObject);
        if (won) { if (owner && furnace != null) Place(furnace.position, furnace.eulerAngles.y); }
        else if (active && round != lastRound) { if (owner) Place(Lair(0), 0f); waitUntil = Time.time + 20f; }
        else if (active && stage > lastStage) { if (owner) Place(Lair(stage), 0f); waitUntil = Time.time + 8f; if (voice != null && wakeClip != null) voice.PlayOneShot(wakeClip, 0.8f); }
        lastRound = active || won ? round : -1;
        lastStage = active || won ? stage : -1;
    }

    /// <summary>A toilet hit it.</summary>
    public void Stun()
    {
        stunUntil = Time.time + 6f;
        if (voice != null && stunClip != null) voice.PlayOneShot(stunClip, 1f);
    }

    /// <summary>It caught someone: it backs off to the far end of its room and waits a moment.</summary>
    public void Retreat()
    {
        waitUntil = Time.time + 4f;
        if (Networking.IsOwner(gameObject) && active) Place(Lair(RoomOf(transform.position.z)), 0f);
    }

    Vector3 Lair(int r)
    {
        if (lairs == null || lairs.Length == 0) return transform.position;
        r = Mathf.Clamp(r, 0, lairs.Length - 1);
        return lairs[r] != null ? lairs[r].position : transform.position;
    }

    void Place(Vector3 p, float yaw)
    {
        transform.position = p;
        transform.rotation = Quaternion.Euler(0, yaw, 0);
        Send();
    }

    void Send()
    {
        syncPos = transform.position;
        syncYaw = transform.eulerAngles.y;
        nextSend = Time.time + 0.25f;
        RequestSerialization();
    }

    public override void OnDeserialization()
    {
        // far away (placed in a new room): jump there; otherwise Update glides there
        if ((syncPos - transform.position).magnitude > 4f) { transform.position = syncPos; transform.rotation = Quaternion.Euler(0, syncYaw, 0); }
    }

    int RoomOf(float z)
    {
        for (int r = 0; r < roomZ0.Length; r++) if (z <= roomZ0[r] && z > roomZ1[r]) return r;
        return -1;
    }

    void Update()
    {
        Init();
        bool owner = Networking.IsOwner(gameObject);
        if (owner) Think();
        else
        {
            // glide to where the owner says it is
            transform.position = Vector3.MoveTowards(transform.position, syncPos, Time.deltaTime * 4f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, syncYaw, 0), Time.deltaTime * 360f);
        }
        // everyone: flap its mouth while it moves, shake it while stunned
        float moved = (transform.position - lastPos).magnitude;
        lastPos = transform.position;
        if (mouth != null)
        {
            float open = moved > 0.001f ? Mathf.Abs(Mathf.Sin(Time.time * 9f)) : 0f;
            mouth.localScale = new Vector3(mouthScale.x, mouthScale.y * (0.35f + 1.4f * open), mouthScale.z);
        }
        if (head != null)
            head.localRotation = Time.time < stunUntil ? Quaternion.Euler(Mathf.Sin(Time.time * 40f) * 6f, Mathf.Sin(Time.time * 23f) * 10f, 0f) : Quaternion.identity;
    }

    void Think()
    {
        if (!active || won) return;
        if (Time.time >= nextThink)
        {
            nextThink = Time.time + 0.1f;
            Vector3 eye = transform.position + Vector3.up * 1.3f;
            watched = false;
            hasTarget = false;
            float best = 9999f;
            Vector3 target = Vector3.zero;
            int n = VRCPlayerApi.GetPlayerCount();
            VRCPlayerApi.GetPlayers(players);
            for (int i = 0; i < n && i < players.Length; i++)
            {
                VRCPlayerApi p = players[i];
                if (!Utilities.IsValid(p)) continue;
                Vector3 pp = p.GetPosition();
                if (RoomOf(pp.z) < 0 || RoomOf(pp.z) > stage) continue;   // in the lobby (safe), or somewhere it can't go
                Vector3 flat = pp - transform.position;
                flat.y = 0f;
                if (flat.magnitude < best) { best = flat.magnitude; target = pp; hasTarget = true; }
                if (!watched)
                {
                    VRCPlayerApi.TrackingData h = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                    Vector3 to = eye - h.position;
                    float dist = to.magnitude;
                    if (dist < viewDistance && dist > 0.01f && Vector3.Angle(h.rotation * Vector3.forward, to) < viewAngle
                        && !Physics.Linecast(h.position, eye, 1, QueryTriggerInteraction.Ignore))
                        watched = true;
                }
            }
            if (hasTarget) goal = Route(transform.position, target);
            speed = (baseSpeed + speedPerRoom * stage) * (best > 20f ? 1.8f : 1f);
        }
        if (watched || !hasTarget || Time.time < stunUntil || Time.time < waitUntil) return;
        Vector3 d = goal - transform.position;
        d.y = 0f;
        if (d.magnitude > 0.05f)
        {
            transform.position = transform.position + d.normalized * Mathf.Min(d.magnitude, speed * Time.deltaTime);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), 240f * Time.deltaTime);
        }
        if (Time.time >= nextSend) Send();
    }

    /// <summary>Where to walk next to reach the target: straight at it in the same room, else through the doorway.</summary>
    Vector3 Route(Vector3 from, Vector3 target)
    {
        int rs = RoomOf(from.z), rp = RoomOf(target.z);
        if (rs < 0 || rp < 0 || rs == rp) return new Vector3(target.x, 0f, target.z);
        int k = rp < rs ? rs : rs + 1;           // the doorway: room k's north wall
        float dir = rp < rs ? 1f : -1f;          // +1 going north
        float dx = doorX[k], dz = roomZ0[k];
        if (Mathf.Abs(from.x - dx) < 0.4f) return new Vector3(dx, 0f, dz + dir * 1.5f);
        return new Vector3(dx, 0f, dz - dir * 1.0f);
    }
}
