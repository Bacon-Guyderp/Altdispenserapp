using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// The tiny tunnel into the secret vault. If you walk into it as any tiny alien (Pip,
/// Frostling, Sparkrat...), you're pulled through into the vault. Anyone else just bumps the wall.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class XenoVaultDoor : UdonSharpBehaviour
{
    public XenoDialWatch watch;
    public Transform destination;
    public AudioSource sound;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || watch == null || destination == null) return;
        if (!watch.IsTiny()) return;
        player.TeleportTo(destination.position, destination.rotation);
        if (sound != null) sound.Play();
    }
}
