using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;
using DeterministicChaos.Content.SoulTraits;

namespace DeterministicChaos.Content.Items
{
    public class RustyKnife : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 30;
            Item.damage = 11;
            Item.knockBack = 2.5f;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 2);
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<RustyKnifeSwing>();
            Item.shootSpeed = 1f;
            Item.DamageType = ModContent.GetInstance<MeleeMagicDamageClass>();
            Item.mana = 0; // Normal attack costs no mana
        }

        public override void SetStaticDefaults()
        {
            // Register +3 Determination weapon investment
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 3);
        }

        public override bool AltFunctionUse(Player player)
        {
            // Only players with a Determination soul can use the timed attack
            return player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Determination;
        }

        public override bool MeleePrefix()
        {
            return true;
        }

        public override bool CanUseItem(Player player)
        {
            var knifePlayer = player.GetModPlayer<RustyKnifePlayer>();

            if (player.altFunctionUse == 2)
            {
                // Alt fire: requires Determination soul
                if (player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Determination)
                    return false;

                // Alt fire: check if prompt is already active
                if (knifePlayer.PromptActive)
                {
                    // Second press — lock the marker (handled in RustyKnifePlayer)
                    // Don't actually "use" the item again
                    return false;
                }

                // Need 200 mana to activate
                if (player.statMana < 200)
                    return false;

                Item.useTime = 20;
                Item.useAnimation = 20;
                Item.shoot = ModContent.ProjectileType<RustyKnifeSwing>();
                Item.UseSound = null;
                Item.channel = false;
                Item.autoReuse = false;
                Item.mana = 0;
            }
            else
            {
                // Normal knife swing — don't allow during prompt
                if (knifePlayer.PromptActive)
                    return false;

                Item.useTime = 12;
                Item.useAnimation = 12;
                Item.shoot = ModContent.ProjectileType<RustyKnifeSwing>();
                Item.UseSound = null;
                Item.channel = false;
                Item.autoReuse = true;
                Item.mana = 0;
            }

            return base.CanUseItem(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Only spawn on owning client
            if (player.whoAmI != Main.myPlayer)
                return false;

            if (player.altFunctionUse == 2)
            {
                // Start attack prompt
                var knifePlayer = player.GetModPlayer<RustyKnifePlayer>();
                if (!knifePlayer.PromptActive)
                {
                    // Consume 200 mana
                    player.statMana -= 200;
                    if (player.statMana < 0) player.statMana = 0;
                    player.manaRegenDelay = (int)player.maxRegenDelay;

                    knifePlayer.StartPrompt(damage);

                    // Play menu popup sound
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/DTMenu"), player.Center);
                }
                return false;
            }
            else
            {
                // Normal knife swing with varied combo (always top-to-bottom)
                float swingDirection = 1f;
                player.GetModPlayer<RustyKnifePlayer>().swingCombo++;

                Vector2 toMouse = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
                float aimAngle = toMouse.ToRotation();

                Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                    ModContent.ProjectileType<RustyKnifeSwing>(), damage, knockback,
                    player.whoAmI, swingDirection, aimAngle);
            }

            return false;
        }

        public override void HoldItem(Player player)
        {
            var knifePlayer = player.GetModPlayer<RustyKnifePlayer>();

            // Handle second alt-fire press to lock the marker
            if (player.whoAmI == Main.myPlayer && knifePlayer.PromptActive && !knifePlayer.MarkerLocked)
            {
                if (Main.mouseRight && Main.mouseRightRelease)
                {
                    knifePlayer.LockMarker();
                }
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            foreach (TooltipLine line in tooltips)
            {
                if (line.Name == "ItemName" && line.Mod == "Terraria")
                {
                    line.OverrideColor = new Color(200, 30, 30);
                }
            }
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            return true;
        }
    }
}
