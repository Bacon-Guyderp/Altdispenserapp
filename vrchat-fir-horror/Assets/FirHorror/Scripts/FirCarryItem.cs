using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Something you carry: a Santa hat (kind 0, sizeId = which tree it fits) or an imitation tree
/// for the furnace (kind 1, index = global item index). Imitations whimper when picked up.
/// </summary>
// NoVariableSync, not None: it shares its GameObject with a VRCObjectSync.
[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class FirCarryItem : UdonSharpBehaviour
{
    public int kind;
    public int sizeId;
    public int index;
    public VRC_Pickup pickup;
    public AudioSource cry;
    public FirAnimatronic anim;
    public FirGameManager game;
    public float scareChance = 0.25f;
    float nextScare;

    public override void OnPickup()
    {
        if (cry != null && !cry.isPlaying) cry.Play();
        if (anim != null) anim.SetMode(kind == 1 ? 2 : 1, 2.5f);
        if (game == null || Time.time < nextScare || Random.value > scareChance) return;
        nextScare = Time.time + 60f;
        if (kind == 1) game.FaceScare(Random.Range(0, 3), 0.9f);   // the little tree screams in your face
        else game.BehindScare();    }
}
