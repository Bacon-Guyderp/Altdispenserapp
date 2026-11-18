using UdonSharp;
using UnityEngine;
using VRC.Udon;
using VRC.SDKBase;

/// <summary>
/// Something you carry: a Santa hat (kind 0, sizeId = which tree it fits) or an imitation tree
/// for the furnace (kind 1, index = global item index). Imitations whimper when picked up.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirCarryItem : UdonSharpBehaviour
{
    public int kind;
    public int sizeId;
    public int index;
    public VRC_Pickup pickup;
    public AudioSource cry;
    public UdonBehaviour anim;
    public UdonBehaviour game;          // FirGameManager
    public float scareChance = 0.25f;
    float nextScare;

    public override void OnPickup()
    {
        if (cry != null && !cry.isPlaying) cry.Play();
        AnimMode(anim, kind == 1 ? 2 : 1, 2.5f);
        if (game == null || Time.time < nextScare || Random.value > scareChance) return;
        nextScare = Time.time + 60f;
        if (kind == 1) GameEvAF("EvFaceScare", Random.Range(0, 3), 0.9f);   // the little tree screams in your face
        else GameEv("BehindScare");
    }

    // ---- calls to other scripts: plain Udon events (see FirGameManager's "public API")
    void GameEv(string ev) { if (game != null) game.SendCustomEvent(ev); }
    void GameEvA(string ev, int a) { if (game == null) return; game.SetProgramVariable("argA", a); game.SendCustomEvent(ev); }
    void GameEvAB(string ev, int a, int b) { if (game == null) return; game.SetProgramVariable("argA", a); game.SetProgramVariable("argB", b); game.SendCustomEvent(ev); }
    void GameEvF(string ev, float f) { if (game == null) return; game.SetProgramVariable("argF", f); game.SendCustomEvent(ev); }
    void GameEvAF(string ev, int a, float f) { if (game == null) return; game.SetProgramVariable("argA", a); game.SetProgramVariable("argF", f); game.SendCustomEvent(ev); }
    void GameSay(string text, float seconds) { if (game == null) return; game.SetProgramVariable("argText", text); game.SetProgramVariable("argF", seconds); game.SendCustomEvent("EvSay"); }
    int GameInt(string name) { return game != null ? (int)game.GetProgramVariable(name) : 0; }
    void AnimMode(UdonBehaviour a, int mode, float seconds)
    {
        if (a == null) return;
        a.SetProgramVariable("argMode", mode);
        a.SetProgramVariable("argSeconds", seconds);
        a.SendCustomEvent("EvSetMode");
    }
}
