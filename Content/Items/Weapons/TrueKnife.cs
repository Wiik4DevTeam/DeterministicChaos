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
    public class TrueKnife : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 30;
            Item.damage = 56;
            Item.knockBack = 4f;
            Item.useTime = 10;
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ModContent.RarityType<DeterminationRarity>();
            Item.value = Item.buyPrice(gold: 25);
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<TrueKnifeSwing>();
            Item.shootSpeed = 1f;
            Item.DamageType = ModContent.GetInstance<MeleeMagicDamageClass>();
            Item.mana = 0;
        }

        public override void SetStaticDefaults()
        {
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 8, SoulTraitType.Determination);
        }

        public override bool AltFunctionUse(Player player)
        {
            return player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Determination;
        }

        public override bool MeleePrefix()
        {
            return true;
        }

        public override bool CanUseItem(Player player)
        {
            var knifePlayer = player.GetModPlayer<TrueKnifePlayer>();

            if (player.altFunctionUse == 2)
            {
                if (player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Determination)
                    return false;

                if (knifePlayer.PromptActive)
                    return false;

                if (player.statMana < 200)
                    return false;

                Item.useTime = 20;
                Item.useAnimation = 20;
                Item.shoot = ModContent.ProjectileType<TrueKnifeSwing>();
                Item.UseSound = null;
                Item.channel = false;
                Item.autoReuse = false;
                Item.mana = 0;
            }
            else
            {
                if (player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Determination)
                    return false;

                if (knifePlayer.PromptActive)
                    return false;

                Item.useTime = 10;
                Item.useAnimation = 10;
                Item.shoot = ModContent.ProjectileType<TrueKnifeSwing>();
                Item.UseSound = null;
                Item.channel = false;
                Item.autoReuse = true;
                Item.mana = 0;
            }

            return base.CanUseItem(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.whoAmI != Main.myPlayer)
                return false;

            if (player.altFunctionUse == 2)
            {
                var knifePlayer = player.GetModPlayer<TrueKnifePlayer>();
                if (!knifePlayer.PromptActive)
                {
                    knifePlayer.StartPrompt(damage);

                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/DTMenu"), player.Center);
                }
                return false;
            }
            else
            {
                float swingDirection = 1f;
                player.GetModPlayer<TrueKnifePlayer>().swingCombo++;

                Vector2 toMouse = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
                float aimAngle = toMouse.ToRotation();

                // Spawn the knife swing
                Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                    ModContent.ProjectileType<TrueKnifeSwing>(), damage, knockback,
                    player.whoAmI, swingDirection, aimAngle);

                // Also send out a Determination projectile aimed at the cursor
                Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                    ModContent.ProjectileType<DeterminationSlash>(), damage, knockback,
                    player.whoAmI, 1f, aimAngle);
            }

            return false;
        }

        public override void HoldItem(Player player)
        {
            var knifePlayer = player.GetModPlayer<TrueKnifePlayer>();

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
                    line.OverrideColor = new Color(255, 50, 50);
                }
            }
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<RealKnife>(), 1)
                .AddIngredient(ModContent.ItemType<Titansblood>(), 10)
                .AddIngredient(ModContent.ItemType<Sparks.SparkOfDetermination>(), 10)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }
}
