using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Systems.Cards
{
    public enum CardSuit
    {
        Spades = 0,
        Hearts = 1,
        Diamonds = 2,
        Clubs = 3
    }

    public enum CardRank
    {
        Ace = 0,
        Two = 1,
        Three = 2,
        Four = 3,
        Five = 4,
        Six = 5,
        Seven = 6,
        Eight = 7,
        Nine = 8,
        Ten = 9,
        Jack = 10,
        Queen = 11,
        King = 12
    }

    public readonly struct PlayingCard
    {
        public PlayingCard(CardSuit suit, CardRank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public CardSuit Suit { get; }
        public CardRank Rank { get; }

        public override string ToString()
        {
            return $"{Rank} of {Suit}";
        }
    }

    public static class CardDeckRenderer
    {
        public const string TexturePath = "DeterministicChaos/Content/Systems/Cards/Cards";
        public const int ColumnCount = 13;
        public const int RowCount = 4;
        public const int TextureWidth = 650;
        public const int TextureHeight = 280;
        public const int CardWidth = TextureWidth / ColumnCount;
        public const int CardHeight = TextureHeight / RowCount;

        public static readonly Point CardSize = new Point(CardWidth, CardHeight);
        public static readonly Vector2 CenterOrigin = new Vector2(CardWidth * 0.5f, CardHeight * 0.5f);
        public static readonly Vector2 TopLeftOrigin = Vector2.Zero;

        public static Asset<Texture2D> TextureAsset => ModContent.Request<Texture2D>(TexturePath, AssetRequestMode.ImmediateLoad);
        public static Texture2D Texture => TextureAsset.Value;

        public static Rectangle GetSourceRectangle(CardSuit suit, CardRank rank)
        {
            return new Rectangle((int)rank * CardWidth, (int)suit * CardHeight, CardWidth, CardHeight);
        }

        public static Rectangle GetSourceRectangle(PlayingCard card)
        {
            return GetSourceRectangle(card.Suit, card.Rank);
        }

        public static void DrawCard(
            SpriteBatch spriteBatch,
            Vector2 position,
            PlayingCard card,
            Color color,
            float rotation = 0f,
            Vector2? origin = null,
            float scale = 1f,
            SpriteEffects effects = SpriteEffects.None,
            float layerDepth = 0f)
        {
            spriteBatch.Draw(
                Texture,
                position,
                GetSourceRectangle(card),
                color,
                rotation,
                origin ?? CenterOrigin,
                scale,
                effects,
                layerDepth);
        }

        public static void DrawCard(
            SpriteBatch spriteBatch,
            Vector2 position,
            CardSuit suit,
            CardRank rank,
            Color color,
            float rotation = 0f,
            Vector2? origin = null,
            float scale = 1f,
            SpriteEffects effects = SpriteEffects.None,
            float layerDepth = 0f)
        {
            DrawCard(spriteBatch, position, new PlayingCard(suit, rank), color, rotation, origin, scale, effects, layerDepth);
        }

        public static void DrawCardWorld(
            SpriteBatch spriteBatch,
            Vector2 worldPosition,
            PlayingCard card,
            Color color,
            Vector2 screenPosition,
            float rotation = 0f,
            Vector2? origin = null,
            float scale = 1f,
            SpriteEffects effects = SpriteEffects.None,
            float layerDepth = 0f)
        {
            DrawCard(spriteBatch, worldPosition - screenPosition, card, color, rotation, origin, scale, effects, layerDepth);
        }

        public static IEnumerable<PlayingCard> CreateStandardDeck()
        {
            for (int suit = 0; suit < RowCount; suit++)
            {
                for (int rank = 0; rank < ColumnCount; rank++)
                {
                    yield return new PlayingCard((CardSuit)suit, (CardRank)rank);
                }
            }
        }
    }
}