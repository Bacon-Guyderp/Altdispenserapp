using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// A resident of the Marrow Arms apartments in Bonetown, living a daily routine on the Bonetown clock.
/// A Bonetown day is short (dayMs of real time), and comes from the server clock, so every player sees every
/// resident in the same place doing the same thing, with nothing synced.
/// The routine is a list of stops: at hour[i] the resident sets off for spot[i] (along a way worked out when the world
/// is built: through doorways, up the outside ramp, over the roof), and once there faces yaw[i] and does pose[i],
/// with doing[i] as the caption over its head. Press it and it stops, turns to you and says its line (captioned);
/// the press is shared (FirShare, kind 6). Afterwards it hurries back to where its routine says it should be.
/// Bones: 0 root (at the feet), 1 torso, 2 skull, 3/4 upper arms, 5/6 forearms, 7/8 legs, 9 jaw.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirResident : UdonSharpBehaviour
{
    public Transform[] pivots;
    public string who = "";
    public string home = "";
    public float walkSpeed = 1.4f;
    public int dayMs = 720000;              // one Bonetown day (12 minutes)
    public float[] hour;                    // when each stop starts (0-24, ascending)
    public Vector3[] spot;                  // where (world)
    public float[] yaw;                     // which way to face there
    public int[] pose;                      // what to do there (see Act)
    public string[] doing;                  // the caption
    public Vector3[] legPts;                // the way to each stop, worked out when the world is built (through doorways, up the ramp, ...)
    public int[] legStart, legLen;          // ...stop i's way is legPts[legStart[i]] .. legPts[legStart[i] + legLen[i] - 1]
    public AudioSource voice;
    public string line = "";
    public TextMeshPro caption;
    public Transform captionTransform;
    public TextMeshPro clockText;           // one resident also keeps the lobby clock

    Quaternion[] restRot;
    Vector3 restPos0;
    Quaternion restRot0;
    Vector3[] pts;
    int npts;
    bool inited, chatting, voicePending;
    float chatStart, chatLength, talkUntil, voiceAt, distance;
    int lastStop = -1, lastWalking = -1, lastMinute = -1;
    Quaternion facing;

    void Start() { Init(); }

    void Init()
    {
        if (inited) return;
        inited = true;
        restRot = new Quaternion[pivots.Length];
        for (int i = 0; i < pivots.Length; i++) restRot[i] = pivots[i].localRotation;
        restPos0 = pivots[0].localPosition;
        restRot0 = pivots[0].localRotation;
        int most = 2;
        for (int i = 0; i < legLen.Length; i++) if (legLen[i] > most) most = legLen[i];
        pts = new Vector3[most];
        facing = transform.rotation;
        Place(true);
    }

    /// <summary>The time of day, in hours (0-24).</summary>
    public float DayHour()
    {
        int ms = Networking.GetServerTimeInMilliseconds() % dayMs;
        if (ms < 0) ms += dayMs;
        return ms * 24f / dayMs;
    }

    /// <summary>The stop the routine is on at this hour.</summary>
    public int StopAt(float h)
    {
        int n = hour.Length;
        int s = n - 1;
        for (int i = 0; i < n; i++) if (hour[i] <= h) s = i;
        return s;
    }

    /// <summary>The way from the previous stop to stop s (fills pts, npts).</summary>
    void Path(int s)
    {
        int n = legLen[s];
        if (pts == null || pts.Length < n) pts = new Vector3[n];
        for (int i = 0; i < n; i++) pts[i] = legPts[legStart[s] + i];
        npts = n;
    }

    /// <summary>Length of the way to stop s, in metres (for the checks).</summary>
    public float PathLength(int s)
    {
        Path(s);
        float d = 0f;
        for (int i = 1; i < npts; i++) d += Vector3.Distance(pts[i - 1], pts[i]);
        return d;
    }

    /// <summary>Is it walking (1) or there (0) right now.</summary>
    public int Walking() { return lastWalking; }
    public int Stop() { return lastStop; }

    void Place(bool snap)
    {
        float h = DayHour();
        int s = StopAt(h);
        float since = h - hour[s];
        if (since < 0f) since += 24f;
        float walked = since * (dayMs / 1000f / 24f) * walkSpeed;
        Path(s);
        Vector3 at = pts[npts - 1];
        Vector3 dir = Vector3.zero;
        int walking = 0;
        for (int i = 1; i < npts; i++)
        {
            float seg = Vector3.Distance(pts[i - 1], pts[i]);
            if (walked < seg)
            {
                at = Vector3.Lerp(pts[i - 1], pts[i], seg > 0.001f ? walked / seg : 1f);
                dir = pts[i] - pts[i - 1];
                walking = 1;
                break;
            }
            walked -= seg;
        }
        Vector3 here = transform.position;
        float gap = Vector3.Distance(here, at);
        if (snap || gap > 8f) transform.position = at;
        else transform.position = Vector3.MoveTowards(here, at, Mathf.Max(walkSpeed * 1.5f, 4f) * Time.deltaTime);
        bool arrived = Vector3.Distance(transform.position, at) < 0.05f;
        if (walking == 1)
        {
            dir.y = 0f;
            if (dir.magnitude > 0.01f) facing = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        else if (!arrived)
        {
            Vector3 d2 = at - transform.position;
            d2.y = 0f;
            if (d2.magnitude > 0.01f) facing = Quaternion.LookRotation(d2.normalized, Vector3.up);
        }
        else facing = Quaternion.Euler(0, yaw[s], 0);
        if (snap) transform.rotation = facing;
        else transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 300f * Time.deltaTime);
        if (walking == 1 || !arrived)
        {
            distance += walkSpeed * Time.deltaTime;
            Rest();
            Walk(Mathf.Sin(distance * 4.6f), 0f);
        }
        else Act(pose[s], Time.time);
        int w = walking == 1 || !arrived ? 1 : 0;
        if (s != lastStop || w != lastWalking)
        {
            lastStop = s;
            lastWalking = w;
            ShowCaption(w == 1 ? "<color=#a0a0a0>on the way:</color> " + doing[s] : doing[s]);
        }
        if (clockText != null)
        {
            int minute = (int)(h * 60f);
            if (minute != lastMinute)
            {
                lastMinute = minute;
                int hh = minute / 60, mm = minute % 60;
                string ampm = hh < 12 ? "AM" : "PM";
                int h12 = hh % 12;
                if (h12 == 0) h12 = 12;
                clockText.text = "<size=60%>BONETOWN TIME</size>\n<b>" + h12 + ":" + (mm < 10 ? "0" : "") + mm + " " + ampm + "</b>";
            }
        }
    }

    void ShowCaption(string what)
    {
        if (caption == null) return;
        caption.text = "<b>" + who + "</b> <size=60%><color=#c0c0c0>" + home + "</color></size>\n<size=75%>" + what + "</size>";
    }

    public override void Interact()
    {
        if (chatting) return;
        Play();
        Share(6);
    }

    /// <summary>Stop, turn to the player, wave and talk. Also sent by FirShare for a press by someone else.</summary>
    public void Play()
    {
        Init();
        if (chatting) return;
        chatting = true;
        chatStart = Time.time;
        float len = voice != null && voice.clip != null ? voice.clip.length : 2f;
        chatLength = len + 1.4f;
        talkUntil = Time.time + 0.3f + len - 0.2f;
        voiceAt = Time.time + 0.3f;
        voicePending = true;
        if (line != "") ShowCaption("\"" + line + "\"");
    }

    public bool IsChatting() { return chatting; }

    void Update()
    {
        if (!inited) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (Utilities.IsValid(lp) && captionTransform != null)
        {
            Vector3 away = captionTransform.position - lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            away.y = 0f;
            if (away.magnitude > 0.01f) captionTransform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
        if (chatting)
        {
            float t = Time.time - chatStart;
            if (voicePending && Time.time >= voiceAt) { voicePending = false; if (voice != null) voice.Play(); }
            if (Utilities.IsValid(lp))
            {
                Vector3 d = lp.GetPosition() - transform.position;
                d.y = 0f;
                if (d.magnitude > 0.05f) facing = Quaternion.LookRotation(d.normalized, Vector3.up);
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 360f * Time.deltaTime);
            Rest();
            float wave = Ramp(t, 0.1f, 0.4f, chatLength - 0.5f, chatLength - 0.2f);
            R(4, -160f * wave, 0, 18f * wave);
            R(6, 0, 0, 25f * Mathf.Sin(t * 9f) * wave);
            float flap = Time.time < talkUntil && t > 0.3f ? Mathf.Abs(Mathf.Sin(t * 13f)) : 0f;
            R(9, 28f * flap, 0, 0);
            if (t >= chatLength)
            {
                chatting = false;
                lastStop = -1;     // put the routine caption back
            }
            return;
        }
        Place(false);
    }

    void R(int i, float x, float y, float z) { pivots[i].localRotation = restRot[i] * Quaternion.Euler(x, y, z); }

    void Rest()
    {
        pivots[0].localPosition = restPos0;
        pivots[0].localRotation = restRot0;
        for (int i = 1; i < pivots.Length; i++) pivots[i].localRotation = restRot[i];
    }

    void Walk(float s, float run)
    {
        float legAmp = Mathf.Lerp(26f, 48f, run), armAmp = Mathf.Lerp(22f, 50f, run);
        pivots[0].localPosition = restPos0 + new Vector3(0, Mathf.Abs(s) * 0.03f, 0);
        R(3, armAmp * s, 0, -8f);
        R(4, -armAmp * s, 0, 8f);
        R(5, -15f, 0, 0);
        R(6, -15f, 0, 0);
        R(7, -legAmp * s, 0, 0);
        R(8, legAmp * s, 0, 0);
    }

    void Sit()
    {
        pivots[0].localPosition = restPos0 + new Vector3(0, -0.45f, -0.12f);
        R(7, -90f, 0, 0);
        R(8, -90f, 0, 0);
    }

    void Talk(float t) { R(9, 22f * Mathf.Abs(Mathf.Sin(t * 11f)), 0, 0); }

    /// <summary>
    /// What it does at a stop: 0 stand about, 1 sleep, 2 sit, 3 cook, 4 read, 5 watch TV, 6 jumping jacks, 7 dance,
    /// 8 sweep, 9 on the phone, 10 water plants, 11 guitar, 12 fold laundry, 13 stretch, 14 type, 15 lift weights,
    /// 16 knock on a door, 17 check the mail, 18 fix something, 19 change a light bulb.
    /// </summary>
    void Act(int a, float t)
    {
        Rest();
        float s = Mathf.Sin(t * 2f);
        if (a == 0) { R(2, 0, 25f * Mathf.Sin(t * 0.6f), 0); R(3, 0, 0, -6f); R(4, 0, 0, 6f); }
        else if (a == 1)
        {
            pivots[0].localPosition = restPos0 + new Vector3(0, 0.6f, 0.85f);
            pivots[0].localRotation = restRot0 * Quaternion.Euler(-90f, 0, 0);
            R(1, 2f * Mathf.Sin(t * 1.3f), 0, 0);
            R(2, 0, 0, 12f);
            R(9, 10f + 8f * Mathf.Sin(t * 1.3f), 0, 0);
        }
        else if (a == 2) { Sit(); R(2, 0, 15f * Mathf.Sin(t * 0.4f), 0); R(5, -60f, 0, 0); R(6, -60f, 0, 0); }
        else if (a == 3) { R(2, 18f, 0, 0); R(4, -55f + 8f * s, 0, 10f * Mathf.Cos(t * 6f)); R(6, -40f, 20f * Mathf.Sin(t * 6f), 0); R(3, -30f, 0, 0); R(5, -60f, 0, 0); }
        else if (a == 4) { Sit(); R(2, 22f, 0, 0); R(3, -55f, 0, 10f); R(4, -55f, 0, -10f); R(5, -50f, 0, 0); R(6, -50f, 0, 0); }
        else if (a == 5)
        {
            Sit();
            R(3, 0, 0, -12f); R(4, 0, 0, 12f);
            float laugh = Mathf.Repeat(t, 7f) < 1.5f ? 1f : 0f;
            R(1, -8f * laugh * Mathf.Abs(Mathf.Sin(t * 12f)), 0, 0);
            R(9, 30f * laugh * Mathf.Abs(Mathf.Sin(t * 12f)), 0, 0);
        }
        else if (a == 6)
        {
            float k = Mathf.Abs(Mathf.Sin(t * 4f));
            pivots[0].localPosition = restPos0 + new Vector3(0, 0.12f * k, 0);
            R(3, 0, 0, -160f * k); R(4, 0, 0, 160f * k); R(7, 0, 0, -14f * k); R(8, 0, 0, 14f * k);
        }
        else if (a == 7)
        {
            pivots[0].localPosition = restPos0 + new Vector3(0.08f * Mathf.Sin(t * 4f), 0.04f * Mathf.Abs(Mathf.Cos(t * 4f)), 0);
            R(1, 0, 15f * Mathf.Sin(t * 4f), 8f * Mathf.Sin(t * 4f));
            R(2, 10f * Mathf.Abs(Mathf.Sin(t * 8f)), 0, 0);
            R(3, -150f * Mathf.Max(0f, Mathf.Sin(t * 2f)), 0, -20f); R(4, -150f * Mathf.Max(0f, -Mathf.Sin(t * 2f)), 0, 20f);
            R(7, -20f * Mathf.Max(0f, Mathf.Sin(t * 4f)), 0, 0); R(8, -20f * Mathf.Max(0f, -Mathf.Sin(t * 4f)), 0, 0);
        }
        else if (a == 8) { R(1, 22f, 28f * Mathf.Sin(t * 3f), 0); R(3, -50f, 0, 0); R(4, -50f, 0, 0); R(5, -20f, 0, 0); R(6, -20f, 0, 0); }
        else if (a == 9) { R(4, -30f, 0, 75f); R(6, -120f, 0, 0); R(3, -30f + 20f * Mathf.Sin(t * 1.7f), 0, -10f); R(2, 0, 20f * Mathf.Sin(t * 0.5f), 8f); Talk(t); }
        else if (a == 10) { R(1, 15f, 0, 0); R(4, -70f, 0, 0); R(6, -20f, 0, 25f + 15f * Mathf.Sin(t * 1.5f)); R(2, 20f, 0, 0); }
        else if (a == 11) { Sit(); R(3, -50f, 0, -20f); R(5, -70f, 0, 0); R(4, -30f, 0, 15f); R(6, -60f + 18f * Mathf.Sin(t * 12f), 0, 0); R(2, 8f * Mathf.Abs(Mathf.Sin(t * 3f)), 0, 0); }
        else if (a == 12) { R(2, 25f, 0, 0); R(3, -60f, 0, -20f * s); R(4, -60f, 0, 20f * s); R(5, -30f, 0, 0); R(6, -30f, 0, 0); }
        else if (a == 13) { R(3, -170f, 0, 0); R(4, -170f, 0, 0); R(1, 0, 0, 15f * Mathf.Sin(t * 0.8f)); }
        else if (a == 14) { Sit(); R(2, 12f, 0, 0); R(3, -70f, 0, 8f); R(4, -70f, 0, -8f); R(5, -30f + 6f * Mathf.Sin(t * 20f), 0, 0); R(6, -30f + 6f * Mathf.Cos(t * 23f), 0, 0); }
        else if (a == 15) { float c = Mathf.Abs(Mathf.Sin(t * 2f)); R(5, -125f * c, 0, 0); R(6, -125f * c, 0, 0); R(3, 0, 0, -10f); R(4, 0, 0, 10f); R(9, 15f * c, 0, 0); }
        else if (a == 16)
        {
            float on = Mathf.Repeat(t, 4f) < 2f ? 1f : 0f;
            R(4, -80f, 0, 0);
            R(6, -40f - 25f * on * Mathf.Max(0f, Mathf.Sin(t * 14f)), 0, 0);
            if (on < 0.5f) Talk(t);
        }
        else if (a == 17) { R(2, 20f, 0, 0); R(4, -80f, 0, 0); R(6, -20f, 0, 0); R(3, -30f, 0, 0); R(5, -60f, 0, 0); }
        else if (a == 18) { pivots[0].localPosition = restPos0 + new Vector3(0, -0.2f, 0); R(1, 40f, 0, 0); R(2, 15f, 0, 0); R(3, -70f, 0, 0); R(4, -70f, 0, 0); R(6, -30f * Mathf.Abs(Mathf.Sin(t * 8f)), 0, 0); R(7, -30f, 0, 0); R(8, -30f, 0, 0); }
        else if (a == 19) { pivots[0].localPosition = restPos0 + new Vector3(0, 0.05f + 0.03f * Mathf.Sin(t * 5f), 0); R(2, -30f, 0, 0); R(3, -170f, 0, 0); R(4, -170f, 0, 0); R(6, 0, 0, 30f * Mathf.Sin(t * 3f)); }
    }

    float Ramp(float t, float a, float b, float c, float d)
    {
        if (t <= a || t >= d) return 0f;
        if (t < b) return S((t - a) / (b - a));
        if (t > c) return 1f - S((t - c) / (d - c));
        return 1f;
    }

    float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    // ---- playing for everyone: FirShare sends the press to the other players
    public UdonBehaviour share;
    public int shareIndex;
    void Share(int kind)
    {
        if (share == null) return;
        share.SetProgramVariable("argKind", kind);
        share.SetProgramVariable("argIndex", shareIndex);
        share.SendCustomEvent("EvShare");
    }
}
