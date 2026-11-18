using UdonSharp;
using UnityEngine;

/// <summary>One key on a keypad or tape deck, or a tree you touch in the carol. value -1 clears.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirKey : UdonSharpBehaviour
{
    public FirWatchdog watch;          // diagnostics: reports each press to the lobby board
    public FirSequenceLock lockTarget;
    public int value;
    public FirAnimatronic anim;   // a tree you touch sings its note

    public override void Interact()
    {
        if (watch != null) watch.Pressed(gameObject.name);
        if (anim != null) anim.Note();
        lockTarget.Press(value);
    }
}
