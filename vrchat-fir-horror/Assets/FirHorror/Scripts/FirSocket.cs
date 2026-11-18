using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// Somewhere that accepts carried items: a tree's head takes the right colour of hat (acceptKind 0),
/// the furnace takes imitation trees (acceptKind 1).
/// No trigger colliders: five times a second it checks how far each candidate is (only items you own).
/// The candidates are plain Transforms with their details in parallel arrays (set by the builder), so
/// this script never has to read another script's variables.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirSocket : UdonSharpBehaviour
{
    public UdonBehaviour game;          // FirGameManager
    public int acceptKind;
    public int socketId;
    public float radius = 0.6f;
    public Transform[] candidates;      // every carry item of the right kind
    public int[] candidateSize;         // hats: colour (0 red, 1 green, 2 blue)
    public int[] candidateIndex;        // imitations: item index
    public VRC_Pickup[] candidatePickup;
    public UdonBehaviour tree;          // FirAnimatronic: reacts to a wrong hat

    float nextCheck, wrongCooldown;

    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.2f;
        if (game == null) return;
        int phase = GameInt("phase");
        if (acceptKind == 0 && phase != 5) return;
        if (acceptKind == 1 && phase != 6) return;
        for (int i = 0; i < candidates.Length; i++)
        {
            Transform c = candidates[i];
            if (c == null || !c.gameObject.activeInHierarchy) continue;
            if (Vector3.Distance(c.position, transform.position) > radius) continue;
            if (!Networking.IsOwner(c.gameObject)) continue;
            Accept(i);
            return;
        }
    }

    void Accept(int i)
    {
        VRC_Pickup pickup = i < candidatePickup.Length ? candidatePickup[i] : null;
        if (acceptKind == 0)
        {
            int[] answers = (int[])game.GetProgramVariable("answers");
            if (answers == null) return;
            if (candidateSize[i] != answers[25 + socketId])
            {
                if (Time.time < wrongCooldown) return;
                wrongCooldown = Time.time + 3f;
                GameEv("EvSfxWrong");
                GameSay("<color=#ff5555>\"THAT'S NOT MY HAT.\"</color>", 2f);
                if (tree != null) { tree.SetProgramVariable("argMode", 4); tree.SetProgramVariable("argSeconds", 1.2f); tree.SendCustomEvent("EvSetMode"); }
                GameEvF("EvFlicker", 0.8f);
                return;
            }
            if (pickup != null) pickup.Drop();
            GameEvA("EvFillSocket", socketId);
        }
        else
        {
            if (!(bool)game.GetProgramVariable("valvesOpen"))
            {
                if (Time.time < wrongCooldown) return;
                wrongCooldown = Time.time + 4f;
                GameSay("<i>The furnace is cold. Open the three gas valves first.</i>", 3f);
                return;
            }
            if (pickup != null) pickup.Drop();
            GameEvA("EvScream", 1);
            GameEvA("EvCollectItem", candidateIndex[i]);
        }
    }

    void GameEv(string ev) { game.SendCustomEvent(ev); }
    void GameEvA(string ev, int a) { game.SetProgramVariable("argA", a); game.SendCustomEvent(ev); }
    void GameEvF(string ev, float f) { game.SetProgramVariable("argF", f); game.SendCustomEvent(ev); }
    void GameSay(string text, float seconds) { game.SetProgramVariable("argText", text); game.SetProgramVariable("argF", seconds); game.SendCustomEvent("EvSay"); }
    int GameInt(string name) { return (int)game.GetProgramVariable(name); }
}
