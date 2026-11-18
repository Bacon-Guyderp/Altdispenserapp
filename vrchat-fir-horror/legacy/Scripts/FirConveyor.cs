using UdonSharp;
using UnityEngine;

/// <summary>Slides its children along local X and wraps them round. Local only. Stops when told to.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FirConveyor : UdonSharpBehaviour
{
    public float speed = 0.4f;
    public float length = 8f;
    public AudioSource hum;
    float stopUntil;

    public void Jam() { stopUntil = Time.time + 6f; if (hum != null) hum.Pause(); SendCustomEventDelayedSeconds("_Resume", 6f); }

    public void _Resume() { if (hum != null) hum.UnPause(); }

    void Update()
    {
        if (Time.time < stopUntil) return;
        float half = length * 0.5f;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform c = transform.GetChild(i);
            Vector3 p = c.localPosition;
            p.x += speed * Time.deltaTime;
            if (p.x > half) p.x -= length;
            c.localPosition = p;
        }
    }
}
