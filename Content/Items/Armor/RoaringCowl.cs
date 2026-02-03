using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items.Armor
{
    // Rogue helmet for the Roaring armor set
    // Provides rogue damage and stealth bonuses (Calamity integration)
    [AutoloadEquip(EquipType.Head)]
    public class RoaringCowl : ModItem, IExtendedHat
    {
        public override void SetDefaults()
        {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.buyPrice(gold: 12);
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.defense = 12;
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
            player.setBonus = "Roaring Shadow\nReacahing maximum stealth meter grants a rapidly decaying fire rate for 5 seconds\nDark Shard Direct Hits spawn Seeking Knives on hit";
            // Enable rogue armor for Calamity
            var modPlayer = player.GetModPlayer<RoaringArmorPlayer>();
            modPlayer.roaringRogueSet = true;
            
            // Try to set Calamity's wearingRogueArmor flag via reflection
            TrySetCalamityRogueArmor(player);
        }

        public override void UpdateEquip(Player player)
        {
            // Crit bonus and movement speed
            player.GetCritChance(DamageClass.Generic) += 8;
            player.moveSpeed += 0.10f;
            // Emit bright light
            Lighting.AddLight(player.Center + new Vector2(0, -16), 0.8f, 0.8f, 0.8f);
        }

        private void TrySetCalamityRogueArmor(Player player)
        {
            // Try to access Calamity's ModPlayer to enable rogue armor benefits and stealth
            try
            {
                foreach (var modPlayer in player.ModPlayers)
                {
                    if (modPlayer.GetType().Name == "CalamityPlayer")
                    {
                        var type = modPlayer.GetType();
                        
                        // Enable rogue armor flag
                        var wearingRogueArmor = type.GetField("wearingRogueArmor");
                        if (wearingRogueArmor != null)
                        {
                            wearingRogueArmor.SetValue(modPlayer, true);
                        }
                        
                        // Enable stealth mechanics, set rogueStealthMax to enable the meter
                        var rogueStealthMax = type.GetField("rogueStealthMax");
                        if (rogueStealthMax != null)
                        {
                            // Set max stealth (1.0 is standard, higher gives more stealth capacity)
                            rogueStealthMax.SetValue(modPlayer, 1.0f);
                        }
                        
                        // Set stealth generation rate (much faster than normal)
                        var stealthGenStandstill = type.GetField("stealthGenStandstill");
                        if (stealthGenStandstill != null)
                        {
                            stealthGenStandstill.SetValue(modPlayer, 0.5f);
                        }
                        
                        var stealthGenMoving = type.GetField("stealthGenMoving");
                        if (stealthGenMoving != null)
                        {
                            stealthGenMoving.SetValue(modPlayer, 0.25f);
                        }
                        
                        break;
                    }
                }
            }
            catch
            {
                // Calamity not installed, continue without rogue benefits
            }
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

        public string ExtensionTexture => "DeterministicChaos/Content/Items/Armor/RoaringCowl_Extension";
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
