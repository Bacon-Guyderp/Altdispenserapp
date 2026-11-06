using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>Press to move yourself to another area (hangout <-> game).</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GeoTeleporter : UdonSharpBehaviour
{
    public Transform destination;

    public override void Interact()
    {
        if (destination == null) return;
        Networking.LocalPlayer.TeleportTo(destination.position, destination.rotation);
    }
}
