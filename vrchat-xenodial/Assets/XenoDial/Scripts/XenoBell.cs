using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

/// <summary>The bell on top of the tower. Ring it and everyone hears it.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class XenoBell : UdonSharpBehaviour
{
    public AudioSource sound;
    public Transform bell;
    private float _t = 99f;

    public override void Interact()
    {
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(Ring));
    }

    public void Ring()
    {
        if (sound != null) sound.Play();
        _t = 0f;
    }

    void Update()
    {
        if (_t > 2f || bell == null) return;
        _t += Time.deltaTime;
        bell.localRotation = Quaternion.Euler(Mathf.Sin(_t * 14f) * 18f * (1f - _t / 2f), 0f, 0f);
    }
}
