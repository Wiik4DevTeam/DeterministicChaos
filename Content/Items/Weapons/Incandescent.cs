using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;
using DeterministicChaos.Content.SoulTraits;
using DeterministicChaos.Content.Items.Accessories;
using DeterministicChaos.Content.Items.BossBags;
using DeterministicChaos.Content.Items.BossSummons;
using DeterministicChaos.Content.Items.Consumables;
using DeterministicChaos.Content.Items.DamageClasses;
using DeterministicChaos.Content.Items.Globals;
using DeterministicChaos.Content.Items.Materials;
using DeterministicChaos.Content.Items.Placeable;
using DeterministicChaos.Content.Items.Rarities;
using DeterministicChaos.Content.Items.Weapons;

namespace DeterministicChaos.Content.Items.Weapons
{
    public class Incandescent : ModItem
    {
        public const int MANA_COST = 0; // No mana cost, melee/ranged hybrid

        public override void SetDefaults()
        {
            Item.width = 36;
            Item.height = 36;
            Item.damage = 32;
            Item.knockBack = 7f;
            Item.useTime = 8;
            Item.useAnimation = 8;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = false;
            Item.rare = ModContent.RarityType<BraveryRarity>();
            Item.value = Item.buyPrice(gold: 25);
            Item.UseSound = null; // Sounds handled by player class
            Item.shoot = ModContent.ProjectileType<ToughGauntletProjectile>();
            Item.shootSpeed = 16f;
            Item.DamageType = ModContent.GetInstance<RangedMeleeDamageClass>();
        }

        public override void SetStaticDefaults()
        {
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 10, SoulTraitType.Bravery);
        }

        public override bool CanUseItem(Player player)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait != SoulTraitType.Bravery)
                return false;

            var incPlayer = player.GetModPlayer<IncandescentPlayer>();

            if (incPlayer.AttackCooldown > 0)
                return false;

            if (incPlayer.IsWindingUp)
                return false;

            return true;
        }

        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            return false;
        }

        public override bool? UseItem(Player player)
        {
            var incPlayer = player.GetModPlayer<IncandescentPlayer>();
            bool isRightClick = player.altFunctionUse == 2;

            incPlayer.ProcessInput(isRightClick);

            return true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            Color braveryColor = new Color(255, 100, 30);

            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = braveryColor;
                }
            }

            var player = Main.LocalPlayer;
            var incPlayer = player.GetModPlayer<IncandescentPlayer>();

            if (incPlayer.ComboMeter > 0)
            {
                string comboText = $"Combo: {incPlayer.ComboMeter}x";
                Color comboColor = Color.Lerp(new Color(255, 180, 60), new Color(255, 60, 20),
                    MathHelper.Clamp(incPlayer.ComboMeter / 20f, 0f, 1f));
                tooltips.Add(new TooltipLine(Mod, "ComboMeter", comboText) { OverrideColor = comboColor });
            }
        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame,
            Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            var player = Main.LocalPlayer;
            var incPlayer = player.GetModPlayer<IncandescentPlayer>();

            if (incPlayer.ComboMeter > 0 && player.HeldItem.ModItem is Incandescent)
            {
                // Draw combo meter as a small bar below the item
                float comboFraction = MathHelper.Clamp(incPlayer.ComboMeter / 20f, 0f, 1f);
                Color barColor = Color.Lerp(new Color(255, 180, 60), new Color(255, 40, 10), comboFraction);

                Vector2 barPos = position + new Vector2(-frame.Width * scale * 0.3f, frame.Height * scale * 0.4f);
                float barWidth = frame.Width * scale * 0.6f;
                float barHeight = 3f;

                spriteBatch.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value,
                    new Rectangle((int)barPos.X, (int)barPos.Y, (int)(barWidth * comboFraction), (int)barHeight),
                    barColor);
            }
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<ToughGauntlet>(), 1)
                .AddIngredient(ModContent.ItemType<Titansblood>(), 10)
                .AddIngredient(ModContent.ItemType<Sparks.SparkOfBravery>(), 10)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }
}
