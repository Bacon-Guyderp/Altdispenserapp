using TMPro;
using UdonSharp;
using UnityEngine;

/// <summary>
/// A local entry lock: the tape deck in chapter 3 (values 0-3 = tape colours, needs the tapes
/// first) and the office keypad in chapter 4 (digits). The answer comes from the synced seed.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirSequenceLock : UdonSharpBehaviour
{
    public FirGameManager game;
    public int chapter;
    public int length = 4;
    public bool needsItems;
    public TextMeshPro display;
    public AudioSource beep;
    public AudioClip[] valueClips;   // optional: a sound per value (the tapes)
    public string[] valueNames;

    int[] entry;
    int count;

    void Start() { entry = new int[8]; Show(); }

    public void Press(int v)
    {
        if (game.phase < chapter) { ShowMsg("NO POWER"); return; }
        if (game.phase > chapter) { ShowMsg("OPEN"); return; }
        if (needsItems && !game.ChapterItemsDone(chapter))
        {
            ShowMsg("TAPES " + game.ChapterItemsHave(chapter) + "/4");
            game.PlaySfx(game.clipWrong);
            return;
        }
        if (v < 0) { count = 0; Show(); return; }
        if (beep != null)
        {
            if (valueClips != null && v < valueClips.Length && valueClips[v] != null) beep.PlayOneShot(valueClips[v]);
            else beep.Play();
        }
        entry[count++] = v;
        Show();
        if (count < length) return;
        bool ok = true;
        for (int i = 0; i < length; i++) if (entry[i] != game.CodeAt(chapter, i)) ok = false;
        count = 0;
        if (ok) { ShowMsg("ACCEPTED"); game.SolveLock(chapter); }
        else { ShowMsg("WRONG"); game.WrongCode(chapter); }
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
}
