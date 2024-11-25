using System.Collections.Generic;
using PurrNet;
using PurrNet.Logging;
using PurrNet.StateMachine;

public enum CardValue
{
    None = 0,
    Ace = 1,
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13
}

public enum RankGuess
{
    None = 0,
    Ace = 1,
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13,
    Exact = 14
}

public struct PlayerMatchResultData
{
    public ulong steamId;
    public PlayerID playerId;
    public CardValue trueCardValue;
    public int guessingOrder;

    public RankGuess firstRankGuess;
    public RankGuess secondRankGuess;
    public CardValue cardValueGuess;

    public override string ToString()
    {
        return $"Player {playerId} (SteamId: {steamId})\n " +
               $"| Card: {trueCardValue}\n " +
               $"| First guess: {firstRankGuess}\n " +
               $"| Second guess: {secondRankGuess}\n " +
               $"| Exact guess: {cardValueGuess}";
    }
}


public class SomeBehaviour : StateNode<Dictionary<PlayerID, PlayerMatchResultData>>
{
    public override void Enter(Dictionary<PlayerID, PlayerMatchResultData> data, bool asServer)
    {
        base.Enter(data, asServer);
        
        PurrLogger.Log($"Entered state with {data.Count} players");
    }
}
