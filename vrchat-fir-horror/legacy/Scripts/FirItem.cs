using UdonSharp;
using UnityEngine;
using VRC.Udon;

/// <summary>A collectible you press (batteries, fuses, tapes). The manager hides it for everyone.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirItem : UdonSharpBehaviour
{
    public UdonBehaviour watch;          // diagnostics: reports each press to the lobby board
    public UdonBehaviour game;          // FirGameManager
    public int index;
    public UdonBehaviour scareOnTake;   // FirJumpscare   // optional: something happens when you grab it
    public bool fake;                  // a mimic: it bites and vanishes (comes back next game)

    public override void Interact()
    {
        if (fake)
        {
            if (scareOnTake != null) scareOnTake.SendCustomEvent("Fire");
            gameObject.SetActive(false);
            Report();
            return;
        }
        if (game == null) return;
        int before = (int)game.GetProgramVariable("itemMask");
        game.SetProgramVariable("argA", index);
        game.SendCustomEvent("EvCollectItem");
        if (scareOnTake != null && (int)game.GetProgramVariable("itemMask") != before) scareOnTake.SendCustomEvent("Fire");
        Report();
    }
    void Report() { if (watch == null) return; watch.SetProgramVariable("argWhat", gameObject.name); watch.SendCustomEvent("EvPressed"); }
}
