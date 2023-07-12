﻿using Corelibs.Basic.Collections;
using Corelibs.Basic.DDD;
using Trinica.Entities.Gameplay.Cards;
using Trinica.Entities.Shared;
using Trinica.Entities.Users;

namespace Trinica.Entities.Gameplay;

public record GameId(string Value) : EntityId(Value);

public class Game : Entity<GameId>, IAggregateRoot<GameId>
{
    public const string DefaultCollectionName = "games";

    public Player[] Players { get; private set; }
    public FieldDeck CommonPool { get; private set; }
    public ICard CenterCard { get; private set; }
    public int CenterCardRoundsAlive { get; private set; }
    public UserId[] CardsLayOrderPerPlayer { get; private set; }

    public RoundSettings RoundSettings { get; private set; } = new();

    public GameActionController ActionController { get; private set; }

    public Game(
        GameId id,
        Player[] players) : base(id)
    {
        Players = players;
        ActionController = new(StartGame, Players.ToIds());
    }

    public bool StartGame(UserId playerId, Random random = null)
    {
        if (!ActionController.CanDo(StartGame, playerId))
            return false;

        return ActionController.SetPlayerDoneOrNextExpectedAction(playerId, TakeCardsToCommonPool);
    }

    public bool TakeCardsToCommonPool(Random random = null)
    {
        if (!ActionController.CanDo(TakeCardsToCommonPool))
            return false;

        CommonPool = Players.ShuffleAllAndTakeHalfCards(random ?? new());

        return ActionController.SetNextExpectedAction(TakeCardsToHand, Players.ToIds());
    }

    public bool StartRound(Random random)
    {
        if (!ActionController.CanDo(StartRound))
            return false;

        if (CenterCard is not null)
        {
            CenterCardRoundsAlive++;
            if (CenterCardRoundsAlive >= 6)
                return ActionController.SetNextExpectedAction(FinishGame);
        }

        _cardIndex = 0;
        _cards = Players.GetBattlingCardsBySpeed(random);
        _cards.ForEach(card =>
        {
            if (card is not ICombatCard combatCard)
                return;

            combatCard.Effects.ForEach(effect =>
                effect.OnRoundStart(combatCard, null, null, RoundSettings));
        });

        return ActionController.SetNextExpectedAction(PerformRound, PerformMove);
    }

    public bool TakeCardsToHand(UserId playerId, CardToTake[] cards, Random random = null)
    {
        if (!ActionController.CanDo(TakeCardsToHand, playerId))
            return false;

        var player = Players.OfId(playerId);
        cards.ForEach(card =>
        {
            if (card.Source == CardSource.CommonPool)
                player.AddCardToHand(CommonPool.TakeCard(random ?? new()));
            else
            if (card.Source == CardSource.Own)
                player.TakeCardToHand(random ?? new());
        });

        return ActionController.SetPlayerDoneOrNextExpectedAction(playerId, CalculateLayDownOrderPerPlayer);
    }

    public bool CalculateLayDownOrderPerPlayer()
    {
        if (!ActionController.CanDo(CalculateLayDownOrderPerPlayer))
            return false;

        CardsLayOrderPerPlayer = Players
            .GetPlayersOrderedByHeroSpeed()
            .ToIds();

        return ActionController.SetNextExpectedAction(LayCardsToBattle, CardsLayOrderPerPlayer, mustObeyOrder: true);
    }

    public bool LayCardsToBattle(UserId playerId, CardToLay[] cards)
    {
        if (!ActionController.CanDo(LayCardsToBattle, playerId))
            return false;

        var player = Players.OfId(playerId);
        if (CenterCard is not null)
        {
            var cardToCenter = cards.FirstOrDefault(c => c.ToCenter);
            if (cardToCenter is not null)
            {
                CenterCard = player.TakeCardFromHand(cardToCenter.SourceCardId);
                cards = cards.Except(cardToCenter).ToArray();
                CenterCardRoundsAlive = 0;
            }
        }

        if (!player.LayCardsToBattle(cards))
            return false;

        return ActionController.SetPlayerDoneOrNextExpectedAction(playerId, nameof(PlayDices), Players.ToIds());
    }

