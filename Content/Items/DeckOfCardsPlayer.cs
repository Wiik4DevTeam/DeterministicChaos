using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;
using DeterministicChaos.Content.Systems.Cards;

namespace DeterministicChaos.Content.Items
{
    public class DeckOfCardsPlayer : ModPlayer
    {
        private const int CardSpawnCooldown = 60;
        private const int HandRevealDelay = 30;
        private const int GoodHandHardPityThreshold = 6;
        private const int GreatHandHardPityThreshold = 11;
        private const float DeckAttackDamageScale = 1.58f;
        private const int MaxDeckProjectileDamage = 50;
        private const float SuitOpeningDamageBonus = 1.35f;
        private const float SuitHitDecayStep = 0.16f;
        private const float SuitDamageFloor = 0.5f;

        private enum DeckHandState
        {
            Collecting,
            RevealPause,
            AwaitingTrigger
        }

        private sealed class DisplayCardState
        {
            public PlayingCard Card;
            public int Age;
            public bool Highlighted;
            public bool Hidden;
        }

        public bool deckEquipped;
        public bool deckVisualsEnabled;

        private readonly List<PlayingCard> currentHand = new();
        private readonly List<DisplayCardState> displayCards = new();
        private List<PlayingCard> queuedHand = new();
        private int spawnCooldown;
        private int handsSinceGoodHand;
        private int handsSinceGreatHand;
        private DeckHandState handState;
        private PokerHandResult pendingResult;
        private int revealTimer;
        private readonly int[] suitHitCounts = new int[CardDeckRenderer.RowCount];

        public IReadOnlyList<PlayingCard> CurrentHand => currentHand;

        public override void ResetEffects()
        {
            deckEquipped = false;
            deckVisualsEnabled = false;
        }

        public override void PostUpdate()
        {
            if (spawnCooldown > 0)
                spawnCooldown--;

            for (int i = 0; i < displayCards.Count; i++)
            {
                displayCards[i].Age++;
            }

            if (handState == DeckHandState.RevealPause)
            {
                revealTimer++;
                if (revealTimer >= HandRevealDelay)
                {
                    HighlightBestHand();
                    handState = DeckHandState.AwaitingTrigger;
                }
            }

            if (!deckEquipped && (currentHand.Count > 0 || queuedHand.Count > 0 || spawnCooldown > 0 || displayCards.Count > 0))
            {
                ClearHand();
            }
        }

        public override void UpdateDead()
        {
            ClearHand();
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
        {
            TryAddCardFromHit(target, damageDone);
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (proj.GetGlobalProjectile<DeckOfCardsGlobalProjectile>().spawnedByDeckOfCards)
                return;

            int type = proj.type;
            if (type == ModContent.ProjectileType<Projectiles.Friendly.FriendlyHeartProjectile>()
                || type == ModContent.ProjectileType<Projectiles.Friendly.FriendlyDiamondProjectile>()
                || type == ModContent.ProjectileType<Projectiles.Friendly.FriendlySpadeProjectile>()
                || type == ModContent.ProjectileType<Projectiles.Friendly.FriendlyClubProjectile>())
            {
                return;
            }

            TryAddCardFromHit(target, damageDone);
        }

        private void TryAddCardFromHit(NPC target, int damageDone)
        {
            if (!deckEquipped || Player.whoAmI != Main.myPlayer || target == null || !target.active || target.friendly || damageDone <= 0)
                return;

            if (handState == DeckHandState.AwaitingTrigger)
            {
                TriggerStoredHand(target, damageDone);
                return;
            }

            if (handState == DeckHandState.RevealPause)
                return;

            if (spawnCooldown > 0)
                return;

            if (queuedHand.Count == 0)
                queuedHand = GenerateQueuedHand();

            PlayingCard nextCard = queuedHand[currentHand.Count];
            currentHand.Add(nextCard);
            displayCards.Add(new DisplayCardState { Card = nextCard, Age = 0 });
            spawnCooldown = CardSpawnCooldown;

            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.15f, Volume = 0.3f }, Player.Center);

            if (currentHand.Count >= 5)
            {
                pendingResult = PokerHandEvaluator.Evaluate(currentHand);
                revealTimer = 0;
                handState = DeckHandState.RevealPause;
            }
        }

