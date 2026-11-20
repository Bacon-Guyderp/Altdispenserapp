using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// A pixel-RPG encounter: a monster blocks the path, and you talk your way past. The menu has BONK, CHAT (three
/// things to say each round), SNACK and PARDON. The right thing to say warms it up; once it's your friend,
/// PARDON lets it step aside. BONK just makes everyone sad. Each player has their own encounter (nothing is
/// synced), like a single-player RPG: the monster only steps aside for you.
/// Buttons (FirHuntButton): 21 bonk, 22 chat (value = option 0-2), 23 snack, 24 pardon.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirEncounter : UdonSharpBehaviour
{
    public string monsterName = "MONSTER";
    public string intro = "";
    public string[] options;      // 3 per round
    public int[] correct;         // the right option each round
    public string[] replies;      // 3 per round: what it says back to each option
    public string bonkText = "", snackText = "", notYetText = "", spareText = "";
    public TextMeshPro dialog;
    public TextMeshPro[] optionLabels;   // the CHAT buttons' labels
    public TextMeshPro meter;
    public GameObject blocker;    // the wall that holds you back (hidden once it's your friend)
    public Transform monster;     // steps aside
    public Vector3 stepAside = new Vector3(2.2f, 0, 0);
    public AudioSource sfx;
    public AudioClip blipClip, goodClip, badClip, friendClip;

    int round, warmth;
    bool friend;
    string full = "";
    float typeStart;
    Vector3 home, basePos;
    bool inited;

    void Start() { Init(); }

    void Init()
    {
        if (inited) return;
        inited = true;
        if (monster != null) { home = monster.localPosition; basePos = home; }
        Say("* " + monsterName + " blocks the way!\n" + intro);
        ShowOptions();
    }

    public int argAction;
    public int argValue;

    /// <summary>Called by FirHuntButton (which sets argAction / argValue first).</summary>
    public void EvButton()
    {
        Init();
        int a = argAction, v = argValue;
        if (friend && a != 24) { Say("* " + monsterName + " is your friend now. It waves."); return; }
        if (a == 21) { warmth = 0; round = 0; Say(bonkText + "\n<color=#aaaaaa>(it didn't like that. start again.)</color>"); Play(badClip); }
        else if (a == 22)
        {
            int k = round * 3 + v;
            if (k >= replies.Length) return;
            if (v == correct[round]) { warmth++; round++; Play(goodClip); }
            else Play(badClip);
            Say(replies[k]);
            if (round >= correct.Length) Say(replies[k] + "\n<color=#ffe680>* " + monsterName + " seems ready to be spared.</color>");
        }
        else if (a == 23) { Say(snackText); Play(blipClip); }
        else if (a == 24)
        {
            if (friend) { Say("* You already did. It's fine. It's great, even."); return; }
            if (round < correct.Length) { Say(notYetText); Play(badClip); return; }
            friend = true;
            Say(spareText + "\n<color=#70ff90>* YOU MADE A FRIEND!</color>");
            Play(friendClip);
            if (blocker != null) blocker.SetActive(false);
        }
        ShowOptions();
    }

    void ShowOptions()
    {
        for (int i = 0; i < optionLabels.Length; i++)
        {
            if (optionLabels[i] == null) continue;
            int k = round * 3 + i;
            optionLabels[i].text = friend || round >= correct.Length || k >= options.Length ? "<color=#888888>...</color>" : options[k];
        }
        if (meter != null)
        {
            string m = "";
            for (int i = 0; i < correct.Length; i++) m = m + (i < round || friend ? "<color=#ff6080>v</color>" : "<color=#555555>v</color>");
            meter.text = "FRIENDSHIP " + m;
        }
    }

    void Say(string s)
    {
        full = s;
        typeStart = Time.time;
    }

    void Update()
    {
        if (!inited) return;
        // the text types itself out, a letter at a time
        if (dialog != null && full != null)
        {
            int n = Mathf.Min(full.Length, (int)((Time.time - typeStart) * 45f));
            string shown = full.Substring(0, n);
            if (dialog.text != shown) dialog.text = shown;
        }
        if (monster != null)
        {
            Vector3 goal = friend ? home + stepAside : home;
            basePos = Vector3.MoveTowards(basePos, goal, Time.deltaTime * 1.5f);
            float bob = friend ? 0f : Mathf.Abs(Mathf.Sin(Time.time * 3f)) * 0.06f;   // a little idle hop
            monster.localPosition = basePos + new Vector3(0, bob, 0);
        }
    }

    void Play(AudioClip c) { if (sfx != null && c != null) sfx.PlayOneShot(c, 0.8f); }

    /// <summary>For tests: has this player befriended it?</summary>
    public bool IsFriend() { return friend; }
}
