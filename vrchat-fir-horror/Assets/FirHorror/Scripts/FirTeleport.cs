using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>A teleporter button (the lobby's Station, and each area's way back): press it to go to destination.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirTeleport : UdonSharpBehaviour
{
    public Transform destination;
    public AudioSource sfx;
    public FirCuller culler;   // wakes the area you're going to before you land in it

    public override void Interact()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp) || destination == null) return;
        if (sfx != null) sfx.Play();
        if (culler != null) culler.WakeAt(destination.position);
        lp.TeleportTo(destination.position, destination.rotation);
    }
}
