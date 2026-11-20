using TMPro;
using UdonSharp;
using UnityEngine;

/// <summary>Something you press that says something: plays its voice (if any) and shows its line as a caption for a while. Local.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirSay : UdonSharpBehaviour
{
    public string line = "";
    public TextMeshPro caption;
    public AudioSource voice;
    public float showFor = 5f;
    public Transform spin;            // spins while showing (the save point's star), optional

    float until;

    public override void Interact()
    {
        if (voice != null) voice.Play();
        if (caption != null) caption.text = line;
        until = Time.time + showFor;
    }

    void Update()
    {
        if (spin != null) spin.localRotation = spin.localRotation * Quaternion.Euler(0, (Time.time < until ? 360f : 60f) * Time.deltaTime, 0);
        if (caption != null && until > 0f && Time.time > until) { caption.text = ""; until = 0f; }
    }
}
