using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>A ride seat: press it to sit in its VRCStation (which the ride carries). Jump to get off.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirSeat : UdonSharpBehaviour
{
    public VRCStation station;

    public override void Interact()
    {
        if (station != null) station.UseStation(Networking.LocalPlayer);
    }
}
