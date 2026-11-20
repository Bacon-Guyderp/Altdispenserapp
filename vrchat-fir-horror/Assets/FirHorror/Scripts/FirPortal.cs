using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// A linked pair of portals: walk into one and you step out of the other (facing out of it), and back.
/// Each player's own client moves them (TeleportTo); anything they're holding comes too.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirPortal : UdonSharpBehaviour
{
    public Transform a, b;              // each portal's centre at floor level; forward = the way out
    public float radius = 0.6f;
    public AudioSource sfx;
    public Transform spinA, spinB;      // the swirls

    float ready;

    void Update()
    {
        if (spinA != null) spinA.localRotation = spinA.localRotation * Quaternion.Euler(0, 0, 120f * Time.deltaTime);
        if (spinB != null) spinB.localRotation = spinB.localRotation * Quaternion.Euler(0, 0, -120f * Time.deltaTime);
        if (Time.time < ready) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        Vector3 p = lp.GetPosition();
        if (Near(p, a)) Go(lp, b);
        else if (Near(p, b)) Go(lp, a);
    }

    bool Near(Vector3 p, Transform t)
    {
        if (t == null) return false;
        Vector3 d = p - t.position;
        float y = d.y;
        d.y = 0f;
        return d.magnitude < radius && y > -0.5f && y < 1.5f;
    }

    void Go(VRCPlayerApi lp, Transform to)
    {
        ready = Time.time + 1.2f;
        if (sfx != null) sfx.Play();
        lp.TeleportTo(to.position + to.rotation * new Vector3(0, 0.05f, 1.2f), to.rotation);
    }
}
