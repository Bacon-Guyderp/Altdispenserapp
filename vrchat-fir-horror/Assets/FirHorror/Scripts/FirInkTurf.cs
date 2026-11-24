using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// The Ink Plaza's turf: a grid of floor tiles that take your ink colour as you walk over them.
/// Each player paints their own copy (nothing is synced): pick a colour at the ink pads (action 71, value = colour),
/// and start over with action 72. The board shows how much of the turf is yours.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirInkTurf : UdonSharpBehaviour
{
    public MeshRenderer[] tiles;          // row by row, from origin along +x then +z
    public int columns = 10;
    public Vector3 origin;                // world centre of tile 0
    public float tileSize = 2f;
    public Material blankMat;
    public Material[] inkMats;
    public TextMeshPro board;
    public AudioSource sfx;
    public AudioClip splatClip;

    public int argAction;
    public int argValue;

    int[] owner;                          // -1 blank, else the ink colour
    int ink, mine;
    bool inited;
    float nextSplat;

    void Start() { Init(); }

    void Init()
    {
        if (inited) return;
        inited = true;
        owner = new int[tiles.Length];
        Clear();
    }

    void Clear()
    {
        for (int i = 0; i < tiles.Length; i++) { owner[i] = -1; if (tiles[i] != null) tiles[i].sharedMaterial = blankMat; }
        mine = 0;
        Show();
    }

    public void EvButton()
    {
        Init();
        if (argAction == 71 && argValue >= 0 && argValue < inkMats.Length) { ink = argValue; Show(); }
        else if (argAction == 72) Clear();
    }

    void Update()
    {
        if (!inited) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        Vector3 p = lp.GetPosition() - origin;
        if (p.y < -0.5f || p.y > 1.5f) return;
        int cx = Mathf.RoundToInt(p.x / tileSize), cz = Mathf.RoundToInt(p.z / tileSize);
        if (cx < 0 || cx >= columns || cz < 0) return;
        int i = cz * columns + cx;
        if (i >= tiles.Length || owner[i] == ink) return;
        if (owner[i] == -1) mine++;
        owner[i] = ink;
        if (tiles[i] != null) tiles[i].sharedMaterial = inkMats[ink];
        if (sfx != null && splatClip != null && Time.time >= nextSplat) { sfx.PlayOneShot(splatClip, 0.5f); nextSplat = Time.time + 0.15f; }
        Show();
    }

    public int Painted() { return mine; }

    void Show()
    {
        if (board == null) return;
        int pct = tiles.Length > 0 ? mine * 100 / tiles.Length : 0;
        board.text = "<b>TURF: " + pct + "%</b>\n<size=60%>walk on the floor to ink it. pick a colour at the ink pads</size>";
    }
}
