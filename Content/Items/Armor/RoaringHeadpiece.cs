using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.SoulTraits;

namespace DeterministicChaos.Content.Items.Armor
{
    // Summoner helmet for the Roaring armor set
    // Provides summon damage and minion bonuses
    [AutoloadEquip(EquipType.Head)]
    public class RoaringHeadpiece : ModItem, IExtendedHat
    {
        public override void SetDefaults()
        {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.buyPrice(gold: 12);
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.defense = 8;
        }

        public override bool IsArmorSet(Item head, Item body, Item legs)
        {
            return body.type == ModContent.ItemType<RoaringBreastplate>() && 
                   legs.type == ModContent.ItemType<RoaringLeggings>();
        }

        public override void ArmorSetShadows(Player player)
        {
            // Afterimage effect when full set is worn
            player.armorEffectDrawShadow = true;
        }

        public override void UpdateArmorSet(Player player)
        {
            player.setBonus = "Roaring Shadow\n+5 Soul Investment\n+3 max minions\n-20% minion damage\nShadow clones will mirror your whip attacks with the Gnomon whip";
            // Extra minions
            player.maxMinions += 3;
            // Decreased minion damage
            player.GetDamage(DamageClass.Summon) -= 0.20f;
            // Enable clone whip mirror effect
            player.GetModPlayer<RoaringArmorPlayer>().roaringSummonerSet = true;
            
            // +5 Soul Investment from set bonus
            player.GetModPlayer<SoulTraitPlayer>().ArmorInvestment += 5;
        }

        public override void UpdateEquip(Player player)
        {
            // Whip speed and damage bonus
            player.GetAttackSpeed(DamageClass.SummonMeleeSpeed) += 0.15f;
            player.GetDamage(DamageClass.SummonMeleeSpeed) += 0.10f;
            // Emit bright light
            Lighting.AddLight(player.Center + new Vector2(0, -16), 0.8f, 0.8f, 0.8f);
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            // Draw item at full brightness, unaffected by lighting
            Texture2D texture = Terraria.GameContent.TextureAssets.Item[Item.type].Value;
            Vector2 position = Item.position - Main.screenPosition + new Vector2(Item.width / 2, Item.height - texture.Height / 2);
            Vector2 origin = texture.Size() * 0.5f;

            spriteBatch.Draw(texture, position, null, Color.White, rotation, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            Color statGray = new Color(60, 60, 60);
            foreach (TooltipLine line in tooltips)
            {
                // Stats get a grayer color to differentiate from descriptions
                if (line.Name == "Damage" || line.Name == "Speed" || line.Name == "Knockback" || 
                    line.Name == "CritChance" || line.Name == "Defense" || line.Name == "UseMana" ||
                    line.Name == "Consumable" || line.Name == "Material")
                {
                    line.OverrideColor = statGray;
                }
                else
                {
                    line.OverrideColor = Color.Black;
                }
            }
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset)
        {
            Vector2 position = new Vector2(line.X, line.Y);

            // Draw white shadow outline
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    if (x == 0 && y == 0) continue;

                    Terraria.UI.Chat.ChatManager.DrawColorCodedString(
                        Main.spriteBatch,
                        line.Font,
                        line.Text,
                        position + new Vector2(x, y),
                        Color.White,
                        line.Rotation,
                        line.Origin,
                        line.BaseScale
                    );
                }
            }

            // Draw black text on top
            Terraria.UI.Chat.ChatManager.DrawColorCodedString(
                Main.spriteBatch,
                line.Font,
                line.Text,
                position,
                Color.Black,
                line.Rotation,
                line.Origin,
                line.BaseScale
            );

            return false;
        }

        public string ExtensionTexture => "DeterministicChaos/Content/Items/Armor/RoaringHeadpiece_Extension";
        public Vector2 ExtensionSpriteOffset(PlayerDrawSet drawInfo) => new Vector2(0, -6f);

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<DarkFragment>(), 12)
                .AddIngredient(ItemID.GoldHelmet)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
            
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<DarkFragment>(), 12)
                .AddIngredient(ItemID.PlatinumHelmet)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }
}
