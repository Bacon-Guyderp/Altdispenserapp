using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// Systems check: one test per button. The button writes its own result on its own label, first
/// "testing..." and then PASS or FAIL. If its script stops partway, the label stays on "testing...".
/// Tests: 1 teleport there, 2 own script, 3 event to another script, 4 values between scripts,
/// 6 network sync, 7 skeleton (event), 8 tree (event), 9 lighting (material), 10 sound, 11 teleport back.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirCheckButton : UdonSharpBehaviour
{
    public int test;
    public string title = "";
    public TextMeshPro label;
    public UdonBehaviour hub;          // FirCheckHub (plain Udon reference)
    public UdonBehaviour other;        // test 7: the skeleton, test 8: the tree
    public Transform destination;      // tests 1 and 11
    public Material testMaterial;      // test 9
    public AudioSource sound;          // test 10

    bool darkOn;

    void Start() { Say("<color=#cccccc>not tried yet</color>"); }

    public override void Interact()
    {
        Say("<color=#ffd040>testing...</color>");
        if (test == 1 || test == 11) Teleport();
        else if (test == 2) Say(Pass("this button's own script runs"));
        else if (test == 3)
        {
            hub.SendCustomEvent("Ping");
            Say(Pass("event sent; the hub board's count should go up"));
        }
        else if (test == 4)
        {
            int v = Random.Range(10, 1000);
            hub.SetProgramVariable("argValue", v);
            hub.SendCustomEvent("Echo");
            int back = (int)hub.GetProgramVariable("echoValue");
            if (back == v + 1) Say(Pass("sent " + v + ", got " + back));
            else Say(Fail("sent " + v + ", got " + back));
        }
        else if (test == 6)
        {
            hub.SendCustomEvent("AddShared");
            Say(Pass("pressed; with 2 players, the other should see the counter change"));
        }
        else if (test == 7 || test == 8)
        {
            other.SendCustomEvent(test == 7 ? "Act" : "Talk");
            Say(Pass("event sent; it should move now"));
        }
        else if (test == 9)
        {
            darkOn = !darkOn;
            testMaterial.SetFloat("_Dark", darkOn ? 0.85f : 0f);
            Say(Pass(darkOn ? "the grey test panel should go dark" : "the test panel should be bright again"));
        }
        else if (test == 10)
        {
            sound.Play();
            Say(Pass("you should hear a sound"));
        }
    }

    void Teleport()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        lp.TeleportTo(destination.position, destination.rotation);
        SendCustomEventDelayedSeconds("_CheckArrived", 0.6f);
    }

    public void _CheckArrived()
    {
        Vector3 p = Networking.LocalPlayer.GetPosition();
        float d = Vector3.Distance(p, destination.position);
        if (d < 2f) Say(Pass("you arrived"));
        else Say(Fail("you are " + Mathf.RoundToInt(d) + " m from where you should be"));
    }

    string Pass(string why) { return "<color=#70ff90><b>PASS</b></color> " + why; }
    string Fail(string why) { return "<color=#ff6060><b>FAIL</b></color> " + why; }

    void Say(string result)
    {
        if (label != null) label.text = "<b>" + title + "</b>\n" + result;
    }
}
