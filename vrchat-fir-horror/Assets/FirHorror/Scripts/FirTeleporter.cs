using UdonSharp;
using UnityEngine;
using VRC.Udon;
using VRC.SDKBase;

/// <summary>
/// A teleporter that always works: press it (or, with walkOn, step into its zone) and you are moved to
/// `destination`. It depends on nothing else in the world. Only after you've been moved does it tell
/// another behaviour (`notify` / `notifyEvent`, e.g. the game's StartGame), so nothing that behaviour
/// does can stop the teleport. Same pattern as GeoQuest's teleporter.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirTeleporter : UdonSharpBehaviour
{
    public UdonBehaviour watch;          // diagnostics: reports each press to the lobby board
    public Transform destination;
    public UdonBehaviour notify;
    public string notifyEvent = "";
    public AudioSource sound;
    public bool walkOn;
    public Vector3 zoneSize;   // set by the builder (walk-on pads only)

    float nextZone, cooldownUntil;

    public override void Interact()
    {
        Go();
    }

    public void Go()
    {
        if (Time.time < cooldownUntil) return;
        cooldownUntil = Time.time + 1f;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (destination != null && Utilities.IsValid(lp)) lp.TeleportTo(destination.position, destination.rotation);
        if (sound != null) sound.Play();
        if (notify != null && notifyEvent != "") notify.SendCustomEvent(notifyEvent);
        Report();
    }

    void Update()
    {
        if (!walkOn || Time.time < nextZone) return;
        nextZone = Time.time + 0.15f;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        Vector3 p = transform.InverseTransformPoint(lp.GetPosition() + new Vector3(0f, 0.9f, 0f));
        if (Mathf.Abs(p.x) > zoneSize.x * 0.5f) return;
        if (Mathf.Abs(p.y) > zoneSize.y * 0.5f) return;
        if (Mathf.Abs(p.z) > zoneSize.z * 0.5f) return;
        Go();
    }
    void Report() { if (watch == null) return; watch.SetProgramVariable("argWhat", gameObject.name); watch.SendCustomEvent("EvPressed"); }
}
