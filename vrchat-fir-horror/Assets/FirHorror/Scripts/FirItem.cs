using UdonSharp;
using UnityEngine;

/// <summary>A collectible you press (batteries, fuses, tapes). The manager hides it for everyone.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirItem : UdonSharpBehaviour
{
    public FirGameManager game;
    public int index;
    public FirJumpscare scareOnTake;   // optional: something happens when you grab it

    public override void Interact()
    {
        int before = game.itemMask;
        game.CollectItem(index);
        if (scareOnTake != null && game.itemMask != before) scareOnTake.Fire();
    }
}
