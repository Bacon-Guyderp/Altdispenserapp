using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// A talking-tree animatronic, animated locally on every client: idle sway and twitches, turning
/// to stare at you when you get close, talking, singing and screaming (mouth flapping, eyes
/// tracking, head shaking). Cheap enough for dozens on Quest: it sleeps when you're far away.
/// Modes: 0 idle, 1 stare, 2 talk, 3 sing, 4 scream.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirAnimatronic : UdonSharpBehaviour
{
    public Transform head;            // yaw/pitch pivot (the whole upper tree)
    public Transform mouth;           // scaled on Y to open
    public Transform pupilL, pupilR;
    public AudioSource voice;         // 3D
    public AudioClip talkClip, singClip, screamClip;
    public int chapter;               // for group events
    public float watchRange = 3.5f;   // stares at you inside this range (0 = never)
    public float greetChance = 0.35f; // chance to say something when you walk up
    public float maxYaw = 80f;
    public float turnSpeed = 70f;

    int mode; float modeUntil;
    Quaternion headRest; Vector3 mouthRest, pupilRestL, pupilRestR;
    float seedT, nextTwitch, twitchYaw, nextGreet;
    bool near;
    VRCPlayerApi local;

    void Start()
    {
        local = Networking.LocalPlayer;
        if (head != null) headRest = head.localRotation;
        if (mouth != null) mouthRest = mouth.localScale;
        if (pupilL != null) pupilRestL = pupilL.localPosition;
        if (pupilR != null) pupilRestR = pupilR.localPosition;
        seedT = Random.Range(0f, 100f);
        nextTwitch = Time.time + Random.Range(4f, 15f);
    }

    public void SetMode(int m, float seconds)
    {
        mode = m;
        modeUntil = Time.time + seconds;
        if (voice == null) return;
        AudioClip c = m == 2 ? talkClip : m == 3 ? singClip : m == 4 ? screamClip : null;
        if (c != null) { voice.Stop(); voice.clip = c; voice.Play(); }
    }

    void Update()
    {
        if (!Utilities.IsValid(local) || head == null) return;
        float t = Time.time;
        Vector3 eye = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
        float dist = Vector3.Distance(eye, head.position);
        if (dist > 30f && mode == 0) return;

        if (mode != 0 && t > modeUntil) { mode = 0; if (voice != null && voice.isPlaying && !voice.loop) voice.Stop(); }
        bool nowNear = watchRange > 0 && dist < watchRange;
        if (nowNear && !near && t > nextGreet)
        {
            nextGreet = t + 25f;
            if (Random.value < greetChance && mode == 0) SetMode(2, 2.3f);
        }
        near = nowNear;
        bool watching = mode != 0 || near;

        // head: stare at you, or idle sway with the odd twitch
        Quaternion target;
        if (watching)
        {
            Vector3 to = head.parent != null ? head.parent.InverseTransformPoint(eye) - head.localPosition : eye - head.position;
            float yaw = Mathf.Clamp(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, -maxYaw, maxYaw);
            float pitch = Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -20f, 20f);
            target = Quaternion.Euler(pitch, yaw, 0) * headRest;
        }
        else
        {
            if (t > nextTwitch) { nextTwitch = t + Random.Range(5f, 18f); twitchYaw = Random.Range(-35f, 35f); }
            twitchYaw = Mathf.MoveTowards(twitchYaw, 0, Time.deltaTime * 4f);
            target = Quaternion.Euler(Mathf.Sin((t + seedT) * 0.7f) * 2f, Mathf.Sin((t + seedT) * 0.4f) * 6f + twitchYaw, Mathf.Sin((t + seedT) * 0.9f) * 2f) * headRest;
        }
        float speed = mode == 4 ? 600f : turnSpeed;
        head.localRotation = Quaternion.RotateTowards(head.localRotation, target, speed * Time.deltaTime);
        if (mode == 4) head.localRotation *= Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(-8f, 8f), Random.Range(-6f, 6f));

        // mouth
        float open = 0f;
        if (mode == 2) open = Mathf.Abs(Mathf.Sin(t * 13f)) * (0.6f + 0.4f * Mathf.Sin(t * 3.1f));
        else if (mode == 3) open = 0.45f + 0.45f * Mathf.Sin(t * 5.5f);
        else if (mode == 4) open = 1.6f + Random.Range(0f, 0.4f);
        if (mouth != null) mouth.localScale = new Vector3(mouthRest.x * (1f + open * 0.15f), mouthRest.y * (0.35f + open * 1.6f), mouthRest.z);

        // pupils drift toward you
        if (pupilL != null && pupilR != null)
        {
            Vector3 local3 = head.InverseTransformPoint(eye).normalized * (watching ? 0.012f : 0f);
            local3.z = 0;
            pupilL.localPosition = pupilRestL + local3;
            pupilR.localPosition = pupilRestR + local3;
        }
    }
}
