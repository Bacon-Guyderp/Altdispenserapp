using TMPro;
using UdonSharp;
using UnityEngine;

/// <summary>
/// A keypad that opens a secret door when you type the right code (each player's own; nothing is synced).
/// Buttons (FirHuntButton): 61 = key (value 0-9, 10 clear, 11 enter).
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirCodeLock : UdonSharpBehaviour
{
    public string code = "0000";
    public GameObject door;           // hidden once it's open
    public TextMeshPro display;
    public AudioSource sfx;
    public AudioClip keyClip, openClip, wrongClip;

    public int argAction;
    public int argValue;

    string typed = "";
    bool open;

    void Start() { Show(); }

    public void EvButton()
    {
        if (argAction != 61 || open) return;
        int k = argValue;
        if (k <= 9) { if (typed.Length < code.Length) typed = typed + k.ToString(); Play(keyClip); }
        else if (k == 10) typed = "";
        else if (k == 11)
        {
            if (typed == code) { open = true; if (door != null) door.SetActive(false); Play(openClip); }
            else { typed = ""; Play(wrongClip); }
        }
        Show();
    }

    void Show()
    {
        if (display == null) return;
        if (open) { display.text = "<color=#70ff90>OPEN</color>"; return; }
        string d = "";
        for (int i = 0; i < code.Length; i++) d = d + (i < typed.Length ? typed.Substring(i, 1) : "_") + " ";
        display.text = d;
    }

    void Play(AudioClip c) { if (sfx != null && c != null) sfx.PlayOneShot(c, 0.8f); }

    public bool IsOpen() { return open; }
}
