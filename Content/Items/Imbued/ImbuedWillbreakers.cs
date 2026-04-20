using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Sparks;
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

namespace DeterministicChaos.Content.Items.Imbued
{
    public enum ImbuedWillbreakerVariant
    {
        Determination,
        Integrity,
        Patience,
        Perseverance,
        Kindness,
        Justice,
        Bravery
    }

    public abstract class ImbuedWillbreakerBase : ModItem
    {
        protected abstract ImbuedWillbreakerVariant Variant { get; }
        protected abstract int GetSparkType();

        public override string Texture => "DeterministicChaos/Content/Items/Weapons/RoaringSword";

        private SoulTraitType GetSoulTraitType()
        {
            return Variant switch
            {
                ImbuedWillbreakerVariant.Determination => SoulTraitType.Determination,
                ImbuedWillbreakerVariant.Integrity => SoulTraitType.Integrity,
                ImbuedWillbreakerVariant.Patience => SoulTraitType.Patience,
                ImbuedWillbreakerVariant.Perseverance => SoulTraitType.Perseverance,
                ImbuedWillbreakerVariant.Kindness => SoulTraitType.Kindness,
                ImbuedWillbreakerVariant.Justice => SoulTraitType.Justice,
                ImbuedWillbreakerVariant.Bravery => SoulTraitType.Bravery,
                _ => SoulTraitType.None
            };
        }

        protected Color GetTraitColor()
        {
            return Variant switch
            {
                ImbuedWillbreakerVariant.Determination => new Color(255, 60, 60),
                ImbuedWillbreakerVariant.Integrity => new Color(0, 0, 255),
                ImbuedWillbreakerVariant.Patience => new Color(80, 255, 255),
                ImbuedWillbreakerVariant.Perseverance => new Color(255, 80, 255),
                ImbuedWillbreakerVariant.Kindness => new Color(80, 230, 80),
                ImbuedWillbreakerVariant.Justice => new Color(255, 255, 80),
                ImbuedWillbreakerVariant.Bravery => new Color(255, 190, 60),
                _ => Color.White
            };
        }

        public override void SetStaticDefaults()
        {
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 6, GetSoulTraitType());
        }

        public override void SetDefaults()
        {
            Item.width = 40;
            Item.height = 40;
            Item.damage = 60;
            Item.knockBack = 5f;
            Item.useTime = 14;
            Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.value = Item.buyPrice(gold: 8);
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<RoaringSwordSwing>();
            Item.shootSpeed = 1f;
            Item.DamageType = DamageClass.Melee;

            if (Variant == ImbuedWillbreakerVariant.Perseverance)
                Item.DamageType = ModContent.GetInstance<MeleeMagicDamageClass>();
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool MeleePrefix() => true;

        public override bool CanUseItem(Player player)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                if (Main.projectile[i].active && Main.projectile[i].owner == player.whoAmI)
                {
                    int projType = Main.projectile[i].type;
                    if (projType == ModContent.ProjectileType<RoaringSwordLungeCharge>() ||
                        projType == ModContent.ProjectileType<RoaringSwordLunge>() ||
                        projType == ModContent.ProjectileType<RoaringSwordChainLunge>())
                    {
                        return false;
                    }
                }
            }

            if (player.GetModPlayer<RoaringSwordPlayer>().lungeCooldown > 0)
            {
                if (player.altFunctionUse == 2)
                    return false;
            }

            if (player.altFunctionUse == 2)
            {
                Item.useTime = 10;
                Item.useAnimation = 10;
                Item.shoot = ModContent.ProjectileType<RoaringSwordLungeCharge>();
                Item.UseSound = null;
                Item.channel = true;
                Item.autoReuse = false;
            }
            else
            {
                Item.useTime = 14;
                Item.useAnimation = 14;
                Item.shoot = ModContent.ProjectileType<RoaringSwordSwing>();
                Item.UseSound = SoundID.Item1;
                Item.channel = false;
                Item.autoReuse = true;
            }
            return base.CanUseItem(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.whoAmI != Main.myPlayer)
                return false;

