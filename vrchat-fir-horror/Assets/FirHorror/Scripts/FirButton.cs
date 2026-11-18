using UdonSharp;
using UnityEngine;
using VRC.Udon;

/// <summary>Interact to send one custom event to a behaviour (lobby buttons, stations).</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirButton : UdonSharpBehaviour
{
    public UdonBehaviour watch;          // diagnostics: reports each press to the lobby board
    public UdonBehaviour target;
    public string eventName;
    public AudioSource clickSource;

    public override void Interact()
    {
        if (clickSource != null) clickSource.Play();
        if (target != null) target.SendCustomEvent(eventName);
        Report();
    }
    void Report() { if (watch == null) return; watch.SetProgramVariable("argWhat", gameObject.name); watch.SendCustomEvent("EvPressed"); }
}
