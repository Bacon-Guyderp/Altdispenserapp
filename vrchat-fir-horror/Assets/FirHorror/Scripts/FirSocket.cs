using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Somewhere that accepts carried items: a tree's head takes the right colour of hat (acceptKind 0,
/// sizeId = colour), the furnace takes imitation trees (acceptKind 1).
/// No trigger colliders: five times a second it checks how far each candidate is (only items you own),
/// so it can never block the interact pointer and never depends on physics callbacks.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirSocket : UdonSharpBehaviour
{
    public FirGameManager game;
    public int acceptKind;
    public int socketId;
    public float radius = 0.6f;
    public FirCarryItem[] candidates;   // every carry item of the right kind (set by the builder)
    public FirAnimatronic tree;         // reacts to a wrong hat

    float nextCheck, wrongCooldown;

    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.2f;
        if (game == null) return;
        if (acceptKind == 0 && game.phase != 5) return;
        if (acceptKind == 1 && game.phase != 6) return;
        for (int i = 0; i < candidates.Length; i++)
        {
            FirCarryItem c = candidates[i];
            if (c == null || !c.gameObject.activeInHierarchy) continue;
            if (Vector3.Distance(c.transform.position, transform.position) > radius) continue;
            if (!Networking.IsOwner(c.gameObject)) continue;
            Accept(c);
            return;
        }
    }

    void Accept(FirCarryItem c)
    {
        if (acceptKind == 0)
        {
            if (c.sizeId != game.CodeAt(5, socketId))
            {
                if (Time.time < wrongCooldown) return;
                wrongCooldown = Time.time + 3f;
                game.PlaySfx(game.clipWrong);
                game.Say("<color=#ff5555>\"THAT'S NOT MY HAT.\"</color>", 2f);
                if (tree != null) tree.SetMode(4, 1.2f);
                game.Flicker(0.8f);
                return;
            }
            if (c.pickup != null) c.pickup.Drop();
            game.FillSocket(socketId);
        }
        else
        {
            if (!game.ValvesOpen())
            {
                if (Time.time < wrongCooldown) return;
                wrongCooldown = Time.time + 4f;
                game.Say("<i>The furnace is cold. Open the three gas valves first.</i>", 3f);
                return;
            }
            if (c.pickup != null) c.pickup.Drop();
            game.PlaySfx(game.screams[1]);
            game.CollectItem(c.index);
        }
    }
}
