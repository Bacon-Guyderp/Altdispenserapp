using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// A fairground ride. Everything it does comes from the server clock, so every player sees it in the same
/// place with no syncing, and anyone sitting in one of its seats (a VRCStation, see FirSeat) is carried along.
/// Kinds: 0 spin (a rotor turning about an axis: the big wheel, the carousel, the teacups' platform),
///        1 path (carts going round a loop of points: the ghost train),
///        2 drop (a seat ring that climbs a tower, waits, drops and rests).
/// Extras: upright transforms stay level (the big wheel's gondolas), bobbers go up and down (carousel horses),
/// spinners turn on their own (the teacups).
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirRide : UdonSharpBehaviour
{
    public int kind;
    public Transform rotor;
    public Vector3 axis = new Vector3(0, 1, 0);
    public float degPerSec = 20f;
    public Transform[] upright;
    public Transform[] bobbers;
    public float bobHeight = 0.3f, bobSpeed = 2f;
    public Transform[] spinners;
    public float spinnerDegPerSec = 90f;
    // path
    public Transform[] carts;
    public Vector3[] route;
    public float[] pace;
    public float speed = 2f;
    // drop
    public float dropHeight = 12f, upTime = 9f, holdTime = 2.5f, dropTime = 1.4f, restTime = 8f;
    public AudioSource sfx;            // the drop's scream / the whistle, played as it happens
    public int phaseMs;

    float[] segStart, segTime;
    float period;
    Vector3 rotor0;
    Quaternion rot0;
    Quaternion[] up0, spin0;
    Vector3[] bob0;
    bool inited, dropped;

    void Start() { Init(); }

    void Init()
    {
        if (inited) return;
        inited = true;
        if (rotor != null) { rotor0 = rotor.localPosition; rot0 = rotor.localRotation; }
        up0 = new Quaternion[upright.Length];
        for (int i = 0; i < upright.Length; i++) up0[i] = upright[i].rotation;
        spin0 = new Quaternion[spinners.Length];
        for (int i = 0; i < spinners.Length; i++) spin0[i] = spinners[i].localRotation;
        bob0 = new Vector3[bobbers.Length];
        for (int i = 0; i < bobbers.Length; i++) bob0[i] = bobbers[i].localPosition;
        if (kind == 1)
        {
            int n = route.Length;
            segStart = new float[n];
            segTime = new float[n];
            float t = 0f;
            for (int i = 0; i < n; i++)
            {
                float p = i < pace.Length && pace[i] > 0.05f ? pace[i] : 1f;
                segStart[i] = t;
                segTime[i] = Vector3.Distance(route[i], route[(i + 1) % n]) / (speed * p);
                t += segTime[i];
            }
            period = Mathf.Max(1f, t);
        }
        else if (kind == 2) period = upTime + holdTime + dropTime + restTime;
    }

    float Clock()
    {
        int ms = Networking.GetServerTimeInMilliseconds() + phaseMs;
        int m = ms % 3600000;              // an hour's worth of milliseconds keeps the floats precise
        if (m < 0) m += 3600000;
        return m / 1000f;
    }

    void Update()
    {
        if (!inited) return;
        float t = Clock();
        if (kind == 0 && rotor != null)
        {
            rotor.localRotation = rot0 * Quaternion.AngleAxis(Mathf.Repeat(t * degPerSec, 360f), axis);
        }
        else if (kind == 1)
        {
            for (int c = 0; c < carts.Length; c++)
            {
                float ct = Mathf.Repeat(t + c * period / carts.Length, period);
                int n = route.Length, seg = n - 1;
                for (int i = 0; i < n; i++) if (ct < segStart[i] + segTime[i]) { seg = i; break; }
                float k = segTime[seg] > 0.001f ? Mathf.Clamp01((ct - segStart[seg]) / segTime[seg]) : 1f;
                Vector3 a = route[seg], b = route[(seg + 1) % n];
                carts[c].position = Vector3.Lerp(a, b, k);
                Vector3 d = b - a;
                d.y = 0f;
                if (d.magnitude > 0.01f) carts[c].rotation = Quaternion.RotateTowards(carts[c].rotation, Quaternion.LookRotation(d.normalized, Vector3.up), 200f * Time.deltaTime);
            }
        }
        else if (kind == 2 && rotor != null)
        {
            float p = Mathf.Repeat(t, period), y;
            if (p < upTime) y = dropHeight * S(p / upTime);
            else if (p < upTime + holdTime) y = dropHeight;
            else if (p < upTime + holdTime + dropTime) { float q = (p - upTime - holdTime) / dropTime; y = dropHeight * (1f - q * q); }
            else y = 0f;
            bool falling = p >= upTime + holdTime && p < upTime + holdTime + dropTime;
            if (falling && !dropped && sfx != null) sfx.Play();
            dropped = falling;
            rotor.localPosition = rotor0 + new Vector3(0, y, 0);
        }
        for (int i = 0; i < upright.Length; i++) upright[i].rotation = up0[i];
        for (int i = 0; i < bobbers.Length; i++) bobbers[i].localPosition = bob0[i] + new Vector3(0, bobHeight * Mathf.Sin(t * bobSpeed + i * 1.3f), 0);
        for (int i = 0; i < spinners.Length; i++) spinners[i].localRotation = spin0[i] * Quaternion.AngleAxis(Mathf.Repeat(t * spinnerDegPerSec * (i % 2 == 0 ? 1f : -1.3f), 360f), Vector3.up);
    }

    float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
}
