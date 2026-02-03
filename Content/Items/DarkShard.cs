using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SubworldLibrary;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Subworlds;
using DeterministicChaos.Content.VFX;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Items
{
    public class DarkShard : ModItem
    {
        private static bool? calamityLoaded = null;

        public override void SetStaticDefaults()
        {
            calamityLoaded ??= ModLoader.HasMod("CalamityMod");
        }

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.damage = 49;
            Item.knockBack = 2f;
            Item.useTime = 15;
            Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.value = Item.buyPrice(0, 10, 0, 0);
            Item.UseSound = SoundID.Item1;
            Item.consumable = false;
            Item.shoot = ModContent.ProjectileType<DarkShardProjectile>();
            Item.shootSpeed = 14f;

            // Set as throwing damage by default, Calamity will override if loaded
            Item.DamageType = DamageClass.Throwing;

            // If Calamity is loaded, set as Rogue damage
            if (calamityLoaded == true)
            {
                SetCalamityRogueDefaults();
            }
        }

        private void SetCalamityRogueDefaults()
        {
            try
            {
                // Use reflection to set Calamity Rogue damage class
                if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
                {
                    if (calamity.TryFind<DamageClass>("RogueDamageClass", out var rogueClass))
                    {
                        Item.DamageType = rogueClass;
                    }
                }
            }
            catch
            {
                // Fall back to throwing if Calamity integration fails
                Item.DamageType = DamageClass.Throwing;
            }
        }

        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                // Special action: Dark Fountain
                // Can not use if already in Dark World or cutscene is playing
                return !SubworldSystem.IsActive<DarkDimension>() && !DarkWorldCutscene.IsPlaying;
            }

            // Normal throw attack
            return true;
        }

        public override bool? UseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                // Special action: Enter Dark World
                ActivateDarkWorldPortal(player);
                return true;
            }

            // Normal attack handled by Shoot
            return null;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Do not shoot projectile on right click
            if (player.altFunctionUse == 2)
            {
                return false;
            }

            // Calculate spawn position from player's hand
            Vector2 handPosition = player.RotatedRelativePoint(player.MountedCenter);
            handPosition += velocity.SafeNormalize(Vector2.Zero) * 20f;
            handPosition.Y -= 10f;

            // Check for Calamity stealth strike
            bool isStealthStrike = false;
            if (calamityLoaded == true)
            {
                isStealthStrike = CheckCalamityStealthStrike(player);
            }

            if (isStealthStrike)
            {
                // Fire 5 knives in a fan pattern for stealth strike
                int knifeCount = 7;
                float spreadAngle = MathHelper.ToRadians(12);
                float startAngle = -spreadAngle / 2f;
                float angleStep = spreadAngle / (knifeCount - 1);

                for (int i = 0; i < knifeCount; i++)
                {
                    float angle = startAngle + angleStep * i;
                    Vector2 fanVelocity = velocity.RotatedBy(angle);

                    Projectile.NewProjectile(source, handPosition, fanVelocity, type, damage, knockback, player.whoAmI);
                }
            }
            else
            {
                // Normal single knife throw
                Projectile.NewProjectile(source, handPosition, velocity, type, damage, knockback, player.whoAmI);
            }

            return false;
        }

        private bool CheckCalamityStealthStrike(Player player)
        {
            try
            {
                if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
                {
                    // Get the CalamityPlayer type from the assembly
                    var calPlayerType = calamity.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
                    if (calPlayerType == null)
                        return false;

                    // Find the CalamityPlayer instance in the player's mod players
                    ModPlayer calPlayer = null;
                    foreach (var modPlayer in player.ModPlayers)
                    {
                        if (modPlayer.GetType() == calPlayerType)
                        {
                            calPlayer = modPlayer;
                            break;
                        }
                    }

                    if (calPlayer == null)
                        return false;

                    // Check for StealthStrikeAvailable
                    var stealthProp = calPlayerType.GetProperty("StealthStrikeAvailable");
                    if (stealthProp != null)
                    {
                        return (bool)stealthProp.GetValue(calPlayer);
                    }

                    var stealthField = calPlayerType.GetField("StealthStrikeAvailable");
                    if (stealthField != null)
                    {
                        return (bool)stealthField.GetValue(calPlayer);
                    }

                    // Try alternate field names that Calamity might use
                    var rogueStealth = calPlayerType.GetField("rogueStealth");
                    var rogueStealthMax = calPlayerType.GetField("rogueStealthMax");
                    if (rogueStealth != null && rogueStealthMax != null)
                    {
                        float stealth = (float)rogueStealth.GetValue(calPlayer);
                        float maxStealth = (float)rogueStealthMax.GetValue(calPlayer);
                        // Stealth strike available when stealth is at max
                        return maxStealth > 0 && stealth >= maxStealth;
                    }
                }
            }
            catch
            {
                // Fall back to no stealth strike if Calamity integration fails
            }
            return false;
        }

        private void ActivateDarkWorldPortal(Player player)
        {
            // Store the player position for the subworld to use
            DarkDimension.OriginX = (int)(player.Center.X / 16f);
            DarkDimension.OriginY = (int)(player.Center.Y / 16f);
            DarkDimension.WorldSeed = Main.ActiveWorldFileData.Seed;

            // Detect and store current biome
            DarkDimension.SourceBiome = DetectBiome(player);

            // Mark nearby players to enter
            float range = 800f;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead && Vector2.Distance(p.Center, player.Center) <= range)
                {
                    if (p.GetModPlayer<DarkDimensionPlayer>() != null)
                    {
                        p.GetModPlayer<DarkDimensionPlayer>().shouldEnterDarkDimension = true;
                    }
                }
            }

            // Start the cutscene
            DarkWorldCutscene.StartCutscene(player);
        }

        private DarkDimension.BiomeType DetectBiome(Player player)
        {
            if (player.ZoneCorrupt)
                return DarkDimension.BiomeType.Corruption;
            if (player.ZoneCrimson)
                return DarkDimension.BiomeType.Crimson;
            if (player.ZoneHallow)
                return DarkDimension.BiomeType.Hallow;
            if (player.ZoneJungle)
                return DarkDimension.BiomeType.Jungle;
            if (player.ZoneDesert)
                return DarkDimension.BiomeType.Desert;
            if (player.ZoneSnow)
                return DarkDimension.BiomeType.Snow;
            if (player.ZoneUnderworldHeight)
                return DarkDimension.BiomeType.Underworld;
            if (player.ZoneDungeon)
                return DarkDimension.BiomeType.Dungeon;
            if (player.ZoneRockLayerHeight || player.ZoneDirtLayerHeight)
                return DarkDimension.BiomeType.Underground;
            if (player.ZoneBeach)
                return DarkDimension.BiomeType.Ocean;

            return DarkDimension.BiomeType.Forest;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            Color statGray = new Color(60, 60, 60);
            foreach (TooltipLine line in tooltips)
            {
                // Stats get a GRAYER color to differentiate from descriptions
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
    }
}
