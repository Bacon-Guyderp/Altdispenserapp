using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;

/// <summary>
/// A floor button: while any of its crates sits on it, its door opens (the door object hides). The crates are
/// pickups with object sync, so every player sees them in the same place, and every client works the button out
/// for itself. Pressing the respawn button (FirHuntButton, action 51) sends the crates home.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirPad : UdonSharpBehaviour
{
    public Transform[] crates;
    public VRCObjectSync[] crateSync;
    public float radius = 0.75f;
    public GameObject door;              // hidden while the button is held down
    public GameObject[] showWhenDown;    // e.g. the "TEST COMPLETE" sign
    public MeshRenderer padLight;
    public Material upMat, downMat;
    public AudioSource sfx;
    public AudioClip downClip, upClip;

    public int argAction;
    public int argValue;

    bool down, started;

    void Update()
    {
        bool now = false;
        for (int i = 0; i < crates.Length; i++)
        {
            if (crates[i] == null) continue;
            Vector3 d = crates[i].position - transform.position;
            if (Mathf.Abs(d.y) < 0.8f && new Vector2(d.x, d.z).magnitude < radius) now = true;
        }
        if (now == down && started) return;
        started = true;
        bool changed = now != down;
        down = now;
        if (door != null) door.SetActive(!down);
        for (int i = 0; i < showWhenDown.Length; i++) if (showWhenDown[i] != null) showWhenDown[i].SetActive(down);
        if (padLight != null) padLight.sharedMaterial = down ? downMat : upMat;
        if (changed && sfx != null) { AudioClip c = down ? downClip : upClip; if (c != null) sfx.PlayOneShot(c, 0.8f); }
    }

    public void EvButton()
    {
        if (argAction != 51) return;
        for (int i = 0; i < crateSync.Length; i++)
        {
            if (crateSync[i] == null) continue;
            Networking.SetOwner(Networking.LocalPlayer, crateSync[i].gameObject);
            crateSync[i].Respawn();
        }
    }

    public bool IsDown() { return down; }
}
