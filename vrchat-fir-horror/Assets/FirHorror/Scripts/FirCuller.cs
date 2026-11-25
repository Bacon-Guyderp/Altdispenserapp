using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Keeps only the Station area you're standing in awake. Each area's contents (its people, gags, props, rides and
/// its wings' contents) are switched off while nobody local is inside it: no scripts run there and nothing there is
/// drawn. Floors, walls and ceilings stay on, so arriving anywhere is always safe. Everything that moves runs off the
/// server clock, so an area that wakes up is already in the right place. Purely local: nothing is synced.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirCuller : UdonSharpBehaviour
{
    public GameObject[] parts;     // the switchable contents, grouped by area
    public int[] partArea;         // which area each part belongs to
    public float[] ax0, az0, ax1, az1;   // each area's footprint (with its wings), plus a margin
    public float interval = 0.2f;

    bool[] awake;
    float[] holdUntil;
    float next;
    bool inited;

    void Start() { Init(); }

    void Init()
    {
        if (inited) return;
        inited = true;
        awake = new bool[ax0.Length];
        holdUntil = new float[ax0.Length];
        for (int a = 0; a < awake.Length; a++) awake[a] = true;   // everything starts on (as saved); the first check puts the rest to sleep
    }

    void Update()
    {
        if (!inited) return;
        if (Time.time < next) return;
        next = Time.time + interval;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        Vector3 p = lp.GetPosition();
        for (int a = 0; a < awake.Length; a++)
        {
            bool inside = (p.x > ax0[a] && p.x < ax1[a] && p.z > az0[a] && p.z < az1[a]) || Time.time < holdUntil[a];
            if (inside != awake[a]) Set(a, inside);
        }
    }

    void Set(int a, bool on)
    {
        awake[a] = on;
        for (int i = 0; i < parts.Length; i++) if (partArea[i] == a && parts[i] != null) parts[i].SetActive(on);
    }

    /// <summary>A teleport is about to land here: wake whatever area this is now, and keep it awake for a moment.</summary>
    public void WakeAt(Vector3 p)
    {
        Init();
        for (int a = 0; a < awake.Length; a++)
            if (p.x > ax0[a] && p.x < ax1[a] && p.z > az0[a] && p.z < az1[a])
            {
                holdUntil[a] = Time.time + 1.5f;
                if (!awake[a]) Set(a, true);
            }
    }

    /// <summary>For the tests: is this area awake?</summary>
    public bool IsAwake(int a) { return awake != null && a >= 0 && a < awake.Length && awake[a]; }
}
