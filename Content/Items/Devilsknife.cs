using Microsoft.Xna.Framework;
using System;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class Devilsknife : ModItem
    {
        private static bool? calamityLoaded = null;

        public override void SetStaticDefaults()
        {
            calamityLoaded ??= ModLoader.HasMod("CalamityMod");
        }

        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 30;
            Item.damage = 34;
            Item.knockBack = 3f;
            Item.useTime = 28;
            Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(0, 5, 0, 0);
            Item.UseSound = SoundID.Item1;
            Item.shoot = ModContent.ProjectileType<Projectiles.Friendly.DevilsknifeProjectile>();
            Item.shootSpeed = 14f;

            Item.DamageType = DamageClass.Throwing;

            if (calamityLoaded == true)
                SetCalamityRogueDefaults();
        }

        private void SetCalamityRogueDefaults()
        {
            try
            {
                if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
                {
                    if (calamity.TryFind<DamageClass>("RogueDamageClass", out var rogueClass))
                        Item.DamageType = rogueClass;
                }
            }
            catch
            {
                Item.DamageType = DamageClass.Throwing;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Vector2 handPosition = player.RotatedRelativePoint(player.MountedCenter);
            handPosition += velocity.SafeNormalize(Vector2.Zero) * 20f;

            bool isStealthStrike = calamityLoaded == true && CheckCalamityStealthStrike(player);

            if (isStealthStrike)
            {
                // Stealth strike: four scythes that arc in different directions
                // ai[0] = 1 means stealth mode, ai[1] = arc direction (+1/-1 up/down)
                Projectile.NewProjectile(source, handPosition, velocity, type, damage, knockback, player.whoAmI, 1f, 1f);
                Projectile.NewProjectile(source, handPosition, velocity, type, damage, knockback, player.whoAmI, 1f, -1f);
                Projectile.NewProjectile(source, handPosition, velocity, type, damage, knockback, player.whoAmI, 1f, 0.5f);
                Projectile.NewProjectile(source, handPosition, velocity, type, damage, knockback, player.whoAmI, 1f, -0.5f);
            }
            else
            {
                // Normal: single straight boomerang scythe
                // ai[0] = 0 means normal mode
                Projectile.NewProjectile(source, handPosition, velocity, type, damage, knockback, player.whoAmI, 0f, 0f);
            }

            return false;
        }

        private bool CheckCalamityStealthStrike(Player player)
        {
            try
            {
                if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
                {
                    var calPlayerType = calamity.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
                    if (calPlayerType == null) return false;

                    ModPlayer calPlayer = null;
                    foreach (var modPlayer in player.ModPlayers)
                    {
                        if (modPlayer.GetType() == calPlayerType)
                        {
                            calPlayer = modPlayer;
                            break;
                        }
                    }
                    if (calPlayer == null) return false;

                    var stealthProp = calPlayerType.GetProperty("StealthStrikeAvailable");
                    if (stealthProp != null)
                        return (bool)stealthProp.GetValue(calPlayer);

                    var stealthField = calPlayerType.GetField("StealthStrikeAvailable");
                    if (stealthField != null)
                        return (bool)stealthField.GetValue(calPlayer);

                    var rogueStealth = calPlayerType.GetField("rogueStealth");
                    var rogueStealthMax = calPlayerType.GetField("rogueStealthMax");
                    if (rogueStealth != null && rogueStealthMax != null)
                    {
                        float stealth = (float)rogueStealth.GetValue(calPlayer);
                        float maxStealth = (float)rogueStealthMax.GetValue(calPlayer);
                        return maxStealth > 0 && stealth >= maxStealth;
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
