using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using TMPro;

/// <summary>
/// Hangout TV. Paste a link in the URL box and everyone in the instance watches it
/// together; play/pause/stop are shared, volume is per person. Uses AVPro, which
/// works on both PC and Quest. (On Quest, direct video links are the most reliable.)
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GeoTV : UdonSharpBehaviour
{
    public BaseVRCVideoPlayer player;
    public VRCUrlInputField urlField;
    public AudioSource[] speakers;
    public TextMeshPro statusText;
    public GameObject screenOff;        // shown when nothing is playing

    [UdonSynced] public VRCUrl url = VRCUrl.Empty;
    [UdonSynced] public int startTime;  // server ms at which the video was at 0:00
    [UdonSynced] public bool paused;
    [UdonSynced] public float pausedAt;

    private string _loaded = "";
    private float _lastLoad = -100f, _nextCheck;
    private bool _ready;
    private float _volume = 0.6f;

    void Start() { ApplyVolume(); Show("Paste a video link below"); }

    private int Now() { return Networking.GetServerTimeInMilliseconds(); }
    private void TakeOwner()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
    }
    private void Show(string s) { if (statusText != null) statusText.text = s; }

    // ---------------------------------------------------------------- controls

    public void OnUrlEntered()
    {
        VRCUrl u = urlField.GetUrl();
        if (u == null || u.Get() == "") return;
        TakeOwner();
        url = u;
        paused = false;
        startTime = Now();
        RequestSerialization();
        urlField.SetUrl(VRCUrl.Empty);
        Apply();
    }

    public void TogglePause()
    {
        if (url == null || url.Get() == "") return;
        TakeOwner();
        if (!paused) { paused = true; pausedAt = player.GetTime(); }
        else { paused = false; startTime = Now() - Mathf.RoundToInt(pausedAt * 1000f); }
        RequestSerialization();
        Apply();
    }

    public void StopVideo()
    {
        TakeOwner();
        url = VRCUrl.Empty;
        paused = false;
        RequestSerialization();
        Apply();
    }

    public void VolumeUp() { _volume = Mathf.Clamp01(_volume + 0.1f); ApplyVolume(); }
    public void VolumeDown() { _volume = Mathf.Clamp01(_volume - 0.1f); ApplyVolume(); }

    private void ApplyVolume()
    {
        if (speakers == null) return;
        for (int i = 0; i < speakers.Length; i++) if (speakers[i] != null) speakers[i].volume = _volume;
    }

    // ---------------------------------------------------------------- sync

    public override void OnDeserialization() { Apply(); }

    private void Apply()
    {
        string want = url == null ? "" : url.Get();
        if (want == "")
        {
            if (_loaded != "") player.Stop();
            _loaded = "";
            _ready = false;
            if (screenOff != null) screenOff.SetActive(true);
            Show("Paste a video link below");
            return;
        }
        if (want != _loaded)
        {
            _LoadNow();
            return;
        }
        if (_ready) SyncPlayback();
    }

    public void _LoadNow()
    {
        string want = url == null ? "" : url.Get();
        if (want == "" || want == _loaded) return;
        // VRChat allows one video load every 5 seconds per world.
        float wait = 5.2f - (Time.time - _lastLoad);
        if (wait > 0f) { SendCustomEventDelayedSeconds(nameof(_LoadNow), wait); return; }
        _lastLoad = Time.time;
        _loaded = want;
        _ready = false;
        Show("Loading...");
        player.Stop();
        player.LoadURL(url);
    }

    private void SyncPlayback()
    {
        if (paused)
        {
            if (player.IsPlaying) player.Pause();
            player.SetTime(pausedAt);
            Show("Paused");
            return;
        }
        float t = (Now() - startTime) / 1000f;
        float len = player.GetDuration();
        if (!float.IsInfinity(len) && len > 0f && t > len) t = len;  // finished
        if (len > 0f && !float.IsInfinity(len) && Mathf.Abs(player.GetTime() - t) > 1.5f) player.SetTime(t);
        if (!player.IsPlaying) player.Play();
        Show("");
    }

    public override void OnVideoReady()
    {
        _ready = true;
        if (screenOff != null) screenOff.SetActive(false);
        if (Networking.IsOwner(gameObject) && !paused)
        {
            startTime = Now();  // owner's 0:00 is "now"; others catch up to it
            RequestSerialization();
        }
        SyncPlayback();
    }

    public override void OnVideoEnd() { Show("Finished. Paste another link below"); }

    public override void OnVideoError(VideoError videoError)
    {
        _ready = false;
        if (videoError == VideoError.AccessDenied) Show("That site isn't allowed. Turn on 'Allow Untrusted URLs' or use another link.");
        else if (videoError == VideoError.RateLimited) { Show("Too many loads, retrying..."); _loaded = ""; SendCustomEventDelayedSeconds(nameof(_LoadNow), 6f); }
        else Show("Couldn't play that link (" + videoError + ")");
    }

    void Update()
    {
        // Drift correction for live viewers.
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + 3f;
        if (_ready && url != null && url.Get() != "") SyncPlayback();
    }
}
