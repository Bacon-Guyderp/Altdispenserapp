using UdonSharp;
using UnityEngine;

/// <summary>Interact to send one custom event to a behaviour (lobby buttons, stations).</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirButton : UdonSharpBehaviour
{
    public FirWatchdog watch;          // diagnostics: reports each press to the lobby board
    public UdonSharpBehaviour target;
    public string eventName;
    public AudioSource clickSource;

    public override void Interact()
    {
        if (watch != null) watch.Pressed(gameObject.name);
        if (clickSource != null) clickSource.Play();
        if (target != null) target.SendCustomEvent(eventName);
    }
}
