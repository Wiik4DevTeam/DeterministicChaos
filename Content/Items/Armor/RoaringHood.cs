using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.SoulTraits;

namespace DeterministicChaos.Content.Items.Armor
{
    // Mage helmet for the Roaring armor set
    // Provides magic damage and mana bonuses
    [AutoloadEquip(EquipType.Head)]
    public class RoaringHood : ModItem, IExtendedHat
    {
        public override void SetDefaults()
        {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.buyPrice(gold: 12);
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.defense = 10;
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
            player.setBonus = "Roaring Arcana\n+5 Soul Investment\n+40% magic critical strike chance\n+30% magic fire rate\nGain Mana when you deal less than 10 damage to a target";
            // Crit chance bonus
            player.GetCritChance(DamageClass.Magic) += 40;
            // Attack speed bonus
            player.GetAttackSpeed(DamageClass.Magic) += 0.30f;
            // Enable mana gain on low damage effect
            player.GetModPlayer<RoaringArmorPlayer>().roaringMageSet = true;
            
            // +5 Soul Investment from set bonus
            player.GetModPlayer<SoulTraitPlayer>().ArmorInvestment += 5;
        }

        public override void UpdateEquip(Player player)
        {
            // Magic damage bonus
            player.GetDamage(DamageClass.Magic) += 0.15f;
            // Mana recharge rate bonus
            player.manaRegenBonus += 25;
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

        public string ExtensionTexture => "DeterministicChaos/Content/Items/Armor/RoaringHood_Extension";
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
