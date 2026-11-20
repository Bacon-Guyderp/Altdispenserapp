using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Blocklands: press a block in the quarry to mine it; your blocks go in your pocket. Press a ghost outline on the
/// building plot to place one there (the second layer needs a block underneath). The world is shared: which blocks
/// are mined and which slots are built is synced (bits in a few ints, worked out with arithmetic). Your pocket is
/// your own. Buttons (FirHuntButton): 41 mine (value = block), 42 place (value = slot), 43 reset the world.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FirVoxel : UdonSharpBehaviour
{
    [UdonSynced] public int minedA, minedB;   // blocks 0-29, 30-59
    [UdonSynced] public int placed;           // slots 0-29

    public GameObject[] blocks;
    public GameObject[] slotGhost, slotSolid;
    public int[] slotBelow;                   // the slot that must be built first, or -1
    public TextMeshPro pocketText;
    public AudioSource sfx;
    public AudioClip mineClip, placeClip, nopeClip;

    public int argAction;
    public int argValue;

    int pocket;
    int[] pow;

    void Start()
    {
        pow = new int[30];
        int p = 1;
        for (int i = 0; i < 30; i++) { pow[i] = p; p = p * 2; }
        Apply();
    }

    bool Bit(int mask, int i) { return (mask / pow[i]) % 2 == 1; }
    bool Mined(int i) { return i < 30 ? Bit(minedA, i) : Bit(minedB, i - 30); }
    bool Placed(int j) { return Bit(placed, j); }

    public void EvButton()
    {
        if (pow == null) return;
        int a = argAction, v = argValue;
        if (a == 41)
        {
            if (v < 0 || v >= blocks.Length || Mined(v)) return;
            Take();
            if (v < 30) minedA = minedA + pow[v]; else minedB = minedB + pow[v - 30];
            pocket++;
            Commit();
            Play(mineClip);
        }
        else if (a == 42)
        {
            if (v < 0 || v >= slotSolid.Length || Placed(v)) return;
            if (pocket <= 0 || (slotBelow[v] >= 0 && !Placed(slotBelow[v]))) { Play(nopeClip); Show(); return; }
            Take();
            placed = placed + pow[v];
            pocket--;
            Commit();
            Play(placeClip);
        }
        else if (a == 43) { Take(); minedA = 0; minedB = 0; placed = 0; Commit(); }
    }

    void Take() { if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject); }
    void Commit() { RequestSerialization(); Apply(); }
    public override void OnDeserialization() { Apply(); }

    void Apply()
    {
        if (pow == null) return;
        for (int i = 0; i < blocks.Length; i++) if (blocks[i] != null) blocks[i].SetActive(!Mined(i));
        for (int j = 0; j < slotSolid.Length; j++)
        {
            bool p = Placed(j);
            if (slotSolid[j] != null) slotSolid[j].SetActive(p);
            // a ghost shows where you could build next (on the ground, or on top of a built block)
            if (slotGhost[j] != null) slotGhost[j].SetActive(!p && (slotBelow[j] < 0 || Placed(slotBelow[j])));
        }
        Show();
    }

    void Show() { if (pocketText != null) pocketText.text = "<b>YOUR POCKET</b>\n" + pocket + " block" + (pocket == 1 ? "" : "s"); }

    void Play(AudioClip c) { if (sfx != null && c != null) sfx.PlayOneShot(c, 0.8f); }

    public int Pocket() { return pocket; }
}
