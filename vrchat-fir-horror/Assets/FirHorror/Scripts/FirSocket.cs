using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// A trigger that accepts carried items: a tree's head takes the hat that fits (acceptKind 0),
/// the furnace takes imitation trees (acceptKind 1). Only the item's owner reports it.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirSocket : UdonSharpBehaviour
{
    public FirGameManager game;
    public int acceptKind;
    public int socketId;
    public FirAnimatronic tree;   // reacts to a wrong hat

    void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        FirCarryItem c = other.GetComponent<FirCarryItem>();
        if (c == null || c.kind != acceptKind) return;
        if (!Networking.IsOwner(c.gameObject)) return;
        if (acceptKind == 0)
        {
            if (game.phase != 5) return;
            if (c.sizeId != socketId)
            {
                game.PlaySfx(game.clipWrong);
                game.Say("<i>It doesn't fit.</i>", 2f);
                if (tree != null) tree.SetMode(4, 0.8f);
                return;
            }
            if (c.pickup != null) c.pickup.Drop();
            game.FillSocket(socketId);
        }
        else
        {
            if (game.phase != 6) return;
            if (c.pickup != null) c.pickup.Drop();
            game.PlaySfx(game.screams[1]);
            game.CollectItem(c.index);
        }
    }
}
