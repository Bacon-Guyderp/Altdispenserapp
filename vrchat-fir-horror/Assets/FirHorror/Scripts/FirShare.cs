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
    [UdonSynced] public int pressAt;   // server time of the press: a late joiner's first update carries an old one, and is skipped

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

    /// <summary>Someone here pressed something (it has already played for them): tell everyone else.</summary>
    public void EvShare()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        kind = argKind;
        index = argIndex;
        pressId = Random.Range(1, 2000000000);
        pressAt = Networking.GetServerTimeInMilliseconds();
        lastSeen = pressId;
        RequestSerialization();
    }

    public override void OnDeserialization()
    {
        if (pressId == lastSeen) return;
        lastSeen = pressId;
        int age = Networking.GetServerTimeInMilliseconds() - pressAt;
        if (age > 5000 || age < -5000) return;   // an old press (we've just joined): don't replay it
        UdonBehaviour[] list = kind == 0 ? gags : kind == 1 ? skeletonGags : kind == 2 ? closets : kind == 3 ? gagSkeletons : kind == 4 ? combos : kind == 5 ? wanderers : residents;
        if (index >= 0 && index < list.Length && list[index] != null) list[index].SendCustomEvent("Play");
    }
}
