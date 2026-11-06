using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;

/// <summary>"Reset pins" button: sends every pin that isn't being held back to the rack.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GeoPinRack : UdonSharpBehaviour
{
    public VRCObjectSync[] pins;

    public void ResetPins()
    {
        for (int i = 0; i < pins.Length; i++)
        {
            VRCObjectSync s = pins[i];
            if (s == null) continue;
            VRC_Pickup pickup = (VRC_Pickup)s.GetComponent(typeof(VRC_Pickup));
            if (pickup != null && pickup.IsHeld) continue;
            Networking.SetOwner(Networking.LocalPlayer, s.gameObject);
            s.Respawn();
        }
    }
}
