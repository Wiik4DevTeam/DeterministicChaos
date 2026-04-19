using System;
using System.Collections.Generic;
using System.Linq;

namespace DeterministicChaos.Content.Systems.Cards
{
    public enum PokerHandRank
    {
        HighCard = 0,
        Pair = 1,
        TwoPair = 2,
        ThreeOfAKind = 3,
        Straight = 4,
        Flush = 5,
        FullHouse = 6,
        FourOfAKind = 7,
        StraightFlush = 8,
        RoyalFlush = 9
    }

    public readonly struct PokerHandResult
    {
        public PokerHandResult(PokerHandRank rank, int[] usedCardIndices)
        {
            Rank = rank;
            UsedCardIndices = usedCardIndices;
        }

        public PokerHandRank Rank { get; }
        public int[] UsedCardIndices { get; }

        public float DamageMultiplier => Rank switch
        {
            PokerHandRank.HighCard => 0.55f,
            PokerHandRank.Pair => 0.9f,
            PokerHandRank.TwoPair => 1.2f,
            PokerHandRank.ThreeOfAKind => 1.55f,
            PokerHandRank.Straight => 1.9f,
            PokerHandRank.Flush => 2.2f,
            PokerHandRank.FullHouse => 2.7f,
            PokerHandRank.FourOfAKind => 3.2f,
            PokerHandRank.StraightFlush => 4f,
            PokerHandRank.RoyalFlush => 5f,
            _ => 1f
        };

        public string DisplayName => Rank switch
        {
            PokerHandRank.HighCard => "High Card",
            PokerHandRank.Pair => "Pair",
            PokerHandRank.TwoPair => "Two Pair",
            PokerHandRank.ThreeOfAKind => "Three of a Kind",
            PokerHandRank.Straight => "Straight",
            PokerHandRank.Flush => "Flush",
            PokerHandRank.FullHouse => "Full House",
            PokerHandRank.FourOfAKind => "Four of a Kind",
            PokerHandRank.StraightFlush => "Straight Flush",
            PokerHandRank.RoyalFlush => "Royal Flush",
            _ => "Hand"
        };
    }

    public static class PokerHandEvaluator
    {
        public static PokerHandResult Evaluate(IReadOnlyList<PlayingCard> cards)
        {
            if (cards == null || cards.Count == 0)
                return new PokerHandResult(PokerHandRank.HighCard, Array.Empty<int>());

            var cardsWithValues = cards
                .Select((card, index) => new CardValue(index, card, GetRankValue(card.Rank)))
                .ToList();

            Dictionary<int, List<int>> rankGroups = cardsWithValues
                .GroupBy(card => card.Value)
                .ToDictionary(group => group.Key, group => group.Select(card => card.Index).ToList());

            Dictionary<CardSuit, List<int>> suitGroups = cardsWithValues
                .GroupBy(card => card.Card.Suit)
                .ToDictionary(group => group.Key, group => group.Select(card => card.Index).ToList());

            bool isFlush = suitGroups.Any(group => group.Value.Count == 5);
            bool isStraight = TryGetStraight(cardsWithValues, out _);

            if (isFlush && isStraight)
            {
                bool isRoyal = cardsWithValues.Select(card => card.Value).OrderBy(value => value).SequenceEqual(new[] { 10, 11, 12, 13, 14 });
                return new PokerHandResult(isRoyal ? PokerHandRank.RoyalFlush : PokerHandRank.StraightFlush, cardsWithValues.Select(card => card.Index).ToArray());
            }

            var orderedGroups = rankGroups
                .OrderByDescending(group => group.Value.Count)
                .ThenByDescending(group => group.Key)
                .ToList();

            if (orderedGroups[0].Value.Count == 4)
                return new PokerHandResult(PokerHandRank.FourOfAKind, orderedGroups[0].Value.ToArray());

            if (orderedGroups[0].Value.Count == 3 && orderedGroups.Count > 1 && orderedGroups[1].Value.Count == 2)
                return new PokerHandResult(PokerHandRank.FullHouse, cardsWithValues.Select(card => card.Index).ToArray());

            if (isFlush)
                return new PokerHandResult(PokerHandRank.Flush, cardsWithValues.Select(card => card.Index).ToArray());

            if (isStraight)
                return new PokerHandResult(PokerHandRank.Straight, cardsWithValues.Select(card => card.Index).ToArray());

            if (orderedGroups[0].Value.Count == 3)
                return new PokerHandResult(PokerHandRank.ThreeOfAKind, orderedGroups[0].Value.ToArray());

            if (orderedGroups[0].Value.Count == 2 && orderedGroups.Count > 1 && orderedGroups[1].Value.Count == 2)
            {
                int[] pairCards = orderedGroups
                    .Where(group => group.Value.Count == 2)
                    .Take(2)
                    .SelectMany(group => group.Value)
                    .ToArray();
                return new PokerHandResult(PokerHandRank.TwoPair, pairCards);
            }

            if (orderedGroups[0].Value.Count == 2)
                return new PokerHandResult(PokerHandRank.Pair, orderedGroups[0].Value.ToArray());

            CardValue highCard = cardsWithValues.OrderByDescending(card => card.Value).First();
            return new PokerHandResult(PokerHandRank.HighCard, new[] { highCard.Index });
        }

        public static int GetRankValue(CardRank rank)
        {
            return rank == CardRank.Ace ? 14 : (int)rank + 1;
        }

        private static bool TryGetStraight(List<CardValue> cards, out int highValue)
        {
            highValue = 0;

            int[] values = cards.Select(card => card.Value).Distinct().OrderBy(value => value).ToArray();
            if (values.Length != 5)
                return false;

            bool standardStraight = true;
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] != values[i - 1] + 1)
                {
                    standardStraight = false;
                    break;
                }
            }

            if (standardStraight)
            {
                highValue = values[^1];
                return true;
            }

            bool wheelStraight = values.SequenceEqual(new[] { 2, 3, 4, 5, 14 });
            if (wheelStraight)
            {
                highValue = 5;
                return true;
            }

            return false;
        }

        private readonly record struct CardValue(int Index, PlayingCard Card, int Value);
    }
}