using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Persistence;
using TMPro;

/// <summary>
/// Lap timer for the race track. Cross the start line, pass the halfway gate, cross the
/// start line again. Your best lap is saved (VRChat persistence) and shown on the board.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class XenoRaceTimer : UdonSharpBehaviour
{
    private const string BEST_KEY = "xenodial.bestLap";
    public TextMeshPro board;
    public AudioSource sound;
    public AudioClip startClip, finishClip;

    private bool _running, _halfway;
    private float _start, _best, _last;

    void Start() { Show(); }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        float b;
        if (PlayerData.TryGetFloat(player, BEST_KEY, out b) && b > 0f) _best = b;
        Show();
    }

    public void Gate(int index)
    {
        if (index == 1) { if (_running) _halfway = true; return; }
        if (_running && _halfway)
        {
            _last = Time.time - _start;
            if (_best <= 0f || _last < _best)
            {
                _best = _last;
                PlayerData.SetFloat(BEST_KEY, _best);
            }
            if (sound != null && finishClip != null) sound.PlayOneShot(finishClip);
        }
        else if (sound != null && startClip != null) sound.PlayOneShot(startClip);
        _running = true;
        _halfway = false;
        _start = Time.time;
        Show();
    }

    void Update()
    {
        if (!_running) return;
        if (Time.time - _start > 180f) { _running = false; Show(); return; }
        Show();
    }

    private string Fmt(float t) { return t <= 0f ? "--" : t.ToString("0.00") + "s"; }

    private void Show()
    {
        if (board == null) return;
        string now = _running ? Fmt(Time.time - _start) + (_halfway ? "  <size=60%>(halfway!)</size>" : "") : "Run through the gate";
        board.text = "<b>SPEED TRACK</b>\n<size=120%>" + now + "</size>\n<size=70%>Last lap " + Fmt(_last) +
                     "   Best " + Fmt(_best) + "</size>\n<size=55%>Tip: become Zipwire</size>";
    }
}
