using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>A pressable button: teleports you, toggles an object (just for you), or sends an event.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class XenoButton : UdonSharpBehaviour
{
    public Transform teleportTo;
    public GameObject toggle;
    public UdonSharpBehaviour target;
    public string eventName;

    public override void Interact()
    {
        if (teleportTo != null) Networking.LocalPlayer.TeleportTo(teleportTo.position, teleportTo.rotation);
        if (toggle != null) toggle.SetActive(!toggle.activeSelf);
        if (target != null && !string.IsNullOrEmpty(eventName)) target.SendCustomEvent(eventName);
    }
}
