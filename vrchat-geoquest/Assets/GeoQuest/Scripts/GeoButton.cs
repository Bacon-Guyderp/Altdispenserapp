using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>A pressable cube. Sends one custom event to a target behaviour.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GeoButton : UdonSharpBehaviour
{
    public UdonSharpBehaviour target;
    public string eventName;

    public override void Interact()
    {
        if (target != null) target.SendCustomEvent(eventName);
    }
}
