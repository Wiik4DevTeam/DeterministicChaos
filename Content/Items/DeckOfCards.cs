using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Systems.Cards;

namespace DeterministicChaos.Content.Items
{
    public class DeckOfCards : ModItem
    {
        public override string Texture => "DeterministicChaos/Content/Items/AceOfSpades";

        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 38;
            Item.accessory = true;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Purple;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.luck += 0.5f;
            player.GetCritChance(DamageClass.Generic) += 4f;
            DeckOfCardsPlayer deckPlayer = player.GetModPlayer<DeckOfCardsPlayer>();
            deckPlayer.deckEquipped = true;
            // Respect the vanilla accessory visual toggle (eye icon).
            deckPlayer.deckVisualsEnabled |= !hideVisual;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            DrawDeckFan(spriteBatch, position, scale * 2.85f, 0f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            Vector2 position = Item.Center - Main.screenPosition;
            DrawDeckFan(spriteBatch, position, scale * 0.9f, rotation * 0.2f);
            return false;
        }

        private static void DrawDeckFan(SpriteBatch spriteBatch, Vector2 center, float scale, float baseRotation)
        {
            DrawCard(spriteBatch, center + new Vector2(-10f, 4f) * scale, new PlayingCard(CardSuit.Spades, CardRank.Ace), scale, baseRotation - 0.18f, new Color(230, 230, 230));
            DrawCard(spriteBatch, center + new Vector2(0f, -2f) * scale, new PlayingCard(CardSuit.Hearts, CardRank.King), scale, baseRotation, Color.White);
            DrawCard(spriteBatch, center + new Vector2(10f, 4f) * scale, new PlayingCard(CardSuit.Clubs, CardRank.Queen), scale, baseRotation + 0.18f, new Color(230, 230, 230));
        }

        private static void DrawCard(SpriteBatch spriteBatch, Vector2 position, PlayingCard card, float scale, float rotation, Color color)
        {
            CardDeckRenderer.DrawCard(spriteBatch, position, card, color, rotation, CardDeckRenderer.CenterOrigin, scale * 0.55f);
        }
    }
}