using UdonSharp;
using UnityEngine;
using VRC.Udon;

/// <summary>One key on a keypad or tape deck, or a tree you touch in the carol. value -1 clears.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirKey : UdonSharpBehaviour
{
    public UdonBehaviour watch;          // diagnostics: reports each press to the lobby board
    public UdonBehaviour lockTarget;   // FirSequenceLock
    public int value;
    public UdonBehaviour anim;   // a tree you touch sings its note

    public override void Interact()
    {
        if (anim != null) anim.SendCustomEvent("Note");
        if (lockTarget != null) { lockTarget.SetProgramVariable("argValue", value); lockTarget.SendCustomEvent("EvPress"); }
        Report();
    }
    void Report() { if (watch == null) return; watch.SetProgramVariable("argWhat", gameObject.name); watch.SendCustomEvent("EvPressed"); }
}
