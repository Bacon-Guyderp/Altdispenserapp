using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>The crashed pod. Use it to put the XenoDial on your wrist.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class XenoPod : UdonSharpBehaviour
{
    public XenoDialWatch watch;
    public GameObject displayWatch;   // the watch floating in the pod (hidden for you once taken)

    public override void Interact()
    {
        if (watch == null) return;
        watch.Equip();
        if (displayWatch != null) displayWatch.SetActive(false);
        DisableInteractive = true;
    }

    void Update()
    {
        if (displayWatch != null && displayWatch.activeSelf)
            displayWatch.transform.localRotation = Quaternion.Euler(20f, Time.time * 60f, 0f);
    }
}
