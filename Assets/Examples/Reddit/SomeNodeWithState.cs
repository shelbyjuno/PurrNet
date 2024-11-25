using System.Collections.Generic;
using PurrNet;
using PurrNet.StateMachine;
using UnityEngine;

public class SomeNodeWithState : StateNode
{
    public override void StateUpdate(bool asServer)
    {
        base.StateUpdate(asServer);

        if (Input.GetKeyDown(KeyCode.X))
        {
            var dic = new Dictionary<PlayerID, PlayerMatchResultData>
            {
                // fill dummy data
                {
                    new PlayerID(1, false), new PlayerMatchResultData
                    {
                        steamId = 1,
                        playerId = new PlayerID(1, false),
                        trueCardValue = CardValue.Ace,
                        guessingOrder = 1,
                        firstRankGuess = RankGuess.Ace,
                        secondRankGuess = RankGuess.Ace,
                        cardValueGuess = CardValue.Ace
                    }
                },
                {
                    new PlayerID(2, false), new PlayerMatchResultData
                    {
                        steamId = 2,
                        playerId = new PlayerID(2, false),
                        trueCardValue = CardValue.Two,
                        guessingOrder = 2,
                        firstRankGuess = RankGuess.Two,
                        secondRankGuess = RankGuess.Two,
                        cardValueGuess = CardValue.Two
                    }
                },
                { new PlayerID(3, false), new PlayerMatchResultData
                {
                    steamId = 3,
                    playerId = new PlayerID(3, false),
                    trueCardValue = CardValue.Three,
                    guessingOrder = 3,
                    firstRankGuess = RankGuess.Three,
                    secondRankGuess = RankGuess.Three,
                    cardValueGuess = CardValue.Three
                } }
            };

            machine.Next(dic);
        }
    }
}
