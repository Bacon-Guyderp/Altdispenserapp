using TMPro;
using UdonSharp;
using UnityEngine;

/// <summary>
/// The tile puzzle: a 3 x 3 grid of tiles. Pressing a tile flips it and the tiles next to it (up, down, left,
/// right). Make every tile glow and the gate opens. Each player solves their own (nothing is synced).
/// Buttons (FirHuntButton): 31 = press tile (value 0-8), 32 = start over.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirTiles : UdonSharpBehaviour
{
    public MeshRenderer[] tiles;
    public Material offMat, onMat;
    public GameObject gate;
    public TextMeshPro board;
    public AudioSource sfx;
    public AudioClip flipClip, solvedClip;
    public int[] startPressed;     // the puzzle starts as if these tiles had been pressed on an all-glowing grid

    public int argAction;
    public int argValue;

    bool[] on;
    bool solved, inited;

    void Start() { Init(); }

    void Init()
    {
        if (inited) return;
        inited = true;
        on = new bool[9];
        StartOver();
    }

    void StartOver()
    {
        for (int i = 0; i < 9; i++) on[i] = true;
        for (int i = 0; i < startPressed.Length; i++) Flip(startPressed[i]);
        solved = false;
        if (gate != null) gate.SetActive(true);
        Show();
    }

    public void EvButton()
    {
        Init();
        if (argAction == 32) { StartOver(); return; }
        if (argAction != 31 || solved) return;
        Flip(argValue);
        if (sfx != null && flipClip != null) sfx.PlayOneShot(flipClip, 0.6f);
        bool all = true;
        for (int i = 0; i < 9; i++) if (!on[i]) all = false;
        if (all)
        {
            solved = true;
            if (gate != null) gate.SetActive(false);
            if (sfx != null && solvedClip != null) sfx.PlayOneShot(solvedClip, 1f);
        }
        Show();
    }

    void Flip(int i)
    {
        if (i < 0 || i > 8) return;
        int r = i / 3, c = i % 3;
        Toggle(r, c); Toggle(r - 1, c); Toggle(r + 1, c); Toggle(r, c - 1); Toggle(r, c + 1);
    }

    void Toggle(int r, int c)
    {
        if (r < 0 || r > 2 || c < 0 || c > 2) return;
        on[r * 3 + c] = !on[r * 3 + c];
    }

    void Show()
    {
        for (int i = 0; i < tiles.Length && i < 9; i++) if (tiles[i] != null) tiles[i].sharedMaterial = on[i] ? onMat : offMat;
        if (board != null) board.text = solved ? "<color=#70ff90><b>THE GATE IS OPEN.</b></color>\n* the tiles hum smugly." : "<b>THE TILE GATE</b>\nMake every tile glow. Pressing a tile flips it and its neighbours.";
    }

    public bool IsSolved() { return solved; }
}
