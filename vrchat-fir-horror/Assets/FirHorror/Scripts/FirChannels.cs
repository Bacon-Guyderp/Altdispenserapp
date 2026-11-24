using TMPro;
using UdonSharp;
using UnityEngine;

/// <summary>
/// The warden's TV in the Cellbound room: press the remote and the set changes channel. Each channel is a scene
/// on the screen (the convict hiding in the set, acting out whatever is meant to be on), with its own line and caption.
/// Per player: each viewer flicks their own TV (nothing is synced).
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirChannels : UdonSharpBehaviour
{
    public GameObject[] channels;       // one scene per channel
    public AudioSource[] lines;         // what each channel says
    public string[] captions;
    public TextMeshPro caption;         // under the screen
    public GameObject staticScreen;     // the snow between channels
    public AudioSource click;

    int current = -1;
    float showAt;
    bool pending;

    void Start() { ShowNone(); }

    void ShowNone()
    {
        for (int i = 0; i < channels.Length; i++) if (channels[i] != null) channels[i].SetActive(false);
        if (staticScreen != null) staticScreen.SetActive(true);
    }

    public override void Interact() { Next(); }

    /// <summary>Change to the next channel: a moment of static, then the scene and its line.</summary>
    public void Next()
    {
        for (int i = 0; i < lines.Length; i++) if (lines[i] != null) lines[i].Stop();
        current = (current + 1) % channels.Length;
        ShowNone();
        if (click != null) click.Play();
        if (caption != null) caption.text = "<i>click</i>";
        showAt = Time.time + 0.4f;
        pending = true;
    }

    void Update()
    {
        if (!pending || Time.time < showAt) return;
        pending = false;
        if (staticScreen != null) staticScreen.SetActive(false);
        if (channels[current] != null) channels[current].SetActive(true);
        if (lines[current] != null) lines[current].Play();
        if (caption != null) caption.text = captions[current];
    }

    public int Channel() { return current; }
}