        private void HighlightBestHand()
        {
            HashSet<int> usedIndices = new HashSet<int>(pendingResult.UsedCardIndices);
            for (int i = 0; i < displayCards.Count; i++)
            {
                bool used = usedIndices.Contains(i);
                displayCards[i].Highlighted = used;
                displayCards[i].Hidden = !used;
                displayCards[i].Age = 0;
            }

            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.3f, Volume = 0.4f }, Player.Center);
        }

        private void TriggerStoredHand(NPC target, int damageDone)
        {
            ResetSuitDamageDecay();

            int usedCount = Math.Max(1, pendingResult.UsedCardIndices.Length);
            int scaledHitDamage = Math.Clamp(damageDone, 30, 110);
            float highHandBonus = GetVeryGoodHandDamageMultiplier(pendingResult.Rank);
            int perSuitDamage = Math.Max(1, (int)(scaledHitDamage * pendingResult.DamageMultiplier * DeckAttackDamageScale * highHandBonus / usedCount));

            ExecuteHandPattern(target, perSuitDamage);

            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.15f, Volume = 0.5f }, Player.Center);

            if (pendingResult.Rank >= PokerHandRank.Straight)
                handsSinceGoodHand = 0;
            else
                handsSinceGoodHand++;

            if (pendingResult.Rank >= PokerHandRank.FullHouse)
                handsSinceGreatHand = 0;
            else
                handsSinceGreatHand++;

            ClearHand();
            spawnCooldown = CardSpawnCooldown;
        }

        private void ExecuteHandPattern(NPC target, int perSuitDamage)
        {
            List<PlayingCard> usedCards = pendingResult.UsedCardIndices
                .Select(index => currentHand[index])
                .ToList();

            if (usedCards.Count == 0)
                return;

            Vector2 toTarget = (target.Center - Player.Center).SafeNormalize(Vector2.UnitX);
            Vector2 side = new Vector2(-toTarget.Y, toTarget.X);

            switch (pendingResult.Rank)
            {
                case PokerHandRank.HighCard:
                    SpawnSuitFromPlayer(usedCards[0], target.Center, perSuitDamage);
                    break;

                case PokerHandRank.Pair:
                    for (int i = 0; i < usedCards.Count; i++)
                    {
                        float lane = i == 0 ? -50f : 50f;
                        SpawnSuitFromPlayer(usedCards[i], target.Center + side * lane, perSuitDamage);
                    }
                    break;

                case PokerHandRank.TwoPair:
                    var pairCards = usedCards
                        .GroupBy(card => card.Rank)
                        .Select(group => group.First())
                        .Take(2)
                        .ToList();

                    for (int i = 0; i < pairCards.Count; i++)
                    {
                        float lane = i == 0 ? -70f : 70f;
                        Vector2 originOffset = side * (i == 0 ? -22f : 22f) + toTarget * -8f;
                        SpawnSuitFromPlayer(pairCards[i], target.Center + side * lane, (int)(perSuitDamage * 0.82f), originOffset);
                    }
                    break;

                case PokerHandRank.ThreeOfAKind:
                    for (int i = 0; i < usedCards.Count; i++)
                    {
                        float lane = (i - 1) * 65f;
                        SpawnSuitFromPlayer(usedCards[i], target.Center + side * lane, perSuitDamage);
                    }
                    SpawnSuitFromPlayer(usedCards[1], target.Center, (int)(perSuitDamage * 0.85f));
                    break;

                case PokerHandRank.Straight:
                    for (int i = 0; i < usedCards.Count; i++)
                    {
                        float lane = (i - 2) * 55f;
                        Vector2 originOffset = side * (i - 2) * 10f;
                        SpawnSuitFromPlayer(usedCards[i], target.Center + side * lane + toTarget * 20f, perSuitDamage, originOffset);
                    }
                    break;

                case PokerHandRank.Flush:
                    CardSuit flushSuit = usedCards[0].Suit;
                    for (int lane = -1; lane <= 1; lane++)
                    {
                        Vector2 originOffset = side * lane * 18f + toTarget * -14f;
                        Vector2 aim = target.Center + side * lane * 44f;
                        SpawnSuitFromPlayer(new PlayingCard(flushSuit, CardRank.Ace), aim, (int)(perSuitDamage * 0.68f), originOffset);
                    }
                    break;

                case PokerHandRank.FullHouse:
                    var grouped = usedCards
                        .GroupBy(card => card.Rank)
                        .OrderByDescending(group => group.Count())
                        .ToList();

                    List<PlayingCard> triples = grouped.First().ToList();
                    List<PlayingCard> pair = grouped.Last().ToList();

                    for (int i = 0; i < triples.Count; i++)
                    {
                        float lane = (i - 1) * 60f;
                        SpawnSuitFromPlayer(triples[i], target.Center + side * lane, perSuitDamage);
                    }

                    SpawnSuitFromPlayer(pair[0], target.Center + side * 95f + toTarget * 20f, (int)(perSuitDamage * 1.05f), side * 24f);
                    SpawnSuitFromPlayer(pair[1], target.Center - side * 95f + toTarget * 20f, (int)(perSuitDamage * 1.05f), -side * 24f);
                    break;

                case PokerHandRank.FourOfAKind:
                    for (int i = 0; i < usedCards.Count; i++)
                    {
                        Vector2 originOffset = i switch
                        {
                            0 => side * -34f + toTarget * -18f,
                            1 => side * 34f + toTarget * -18f,
                            2 => side * -34f + toTarget * 18f,
                            _ => side * 34f + toTarget * 18f,
                        };
                        SpawnSuitFromPlayer(usedCards[i], target.Center, (int)(perSuitDamage * 0.95f), originOffset);
                    }
                    break;

                case PokerHandRank.StraightFlush:
                    for (int wave = 0; wave < 1; wave++)
                    {
                        float direction = wave == 0 ? 1f : -1f;
                        for (int i = 0; i < usedCards.Count; i++)
                        {
                            float lane = (i - 2) * 46f * direction;
                            Vector2 originOffset = side * lane * 0.35f + toTarget * (wave == 0 ? -12f : 12f);
                            SpawnSuitFromPlayer(usedCards[i], target.Center + side * lane, (int)(perSuitDamage * 0.8f), originOffset);
                        }
                    }
                    break;

                case PokerHandRank.RoyalFlush:
                    for (int wave = 0; wave < 2; wave++)
                    {
                        float centerPush = (wave - 1) * 28f;
                        for (int i = 0; i < usedCards.Count; i++)
                        {
                            float lane = (i - 2) * 42f;
                            Vector2 originOffset = side * lane * 0.45f + toTarget * centerPush;
                            SpawnSuitFromPlayer(usedCards[i], target.Center + side * lane, (int)(perSuitDamage * 0.78f), originOffset);
                        }
                    }
                    break;

                default:
                    SpawnSuitFromPlayer(usedCards[0], target.Center, perSuitDamage);
                    break;
            }
        }

        private void SpawnSuitFromPlayer(PlayingCard card, Vector2 targetPos, int baseDamage, Vector2? originOffset = null)
        {
            // baseDir is computed from the unmodified Player.Center so close enemies don't break aim.
            Vector2 baseDir = (targetPos - Player.Center).SafeNormalize(Vector2.UnitX);
            Vector2 origin = Player.Center + (originOffset ?? Vector2.Zero);
            Vector2 toTarget = baseDir;
            Vector2 side = new Vector2(-toTarget.Y, toTarget.X);
            float spreadScale = GetSuitSpreadScale(card.Suit);
            float rotationOffset = GetSuitRotationOffset(card.Suit);

            Vector2 spreadAxis = side.RotatedBy(rotationOffset);
            Vector2 forwardAxis = toTarget.RotatedBy(rotationOffset * 0.35f);

            int suitCountInUsedCards = GetUsedSuitCount(card.Suit);
            int volleyCount = 1 + GetRankVolleyBonus(pendingResult.Rank) + (suitCountInUsedCards - 1) + GetSuitVolleyBonus(card.Suit);
            volleyCount = Math.Max(1, (int)Math.Round(volleyCount * 0.55f));
            volleyCount = Math.Min(volleyCount, card.Suit == CardSuit.Hearts ? 2 : 5);
            volleyCount = Math.Min(volleyCount, GetRankVolleyCap(pendingResult.Rank));

            float rankDamageMult = 1f + (int)pendingResult.Rank * 0.11f;
            float suitDamageMult = GetSuitDamageMultiplier(card.Suit);
            int volleyDamage = Math.Max(1, (int)(baseDamage * rankDamageMult * suitDamageMult));
            volleyDamage = Math.Min(volleyDamage, MaxDeckProjectileDamage);

            for (int i = 0; i < volleyCount; i++)
            {
                float t = volleyCount <= 1 ? 0f : (i / (float)(volleyCount - 1) - 0.5f);
                Vector2 volleyOrigin = origin + spreadAxis * (t * 64f * spreadScale) + forwardAxis * (Math.Abs(t) * 22f * spreadScale);
                Vector2 volleyTarget = targetPos + spreadAxis * (t * 108f * spreadScale);

                CardSuitAttackHelper.SpawnSuitAttack(
                    Player.GetSource_FromThis(),
                    Player.whoAmI,
                    volleyOrigin,
                    volleyTarget,
                    card.Suit,
                    volleyDamage,
                    2f,
                    markAsDeckAttack: true,
                    spawnAtTarget: false,
                    baseDir: baseDir);
            }
        }

            private static float GetSuitSpreadScale(CardSuit suit)
            {
                return suit switch
                {
                    CardSuit.Spades => 1.45f,
                    CardSuit.Hearts => 1.4f,
                    CardSuit.Diamonds => 1.35f,
                    CardSuit.Clubs => 1.1f,
                    _ => 1f
                };
            }

            private static float GetSuitRotationOffset(CardSuit suit)
            {
                return suit switch
                {
                    CardSuit.Spades => 0.34f,
                    CardSuit.Hearts => -0.28f,
                    CardSuit.Diamonds => 0.42f,
                    CardSuit.Clubs => 0.12f,
                    _ => 0f
                };
            }

        private int GetUsedSuitCount(CardSuit suit)
        {
            int count = 0;
            for (int i = 0; i < pendingResult.UsedCardIndices.Length; i++)
            {
                if (currentHand[pendingResult.UsedCardIndices[i]].Suit == suit)
                    count++;
            }

            return Math.Max(1, count);
        }

        private static float GetVeryGoodHandDamageMultiplier(PokerHandRank rank)
        {
            return rank switch
            {
                PokerHandRank.FullHouse => 1.1f,
                PokerHandRank.FourOfAKind => 1.18f,
                PokerHandRank.StraightFlush => 1.26f,
                PokerHandRank.RoyalFlush => 1.35f,
                _ => 1f
            };
        }

        private void ResetSuitDamageDecay()
        {
            Array.Clear(suitHitCounts, 0, suitHitCounts.Length);
        }

        public float ConsumeSuitHitDamageMultiplier(CardSuit suit)
        {
            int suitIndex = Math.Clamp((int)suit, 0, suitHitCounts.Length - 1);
            int priorHits = suitHitCounts[suitIndex];
            suitHitCounts[suitIndex] = priorHits + 1;
            return Math.Max(SuitDamageFloor, SuitOpeningDamageBonus - priorHits * SuitHitDecayStep);
        }

        private static int GetRankVolleyBonus(PokerHandRank rank)
        {
            return rank switch
            {
                PokerHandRank.HighCard => 0,
                PokerHandRank.Pair => 1,
                PokerHandRank.TwoPair => 1,
                PokerHandRank.ThreeOfAKind => 2,
                PokerHandRank.Straight => 2,
                PokerHandRank.Flush => 3,
                PokerHandRank.FullHouse => 3,
                PokerHandRank.FourOfAKind => 4,
                PokerHandRank.StraightFlush => 4,
                PokerHandRank.RoyalFlush => 5,
                _ => 0
            };
        }

        private static int GetRankVolleyCap(PokerHandRank rank)
        {
            return rank switch
            {
                PokerHandRank.TwoPair => 2,
                PokerHandRank.Flush => 2,
                PokerHandRank.FullHouse => 2,
                PokerHandRank.FourOfAKind => 3,
                PokerHandRank.StraightFlush => 3,
                PokerHandRank.RoyalFlush => 3,
                _ => 4
            };
        }

        private static int GetSuitVolleyBonus(CardSuit suit)
        {
            return suit switch
            {
                CardSuit.Spades => 2,
                CardSuit.Hearts => 2,
                CardSuit.Diamonds => 1,
                CardSuit.Clubs => 1,
                _ => 0
            };
        }

        private static float GetSuitDamageMultiplier(CardSuit suit)
        {
            return suit switch
            {
                CardSuit.Spades => 1.35f,
                CardSuit.Hearts => 1.28f,
                CardSuit.Diamonds => 1.15f,
                CardSuit.Clubs => 1.15f,
                _ => 1f
            };
        }

        private List<PlayingCard> GenerateQueuedHand()
        {
            PokerHandRank plannedRank = ChoosePlannedRank();

            for (int attempt = 0; attempt < 40; attempt++)
            {
                List<PlayingCard> hand = GenerateHandForRank(plannedRank);
                if (PokerHandEvaluator.Evaluate(hand).Rank == plannedRank)
                {
                    ShuffleList(hand);
                    return hand;
                }
            }

            List<PlayingCard> fallback = GenerateHandForRank(PokerHandRank.Pair);
            ShuffleList(fallback);
            return fallback;
        }

        private static void ShuffleList(List<PlayingCard> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Main.rand.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private PokerHandRank ChoosePlannedRank()
        {
            float luck = Math.Clamp(Player.luck, -0.75f, 2f);

            if (handsSinceGreatHand >= GreatHandHardPityThreshold)
            {
                return PickWeighted(ApplyLuckBias(new (PokerHandRank Rank, float Weight)[]
                {
                    (PokerHandRank.FullHouse, 42f),
                    (PokerHandRank.FourOfAKind, 30f),
                    (PokerHandRank.StraightFlush, 20f),
                    (PokerHandRank.RoyalFlush, 8f)
                }, luck, 0.28f));
            }

            if (handsSinceGoodHand >= GoodHandHardPityThreshold)
            {
                return PickWeighted(ApplyLuckBias(new (PokerHandRank Rank, float Weight)[]
                {
                    (PokerHandRank.Straight, 38f),
                    (PokerHandRank.Flush, 28f),
                    (PokerHandRank.FullHouse, 18f),
                    (PokerHandRank.FourOfAKind, 11f),
                    (PokerHandRank.StraightFlush, 4f),
                    (PokerHandRank.RoyalFlush, 1f)
                }, luck, 0.2f));
            }

            float softGood = handsSinceGoodHand;
            float softGreat = handsSinceGreatHand;

            return PickWeighted(ApplyLuckBias(new (PokerHandRank Rank, float Weight)[]
            {
                (PokerHandRank.HighCard, Math.Max(6f, 25f - softGood * 3f - softGreat * 2f)),
                (PokerHandRank.Pair, Math.Max(12f, 30f - softGood * 2f)),
                (PokerHandRank.TwoPair, 18f + softGood * 1.5f),
                (PokerHandRank.ThreeOfAKind, 11f + softGood * 1.5f),
                (PokerHandRank.Straight, 7f + softGood * 2.4f),
                (PokerHandRank.Flush, 5f + softGood * 2f + softGreat),
                (PokerHandRank.FullHouse, 2.5f + softGood * 1.2f + softGreat * 1.4f),
                (PokerHandRank.FourOfAKind, 1.1f + softGood * 0.8f + softGreat * 1.1f),
                (PokerHandRank.StraightFlush, 0.35f + softGreat * 0.5f),
                (PokerHandRank.RoyalFlush, 0.05f + softGreat * 0.12f)
            }, luck, 0.08f));
        }

        private static (PokerHandRank Rank, float Weight)[] ApplyLuckBias((PokerHandRank Rank, float Weight)[] weights, float luck, float pityStrength)
        {
            if (Math.Abs(luck) <= 0.001f)
                return weights;

            (PokerHandRank Rank, float Weight)[] adjusted = new (PokerHandRank Rank, float Weight)[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                PokerHandRank rank = weights[i].Rank;
                float weight = Math.Max(0.01f, weights[i].Weight);

                float multiplier = 1f;
                if (rank >= PokerHandRank.Straight)
                    multiplier += luck * (0.16f + pityStrength);

                if (rank >= PokerHandRank.FullHouse)
                    multiplier += luck * (0.12f + pityStrength * 0.8f);

                if (rank <= PokerHandRank.Pair)
                    multiplier -= luck * (0.14f + pityStrength * 0.55f);

                adjusted[i] = (rank, Math.Max(0.01f, weight * multiplier));
            }

            return adjusted;
        }

        private static PokerHandRank PickWeighted((PokerHandRank Rank, float Weight)[] weights)
        {
            float totalWeight = weights.Sum(entry => Math.Max(0f, entry.Weight));
            float roll = Main.rand.NextFloat(totalWeight);

            for (int i = 0; i < weights.Length; i++)
            {
                roll -= Math.Max(0f, weights[i].Weight);
                if (roll <= 0f)
                    return weights[i].Rank;
            }

            return weights[^1].Rank;
        }

        private static List<PlayingCard> GenerateHandForRank(PokerHandRank rank)
        {
            return rank switch
            {
                PokerHandRank.HighCard => GenerateHighCardHand(),
                PokerHandRank.Pair => GeneratePairHand(),
                PokerHandRank.TwoPair => GenerateTwoPairHand(),
                PokerHandRank.ThreeOfAKind => GenerateThreeOfAKindHand(),
                PokerHandRank.Straight => GenerateStraightHand(flush: false, royal: false),
                PokerHandRank.Flush => GenerateFlushHand(),
                PokerHandRank.FullHouse => GenerateFullHouseHand(),
                PokerHandRank.FourOfAKind => GenerateFourOfAKindHand(),
                PokerHandRank.StraightFlush => GenerateStraightHand(flush: true, royal: false),
                PokerHandRank.RoyalFlush => GenerateStraightHand(flush: true, royal: true),
                _ => GeneratePairHand()
            };
        }

        private static List<PlayingCard> GenerateHighCardHand()
        {
            while (true)
            {
                List<CardRank> ranks = PickDistinctRanks(5);
                List<PlayingCard> hand = ranks.Select(rank => new PlayingCard(RandomSuit(), rank)).ToList();
                if (PokerHandEvaluator.Evaluate(hand).Rank == PokerHandRank.HighCard)
                    return hand;
            }
        }

        private static List<PlayingCard> GeneratePairHand()
        {
            while (true)
            {
                CardRank pairRank = RandomRank();
                List<CardSuit> pairSuits = PickDistinctSuits(2);
                List<CardRank> kickers = PickDistinctRanks(3, pairRank);
                List<PlayingCard> hand = new()
                {
                    new PlayingCard(pairSuits[0], pairRank),
                    new PlayingCard(pairSuits[1], pairRank)
                };
                hand.AddRange(kickers.Select(rank => new PlayingCard(RandomSuit(), rank)));
                if (PokerHandEvaluator.Evaluate(hand).Rank == PokerHandRank.Pair)
                    return hand;
            }
        }

        private static List<PlayingCard> GenerateTwoPairHand()
        {
            while (true)
            {
                List<CardRank> pairRanks = PickDistinctRanks(2);
                CardRank kicker = PickDistinctRanks(1, pairRanks[0], pairRanks[1])[0];
                List<CardSuit> firstPairSuits = PickDistinctSuits(2);
                List<CardSuit> secondPairSuits = PickDistinctSuits(2);
                List<PlayingCard> hand = new()
                {
                    new PlayingCard(firstPairSuits[0], pairRanks[0]),
                    new PlayingCard(firstPairSuits[1], pairRanks[0]),
                    new PlayingCard(secondPairSuits[0], pairRanks[1]),
                    new PlayingCard(secondPairSuits[1], pairRanks[1]),
                    new PlayingCard(RandomSuit(), kicker)
                };
                if (PokerHandEvaluator.Evaluate(hand).Rank == PokerHandRank.TwoPair)
                    return hand;
            }
        }

        private static List<PlayingCard> GenerateThreeOfAKindHand()
        {
            while (true)
            {
                CardRank tripleRank = RandomRank();
                List<CardRank> kickers = PickDistinctRanks(2, tripleRank);
                List<CardSuit> tripleSuits = PickDistinctSuits(3);
                List<PlayingCard> hand = new()
                {
                    new PlayingCard(tripleSuits[0], tripleRank),
                    new PlayingCard(tripleSuits[1], tripleRank),
                    new PlayingCard(tripleSuits[2], tripleRank),
                    new PlayingCard(RandomSuit(), kickers[0]),
                    new PlayingCard(RandomSuit(), kickers[1])
                };
                if (PokerHandEvaluator.Evaluate(hand).Rank == PokerHandRank.ThreeOfAKind)
                    return hand;
            }
        }

        private static List<PlayingCard> GenerateStraightHand(bool flush, bool royal)
        {
            List<CardRank> ranks = royal ? new List<CardRank>
            {
                CardRank.Ten,
                CardRank.Jack,
                CardRank.Queen,
                CardRank.King,
                CardRank.Ace
            } : BuildStraightRanks();

            while (true)
            {
                CardSuit straightSuit = RandomSuit();
                List<PlayingCard> hand = new();
                for (int i = 0; i < ranks.Count; i++)
                {
                    CardSuit suit = flush ? straightSuit : RandomSuit();
                    hand.Add(new PlayingCard(suit, ranks[i]));
                }

                PokerHandRank result = PokerHandEvaluator.Evaluate(hand).Rank;
                PokerHandRank expected = royal ? PokerHandRank.RoyalFlush : (flush ? PokerHandRank.StraightFlush : PokerHandRank.Straight);
                if (result == expected)
                    return hand;
            }
        }

        private static List<PlayingCard> GenerateFlushHand()
        {
            while (true)
            {
                CardSuit suit = RandomSuit();
                List<CardRank> ranks = PickDistinctRanks(5);
                List<PlayingCard> hand = ranks.Select(rank => new PlayingCard(suit, rank)).ToList();
                if (PokerHandEvaluator.Evaluate(hand).Rank == PokerHandRank.Flush)
                    return hand;
            }
        }

        private static List<PlayingCard> GenerateFullHouseHand()
        {
            CardRank tripleRank = RandomRank();
            CardRank pairRank = PickDistinctRanks(1, tripleRank)[0];
            List<CardSuit> tripleSuits = PickDistinctSuits(3);
            List<CardSuit> pairSuits = PickDistinctSuits(2);
            return new List<PlayingCard>
            {
                new PlayingCard(tripleSuits[0], tripleRank),
                new PlayingCard(tripleSuits[1], tripleRank),
                new PlayingCard(tripleSuits[2], tripleRank),
                new PlayingCard(pairSuits[0], pairRank),
                new PlayingCard(pairSuits[1], pairRank)
            };
        }

        private static List<PlayingCard> GenerateFourOfAKindHand()
        {
            CardRank quadRank = RandomRank();
            CardRank kicker = PickDistinctRanks(1, quadRank)[0];
            return new List<PlayingCard>
            {
                new PlayingCard(CardSuit.Spades, quadRank),
                new PlayingCard(CardSuit.Hearts, quadRank),
                new PlayingCard(CardSuit.Diamonds, quadRank),
                new PlayingCard(CardSuit.Clubs, quadRank),
                new PlayingCard(RandomSuit(), kicker)
            };
        }

        private static List<CardRank> BuildStraightRanks()
        {
            int start = Main.rand.Next(0, 9);
            if (start == 0)
            {
                return new List<CardRank>
                {
                    CardRank.Ace,
                    CardRank.Two,
                    CardRank.Three,
                    CardRank.Four,
                    CardRank.Five
                };
            }

            return Enumerable.Range(start, 5)
                .Select(offset => (CardRank)offset)
                .ToList();
        }

        private static List<CardRank> PickDistinctRanks(int count, params CardRank[] excluded)
        {
            HashSet<CardRank> blocked = new HashSet<CardRank>(excluded);
            List<CardRank> result = new();
            while (result.Count < count)
            {
                CardRank rank = RandomRank();
                if (blocked.Add(rank))
                    result.Add(rank);
            }
            return result;
        }

        private static List<CardSuit> PickDistinctSuits(int count)
        {
            List<CardSuit> suits = Enum.GetValues(typeof(CardSuit)).Cast<CardSuit>().OrderBy(_ => Main.rand.Next()).Take(count).ToList();
            return suits;
        }

        private static CardRank RandomRank()
        {
            return (CardRank)Main.rand.Next(CardDeckRenderer.ColumnCount);
        }

        private static CardSuit RandomSuit()
        {
            return (CardSuit)Main.rand.Next(CardDeckRenderer.RowCount);
        }

        private void ClearHand()
        {
            currentHand.Clear();
            displayCards.Clear();
            queuedHand.Clear();
            ResetSuitDamageDecay();
            revealTimer = 0;
            pendingResult = default;
            handState = DeckHandState.Collecting;
        }

        public void DrawCurrentHand(SpriteBatch spriteBatch)
        {
            Vector2 anchor = Player.Top + new Vector2(0f, -34f) - Main.screenPosition;
            float visibleCount = 0f;
            for (int i = 0; i < displayCards.Count; i++)
            {
                if (!displayCards[i].Hidden)
                    visibleCount++;
            }

            if (visibleCount <= 0f)
                return;

            float centerOffset = (visibleCount - 1f) * 0.5f;
            int visibleIndex = 0;
            for (int i = 0; i < displayCards.Count; i++)
            {
                DisplayCardState state = displayCards[i];
                if (state.Hidden)
                    continue;

                float offsetIndex = visibleIndex - centerOffset;
                Vector2 drawPos = anchor + new Vector2(offsetIndex * 28f, Math.Abs(offsetIndex) * 4f);
                float rotation = offsetIndex * 0.09f;
                float fadeIn = MathHelper.Clamp(state.Age / 10f, 0f, 1f);
                float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f + i * 0.7f);
                float baseGlow = 0.28f * fadeIn;
                float highlightGlow = state.Highlighted ? 0.45f + pulse * 0.35f : 0f;
                float scale = state.Highlighted ? 0.78f : 0.72f;
                Color suitGlow = GetSuitGlowColor(state.Card.Suit);

                CardDeckRenderer.DrawCard(spriteBatch, drawPos, state.Card, suitGlow * (baseGlow + highlightGlow), rotation, CardDeckRenderer.CenterOrigin, scale * 1.12f);
                CardDeckRenderer.DrawCard(spriteBatch, drawPos, state.Card, Color.White * fadeIn, rotation, CardDeckRenderer.CenterOrigin, scale);
                visibleIndex++;
            }
        }

        private static Color GetSuitGlowColor(CardSuit suit)
        {
            return suit switch
            {
                CardSuit.Spades => new Color(90, 120, 255),
                CardSuit.Hearts => new Color(255, 90, 110),
                CardSuit.Diamonds => new Color(255, 180, 80),
                CardSuit.Clubs => new Color(80, 220, 160),
                _ => Color.White
            };
        }
    }

    public class DeckOfCardsGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public bool spawnedByDeckOfCards;

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(spawnedByDeckOfCards);
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
        {
            spawnedByDeckOfCards = bitReader.ReadBit();
        }

        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (!spawnedByDeckOfCards)
                return;

            if (!TryGetSuitForProjectile(projectile.type, out CardSuit suit))
                return;

            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
                return;

            Player owner = Main.player[projectile.owner];
            if (owner == null || !owner.active)
                return;

            float suitMult = owner.GetModPlayer<DeckOfCardsPlayer>().ConsumeSuitHitDamageMultiplier(suit);
            modifiers.SourceDamage *= suitMult;
        }

        private static bool TryGetSuitForProjectile(int projectileType, out CardSuit suit)
        {
            if (projectileType == ModContent.ProjectileType<Projectiles.Friendly.FriendlyHeartProjectile>())
            {
                suit = CardSuit.Hearts;
                return true;
            }

            if (projectileType == ModContent.ProjectileType<Projectiles.Friendly.FriendlyDiamondProjectile>())
            {
                suit = CardSuit.Diamonds;
                return true;
            }

            if (projectileType == ModContent.ProjectileType<Projectiles.Friendly.FriendlySpadeProjectile>())
            {
                suit = CardSuit.Spades;
                return true;
            }

            if (projectileType == ModContent.ProjectileType<Projectiles.Friendly.FriendlyClubProjectile>())
            {
                suit = CardSuit.Clubs;
                return true;
            }

            suit = default;
            return false;
        }
    }

    public class DeckOfCardsDrawSystem : ModSystem
    {
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead)
                return;

            // Don't draw hand cards while major UI is open (inventory, map, options).
            if (Main.playerInventory || Main.mapFullscreen || Main.ingameOptionsWindow)
                return;

            DeckOfCardsPlayer deckPlayer = player.GetModPlayer<DeckOfCardsPlayer>();
            if (!deckPlayer.deckVisualsEnabled || deckPlayer.CurrentHand.Count == 0)
                return;

            int drawIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Entity Health Bars"));
            if (drawIndex == -1)
                drawIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));

            if (drawIndex == -1)
                return;

            layers.Insert(drawIndex, new LegacyGameInterfaceLayer(
                "DeterministicChaos: Deck Of Cards Hand",
                () =>
                {
                    deckPlayer.DrawCurrentHand(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.Game));
        }
    }
}