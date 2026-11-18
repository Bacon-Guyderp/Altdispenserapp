using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.Udon;

/// <summary>
/// Diagnostics board in the lobby. Every second it checks that the game manager is still running
/// (its heartbeat goes up every frame). VRChat stops a script for good if it hits an error, so if the
/// heartbeat stops, this board says what the game manager was doing at that moment (game.step) and
/// what was pressed last. It depends on nothing but reading two values, so it keeps working.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirWatchdog : UdonSharpBehaviour
{
    public UdonBehaviour game;          // FirGameManager
    public string argWhat = "";
    public void EvPressed() { Pressed(argWhat); }
    public TextMeshPro board;

    int lastBeat = -1;
    float lastChange;
    float nextCheck;
    string lastPressed = "nothing yet";
    float pressedAt;
    bool reported;

    /// <summary>Called first thing by every button, key, item and gag, so we can see presses arrive.</summary>
    public void Pressed(string what)
    {
        lastPressed = what;
        pressedAt = Time.time;
        nextCheck = 0f;
    }

    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 1f;
        if (board == null) return;
        if (game == null) { board.text = "<color=#ff6060><b>DIAGNOSTICS: the game manager is missing.</b></color>\nRun Fir Warning > Build World again."; return; }
        int beat = (int)game.GetProgramVariable("heartbeat");
        string step = (string)game.GetProgramVariable("step");
        if (beat != lastBeat) { lastBeat = beat; lastChange = Time.time; }
        string pressed = "Last pressed: " + lastPressed + (pressedAt > 0f ? " (" + Mathf.RoundToInt(Time.time - pressedAt) + " s ago)" : "");
        if (Time.time - lastChange > 2.5f)
        {
            board.text = "<color=#ff6060><b>DIAGNOSTICS: the game manager has STOPPED.</b></color>\n" +
                         "It stopped during: <b>" + step + "</b>\n" + pressed +
                         "\n<size=80%>Please send a photo of this board to the world's creator.</size>";
            reported = true;
        }
        else if (!reported)
            board.text = "<color=#70ff90>Diagnostics: game manager running</color> (" + step + ")\n" + pressed;
    }
}
