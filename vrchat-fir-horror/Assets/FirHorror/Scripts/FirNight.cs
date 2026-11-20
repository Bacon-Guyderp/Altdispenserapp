using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// The night shift at Pine Pete's Pizza Palace. START in the security office begins a night: from 12 AM to
/// 6 AM (each hour is hourSeconds long). Three animatronic tree mascots creep, room by room, from the stage to
/// the office's doors: Pine Pete down the west side, Holly down the east, Sprucey either way. When one reaches
/// a door it waits a moment; if that door is shut by then, it gives up and goes back to the stage, otherwise it
/// gets you. Doors and lights use power; when the power runs out, the doors open, the lights die, and Pete comes.
///
/// Networking: the night is a handful of manually synced values. The object's owner runs the animatronics and
/// the power; pressing a button takes ownership (so whoever is playing runs it) and sends the new state.
/// Each player's camera monitor is their own (not synced).
/// Positions: 0 stage, 1 dining room, 2 side room (parts / kitchen), 3 hall, 4 at the door.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FirNight : UdonSharpBehaviour
{
    [UdonSynced] public bool running;
    [UdonSynced] public int nightId;
    [UdonSynced] public int startMs;       // server time the night began
    [UdonSynced] public int power = 1000;  // tenths of a percent
    [UdonSynced] public bool doorL, doorR, lightL, lightR;
    [UdonSynced] public int posPete, posHolly, posSprucey;
    [UdonSynced] public int sideSprucey;   // 0 west, 1 east
    [UdonSynced] public int outcome;       // 0 playing, 1-3 caught by Pete / Holly / Sprucey, 9 survived

    // ---- set up by the builder
    public float hourSeconds = 45f;
    public float tickSeconds = 4.5f;
    public int[] petePerHour, hollyPerHour, sprucePerHour;   // chance (in 20) to move each tick, by hour
    public Transform[] peteSpots, hollySpots, spruceSpotsW, spruceSpotsE;   // positions 0-4 in the building
    public Transform pete, holly, sprucey;                   // the animatronics
    public Transform doorPanelL, doorPanelR;                  // slide down to close
    public GameObject shadowL, shadowR;                       // the dark in each doorway (hidden while its light is on)
    public GameObject lampL, lampR;
    public TextMeshPro clockText, powerText, monitorText, statusText, captionText;
    public string[] camNames;                                 // 1-6
    public AudioSource sfx;
    public AudioClip doorClip, lightClip, chimeClip, screamClip, stepClip, powerOutClip, knockClip, startClip;
    public AudioSource phone;                                 // the voicemail from the last night guard
    public string[] phoneCaptions;
    public float[] phoneCaptionAt;
    public Transform[] scareFaces;                            // one big face per animatronic (1-3)
    public Vector3 officeMin, officeMax;                      // the office (who sees the scare)

    // ---- arguments (FirHuntButton sets these, then sends EvButton)
    public int argAction;
    public int argValue;

    // ---- this player only
    int cam = 1, lastNight = -1, lastOutcome, lastPete, lastHolly, lastSprucey;
    float nextTick, nextDrain, nextSend, scareUntil, phoneStart = -1f;
    bool synced, applied, dirty;
    float loadedAt;
    Vector3 doorL0, doorR0;
    int scareWho;

    void Start()
    {
        loadedAt = Time.time;
        if (doorPanelL != null) doorL0 = doorPanelL.localPosition;
        if (doorPanelR != null) doorR0 = doorPanelR.localPosition;
        for (int i = 0; i < scareFaces.Length; i++) if (scareFaces[i] != null) scareFaces[i].gameObject.SetActive(false);
        Apply(false);
    }

    int Now() { return Networking.GetServerTimeInMilliseconds(); }

    /// <summary>Hours since 12 AM (0-6), from the shared clock.</summary>
    public int Hour()
    {
        if (!running) return 0;
        int el = Now() - startMs;
        if (el < 0) el = 0;
        return Mathf.Min(6, (int)(el / (hourSeconds * 1000f)));
    }

    // =========================================================== buttons

    public void EvButton()
    {
        int a = argAction, v = argValue;
        if (a == 1) Begin();
        else if (a == 2) { Take(); running = false; outcome = 0; ResetPositions(); Commit(); }
        else if (a == 7) Door(v);
        else if (a == 8) Light(v);
        else if (a == 9) { cam = Mathf.Clamp(v, 1, 6); UpdateMonitor(); Play(lightClip, 0.4f); }
        else if (a == 10) PlayPhone();
    }

    void Begin()
    {
        if (running && outcome == 0) return;
        Take();
        running = true;
        nightId = Random.Range(1, 60000);
        startMs = Now();
        power = 1000;
        doorL = false; doorR = false; lightL = false; lightR = false;
        outcome = 0;
        ResetPositions();
        Commit();
    }

    void ResetPositions() { posPete = 0; posHolly = 0; posSprucey = 0; sideSprucey = Random.Range(0, 2); }

    void Door(int side)
    {
        if (!running || outcome != 0 || power <= 0) { Play(knockClip, 0.6f); return; }
        Take();
        if (side == 0) doorL = !doorL; else doorR = !doorR;
        Commit();
    }

    void Light(int side)
    {
        if (!running || outcome != 0 || power <= 0) { Play(knockClip, 0.6f); return; }
        Take();
        if (side == 0) lightL = !lightL; else lightR = !lightR;
        Commit();
    }

    void Take() { if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject); }
    void Commit() { synced = true; dirty = false; nextSend = Time.time + 1f; RequestSerialization(); Apply(true); }

    public override void OnDeserialization()
    {
        bool first = !synced && Time.time < loadedAt + 10f;
        synced = true;
        Apply(!first);
    }

    // =========================================================== the night (run by the owner)

    void Update()
    {
        if (running && outcome == 0 && Networking.IsOwner(gameObject)) Run();
        // send what changed (at most once a second), including the very last change of a night
        if (dirty && Networking.IsOwner(gameObject) && Time.time >= nextSend) { dirty = false; nextSend = Time.time + 1f; RequestSerialization(); }
        UpdateLocal();
    }

    void Run()
    {
        bool changed = false;
        // the clock: 6 AM and you've made it
        if (Hour() >= 6) { outcome = 9; changed = true; }
        // the power
        if (outcome == 0 && Time.time >= nextDrain)
        {
            nextDrain = Time.time + 1f;
            int use = 1 + (doorL ? 2 : 0) + (doorR ? 2 : 0) + (lightL ? 1 : 0) + (lightR ? 1 : 0);
            power = Mathf.Max(0, power - use);
            if (power == 0 && (doorL || doorR || lightL || lightR)) { doorL = false; doorR = false; lightL = false; lightR = false; }
            changed = true;
        }
        // the animatronics
        if (outcome == 0 && Time.time >= nextTick)
        {
            nextTick = Time.time + tickSeconds;
            int h = Hour();
            if (power <= 0)
            {
                // the power's gone: Pete walks straight in through the west door
                posPete = Mathf.Min(4, posPete + 1);
                if (posPete >= 4 && Random.Range(0, 3) == 0) outcome = 1;
            }
            else
            {
                posPete = Step(posPete, Level(petePerHour, h), doorL, 1);
                if (outcome == 0) posHolly = Step(posHolly, Level(hollyPerHour, h), doorR, 2);
                if (outcome == 0)
                {
                    int before = posSprucey;
                    posSprucey = Step(posSprucey, Level(sprucePerHour, h), sideSprucey == 0 ? doorL : doorR, 3);
                    if (posSprucey == 0 && before > 0) sideSprucey = Random.Range(0, 2);   // back on stage: picks a side again
                }
            }
            changed = true;
        }
        if (changed) { dirty = true; Apply(true); }
    }

    int Level(int[] perHour, int h) { return perHour.Length == 0 ? 0 : perHour[Mathf.Clamp(h, 0, perHour.Length - 1)]; }

    /// <summary>One tick for one animatronic: maybe move on; at the door, a shut door sends it back, an open one is the end.</summary>
    int Step(int pos, int level, bool doorShut, int who)
    {
        if (pos >= 4)
        {
            if (doorShut) return 0;                       // it gives up and goes back to the stage
            if (Random.Range(0, 20) < level + 6) { outcome = who; return 4; }
            return 4;
        }
        if (Random.Range(0, 20) < level) return pos + 1;
        return pos;
    }

    // =========================================================== how it looks (every player)

    void Apply(bool announce)
    {
        bool newNight = running && nightId != lastNight;
        if (newNight) { lastNight = nightId; lastOutcome = 0; if (announce) Play(startClip, 1f); }
        if (!running) lastNight = -1;
        if (announce && outcome != lastOutcome)
        {
            if (outcome >= 1 && outcome <= 3) Scare(outcome);
            else if (outcome == 9) Play(chimeClip, 1f);
        }
        if (announce && running)
        {
            if (posPete != lastPete || posHolly != lastHolly || posSprucey != lastSprucey) Play(stepClip, 0.5f);
            if ((posPete == 4 && lastPete != 4) || (posHolly == 4 && lastHolly != 4) || (posSprucey == 4 && lastSprucey != 4)) Play(knockClip, 0.8f);
        }
        if (announce && running && power == 0 && applied && lastPower > 0) Play(powerOutClip, 1f);
        lastOutcome = outcome; lastPete = posPete; lastHolly = posHolly; lastSprucey = posSprucey; lastPower = power;
        applied = true;

        Place(pete, peteSpots, running ? posPete : 0);
        Place(holly, hollySpots, running ? posHolly : 0);
        Place(sprucey, sideSprucey == 0 ? spruceSpotsW : spruceSpotsE, running ? posSprucey : 0);
        if (shadowL != null) shadowL.SetActive(running && !lightL);
        if (shadowR != null) shadowR.SetActive(running && !lightR);
        if (lampL != null) lampL.SetActive(running && lightL);
        if (lampR != null) lampR.SetActive(running && lightR);
        if ((lightL && !lastLightL) || (lightR && !lastLightR)) Play(lightClip, 0.5f);
        if ((doorL != lastDoorL || doorR != lastDoorR) && announce) Play(doorClip, 0.8f);
        lastLightL = lightL; lastLightR = lightR; lastDoorL = doorL; lastDoorR = doorR;
        UpdateTexts();
        UpdateMonitor();
    }
    int lastPower = 1000;
    bool lastLightL, lastLightR, lastDoorL, lastDoorR;

    void Place(Transform t, Transform[] spots, int pos)
    {
        if (t == null || spots.Length == 0) return;
        Transform s = spots[Mathf.Clamp(pos, 0, spots.Length - 1)];
        if (s == null) return;
        t.position = s.position;
        t.rotation = s.rotation;
    }

    void UpdateLocal()
    {
        // doors slide (each player animates their own)
        if (doorPanelL != null) doorPanelL.localPosition = Vector3.MoveTowards(doorPanelL.localPosition, doorL0 + (doorL ? Vector3.zero : new Vector3(0, 2.8f, 0)), Time.deltaTime * 9f);
        if (doorPanelR != null) doorPanelR.localPosition = Vector3.MoveTowards(doorPanelR.localPosition, doorR0 + (doorR ? Vector3.zero : new Vector3(0, 2.8f, 0)), Time.deltaTime * 9f);
        if (Time.time >= nextClock) { nextClock = Time.time + 0.5f; UpdateTexts(); }
        // the face in front of you
        if (scareWho > 0 && scareWho <= scareFaces.Length)
        {
            Transform f = scareFaces[scareWho - 1];
            if (Time.time > scareUntil) { if (f != null) f.gameObject.SetActive(false); scareWho = 0; }
            else if (f != null)
            {
                VRCPlayerApi lp = Networking.LocalPlayer;
                if (Utilities.IsValid(lp))
                {
                    VRCPlayerApi.TrackingData hd = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                    f.position = hd.position + hd.rotation * new Vector3(0f, -0.1f, 0.55f);
                    f.rotation = hd.rotation;
                }
            }
        }
        // the voicemail's captions
        if (phoneStart >= 0f && captionText != null)
        {
            float t = Time.time - phoneStart;
            string c = "";
            for (int i = 0; i < phoneCaptions.Length && i < phoneCaptionAt.Length; i++) if (t >= phoneCaptionAt[i]) c = phoneCaptions[i];
            if (phone != null && phone.clip != null && t > phone.clip.length + 0.5f) { c = ""; phoneStart = -1f; }
            captionText.text = c;
        }
    }
    float nextClock;

    void UpdateTexts()
    {
        if (clockText != null)
        {
            int h = Hour();
            clockText.text = !running ? "<color=#888888>--:--</color>" : outcome == 9 ? "<color=#70ff90>6 AM</color>" : (h == 0 ? "12" : h.ToString()) + " AM";
        }
        if (powerText != null)
        {
            int use = 1 + (doorL ? 2 : 0) + (doorR ? 2 : 0) + (lightL ? 1 : 0) + (lightR ? 1 : 0);
            string bars = "";
            for (int i = 0; i < use; i++) bars = bars + "|";
            powerText.text = !running ? "POWER: --" : "POWER: " + (power / 10) + "%\n<size=70%>usage " + bars + "</size>";
        }
        if (statusText != null)
        {
            if (!running) statusText.text = "<b>NIGHT SHIFT</b>\nPress <color=#ffd060>START</color> to begin your shift. Survive until 6 AM.";
            else if (outcome == 9) statusText.text = "<color=#70ff90><b>6 AM! YOU SURVIVED!</b></color>\nPress START for another night.";
            else if (outcome >= 1 && outcome <= 3) statusText.text = "<color=#ff5050><b>" + (outcome == 1 ? "PINE PETE" : outcome == 2 ? "HOLLY" : "SPRUCEY") + " GOT YOU.</b></color>\nPress START to try again.";
            else if (power <= 0) statusText.text = "<color=#ff5050><b>THE POWER IS OUT.</b></color>\n...is that music?";
            else statusText.text = "<b>ON SHIFT.</b> Doors and lights use power.\nCheck the cameras. Close a door when something's in it.";
        }
    }

    void UpdateMonitor()
    {
        if (monitorText == null) return;
        if (!running) { monitorText.text = "<color=#5a5>CAM " + cam + "  " + Cam(cam) + "</color>\n\n<color=#888>(no signal: the night hasn't started)</color>"; return; }
        if (power <= 0) { monitorText.text = "<color=#a33>NO POWER</color>"; return; }
        string who = "";
        if (At(posPete, 0, cam)) who = who + "PINE PETE is here.\n";
        if (At(posHolly, 1, cam)) who = who + "HOLLY is here.\n";
        if (At(posSprucey, sideSprucey, cam)) who = who + "SPRUCEY is here.\n";
        if (who == "") who = "<color=#888>Nothing. Probably.</color>";
        else who = "<color=#ff6060>" + who + "</color>";
        monitorText.text = "<color=#5f5>CAM " + cam + "  " + Cam(cam) + "</color>\n\n" + who;
    }

    string Cam(int c) { return c >= 1 && c <= camNames.Length ? camNames[c - 1] : ""; }

    /// <summary>Is an animatronic at position pos (on side 0 west / 1 east) on camera c? Cameras: 1 stage, 2 dining,
    /// 3 parts room (west), 4 kitchen (east), 5 west hall, 6 east hall.</summary>
    bool At(int pos, int side, int c)
    {
        if (pos == 0) return c == 1;
        if (pos == 1) return c == 2;
        if (pos == 2) return c == (side == 0 ? 3 : 4);
        if (pos == 3) return c == (side == 0 ? 5 : 6);
        return false;   // at the door: only the door light shows it
    }

    void Scare(int who)
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        Play(screamClip, 1f);
        if (!Utilities.IsValid(lp)) return;
        Vector3 p = lp.GetPosition();
        if (p.x < officeMin.x || p.x > officeMax.x || p.z < officeMin.z || p.z > officeMax.z) return;   // only the guards in the office see it
        scareWho = who;
        scareUntil = Time.time + 1.6f;
        if (who - 1 < scareFaces.Length && scareFaces[who - 1] != null) scareFaces[who - 1].gameObject.SetActive(true);
    }

    void PlayPhone()
    {
        if (phone == null) return;
        phone.Stop();
        phone.Play();
        phoneStart = Time.time;
    }

    void Play(AudioClip c, float v) { if (sfx != null && c != null) sfx.PlayOneShot(c, v); }
}
