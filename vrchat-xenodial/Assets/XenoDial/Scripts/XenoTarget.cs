using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

/// <summary>
/// A training target or boulder. Any alien power can smash it: everyone sees it burst,
/// and it rebuilds itself a few seconds later.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class XenoTarget : UdonSharpBehaviour
{
    public GameObject visual;
    public Collider hitCollider;
    public Renderer debris;
    public AudioSource sound;
    public float hitRadius = 0.9f;      // how forgiving fireball aiming is
    public float respawnSeconds = 6f;
    public bool hover;                  // floating targets bob and spin

    private bool _broken;
    private float _burstT = 99f;
    private Vector3 _home;
    private float _seed;

    void Start()
    {
        _home = visual.transform.localPosition;
        _seed = transform.position.x * 0.37f + transform.position.z * 0.11f;
        debris.gameObject.SetActive(false);
    }

    public bool IsBroken() { return _broken; }

    /// <summary>Called by the attacker's watch. Returns true if this smash counts.</summary>
    public bool Smash()
    {
        if (_broken) return false;
        Break();
        SendCustomNetworkEvent(NetworkEventTarget.Others, nameof(Break));
        return true;
    }

    public void Break()
    {
        if (_broken) return;
        _broken = true;
        visual.SetActive(false);
        if (hitCollider != null) hitCollider.enabled = false;
        debris.gameObject.SetActive(true);
        _burstT = 0f;
        if (sound != null) sound.Play();
        SendCustomEventDelayedSeconds(nameof(_Rebuild), respawnSeconds);
    }

    public void _Rebuild()
    {
        _broken = false;
        visual.SetActive(true);
        if (hitCollider != null) hitCollider.enabled = true;
    }

    void Update()
    {
        if (_burstT < 0.5f)
        {
            _burstT += Time.deltaTime;
            debris.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 3f, _burstT / 0.5f);
            if (_burstT >= 0.5f) debris.gameObject.SetActive(false);
        }
        if (hover && !_broken)
        {
            float t = Time.time + _seed;
            visual.transform.localPosition = _home + Vector3.up * Mathf.Sin(t * 1.3f) * 0.25f;
            visual.transform.localRotation = Quaternion.Euler(0f, t * 40f, 0f);
        }
    }
}
