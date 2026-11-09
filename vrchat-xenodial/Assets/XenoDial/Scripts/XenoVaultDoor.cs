using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// The tiny tunnel into the secret vault. If you walk into it while you're Pip (the tiny
/// alien), you're pulled through into the vault. Anyone else just bumps the wall.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class XenoVaultDoor : UdonSharpBehaviour
{
    public XenoDialWatch watch;
    public int requiredForm = 4;
    public Transform destination;
    public AudioSource sound;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || watch == null || destination == null) return;
        if (watch.CurrentForm() != requiredForm) return;
        player.TeleportTo(destination.position, destination.rotation);
        if (sound != null) sound.Play();
    }
}
