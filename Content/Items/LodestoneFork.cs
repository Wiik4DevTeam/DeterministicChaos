using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Items
{
    public class LodestoneFork : ModItem
    {
        private static bool? calamityLoaded = null;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
            calamityLoaded ??= ModLoader.HasMod("CalamityMod");
        }

        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 10;
            Item.damage = 46;
            Item.knockBack = 1.5f;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 8;
            Item.useAnimation = 8;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.DamageType = DamageClass.Throwing;
            Item.UseSound = SoundID.Item1;
            Item.shoot = ModContent.ProjectileType<LodestoneForkSpam>();
            Item.shootSpeed = 13f;
            Item.rare = ModContent.RarityType<TitanRarity>();
            Item.value = Item.buyPrice(gold: 18);

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
            bool isStealthStrike = calamityLoaded == true && CheckCalamityStealthStrike(player);

            if (isStealthStrike)
            {
                Projectile.NewProjectile(source, position, velocity,
                    ModContent.ProjectileType<LodestoneForkStealth>(), damage * 2, knockback * 1.5f, player.whoAmI);
                return false;
            }

            // Normal attack: random scale variance ~1.2x
            float scale = Main.rand.NextFloat(0.6f, 1.4f);
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, ai0: scale);
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

                    var rogueStealth    = calPlayerType.GetField("rogueStealth");
                    var rogueStealthMax = calPlayerType.GetField("rogueStealthMax");
                    if (rogueStealth != null && rogueStealthMax != null)
                    {
                        float stealth    = (float)rogueStealth.GetValue(calPlayer);
                        float maxStealth = (float)rogueStealthMax.GetValue(calPlayer);
                        return maxStealth > 0 && stealth >= maxStealth;
                    }
                }
            }
            catch { }
            return false;
        }

        public override void AddRecipes() { }
    }
}

