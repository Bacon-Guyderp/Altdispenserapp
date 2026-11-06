using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// One copy per player (spawned by VRCPlayerObject). Owned by that player and
/// holds their guess and score for the current game.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PlayerGuess : UdonSharpBehaviour
{
    [UdonSynced] public int gameId = -1;
    [UdonSynced] public int round;
    [UdonSynced] public int location = -1;
    [UdonSynced] public float lat;
    [UdonSynced] public float lon;
    [UdonSynced] public float distanceKm;
    [UdonSynced] public int roundScore;
    [UdonSynced] public int totalScore;
    [UdonSynced] public int scoredRound;

    public bool HasGuess(int g, int r, int loc)
    {
        return gameId == g && round == r && location == loc;
    }

    /// <summary>Only call on the local player's own copy.</summary>
    public void ResetForGame(int g)
    {
        gameId = g;
        round = 0;
        location = -1;
        roundScore = 0;
        totalScore = 0;
        scoredRound = 0;
        distanceKm = 0;
        RequestSerialization();
    }

    public void Submit(int g, int r, int loc, float guessLat, float guessLon, float km, int score)
    {
        if (gameId != g) ResetForGame(g);
        round = r;
        location = loc;
        lat = guessLat;
        lon = guessLon;
        distanceKm = km;
        roundScore = score;
        RequestSerialization();
    }

    /// <summary>Called once at reveal time: banks this round's points.</summary>
    public void BankRound(int g, int r, int loc)
    {
        if (gameId != g) ResetForGame(g);
        if (scoredRound == r) return;
        scoredRound = r;
        if (HasGuess(g, r, loc)) totalScore += roundScore;
        RequestSerialization();
    }
}