    public bool PlayDices(UserId playerId, Random random) =>
        PlayDices(playerId, () => random);

    public bool PlayDices(UserId playerId, Func<Random> getRandom)
    {
        if (!ActionController.CanDo(nameof(PlayDices), playerId))
            return false;

        var player = Players.OfId(playerId);
        player.PlayDices(getRandom);

        return ActionController.SetPlayerDoneOrNextExpectedAction(playerId, Players.ToIds(), ReplayDices, PassReplayDices);
    }

    public bool PassReplayDices(UserId playerId)
    {
        if (!ActionController.CanDo(PassReplayDices, playerId))
            return false;

        return ActionController.SetPlayerDoneOrNextExpectedAction(playerId, Players.ToIds(), AssignDiceToCard, ConfirmAssignDicesToCards);
    }

    public bool ReplayDices(UserId playerId, int n, Func<Random> getRandom)
    {
        if (!ActionController.CanDo(ReplayDices, playerId))
            return false;

        var player = Players.OfId(playerId);
        player.PlayDices(n, getRandom);

        return ActionController.SetPlayerDoneOrNextExpectedAction(playerId, Players.ToIds(), AssignDiceToCard, ConfirmAssignDicesToCards);
    }

    public bool AssignDiceToCard(UserId playerId, int diceIndex, CardId cardId)
    {
        if (!ActionController.CanDo(AssignDiceToCard, playerId))
            return false;

        var player = Players.OfId(playerId);
        if (!player.AssignDiceToCard(diceIndex, cardId))
            return false;

        return true;
    }

    public bool RemoveDiceFromCard(UserId playerId, CardId cardId)
    {
        if (!ActionController.CanDo(RemoveDiceFromCard, playerId))
            return false;

        var player = Players.OfId(playerId);
        player.RemoveDiceFromCard(cardId);

        return true;
    }

    public bool ConfirmAssignDicesToCards(UserId playerId)
    {
        return ActionController.SetPlayerDoneOrNextExpectedAction(playerId, Players.ToIds(), ChooseCardSkill, AssignCardTarget, RemoveCardTarget, ConfirmCardTargets);
    }

    public bool ChooseCardSkill(UserId playerId, CardId cardId, int skillIndex)
    {
        if (!ActionController.CanDo(ChooseCardSkill, playerId))
            return false;

        var player = Players.OfId(playerId);
        player.ChooseCardSkill(cardId, skillIndex);

        return true;
    }

    public bool AssignCardTarget(UserId playerId, CardId cardId, CardId targetCardId)
    {
        if (!ActionController.CanDo(AssignCardTarget, playerId))
            return false;

        var player = Players.OfId(playerId);
        player.AssignCardTarget(cardId, targetCardId);

        return true;
    }

    public bool RemoveCardTarget(UserId playerId, CardId cardId, CardId targetCardId)
    {
        if (!ActionController.CanDo(RemoveCardTarget, playerId))
            return false;

        var player = Players.OfId(playerId);
        player.RemoveCardTarget(cardId, targetCardId);

        return true;
    }

    public bool ConfirmCardTargets(UserId playerId)
    {
        if (!ActionController.CanDo(ConfirmCardTargets, playerId))
            return false;

        return ActionController.SetPlayerDoneOrNextExpectedAction(playerId, StartRound);
    }

