using UdonSharp;
using UnityEngine;
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
    public FirAnimatronic anim;

    public override void OnPickup()
    {
        if (cry != null && !cry.isPlaying) cry.Play();
        if (anim != null) anim.SetMode(kind == 1 ? 2 : 1, 2.5f);
    }
}
