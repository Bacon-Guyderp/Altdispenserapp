using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>A trigger across the race track. Tells the timer when you run through it.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class XenoRaceGate : UdonSharpBehaviour
{
    public XenoRaceTimer timer;
    public int gateIndex;   // 0 = start/finish, 1 = halfway

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player != null && player.isLocal && timer != null) timer.Gate(gateIndex);
    }
}
