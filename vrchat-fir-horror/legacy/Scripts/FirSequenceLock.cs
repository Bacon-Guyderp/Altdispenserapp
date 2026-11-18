using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.Udon;

/// <summary>
/// A local entry lock: the Showroom carol (values 0-3 = trees), the Stockroom fuse box and the
/// office door (digits), and the tape deck (values 0-3 = tape colours). The answer comes from the
/// synced seed via FirGameManager.CodeAt.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirSequenceLock : UdonSharpBehaviour
{
    public UdonBehaviour game;          // FirGameManager
    public int argValue;
    public void EvPress() { Press(argValue); }
    public int chapter;
    public int length = 4;
    public bool needsItems;
    public string needText = "TAPES";
    public TextMeshPro display;
    public AudioSource beep;
    public AudioClip[] valueClips;   // optional: a sound per value (the tapes)
    public string[] valueNames;

    int[] entry;
    int count;
    float lastPress;

    void Start() { entry = new int[8]; Show(); }

    public void Press(int v)
    {
        if (game == null) return;
        int phase = GameInt("phase");
        if (phase < chapter) { ShowMsg("NO POWER"); return; }
        if (phase > chapter) { ShowMsg("OPEN"); return; }
        int[] have = (int[])game.GetProgramVariable("chapterHave");
        int[] need = (int[])game.GetProgramVariable("chapterNeed");
        if (have == null || need == null) return;   // the game manager hasn't started yet
        if (needsItems && have[chapter] < need[chapter])
        {
            ShowMsg(needText + " " + have[chapter] + "/" + need[chapter]);
            GameEv("EvSfxWrong");
            return;
        }
        if (v < 0) { count = 0; Show(); return; }
        // a half-entered code is forgotten after 8 s, so leftovers never spoil the next try
        if (count > 0 && Time.time - lastPress > 8f) count = 0;
        lastPress = Time.time;
        if (beep != null)
        {
            if (valueClips != null && v < valueClips.Length && valueClips[v] != null) beep.PlayOneShot(valueClips[v]);
            else beep.Play();
        }
        entry[count] = v;
        count = count + 1;
        Show();
        if (count < length)
        {
            // the building doesn't like being solved
            float r = Random.value;
            if (r < 0.12f) { GameEvF("EvFlicker", 0.9f); GameEv("EvSfxWhisper"); }
            else if (r < 0.17f) GameEv("BehindScare");
            return;
        }
        bool ok = true;
        int[] answers = (int[])game.GetProgramVariable("answers");
        if (answers == null) return;
        for (int i = 0; i < length; i++) if (entry[i] != answers[chapter * 5 + i]) ok = false;
        count = 0;
        if (ok) { ShowMsg("ACCEPTED"); GameEvA("EvSolveLock", chapter); }
        else { ShowMsg("WRONG"); GameEvA("EvWrongCode", chapter); }
    }

    void ShowMsg(string msg) { if (display != null) display.text = msg; }

    void Show()
    {
        if (display == null) return;
        string s = "";
        for (int i = 0; i < length; i++)
        {
            if (i < count) s += valueNames != null && entry[i] < valueNames.Length ? valueNames[entry[i]] : entry[i].ToString();
            else s += "_";
            s += " ";
        }
        display.text = s;
    }

    // ---- calls to other scripts: plain Udon events (see FirGameManager's "public API")
    void GameEv(string ev) { if (game != null) game.SendCustomEvent(ev); }
    void GameEvA(string ev, int a) { if (game == null) return; game.SetProgramVariable("argA", a); game.SendCustomEvent(ev); }
    void GameEvF(string ev, float f) { if (game == null) return; game.SetProgramVariable("argF", f); game.SendCustomEvent(ev); }
    void GameEvAF(string ev, int a, float f) { if (game == null) return; game.SetProgramVariable("argA", a); game.SetProgramVariable("argF", f); game.SendCustomEvent(ev); }
    void GameEvABF(string ev, int a, int b, float f) { if (game == null) return; game.SetProgramVariable("argA", a); game.SetProgramVariable("argB", b); game.SetProgramVariable("argF", f); game.SendCustomEvent(ev); }
    void GameSay(string text, float seconds) { if (game == null) return; game.SetProgramVariable("argText", text); game.SetProgramVariable("argF", seconds); game.SendCustomEvent("EvSay"); }
    int GameInt(string name) { return game != null ? (int)game.GetProgramVariable(name) : 0; }
    float GameFloat(string name) { return game != null ? (float)game.GetProgramVariable(name) : 0f; }
    bool GameBool(string name) { return game != null && (bool)game.GetProgramVariable(name); }
}
