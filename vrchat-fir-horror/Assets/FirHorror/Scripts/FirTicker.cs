using TMPro;
using UdonSharp;
using UnityEngine;

/// <summary>A news ticker: shows each headline in turn (each player's own, no syncing needed).</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirTicker : UdonSharpBehaviour
{
    public TextMeshPro text;
    public string[] headlines;
    public float interval = 6f;
    int i;
    float next;

    void Update()
    {
        if (Time.time < next || headlines.Length == 0 || text == null) return;
        next = Time.time + interval;
        text.text = headlines[i % headlines.Length];
        i++;
    }
}
