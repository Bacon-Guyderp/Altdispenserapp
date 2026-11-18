using TMPro;
using UdonSharp;
using UnityEngine;

/// <summary>
/// Systems check, test 5: a typed UdonSharp call (hub.TypedPing(v)) and a typed field read. This is the
/// kind of call the old map used everywhere. Kept in its own script so a failure here can't stop anything else.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirCheckTyped : UdonSharpBehaviour
{
    public FirCheckHub hub;
    public TextMeshPro label;

    void Start() { Say("<color=#cccccc>not tried yet</color>"); }

    public override void Interact()
    {
        Say("<color=#ffd040>testing...</color>");
        int v = Random.Range(10, 1000);
        hub.TypedPing(v);
        if (hub.typedValue == v) Say("<color=#70ff90><b>PASS</b></color> typed call and read work");
        else Say("<color=#ff6060><b>FAIL</b></color> sent " + v + ", read " + hub.typedValue);
    }

    void Say(string result)
    {
        if (label != null) label.text = "<b>5. Typed script call</b>\n" + result;
    }
}
