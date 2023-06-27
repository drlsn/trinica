﻿using CardGame.Entities.Users;
using Corelibs.Basic.DDD;

namespace CardGame.Entities.Gameplay;

public record GameId(string Value) : EntityId(Value);

public class Game : Entity<GameId>
{
    public Player[] Players { get; private set; }
    public FieldDeck CommonPool { get; private set; }

    public UserId[] CurrentRoundMoveOrder { get; private set; }

    public Game(
        GameId id,
        Player[] players) : base(id)
    {
        Players = players;
    }

    public void TakeCardsToCommonPool(Random random)
    {
        CommonPool = Players.ShuffleAndGetHalfCards(random);
    }

    public void DrawCardsToHand()
    {
        var deckRandomHalf = Players.Select(player =>
        {
            // player.Deck.Shuffle(random);
            return default(EntireDeck);// player.Deck.TakeHalf(random);
        });

        // CommonPool = deckRandomHalf;

        // player.Cards.TakeRandomHalf(random)
        // player.Cards.TakeRandomHalf(random)
    }

    public void CalculateRoundPlayerOrder()
    {
        var deckRandomHalf = Players.Select(player =>
        {
            // player.GetOverallSpeed();
            return default(EntireDeck);// player.Deck.TakeHalf(random);
        });

        // CommonPool = deckRandomHalf;

        // player.Cards.TakeRandomHalf(random)
        // player.Cards.TakeRandomHalf(random)
    }
}