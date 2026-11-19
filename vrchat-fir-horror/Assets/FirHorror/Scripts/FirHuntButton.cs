using UdonSharp;
using UnityEngine;
using VRC.Udon;

/// <summary>
/// A button, item, keypad key or lever in the horror game. Pressing it hands its action and value to the game
/// (FirHunt.EvButton), which decides what happens. Actions: 1 start/join, 2 reset, 3 pick up item (value = item),
/// 4 panel (value = room), 5 keypad key (value = room * 100 + key; 10 clear, 11 enter), 6 toy (value = toy).
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirHuntButton : UdonSharpBehaviour
{
    public UdonBehaviour hunt;
    public int action;
    public int value;
    public Transform press;      // pushed in a little when pressed (optional)

    float upAt;
    Vector3 rest;
    bool down;

    public override void Interact()
    {
        if (press != null && !down) { rest = press.localPosition; press.localPosition = rest + new Vector3(0, 0, 0.02f); down = true; upAt = Time.time + 0.15f; }
        if (hunt == null) return;
        hunt.SetProgramVariable("argAction", action);
        hunt.SetProgramVariable("argValue", value);
        hunt.SendCustomEvent("EvButton");
    }

    void Update()
    {
        if (down && Time.time > upAt) { down = false; press.localPosition = rest; }
    }
}
