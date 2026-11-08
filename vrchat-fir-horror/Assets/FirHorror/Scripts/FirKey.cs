using UdonSharp;
using UnityEngine;

/// <summary>One key on a keypad or tape deck. value -1 clears.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirKey : UdonSharpBehaviour
{
    public FirSequenceLock lockTarget;
    public int value;

    public override void Interact() { lockTarget.Press(value); }
}