            if (player.altFunctionUse == 2)
            {
                Projectile.NewProjectile(source, player.Center, velocity, ModContent.ProjectileType<RoaringSwordLungeCharge>(), damage, knockback, player.whoAmI);
            }
            else
            {
                int combo = player.GetModPlayer<RoaringSwordPlayer>().swingCombo;
                float swingDirection = (combo % 2 == 0) ? 1f : -1f;
                player.GetModPlayer<RoaringSwordPlayer>().swingCombo++;

                Vector2 toMouse = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
                float aimAngle = toMouse.ToRotation();

                Projectile.NewProjectile(source, player.Center, Vector2.Zero, ModContent.ProjectileType<RoaringSwordSwing>(), damage, knockback, player.whoAmI, swingDirection, aimAngle);
            }
            return false;
        }

        public override void HoldItem(Player player)
        {
            var swordPlayer = player.GetModPlayer<RoaringSwordPlayer>();
            swordPlayer.isHoldingWillbreaker = true;
            swordPlayer.imbuedWillbreakerVariant = (int)Variant;
            if (Variant == ImbuedWillbreakerVariant.Patience)
                swordPlayer.willbreakerMaxMarks = 7;

            Lighting.AddLight(player.Center, 0.6f, 0.2f, 0.6f);

            bool lungeActive = false;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                if (Main.projectile[i].active && Main.projectile[i].owner == player.whoAmI)
                {
                    int projType = Main.projectile[i].type;
                    if (projType == ModContent.ProjectileType<RoaringSwordLungeCharge>() ||
                        projType == ModContent.ProjectileType<RoaringSwordLunge>() ||
                        projType == ModContent.ProjectileType<RoaringSwordChainLunge>())
                    {
                        lungeActive = true;
                        break;
                    }
                }
            }

            if (Main.mouseRight && player.itemAnimation == 0 && player.whoAmI == Main.myPlayer &&
                !lungeActive && swordPlayer.lungeCooldown <= 0)
            {
                Vector2 velocity = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
                Projectile.NewProjectile(
                    player.GetSource_ItemUse(Item),
                    player.Center,
                    velocity,
                    ModContent.ProjectileType<RoaringSwordLungeCharge>(),
                    player.GetWeaponDamage(Item),
                    player.GetWeaponKnockback(Item, Item.knockBack),
                    player.whoAmI
                );
            }
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
        {
            if (Variant == ImbuedWillbreakerVariant.Determination)
                damage *= 1.10f;
        }

        public override Color? GetAlpha(Color lightColor) => GetTraitColor();

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Item[Item.type].Value;
            Vector2 pos = Item.position - Main.screenPosition + new Vector2(Item.width / 2, Item.height - texture.Height / 2);
            Vector2 origin = texture.Size() * 0.5f;
            spriteBatch.Draw(texture, pos, null, GetTraitColor(), rotation, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            Color traitColor = GetTraitColor();
            foreach (TooltipLine line in tooltips)
            {
                if (line.Name == "ItemName")
                    line.OverrideColor = traitColor;
            }
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<RoaringSword>(), 1)
                .AddIngredient(GetSparkType(), 1)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }

    // --- Concrete variants ---

    public class DeterminationWillbreaker : ImbuedWillbreakerBase
    {
        protected override ImbuedWillbreakerVariant Variant => ImbuedWillbreakerVariant.Determination;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfDetermination>();
    }

    public class IntegrityWillbreaker : ImbuedWillbreakerBase
    {
        protected override ImbuedWillbreakerVariant Variant => ImbuedWillbreakerVariant.Integrity;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfIntegrity>();
    }

    public class PatienceWillbreaker : ImbuedWillbreakerBase
    {
        protected override ImbuedWillbreakerVariant Variant => ImbuedWillbreakerVariant.Patience;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPatience>();
    }

    public class PerseveranceWillbreaker : ImbuedWillbreakerBase
    {
        protected override ImbuedWillbreakerVariant Variant => ImbuedWillbreakerVariant.Perseverance;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPerseverance>();
    }

    public class KindnessWillbreaker : ImbuedWillbreakerBase
    {
        protected override ImbuedWillbreakerVariant Variant => ImbuedWillbreakerVariant.Kindness;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfKindness>();
    }

    public class JusticeWillbreaker : ImbuedWillbreakerBase
    {
        protected override ImbuedWillbreakerVariant Variant => ImbuedWillbreakerVariant.Justice;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfJustice>();
    }

    public class BraveryWillbreaker : ImbuedWillbreakerBase
    {
        protected override ImbuedWillbreakerVariant Variant => ImbuedWillbreakerVariant.Bravery;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfBravery>();
    }
}
