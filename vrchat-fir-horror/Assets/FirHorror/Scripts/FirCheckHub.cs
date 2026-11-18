using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Systems check: the hub other test buttons talk to. It shows on its own board that it is running,
/// what it has received, and a counter that is shared over the network (manual sync).
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FirCheckHub : UdonSharpBehaviour
{
    public TextMeshPro board;

    [UdonSynced] public int sharedCount;

    // test 3: SendCustomEvent arrives here
    public int pingCount;
    // test 4: SetProgramVariable / GetProgramVariable round trip
    public int argValue;
    public int echoValue;
    // test 5: a typed UdonSharp call (FirCheckTyped calls TypedPing directly)
    public int typedValue;

    int heartbeat;
    float nextShow;
    string syncNote = "nobody has pressed it yet";

    void Start() { Show(); }

    public void Ping()
    {
        pingCount = pingCount + 1;
        Show();
    }

    public void Echo()
    {
        echoValue = argValue + 1;
    }

    public void TypedPing(int v)
    {
        typedValue = v;
        Show();
    }

    public void AddShared()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        sharedCount = sharedCount + 1;
        RequestSerialization();
        syncNote = "you pressed it";
        Show();
    }

    public override void OnDeserialization()
    {
        syncNote = "received over the network";
        Show();
    }

    void Update()
    {
        heartbeat = heartbeat + 1;
        if (Time.time < nextShow) return;
        nextShow = Time.time + 1f;
        Show();
    }

    void Show()
    {
        if (board == null) return;
        board.text = "<color=#70ff90><b>HUB: running</b></color>  (frame " + heartbeat + ")\n" +
                     "Events received (test 3): " + pingCount + "\n" +
                     "Typed call value (test 5): " + typedValue + "\n" +
                     "Shared counter (test 6): <b>" + sharedCount + "</b>, " + syncNote + "\n" +
                     "Players here: " + VRCPlayerApi.GetPlayerCount();
    }
}
