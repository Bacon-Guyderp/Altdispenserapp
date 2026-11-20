using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// Plays gags for everyone. When someone presses a gag, skeleton routine, the closet, a gag skeleton or a combo
/// act, it plays for them at once and tells this script (EvShare, with argKind and argIndex). This sends a press
/// over the network (manual sync); everyone else plays the same thing when it arrives. Each press has a random id,
/// so nothing plays twice, and a player who joins late doesn't get old presses replayed.
/// Kinds: 0 gags, 1 skeleton routines, 2 closets, 3 gag skeletons, 4 combo acts, 5 Bonetown wanderers.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FirShare : UdonSharpBehaviour
{
    [UdonSynced] public int pressId;
    [UdonSynced] public int kind;
    [UdonSynced] public int index;

    public UdonBehaviour[] gags;
    public UdonBehaviour[] skeletonGags;
    public UdonBehaviour[] closets;
    public UdonBehaviour[] gagSkeletons;
    public UdonBehaviour[] combos;
    public UdonBehaviour[] wanderers;
    public UdonBehaviour[] residents;

    // arguments for EvShare
    public int argKind;
    public int argIndex;

    int lastSeen;
    bool synced;

    /// <summary>Someone here pressed something (it has already played for them): tell everyone else.</summary>
    public void EvShare()
    {
        synced = true;
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        kind = argKind;
        index = argIndex;
        pressId = Random.Range(1, 2000000000);
        lastSeen = pressId;
        RequestSerialization();
    }

    public override void OnDeserialization()
    {
        if (!synced) { synced = true; lastSeen = pressId; return; }   // just joined: don't replay the last press
        if (pressId == lastSeen) return;
        lastSeen = pressId;
        UdonBehaviour[] list = kind == 0 ? gags : kind == 1 ? skeletonGags : kind == 2 ? closets : kind == 3 ? gagSkeletons : kind == 4 ? combos : kind == 5 ? wanderers : residents;
        if (index >= 0 && index < list.Length && list[index] != null) list[index].SendCustomEvent("Play");
    }
}
