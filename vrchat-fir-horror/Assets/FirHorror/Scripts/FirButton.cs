using UdonSharp;
using UnityEngine;

/// <summary>Interact to send one custom event to a behaviour (lobby buttons, stations).</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirButton : UdonSharpBehaviour
{
    public UdonSharpBehaviour target;
    public string eventName;
    public AudioSource clickSource;

    public override void Interact()
    {
        if (clickSource != null) clickSource.Play();
        if (target != null) target.SendCustomEvent(eventName);
    }
}