    private ICard[] _cards;
    private int _cardIndex;
    public bool PerformMove(Random random)
    {
        if (!ActionController.CanDo(PerformMove))
            return false;

        var card = _cards[_cardIndex];
        _cardIndex++;

        var player = Players.GetPlayerWithCard(card.Id);
        var cardAssignment = player.CardAssignments[card.Id];
        var targetCards = _cards.Where(c => cardAssignment.TargetCardIds.Contains(c.Id)).Cast<ICombatCard>().ToArray();
        targetCards = targetCards.Where(c => !RoundSettings.NotAllowedAsTargetCards.ContainsKey(c.Id.Value)).ToArray();
        if (targetCards.IsEmpty())
            return true;

        var otherPlayers = Players.NotOfId(player.Id);
        var enemiesBattlingCards = otherPlayers.GetBattlingCards().OfType<ICombatCard>().ToArray();

        if (!RoundSettings.PrioritizedToAttackCards.IsNullOrEmpty())
        {
            var cardId = RoundSettings.PrioritizedToAttackCards.Values.Shuffle(random).First();
            targetCards = enemiesBattlingCards.Where(c => c.Id == cardId).ToArray();
        }

        if (card is not ICombatCard combatCard)
            return true;

        if (cardAssignment.DiceOutcome is null)
            return false;

        var moveType = cardAssignment.DiceOutcome.IsElement() ? MoveType.Skill : MoveType.Attack;

        // ----- use items! -----
        var cardWithItems = combatCard as ICardWithItems;
        if (cardWithItems is not null)
            cardWithItems.ItemCards.ForEach(itemCard =>
                combatCard.Statistics.Modify(itemCard.Statistics, itemCard.Id.Value));

        targetCards.ForEach(targetCard =>
        {
            var cardWithItems = targetCard as ICardWithItems;
            if (cardWithItems is not null)
                cardWithItems.ItemCards.ForEach(itemCard =>
                    targetCard.Statistics.Modify(itemCard.Statistics, itemCard.Id.Value));
        });

        // Attacker - BeforeMoveAtAll
        var moveAtAll = new Move()
        {
            Damage = CalculateDamage(combatCard, null, moveType, cardAssignment.SkillIndex),
            Type = moveType
        };
        combatCard.Effects.ForEach(effect =>
            effect.BeforeMoveAtAll(combatCard, new(targetCards, enemiesBattlingCards, null), moveAtAll));

        var movesAtSingle = new Dictionary<CardId, Move>();
        targetCards.ForEach(targetCard =>
        {
            var damage = CalculateDamage(combatCard, targetCard, moveType, cardAssignment.SkillIndex);
            movesAtSingle.Add(targetCard.Id, new Move()
            {
                Damage = moveAtAll.Damage,
                Type = moveType
            });
        });

        // Attacker - BeforeMoveAtSingleTarget
        combatCard.Effects.ForEach(effect =>
           targetCards.ForEach(targetCard =>
           {
               effect.BeforeMoveAtSingleTarget(combatCard, targetCard, movesAtSingle[targetCard.Id]);
           }));

        // Defender - BeforeReceive
        targetCards.ForEach(targetCard =>
        {
            targetCard.Effects.ForEach(effect =>
                effect.BeforeReceive(targetCard, new(combatCard, enemiesBattlingCards, null), movesAtSingle[targetCard.Id]));
        });

        // Update After Move Modified By Effects
        if (cardWithItems is not null && !moveAtAll.ItemsEnabled)
            cardWithItems.ItemCards.ForEach(itemCard =>
                combatCard.Statistics.RemoveAll(itemCard.Id.Value));

        // ------------------------
        // PERFORM ACTION!!!
        // ------------------------
        if (moveAtAll.MoveEnabled)
        {
            if (moveType is MoveType.Attack && 
                moveAtAll.AttackEnabled &&
                (card is HeroCard || card is UnitCard))
            {
                foreach (var targetCard in targetCards)
                {
                    var move = movesAtSingle[targetCard.Id];

                    var targetPlayer = Players.GetPlayerWithCard(targetCard.Id);
                    targetPlayer.InflictDamage(move.Damage, targetCard.Id);
                    if (targetPlayer.IsCardDead(targetCard))
                    {
                        if (targetCard is HeroCard)
                            return ActionController.SetNextExpectedAction(FinishGame);

                        if (CenterCard == targetCard)
                            CenterCardRoundsAlive = 0;
                    }
                }
            }
            else
            if (moveType is MoveType.Skill && moveAtAll.SkillsEnabled)
            {
                foreach (var targetCard in targetCards)
                {
                    var move = movesAtSingle[targetCard.Id];
                    if (!move.SkillsEnabled)
                        continue;

                    var targetPlayer = Players.GetPlayerWithCard(targetCard.Id);
                    if (combatCard.DoesPowerDamage(cardAssignment.SkillIndex))
                        targetPlayer.InflictDamage(move.Damage, targetCard.Id);

                    if (combatCard is SpellCard)
                        player.KillCard(combatCard.Id);

                    if (targetPlayer.IsCardDead(targetCard))
                    {
                        if (targetCard is HeroCard)
                            return ActionController.SetNextExpectedAction(FinishGame);

                        if (CenterCard == targetCard)
                            CenterCardRoundsAlive = 0;
                    }

                    if (move.EffectsEnabled)
                    {
                        var effects = combatCard.GetEffects(cardAssignment.SkillIndex);
                        targetCard.Effects.AddRange(effects);
                    }

                };
            }
        }

        // Attacker - AfterMoveAtAll
        combatCard.Effects.ForEach(effect =>
            effect.AfterMoveAtAll(combatCard, new(targetCards, enemiesBattlingCards, null), new()
            {
                Damage = 23,
                Type = moveType
            }));

        // Attacker - AfterMoveAtSingleTarget
        combatCard.Effects.ForEach(effect =>
           targetCards.ForEach(targetCard =>
           {
               var damage = CalculateDamage(combatCard, targetCard, moveType, cardAssignment.SkillIndex);
               effect.AfterMoveAtSingleTarget(combatCard, targetCard, new()
               {
                   Damage = damage,
                   Type = moveType
               });
           }));

        // Defender - AfterReceive
        targetCards.ForEach(targetCard =>
        {
            var damage = CalculateDamage(combatCard, targetCard, moveType, cardAssignment.SkillIndex);
            targetCard.Effects.ForEach(effect =>
                effect.AfterReceive(targetCard, new(combatCard, enemiesBattlingCards, null), new()
                {
                    Damage = 23,
                    Type = moveType
                }));
        });

        if (IsGameOver())
            return ActionController.SetNextExpectedAction(FinishGame);

        // ----- unuse items! -----
        if (cardWithItems is not null)
            cardWithItems.ItemCards.ForEach(itemCard =>
                combatCard.Statistics.RemoveAll(itemCard.Id.Value));

        if (!IsRoundOngoing())
            return ActionController.SetNextExpectedAction(FinishRound);

        return ActionController.SetNextExpectedAction(PerformMove);
    }

