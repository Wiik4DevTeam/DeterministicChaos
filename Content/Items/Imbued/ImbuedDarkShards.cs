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
    public enum ImbuedDarkShardVariant
    {
        Determination,
        Integrity,
        Patience,
        Perseverance,
        Kindness,
        Justice,
        Bravery
    }

    public abstract class ImbuedDarkShardBase : ModItem
    {
        protected abstract ImbuedDarkShardVariant Variant { get; }
        protected abstract int GetSparkType();
        protected abstract int GetProjectileType();

        private static bool? calamityLoaded = null;

        public override string Texture => "DeterministicChaos/Content/Items/Accessories/DarkShard";

        private SoulTraitType GetSoulTraitType()
        {
            return Variant switch
            {
                ImbuedDarkShardVariant.Determination => SoulTraitType.Determination,
                ImbuedDarkShardVariant.Integrity => SoulTraitType.Integrity,
                ImbuedDarkShardVariant.Patience => SoulTraitType.Patience,
                ImbuedDarkShardVariant.Perseverance => SoulTraitType.Perseverance,
                ImbuedDarkShardVariant.Kindness => SoulTraitType.Kindness,
                ImbuedDarkShardVariant.Justice => SoulTraitType.Justice,
                ImbuedDarkShardVariant.Bravery => SoulTraitType.Bravery,
                _ => SoulTraitType.None
            };
        }

        protected Color GetTraitColor()
        {
            return Variant switch
            {
                ImbuedDarkShardVariant.Determination => new Color(255, 60, 60),
                ImbuedDarkShardVariant.Integrity => new Color(0, 0, 255),
                ImbuedDarkShardVariant.Patience => new Color(80, 255, 255),
                ImbuedDarkShardVariant.Perseverance => new Color(255, 80, 255),
                ImbuedDarkShardVariant.Kindness => new Color(80, 230, 80),
                ImbuedDarkShardVariant.Justice => new Color(255, 255, 80),
                ImbuedDarkShardVariant.Bravery => new Color(255, 190, 60),
                _ => Color.White
            };
        }

        public override void SetStaticDefaults()
        {
            calamityLoaded ??= ModLoader.HasMod("CalamityMod");
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 6, GetSoulTraitType());
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
            Item.shoot = GetProjectileType();
            Item.shootSpeed = 14f;

            Item.DamageType = DamageClass.Throwing;
            if (calamityLoaded == true)
                SetCalamityRogueDefaults();

            // Patience: 20% less base damage
            if (Variant == ImbuedDarkShardVariant.Patience)
            {
                Item.damage = (int)(49 * 0.80f);
            }
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

        public override bool AltFunctionUse(Player player)
        {
            // All imbued variants retain Dark Fountain functionality
            return true;
        }

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                if (Subworlds.DarkDimension.IsInDarkWorld)
                    return !VFX.TitanSpawnCutscene.IsActive;
                return !VFX.DarkWorldCutscene.IsPlaying;
            }
            return true;
        }

        public override bool? UseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                if (Subworlds.DarkDimension.IsInDarkWorld)
                {
                    VFX.TitanSpawnCutscene.StartCutscene();
                    return true;
                }
                DarkShard.ActivateDarkWorldPortalStatic(player);
                return true;
            }
            return null;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.altFunctionUse == 2)
                return false;

            Vector2 handPosition = player.RotatedRelativePoint(player.MountedCenter);
            handPosition += velocity.SafeNormalize(Vector2.Zero) * 20f;
            handPosition.Y -= 10f;

            bool isStealthStrike = DarkShardPlayer.CheckCalamityStealthStrike(player);

            // Perseverance: Consume 250 mana to force a stealth strike
            if (Variant == ImbuedDarkShardVariant.Perseverance && !isStealthStrike)
            {
                if (DarkShardPlayer.TryConsumeMana(player, 250))
                    isStealthStrike = true;
            }

            // Bravery: Stealth strike is completely removed
            if (Variant == ImbuedDarkShardVariant.Bravery)
                isStealthStrike = false;

            int finalDamage = damage;

            // Patience: +50% stealth strike damage
            if (Variant == ImbuedDarkShardVariant.Patience && isStealthStrike)
                finalDamage = (int)(damage * 1.50f / 0.80f); // Undo the 0.80 from SetDefaults, then apply 1.50

            if (isStealthStrike)
            {
                int knifeCount = 7;
                float spreadAngle = MathHelper.ToRadians(12);
                float startAngle = -spreadAngle / 2f;
                float angleStep = spreadAngle / (knifeCount - 1);

                for (int i = 0; i < knifeCount; i++)
                {
                    float angle = startAngle + angleStep * i;
                    Vector2 fanVelocity = velocity.RotatedBy(angle);
                    int p = Projectile.NewProjectile(source, handPosition, fanVelocity, type, finalDamage, knockback, player.whoAmI);
                    if (p >= 0 && p < Main.maxProjectiles)
                        Main.projectile[p].ai[0] = 1f; // Mark as stealth strike
                }
            }
            else
            {
                Projectile.NewProjectile(source, handPosition, velocity, type, finalDamage, knockback, player.whoAmI);
            }

            return false;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit)
        {
            // Determination: +7% critical strike chance
            if (Variant == ImbuedDarkShardVariant.Determination)
                crit += 7;
        }

        public override Color? GetAlpha(Color lightColor)
        {
            return GetTraitColor();
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Item[Item.type].Value;
            Vector2 position = Item.position - Main.screenPosition + new Vector2(Item.width / 2, Item.height - texture.Height / 2);
            Vector2 origin = texture.Size() * 0.5f;
            spriteBatch.Draw(texture, position, null, GetTraitColor(), rotation, origin, scale, SpriteEffects.None, 0f);
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
                .AddIngredient(ModContent.ItemType<DarkShard>(), 1)
                .AddIngredient(GetSparkType(), 1)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }

    public class DeterminationDarkShard : ImbuedDarkShardBase
    {
        protected override ImbuedDarkShardVariant Variant => ImbuedDarkShardVariant.Determination;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfDetermination>();
        protected override int GetProjectileType() => ModContent.ProjectileType<DeterminationDarkShardProjectile>();
    }

    public class IntegrityDarkShard : ImbuedDarkShardBase
    {
        protected override ImbuedDarkShardVariant Variant => ImbuedDarkShardVariant.Integrity;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfIntegrity>();
        protected override int GetProjectileType() => ModContent.ProjectileType<IntegrityDarkShardProjectile>();
    }

    public class PatienceDarkShard : ImbuedDarkShardBase
    {
        protected override ImbuedDarkShardVariant Variant => ImbuedDarkShardVariant.Patience;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPatience>();
        protected override int GetProjectileType() => ModContent.ProjectileType<PatienceDarkShardProjectile>();
    }

    public class PerseveranceDarkShard : ImbuedDarkShardBase
    {
        protected override ImbuedDarkShardVariant Variant => ImbuedDarkShardVariant.Perseverance;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPerseverance>();
        protected override int GetProjectileType() => ModContent.ProjectileType<PerseveranceDarkShardProjectile>();
    }

    public class KindnessDarkShard : ImbuedDarkShardBase
    {
        protected override ImbuedDarkShardVariant Variant => ImbuedDarkShardVariant.Kindness;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfKindness>();
        protected override int GetProjectileType() => ModContent.ProjectileType<KindnessDarkShardProjectile>();
    }

    public class JusticeDarkShard : ImbuedDarkShardBase
    {
        protected override ImbuedDarkShardVariant Variant => ImbuedDarkShardVariant.Justice;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfJustice>();
        protected override int GetProjectileType() => ModContent.ProjectileType<JusticeDarkShardProjectile>();
    }

    public class BraveryDarkShard : ImbuedDarkShardBase
    {
        protected override ImbuedDarkShardVariant Variant => ImbuedDarkShardVariant.Bravery;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfBravery>();
        protected override int GetProjectileType() => ModContent.ProjectileType<BraveryDarkShardProjectile>();
    }
}
