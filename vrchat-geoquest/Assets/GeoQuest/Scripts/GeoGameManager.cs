using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Image;
using VRC.SDK3.Persistence;
using VRC.Udon.Common.Interfaces;
using TMPro;

/// <summary>
/// GeoQuest game loop. One synced state machine for the instance:
/// Lobby -> (Guess -> Reveal) x rounds -> Game over.
/// Panoramas are streamed with VRCImageDownloader, one at a time, so the world
/// download stays tiny and Quest memory stays low no matter how many places exist.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GeoGameManager : UdonSharpBehaviour
{
    public const int PHASE_LOBBY = 0, PHASE_GUESS = 1, PHASE_REVEAL = 2, PHASE_OVER = 3;
    private const int MAX_ROUNDS = 10;
    private const string BEST_KEY = "geoquest.best";

    [Header("Location table (written by GeoQuest > Build World)")]
    public VRCUrl[] urls;
    public float[] lats;
    public float[] lons;
    public string[] titles;
    public string[] countries;
    public string[] credits;

    [Header("Scene")]
    public Material panoramaMaterial;
    public Texture lobbyPanorama;
    public MapBoard map;
    public PlayerGuess guessTemplate;
    public TextMeshPro statusText;
    public TextMeshPro scoreboardText;
    public TextMeshPro infoText;
    public GameObject[] lobbyObjects;   // visible in lobby / game over
    public GameObject[] guessObjects;   // visible while guessing
    public GameObject[] revealObjects;  // visible during reveal
    public float revealSeconds = 25f;

    [UdonSynced] public int phase;
    [UdonSynced] public int gameId;
    [UdonSynced] public int round;
    [UdonSynced] public int totalRounds = 5;
    [UdonSynced] public int roundSeconds = 120;
    [UdonSynced] public int deadline;
    [UdonSynced] public int[] roundLocations = new int[MAX_ROUNDS];

    private int[] _roundOptions = { 3, 5, 10 };
    private int[] _timerOptions = { 0, 30, 60, 120, 180, 300 };

    private int _seenGame = -1, _seenRound = -1, _seenPhase = -1, _shownLocation = -2;
    private PlayerGuess _local;
    private VRCPlayerApi[] _players = new VRCPlayerApi[100];
    private float _nextTick;

    private VRCImageDownloader _shownDl, _pendingDl;
    private int _wantLocation = -1, _loadingLocation = -1;
    private float _lastDownload = -100f;
    private bool _loading, _loadFailed;
    private int _retries;
    private int[] _recent = new int[200];
    private int _recentHead;

    void Start()
    {
        for (int i = 0; i < _recent.Length; i++) _recent[i] = -1;
        ApplyState();
    }

    // ================================================================ helpers

    private int Now() { return Networking.GetServerTimeInMilliseconds(); }

    private void TakeOwner()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
    }

    private void Commit()
    {
        RequestSerialization();
        ApplyState();
    }

    public int CurrentLocation()
    {
        if ((phase != PHASE_GUESS && phase != PHASE_REVEAL) || round < 1 || round > MAX_ROUNDS) return -1;
        int loc = roundLocations[round - 1];
        return (urls != null && loc >= 0 && loc < urls.Length) ? loc : -1;
    }

    private PlayerGuess LocalGuess()
    {
        if (_local == null && guessTemplate != null && Networking.LocalPlayer != null)
            _local = (PlayerGuess)Networking.FindComponentInPlayerObjects(Networking.LocalPlayer, guessTemplate);
        return _local;
    }

    private PlayerGuess GuessOf(VRCPlayerApi p)
    {
        if (p == null || !Utilities.IsValid(p) || guessTemplate == null) return null;
        return (PlayerGuess)Networking.FindComponentInPlayerObjects(p, guessTemplate);
    }

    public bool CanPlacePin()
    {
        if (phase != PHASE_GUESS || CurrentLocation() < 0) return false;
        PlayerGuess g = LocalGuess();
        return g == null || !g.HasGuess(gameId, round, CurrentLocation());
    }

    private int PickLocation()
    {
        int n = urls.Length;
        int c = 0;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            c = Random.Range(0, n);
            bool used = false;
            for (int r = 0; r < round - 1 && !used; r++) used = roundLocations[r] == c;
            // Avoid places seen in recent games too, as long as there are plenty to choose from.
            if (attempt < 30) for (int i = 0; i < _recent.Length && !used; i++) used = _recent[i] == c;
            if (!used) break;
        }
        Remember(c);
        return c;
    }

    private void Remember(int c)
    {
        _recent[_recentHead] = c;
        _recentHead = (_recentHead + 1) % _recent.Length;
    }

    public float DistanceKm(float lat1, float lon1, float lat2, float lon2)
    {
        float d2r = Mathf.Deg2Rad;
        float dLat = (lat2 - lat1) * d2r, dLon = (lon2 - lon1) * d2r;
        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                  Mathf.Cos(lat1 * d2r) * Mathf.Cos(lat2 * d2r) * Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
        return 6371f * 2f * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(Mathf.Max(0f, 1f - a)));
    }

    public int Score(float km)
    {
        if (km < 0.15f) return 5000;
        return Mathf.RoundToInt(5000f * Mathf.Exp(-km / 1500f));
    }

    private string FormatKm(float km)
    {
        if (km < 1f) return Mathf.RoundToInt(km * 1000f) + " m";
        if (km < 100f) return km.ToString("0.0") + " km";
        return Mathf.RoundToInt(km).ToString("N0") + " km";
    }

    // ================================================================ buttons (anyone)

    public void StartGame()
    {
        if (phase == PHASE_GUESS || phase == PHASE_REVEAL) return;
        if (urls == null || urls.Length == 0) return;
        TakeOwner();
        gameId++;
        round = 1;
        roundLocations = new int[MAX_ROUNDS];
        for (int i = 0; i < MAX_ROUNDS; i++) roundLocations[i] = -1;
        roundLocations[0] = PickLocation();
        BeginGuessing();
    }

    private void BeginGuessing()
    {
        phase = PHASE_GUESS;
        // +6 s grace so everyone's photo has time to arrive.
        deadline = roundSeconds > 0 ? Now() + roundSeconds * 1000 + 6000 : 0;
        Commit();
    }

    public void NextRound()
    {
        if (phase != PHASE_REVEAL) return;
        TakeOwner();
        if (round >= totalRounds)
        {
            phase = PHASE_OVER;
            Commit();
            return;
        }
        round++;
        roundLocations[round - 1] = PickLocation();
        BeginGuessing();
    }

    /// <summary>Skip a broken / ugly photo. Only before anyone has guessed.</summary>
    public void Reroll()
    {
        if (phase != PHASE_GUESS) return;
        TakeOwner();
        roundLocations[round - 1] = PickLocation();
        BeginGuessing();
    }

    public void EndRoundNow()
    {
        if (phase != PHASE_GUESS) return;
        TakeOwner();
        StartReveal();
    }

    private void StartReveal()
    {
        phase = PHASE_REVEAL;
        deadline = Now() + Mathf.RoundToInt(revealSeconds * 1000f);
        Commit();
    }

    public void BackToLobby()
    {
        TakeOwner();
        phase = PHASE_LOBBY;
        round = 0;
        Commit();
    }

    public void CycleRounds()
    {
        if (phase == PHASE_GUESS || phase == PHASE_REVEAL) return;
        TakeOwner();
        int idx = 0;
        for (int i = 0; i < _roundOptions.Length; i++) if (_roundOptions[i] == totalRounds) idx = i;
        totalRounds = _roundOptions[(idx + 1) % _roundOptions.Length];
        Commit();
    }

    public void CycleTimer()
    {
        if (phase == PHASE_GUESS || phase == PHASE_REVEAL) return;
        TakeOwner();
        int idx = 0;
        for (int i = 0; i < _timerOptions.Length; i++) if (_timerOptions[i] == roundSeconds) idx = i;
        roundSeconds = _timerOptions[(idx + 1) % _timerOptions.Length];
        Commit();
    }

    // ================================================================ guessing

    public void OnPinPlaced()
    {
        UpdateTexts();
    }

    public void SubmitGuess()
    {
        int loc = CurrentLocation();
        PlayerGuess g = LocalGuess();
        if (!CanPlacePin() || g == null || map == null || !map.hasPin) return;
        float km = DistanceKm(map.pinLat, map.pinLon, lats[loc], lons[loc]);
        g.Submit(gameId, round, loc, map.pinLat, map.pinLon, km, Score(km));
        UpdateTexts();
    }

    // ================================================================ sync / state

    public override void OnDeserialization()
    {
        ApplyState();
    }

    public override void OnPlayerJoined(VRCPlayerApi player) { UpdateTexts(); }
    public override void OnPlayerLeft(VRCPlayerApi player) { UpdateTexts(); }

    private void ApplyState()
    {
        PlayerGuess g = LocalGuess();
        if (g != null && gameId != _seenGame && g.gameId != gameId) g.ResetForGame(gameId);
        if (g != null) _seenGame = gameId;

        bool newStage = phase != _seenPhase || round != _seenRound;
        int loc = CurrentLocation();

        if (newStage && phase == PHASE_GUESS && map != null) map.ResetForRound();
        if (loc != _shownLocation)
        {
            if (phase == PHASE_GUESS && map != null) map.ResetForRound();
            _shownLocation = loc;
            RequestPanorama(loc);
        }
        if (phase == PHASE_REVEAL && loc >= 0)
        {
            if (g != null) g.BankRound(gameId, round, loc);
            if (map != null) map.ShowAnswer(lats[loc], lons[loc]);
        }
        if (newStage && phase == PHASE_OVER && g != null) SaveBest(g.totalScore);

        _seenPhase = phase;
        _seenRound = round;
        SetActiveAll(lobbyObjects, phase == PHASE_LOBBY || phase == PHASE_OVER);
        SetActiveAll(guessObjects, phase == PHASE_GUESS);
        SetActiveAll(revealObjects, phase == PHASE_REVEAL);
        UpdateTexts();
    }

    private void SetActiveAll(GameObject[] objs, bool on)
    {
        if (objs == null) return;
        for (int i = 0; i < objs.Length; i++) if (objs[i] != null) objs[i].SetActive(on);
    }

    void Update()
    {
        if (Time.time < _nextTick) return;
        _nextTick = Time.time + 0.5f;

        // Player objects can appear a moment after joining.
        PlayerGuess g = LocalGuess();
        if (g != null && _seenGame != gameId) ApplyState();

        if (Networking.IsOwner(gameObject))
        {
            int left = deadline - Now();
            if (phase == PHASE_GUESS && ((deadline != 0 && left <= 0) || AllGuessed())) StartReveal();
            else if (phase == PHASE_REVEAL && left <= 0) NextRound();
        }
        UpdateTexts();
    }

    private bool AllGuessed()
    {
        int loc = CurrentLocation();
        int n = VRCPlayerApi.GetPlayerCount();
        if (n == 0) return false;
        _players = VRCPlayerApi.GetPlayers(_players);
        int guessed = 0, counted = 0;
        for (int i = 0; i < n; i++)
        {
            PlayerGuess pg = GuessOf(_players[i]);
            if (pg == null) continue;
            counted++;
            if (pg.HasGuess(gameId, round, loc)) guessed++;
        }
        return counted > 0 && guessed == counted;
    }

    private void SaveBest(int total)
    {
        int best;
        if (!PlayerData.TryGetInt(Networking.LocalPlayer, BEST_KEY, out best)) best = 0;
        if (total > best) PlayerData.SetInt(BEST_KEY, total);
    }

    private int GetBest()
    {
        int best;
        if (Networking.LocalPlayer == null) return 0;
        if (!PlayerData.TryGetInt(Networking.LocalPlayer, BEST_KEY, out best)) best = 0;
        return best;
    }

    // ================================================================ panorama streaming

    private void RequestPanorama(int loc)
    {
        _wantLocation = loc;
        _retries = 0;
        _loadFailed = false;
        if (loc < 0)
        {
            if (_pendingDl != null) { _pendingDl.Dispose(); _pendingDl = null; }
            if (_shownDl != null) { _shownDl.Dispose(); _shownDl = null; }
            _loading = false;
            panoramaMaterial.SetTexture("_MainTex", lobbyPanorama);
            panoramaMaterial.SetFloat("_Yaw", 0f);
            panoramaMaterial.SetColor("_Tint", Color.white);
            return;
        }
        _loading = true;
        panoramaMaterial.SetColor("_Tint", new Color(0.15f, 0.15f, 0.15f, 1f));
        _DoDownload();
    }

    public void _DoDownload()
    {
        int loc = _wantLocation;
        if (loc < 0 || !_loading) return;
        // VRChat allows one image download every 5 seconds per world.
        float wait = 5.2f - (Time.time - _lastDownload);
        if (wait > 0f)
        {
            SendCustomEventDelayedSeconds(nameof(_DoDownload), wait);
            return;
        }
        if (_loadingLocation == loc && _pendingDl != null) return;
        if (_pendingDl != null) _pendingDl.Dispose();
        _pendingDl = new VRCImageDownloader();
        TextureInfo info = new TextureInfo();
        info.GenerateMipMaps = false;
        info.WrapModeU = TextureWrapMode.Repeat;
        info.WrapModeV = TextureWrapMode.Clamp;
        info.FilterMode = FilterMode.Bilinear;
        _loadingLocation = loc;
        _lastDownload = Time.time;
        _pendingDl.DownloadImage(urls[loc], null, (IUdonEventReceiver)this, info);
        UpdateTexts();
    }

    public override void OnImageLoadSuccess(IVRCImageDownload result)
    {
        if (_wantLocation < 0 || result.Url == null || result.Url.Get() != urls[_wantLocation].Get()) return;
        if (_shownDl != null && _shownDl != _pendingDl) _shownDl.Dispose();
        _shownDl = _pendingDl;
        _pendingDl = null;
        _loadingLocation = -1;
        _loading = false;
        panoramaMaterial.SetTexture("_MainTex", result.Result);
        panoramaMaterial.SetFloat("_Yaw", (_wantLocation * 0.6180339f) % 1f);
        panoramaMaterial.SetColor("_Tint", Color.white);
        UpdateTexts();
    }

    public override void OnImageLoadError(IVRCImageDownload result)
    {
        if (_wantLocation < 0 || result.Url == null || result.Url.Get() != urls[_wantLocation].Get()) return;
        _loadingLocation = -1;
        if (_pendingDl != null) { _pendingDl.Dispose(); _pendingDl = null; }
        Debug.LogWarning("[GeoQuest] image failed: " + result.ErrorMessage);
        if (_retries++ < 2) { SendCustomEventDelayedSeconds(nameof(_DoDownload), 1f); return; }
        _loading = false;
        _loadFailed = true;
        UpdateTexts();
    }

    // ================================================================ text

    private void UpdateTexts()
    {
        if (statusText != null) statusText.text = StatusLine();
        if (infoText != null) infoText.text = InfoLine();
        if (scoreboardText != null) scoreboardText.text = Scoreboard();
    }

    private string StatusLine()
    {
        int count = urls == null ? 0 : urls.Length;
        if (count == 0) return "<color=#ff8080>No places loaded.</color>\nRun GeoQuest > Build World in Unity.";
        if (phase == PHASE_LOBBY || phase == PHASE_OVER)
        {
            string head = phase == PHASE_OVER ? "<b>Game over!</b>" : "<b>GEOQUEST</b>";
            return head + "\n" + count.ToString("N0") + " places  |  Rounds: " + totalRounds + "  |  Timer: " +
                   (roundSeconds == 0 ? "off" : roundSeconds + "s") + "\nYour best: " + GetBest().ToString("N0");
        }
        string s = "Round " + round + " / " + totalRounds;
        if (phase == PHASE_GUESS)
        {
            if (deadline != 0)
            {
                int left = Mathf.Max(0, (deadline - Now()) / 1000);
                s += "   <b>" + (left / 60) + ":" + (left % 60).ToString("00") + "</b>";
            }
            s += "   Guessed " + CountGuessed() + "/" + VRCPlayerApi.GetPlayerCount();
            if (_loading) s += "\n<color=#ffd060>Loading photo...</color>";
            else if (_loadFailed) s += "\n<color=#ff8080>Photo failed to load. Enable 'Allow Untrusted URLs' or press Skip.</color>";
            else s += "\nLook around, then point at the map and pull the trigger / click.";
        }
        else
        {
            int left = Mathf.Max(0, (deadline - Now()) / 1000);
            s += "   next round in " + left + "s";
        }
        return s;
    }

    private int CountGuessed()
    {
        int loc = CurrentLocation();
        int n = VRCPlayerApi.GetPlayerCount();
        _players = VRCPlayerApi.GetPlayers(_players);
        int c = 0;
        for (int i = 0; i < n; i++)
        {
            PlayerGuess pg = GuessOf(_players[i]);
            if (pg != null && pg.HasGuess(gameId, round, loc)) c++;
        }
        return c;
    }

    private string InfoLine()
    {
        int loc = CurrentLocation();
        PlayerGuess g = LocalGuess();
        if (phase == PHASE_GUESS)
        {
            if (g != null && g.HasGuess(gameId, round, loc)) return "Guess locked in! Waiting for others...";
            if (map != null && map.hasPin) return "Pin placed. Press <b>GUESS</b> to lock it in.";
            return "";
        }
        if (phase == PHASE_REVEAL && loc >= 0)
        {
            string s = "<b>" + titles[loc] + "</b>";
            if (countries[loc] != "") s += "\n" + countries[loc];
            if (g != null && g.HasGuess(gameId, round, loc))
                s += "\nYou were " + FormatKm(g.distanceKm) + " away: <b>+" + g.roundScore.ToString("N0") + "</b>";
            else s += "\nYou didn't guess: +0";
            s += "\n<size=60%>Photo: " + credits[loc] + "</size>";
            return s;
        }
        return "";
    }

    private string Scoreboard()
    {
        int n = VRCPlayerApi.GetPlayerCount();
        _players = VRCPlayerApi.GetPlayers(_players);
        int loc = CurrentLocation();
        PlayerGuess[] gs = new PlayerGuess[n];
        int[] totals = new int[n];
        int[] order = new int[n];
        for (int i = 0; i < n; i++)
        {
            gs[i] = GuessOf(_players[i]);
            totals[i] = (gs[i] != null && gs[i].gameId == gameId) ? gs[i].totalScore : 0;
            order[i] = i;
        }
        for (int i = 1; i < n; i++)  // insertion sort, highest first
        {
            int k = order[i], j = i - 1;
            while (j >= 0 && totals[order[j]] < totals[k]) { order[j + 1] = order[j]; j--; }
            order[j + 1] = k;
        }

        int pinSlot = 0;
        string s = phase == PHASE_OVER ? "<b>FINAL SCORES</b>\n" : "<b>SCORES</b>\n";
        for (int r = 0; r < n; r++)
        {
            int i = order[r];
            VRCPlayerApi p = _players[i];
            if (!Utilities.IsValid(p)) continue;
            string name = p.displayName;
            if (name.Length > 16) name = name.Substring(0, 16);
            string line = (r + 1) + ". " + name + "  " + totals[i].ToString("N0");
            bool guessed = gs[i] != null && loc >= 0 && gs[i].HasGuess(gameId, round, loc);
            if (phase == PHASE_GUESS && guessed) line += "  <color=#80ff80>(guessed)</color>";
            if (phase == PHASE_REVEAL && guessed)
            {
                line += "  <color=#ffe080>+" + gs[i].roundScore.ToString("N0") + "</color> <size=70%>(" + FormatKm(gs[i].distanceKm) + ")</size>";
                if (!p.isLocal && map != null && pinSlot < map.OtherPinCount())
                {
                    map.SetOtherPin(pinSlot, true, gs[i].lat, gs[i].lon, name);
                    pinSlot++;
                }
            }
            if (p.isLocal) line = "<color=#80d0ff>" + line + "</color>";
            if (r < 14) s += line + "\n";
        }
        if (map != null)
        {
            for (int k = pinSlot; k < map.OtherPinCount(); k++) map.SetOtherPin(k, false, 0f, 0f, "");
            map.RefreshPins();
        }
        return s;
    }
}