    public bool PerformRound(Random random)
    {
        if (!ActionController.CanDo(PerformRound))
            return false;

        _cards ??= Players.GetBattlingCardsBySpeed(random);

        while (IsRoundOngoing())
            if (!PerformMove(random))
                return false;

        return true;
    }

    public bool FinishRound(Random random)
    {
        if (!ActionController.CanDo(FinishRound))
            return false;

        _cards.ForEach(card =>
        {
            if (card is not ICombatCard combatCard)
                return;

            combatCard.Effects.ForEach(effect =>
                effect.OnRoundFinish(combatCard));
        });

        return ActionController.SetNextExpectedAction(TakeCardsToHand, Players.ToIds());
    }

    public bool IsGameOver() =>
        IsGameOverByHeroElimination() ||
        IsGameOverByCenterOccupied();

    public bool FinishGame(Random random)
    {
        if (!ActionController.CanDo(FinishGame))
            return false;

        return ActionController.SetNextExpectedAction("None");
    }

    public bool IsGameOverByHeroElimination() => 
        Players.Any(p => p.HeroCard is null) ||
        Players.Any(p => p.HeroCard.Statistics.HP.CalculatedValue <= 0);

    public bool IsGameOverByCenterOccupied() => CenterCardRoundsAlive >= 6;

    public bool IsDead(ICard card) => Players.FirstOrDefault(p => p.DeadDeck.Contains(card)) is not null;

    public bool CanDo(Delegate @delegate, UserId userId = null) => ActionController.CanDo(@delegate, userId);
    public bool CanDo(string type, UserId userId = null) => ActionController.CanDo(type, userId);

    public bool IsRoundOngoing() => 
        _cards is not null &&
        _cardIndex < _cards.Length &&
        !IsGameOver();

    public static int CalculateDamage(ICombatCard attacker, ICombatCard defender, MoveType moveType, int skillIndex)
    {
        if (attacker is SpellCard spellCard && moveType == MoveType.Skill)
            return spellCard.Damage;

        return moveType switch
        {
            MoveType.Skill => attacker.Statistics.Power.CalculatedValue /* + skill.Damage */,
            MoveType.Attack => attacker.Statistics.Attack.CalculatedValue,
            _ => 0
        };
    }
}
