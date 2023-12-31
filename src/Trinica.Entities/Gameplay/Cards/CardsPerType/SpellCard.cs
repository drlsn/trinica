﻿using Trinica.Entities.Shared;
using Trinica.Entities.SpellCards;

namespace Trinica.Entities.Gameplay.Cards;

public class SpellCard : Card, ICard, ICombatCard, ICardWithElements
{
    public SpellCardId Id { get; private set; }

    public StatisticPointGroup Statistics { get; private set; } = new();

    public List<IEffect> Effects { get; private set; }

    public SpellCard(
       SpellCardId id,
       IEnumerable<IEffect> effects, 
       IEnumerable<Element> requiredElements,
       int damage = 0)
    {
        Id = id;
        Effects = effects.ToList();
        RequiredElements = requiredElements.ToArray();
        Damage = damage;
    }

    public SpellCard(
        SpellCardId id,
        string name,
        Race race,
        Class @class,
        Fraction fraction,
        IEnumerable<IEffect> effects,
        IEnumerable<Element> requiredElements,
        int damage = 0) : base(name, race, @class, fraction)
    {
        Id = id;
        Effects = effects.ToList();
        RequiredElements = requiredElements.ToArray();
        Damage = damage;
    }

    public Element[] RequiredElements { get; init; }

    CardId ICard.Id => new CardId(Id.Value);
    CardId ICombatCard.Id => new CardId(Id.Value);

    public bool DoesDamage => Damage > 0;
    public int Damage { get; init; }

    public bool DoesPowerDamage(int skillIndex) => DoesDamage;

    public IEffect[] GetEffects(int skillIndex) => Effects.ToArray();

    public override string ToString() => Id.Value;
}
