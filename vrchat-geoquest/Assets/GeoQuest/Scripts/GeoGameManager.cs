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
    private const string UNITS_KEY = "geoquest.miles";

    [Header("Location table (written by GeoQuest > Build World)")]
    public VRCUrl[] urls;
    public float[] lats;
    public float[] lons;
    public string[] titles;
    public string[] countries;
    public string[] credits;
    public int[] kinds;                 // 0 = 360° panorama, 1 = flat photo
    public int[] locCounty;             // county index of each place (-1 = none)
    public float[] townKm;              // 0 = inside the town named in titles[], >0 = km to it, <0 = titles[] is a full label

    [Header("County picker (written by Build World)")]
    public string[] countyNames;
    public byte[] countyGrid;           // gridW x gridH county indices over the map area, row 0 = south, 255 = not NC
    public int gridW, gridH;
    [Tooltip("128x1 readable texture: pixel i is white when county i is in play. The map shader greys out the rest.")]
    public Texture2D selectionTexture;
    public GeoCountyMenu countyMenu;

    [Header("Scene")]
    public Material panoramaMaterial;
    public Texture lobbyPanorama;
    [Tooltip("Big screen that shows flat (non-360) photos.")]
    public Transform photoScreen;
    public Material screenMaterial;
    public float screenHeight = 4.6f;
    public float screenMaxWidth = 9.2f;
    [Tooltip("Big floor map (unscaled root, local x = east, z = north) and the beam that marks the answer on it.")]
    public Transform floorMap;
    public float floorMapWidth = 6f, floorMapHeight = 3f;
    public Transform floorAnswerBeam;
    [Tooltip("Only players within this distance of the game area take part (people in the hangout don't hold up rounds).")]
    public Transform gameArea;
    public float gameAreaRadius = 60f;
    public MapBoard map;
    public PlayerGuess guessTemplate;
    public TextMeshPro statusText;
    public TextMeshPro scoreboardText;
    public TextMeshPro infoText;
    public GameObject[] lobbyObjects;   // visible in lobby / game over
    public GameObject[] guessObjects;   // visible while guessing
    public GameObject[] revealObjects;  // visible during reveal
    public float revealSeconds = 25f;
    [Tooltip("Points = 5000 * e^(-km / this). ~90 for North Carolina, ~1500 for the whole world.")]
    public float scoreScaleKm = 90f;
    public string gameTitle = "GEOQUEST: NORTH CAROLINA";

    [UdonSynced] public int phase;
    [UdonSynced] public int gameId;
    [UdonSynced] public int round;
    [UdonSynced] public int totalRounds = 5;
    [UdonSynced] public int roundSeconds = 120;
    [UdonSynced] public int deadline;
    [UdonSynced] public int photoMode;   // 0 = mixed, 1 = 360° only, 2 = flat only
    [UdonSynced] public int[] countyMask = { -1, -1, -1, -1 };   // bit i = county i is in play
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
    private int _panoCount, _flatCount;
    private string _lastError = "";
    private int _recentHead;
    private int[] _cntPano, _cntFlat;      // places per county
    private int _nCounties;
    private bool _miles;
    private int _m0 = 1, _m1 = 1, _m2 = 1, _m3 = 1;   // selection last painted (forces first paint)
    private int[] _pool;
    private int _poolSize, _poolGame = -1, _poolRound = -1;
    private Color[] _selPixels;

    void Start()
    {
        for (int i = 0; i < _recent.Length; i++) _recent[i] = -1;
        int n = urls == null ? 0 : urls.Length;
        for (int i = 0; i < n; i++)
        {
            if (IsFlat(i)) _flatCount++;
            else _panoCount++;
        }
        _nCounties = countyNames == null ? 0 : countyNames.Length;
        _cntPano = new int[_nCounties + 1];
        _cntFlat = new int[_nCounties + 1];
        for (int i = 0; i < n; i++)
        {
            int c = CountyOf(i);
            if (c < 0) c = _nCounties;   // last slot: places outside any county outline
            if (IsFlat(i)) _cntFlat[c]++;
            else _cntPano[c]++;
        }
        if (photoScreen != null) photoScreen.gameObject.SetActive(false);
        ApplyState();
    }

    // ================================================================ counties

    private int CountyOf(int loc)
    {
        return (locCounty != null && loc < locCounty.Length) ? locCounty[loc] : -1;
    }

    public bool CountyOn(int c)
    {
        return c >= 0 && c < _nCounties && (countyMask[c >> 5] & (1 << (c & 31))) != 0;
    }

    public int SelectedCount()
    {
        int k = 0;
        for (int c = 0; c < _nCounties; c++) if (CountyOn(c)) k++;
        return k;
    }

    private bool AllCountiesOn() { return _nCounties == 0 || SelectedCount() == _nCounties; }

    /// <summary>County index under a map position, or -1.</summary>
    public int CountyAt(float lat, float lon)
    {
        if (countyGrid == null || gridW <= 0 || map == null) return -1;
        int gx = Mathf.FloorToInt((lon - map.minLon) / (map.maxLon - map.minLon) * gridW);
        int gy = Mathf.FloorToInt((lat - map.minLat) / (map.maxLat - map.minLat) * gridH);
        if (gx < 0 || gy < 0 || gx >= gridW || gy >= gridH) return -1;
        int v = countyGrid[gy * gridW + gx];
        return v < _nCounties ? v : -1;
    }

    public bool CanPickCounty() { return (phase == PHASE_LOBBY || phase == PHASE_OVER) && _nCounties > 0; }

    /// <summary>Lobby: switch one county on or off (from the county menu).</summary>
    public void ToggleCounty(int c)
    {
        if (!CanPickCounty() || c < 0 || c >= _nCounties) return;
        TakeOwner();
        int[] m = { countyMask[0], countyMask[1], countyMask[2], countyMask[3] };
        m[c >> 5] = m[c >> 5] ^ (1 << (c & 31));
        countyMask = m;
        Commit();
    }

    public void AllCounties()
    {
        if (!CanPickCounty()) return;
        TakeOwner();
        countyMask = new int[] { -1, -1, -1, -1 };
        Commit();
    }

    public void ClearCounties()
    {
        if (!CanPickCounty()) return;
        TakeOwner();
        countyMask = new int[] { 0, 0, 0, 0 };
        Commit();
    }

    public int PlacesIn(int c)
    {
        if (photoMode == 1) return _cntPano[c];
        if (photoMode == 2) return _cntFlat[c];
        return _cntPano[c] + _cntFlat[c];
    }

    /// <summary>How many places the current county + photo-type choice leaves.</summary>
    private int PlacesInPlay()
    {
        int k = 0;
        for (int c = 0; c < _nCounties; c++) if (CountyOn(c)) k += PlacesIn(c);
        if (AllCountiesOn()) k += PlacesIn(_nCounties);
        return k;
    }

    private void PaintSelection()
    {
        if (countyMask[0] == _m0 && countyMask[1] == _m1 && countyMask[2] == _m2 && countyMask[3] == _m3 && _selPixels != null) return;
        _m0 = countyMask[0]; _m1 = countyMask[1]; _m2 = countyMask[2]; _m3 = countyMask[3];
        if (selectionTexture == null) return;
        if (_selPixels == null) _selPixels = new Color[selectionTexture.width];
        for (int i = 0; i < _selPixels.Length; i++) _selPixels[i] = CountyOn(i) ? Color.white : Color.black;
        selectionTexture.SetPixels(_selPixels);
        selectionTexture.Apply();
    }

    private bool Allowed(int loc)
    {
        if (photoMode == 1 && IsFlat(loc)) return false;
        if (photoMode == 2 && !IsFlat(loc)) return false;
        int c = CountyOf(loc);
        return c < 0 ? AllCountiesOn() : CountyOn(c);
    }

    /// <summary>List every place the current settings allow (done once per game by whoever runs it).</summary>
    private void BuildPool()
    {
        int n = urls.Length;
        if (_pool == null) _pool = new int[n];
        _poolSize = 0;
        for (int i = 0; i < n; i++) if (Allowed(i)) _pool[_poolSize++] = i;
        _poolGame = gameId;
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

    private bool InGame(VRCPlayerApi p)
    {
        if (gameArea == null) return true;
        return (p.GetPosition() - gameArea.position).sqrMagnitude < gameAreaRadius * gameAreaRadius;
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

    public bool IsFlat(int loc)
    {
        return kinds != null && loc >= 0 && loc < kinds.Length && kinds[loc] == 1;
    }

    private int PickLocation()
    {
        if (_poolGame != gameId || _pool == null) BuildPool();   // e.g. the host changed mid-game
        if (_poolSize == 0) return Random.Range(0, urls.Length);
        int c = _pool[0];
        for (int attempt = 0; attempt < 60; attempt++)
        {
            c = _pool[Random.Range(0, _poolSize)];
            bool used = false;
            for (int r = 0; r < round - 1 && !used; r++) used = roundLocations[r] == c;
            // Avoid places seen in recent games too, as long as there are plenty to choose from.
            if (attempt < 30 && _poolSize > 250) for (int i = 0; i < _recent.Length && !used; i++) used = _recent[i] == c;
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
        if (km < 0.05f) return 5000;
        return Mathf.RoundToInt(5000f * Mathf.Exp(-km / scoreScaleKm));
    }

    /// <summary>A distance in the local player's units (km or miles).</summary>
    private string FormatKm(float km)
    {
        if (_miles)
        {
            float mi = km * 0.621371f;
            if (mi < 0.2f) return Mathf.RoundToInt(km * 3280.84f).ToString("N0") + " ft";
            if (mi < 100f) return mi.ToString("0.0") + " mi";
            return Mathf.RoundToInt(mi).ToString("N0") + " mi";
        }
        if (km < 1f) return Mathf.RoundToInt(km * 1000f) + " m";
        if (km < 100f) return km.ToString("0.0") + " km";
        return Mathf.RoundToInt(km).ToString("N0") + " km";
    }

    /// <summary>Units button: km or miles. Your own choice, remembered between visits.</summary>
    public void ToggleUnits()
    {
        _miles = !_miles;
        PlayerData.SetBool(UNITS_KEY, _miles);
        UpdateTexts();
    }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        bool mi;
        if (PlayerData.TryGetBool(player, UNITS_KEY, out mi)) _miles = mi;
        UpdateTexts();
    }

    /// <summary>"Clayton" when inside the town limits, otherwise "2.1 mi from Kenly".</summary>
    private string PlaceName(int loc)
    {
        float d = (townKm != null && loc < townKm.Length) ? townKm[loc] : -1f;
        if (d < 0f || titles[loc] == "") return titles[loc];   // older place lists carry a ready-made title
        if (d < 0.05f) return titles[loc];
        return FormatKm(d) + " from " + titles[loc];
    }

    // ================================================================ buttons (anyone)

    public void StartGame()
    {
        if (phase == PHASE_GUESS || phase == PHASE_REVEAL) return;
        if (urls == null || urls.Length == 0) return;
        if (PlacesInPlay() == 0) return;   // the status board says why
        TakeOwner();
        gameId++;
        BuildPool();
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

    public void CycleMode()
    {
        if (phase == PHASE_GUESS || phase == PHASE_REVEAL) return;
        TakeOwner();
        photoMode = (photoMode + 1) % 3;
        Commit();   // also refreshes the county menu's place counts
    }

    private string ModeName()
    {
        if (photoMode == 1) return "360° only";
        if (photoMode == 2) return "Flat only";
        return "Mixed";
    }

    private string CountyChoice()
    {
        int k = SelectedCount();
        if (_nCounties == 0 || k == _nCounties) return "All " + _nCounties;
        if (k == 0) return "<color=#ff8080>none picked</color>";
        if (k > 4) return k + " picked";
        string s = "";
        for (int c = 0; c < _nCounties; c++)
            if (CountyOn(c)) s += (s == "" ? "" : ", ") + countyNames[c];
        return s;
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

        PaintSelection();
        if (countyMenu != null) countyMenu.Refresh();
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
            PlaceFloorBeam(lats[loc], lons[loc]);
        }
        else if (floorAnswerBeam != null) floorAnswerBeam.gameObject.SetActive(false);
        if (newStage && phase == PHASE_OVER && g != null) SaveBest(g.totalScore);

        _seenPhase = phase;
        _seenRound = round;
        SetActiveAll(lobbyObjects, phase == PHASE_LOBBY || phase == PHASE_OVER);
        SetActiveAll(guessObjects, phase == PHASE_GUESS);
        SetActiveAll(revealObjects, phase == PHASE_REVEAL);
        UpdateTexts();
    }

    private void PlaceFloorBeam(float lat, float lon)
    {
        if (floorAnswerBeam == null || floorMap == null || map == null) return;
        float u = (lon - map.minLon) / (map.maxLon - map.minLon) - 0.5f;
        float v = (lat - map.minLat) / (map.maxLat - map.minLat) - 0.5f;
        floorAnswerBeam.position = floorMap.TransformPoint(new Vector3(u * floorMapWidth, 0f, v * floorMapHeight));
        floorAnswerBeam.gameObject.SetActive(true);
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
            if (!Utilities.IsValid(_players[i]) || !InGame(_players[i])) continue;
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
            ShowBackdrop(1f);
            if (photoScreen != null) photoScreen.gameObject.SetActive(false);
            return;
        }
        _loading = true;
        if (photoScreen != null) photoScreen.gameObject.SetActive(false);
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
        info.GenerateMipMaps = IsFlat(loc);  // the flat screen is seen from a distance
        info.WrapModeU = TextureWrapMode.Repeat;
        info.WrapModeV = TextureWrapMode.Clamp;
        info.FilterMode = FilterMode.Bilinear;
        _loadingLocation = loc;
        _lastDownload = Time.time;
        Debug.Log("[GeoQuest] loading photo " + loc + ": " + urls[loc].Get());
        _pendingDl.DownloadImage(urls[loc], null, (IUdonEventReceiver)this, info);
        UpdateTexts();
    }

    public override void OnImageLoadSuccess(IVRCImageDownload result)
    {
        // Only one download is in flight at a time (older ones are disposed), so any success
        // that arrives while we're loading is the photo we asked for.
        if (_wantLocation < 0 || !_loading) return;
        Debug.Log("[GeoQuest] photo loaded: " + urls[_wantLocation].Get());
        if (_shownDl != null && _shownDl != _pendingDl) _shownDl.Dispose();
        _shownDl = _pendingDl;
        _pendingDl = null;
        _loadingLocation = -1;
        _loading = false;
        Texture2D tex = result.Result;
        if (IsFlat(_wantLocation) && photoScreen != null)
        {
            // Flat photo: dim lobby sky behind, photo on the big screen sized to its aspect.
            ShowBackdrop(0.35f);
            screenMaterial.SetTexture("_MainTex", tex);
            float aspect = tex.height > 0 ? (float)tex.width / tex.height : 1.5f;
            float h = screenHeight, w = h * aspect;
            if (w > screenMaxWidth) { w = screenMaxWidth; h = w / aspect; }
            photoScreen.localScale = new Vector3(w, h, 1f);
            photoScreen.gameObject.SetActive(true);
        }
        else
        {
            if (photoScreen != null) photoScreen.gameObject.SetActive(false);
            panoramaMaterial.SetTexture("_MainTex", tex);
            panoramaMaterial.SetFloat("_Yaw", (_wantLocation * 0.6180339f) % 1f);
            panoramaMaterial.SetColor("_Tint", Color.white);
        }
        UpdateTexts();
    }

    private void ShowBackdrop(float brightness)
    {
        panoramaMaterial.SetTexture("_MainTex", lobbyPanorama);
        panoramaMaterial.SetFloat("_Yaw", 0f);
        panoramaMaterial.SetColor("_Tint", new Color(brightness, brightness, brightness, 1f));
    }

    public override void OnImageLoadError(IVRCImageDownload result)
    {
        if (_wantLocation < 0 || !_loading) return;
        _loadingLocation = -1;
        if (_pendingDl != null) { _pendingDl.Dispose(); _pendingDl = null; }
        _lastError = result.ErrorMessage;
        Debug.LogWarning("[GeoQuest] photo failed (" + urls[_wantLocation].Get() + "): " + _lastError);
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
            string head = phase == PHASE_OVER ? "<b>Game over!</b>" : "<b>" + gameTitle + "</b>";
            int inPlay = PlacesInPlay();
            string places = inPlay > 0 ? inPlay.ToString("N0") + " places"
                : "<color=#ff8080>0 places: pick more counties or change Photo type</color>";
            return head + "\nCounties: " + CountyChoice() + "  |  Photos: " + ModeName() + "\n" + places +
                   "\nRounds: " + totalRounds + "  |  Timer: " + (roundSeconds == 0 ? "off" : roundSeconds + "s") +
                   "  |  Units: " + (_miles ? "miles" : "km") + "  |  Best: " + GetBest().ToString("N0");
        }
        string s = "Round " + round + " / " + totalRounds;
        if (phase == PHASE_GUESS)
        {
            if (deadline != 0)
            {
                int left = Mathf.Max(0, (deadline - Now()) / 1000);
                s += "   <b>" + (left / 60) + ":" + (left % 60).ToString("00") + "</b>";
            }
            s += "   Guessed " + CountGuessed() + "/" + CountInGame();
            if (_loading) s += "\n<color=#ffd060>Loading photo...</color>";
            else if (_loadFailed) s += "\n<color=#ff8080>Photo failed to load (" + _lastError + "). Press Skip photo.</color>";
            else if (IsFlat(CurrentLocation())) s += "\nStudy the photo, then point at the map and pull the trigger / click.";
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
            if (!Utilities.IsValid(_players[i]) || !InGame(_players[i])) continue;
            PlayerGuess pg = GuessOf(_players[i]);
            if (pg != null && pg.HasGuess(gameId, round, loc)) c++;
        }
        return c;
    }

    private int CountInGame()
    {
        int n = VRCPlayerApi.GetPlayerCount();
        _players = VRCPlayerApi.GetPlayers(_players);
        int c = 0;
        for (int i = 0; i < n; i++) if (Utilities.IsValid(_players[i]) && InGame(_players[i])) c++;
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
        if (CanPickCounty()) return "Press <b>Counties</b> under the map to choose which counties are in play.";
        if (phase == PHASE_REVEAL && loc >= 0)
        {
            string s = "<b>" + PlaceName(loc) + "</b>";
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

        int pinSlot = 0, rank = 0;
        string s = phase == PHASE_OVER ? "<b>FINAL SCORES</b>\n" : "<b>SCORES</b>\n";
        for (int r = 0; r < n; r++)
        {
            int i = order[r];
            VRCPlayerApi p = _players[i];
            if (!Utilities.IsValid(p) || !InGame(p)) continue;  // people in the hangout aren't playing
            rank++;
            string name = p.displayName;
            if (name.Length > 16) name = name.Substring(0, 16);
            string line = rank + ". " + name + "  " + totals[i].ToString("N0");
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
            if (rank <= 14) s += line + "\n";
        }
        if (map != null)
        {
            for (int k = pinSlot; k < map.OtherPinCount(); k++) map.SetOtherPin(k, false, 0f, 0f, "");
            map.RefreshPins();
        }
        return s;
    }
}
