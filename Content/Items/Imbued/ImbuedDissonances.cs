using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Sparks;
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
    public enum ImbuedDissonanceVariant
    {
        Determination,
        Integrity,
        Patience,
        Perseverance,
        Kindness,
        Justice,
        Bravery
    }

    public abstract class ImbuedDissonanceBase : ModItem
    {
        protected abstract ImbuedDissonanceVariant Variant { get; }
        protected abstract int GetSparkType();

        public override string Texture => "DeterministicChaos/Content/Items/Weapons/RoaringGun";

        private SoulTraitType GetSoulTraitType()
        {
            return Variant switch
            {
                ImbuedDissonanceVariant.Determination => SoulTraitType.Determination,
                ImbuedDissonanceVariant.Integrity => SoulTraitType.Integrity,
                ImbuedDissonanceVariant.Patience => SoulTraitType.Patience,
                ImbuedDissonanceVariant.Perseverance => SoulTraitType.Perseverance,
                ImbuedDissonanceVariant.Kindness => SoulTraitType.Kindness,
                ImbuedDissonanceVariant.Justice => SoulTraitType.Justice,
                ImbuedDissonanceVariant.Bravery => SoulTraitType.Bravery,
                _ => SoulTraitType.None
            };
        }

        protected Color GetTraitColor()
        {
            return Variant switch
            {
                ImbuedDissonanceVariant.Determination => new Color(255, 60, 60),
                ImbuedDissonanceVariant.Integrity => new Color(0, 0, 255),
                ImbuedDissonanceVariant.Patience => new Color(80, 255, 255),
                ImbuedDissonanceVariant.Perseverance => new Color(255, 80, 255),
                ImbuedDissonanceVariant.Kindness => new Color(80, 230, 80),
                ImbuedDissonanceVariant.Justice => new Color(255, 255, 80),
                ImbuedDissonanceVariant.Bravery => new Color(255, 190, 60),
                _ => Color.White
            };
        }

        public int GetMaxStacks()
        {
            if (Variant == ImbuedDissonanceVariant.Patience) return 30;
            if (Variant == ImbuedDissonanceVariant.Bravery) return 10;
            return RoaringGun.MaxStacks;
        }

        public override void SetStaticDefaults()
        {
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 6, GetSoulTraitType());
        }

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 20;
            Item.damage = 34;
            Item.knockBack = 3f;
            Item.useTime = RoaringGun.BaseUseTime;
            Item.useAnimation = RoaringGun.BaseUseTime;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.value = Item.buyPrice(gold: 5);
            Item.UseSound = SoundID.Item11;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 16f;
            Item.useAmmo = AmmoID.Bullet;
            Item.DamageType = DamageClass.Ranged;

            if (Variant == ImbuedDissonanceVariant.Perseverance)
                Item.DamageType = ModContent.GetInstance<RangedMagicDamageClass>();
        }

        public override void HoldItem(Player player)
        {
            var gunPlayer = player.GetModPlayer<RoaringGunPlayer>();
            gunPlayer.isHoldingRoaringGun = true;
            gunPlayer.imbuedDissonanceVariant = (int)Variant;
            gunPlayer.gunMaxStacks = GetMaxStacks();
            Lighting.AddLight(player.Center, 0.9f, 0.9f, 0.9f);
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
        {
            int stacks = player.GetModPlayer<RoaringGunPlayer>().gunStacks;
            damage += stacks * 0.02f;
        }

        public override float UseSpeedMultiplier(Player player)
        {
            int stacks = player.GetModPlayer<RoaringGunPlayer>().gunStacks;

            // Bravery: no fire rate increase from stacks
            if (Variant == ImbuedDissonanceVariant.Bravery)
                return 1f;

            // Patience: extended curve with higher max fire rate
            if (Variant == ImbuedDissonanceVariant.Patience)
            {
                if (stacks == 0) return 1f;
                else if (stacks <= 28) return 1f + (stacks / 28f) * 2f; // 1x to 3x
                else if (stacks == 29) return 5f;
                else return 7f; // 30 stacks = 7x
            }

            // Default curve (same as base RoaringGun)
            if (stacks == 0) return 1f;
            else if (stacks <= 18) return 1f + (stacks / 18f) * 1f;
            else if (stacks == 19) return 2.5f;
            else return 5f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            var gunPlayer = player.GetModPlayer<RoaringGunPlayer>();

            if (gunPlayer.decayTimer <= 0 && gunPlayer.gunStacks > 0)
            {
                float speedMult = UseSpeedMultiplier(player);
                int effectiveUseTime = (int)(RoaringGun.BaseUseTime / speedMult);
                gunPlayer.decayTimer = effectiveUseTime * 2;
            }

            // Bravery: fire extra pellets based on stacks (shotgun spread)
            if (Variant == ImbuedDissonanceVariant.Bravery && gunPlayer.gunStacks > 0)
            {
                int pellets = 1 + gunPlayer.gunStacks;
                float spreadAngle = MathHelper.ToRadians(Math.Min(pellets * 4f, 50f));

                for (int i = 0; i < pellets; i++)
                {
                    float angle = (Main.rand.NextFloat() - 0.5f) * spreadAngle;
                    Vector2 pelletVel = velocity.RotatedBy(angle);
                    pelletVel *= 1f + Main.rand.NextFloat(-0.15f, 0.15f);
                    Projectile.NewProjectile(source, position, pelletVel, type, damage, knockback, player.whoAmI);
                }
                return false;
            }

            return true;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit)
        {
            if (Variant == ImbuedDissonanceVariant.Determination)
                crit += 10;
        }

        public override Color? GetAlpha(Color lightColor)
        {
            return GetTraitColor();
        }

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

            Player player = Main.LocalPlayer;
            int stacks = player.GetModPlayer<RoaringGunPlayer>().gunStacks;
            int maxStacks = GetMaxStacks();
            tooltips.Add(new TooltipLine(Mod, "StackInfo", $"Current stacks: {stacks}/{maxStacks}"));
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<RoaringGun>(), 1)
                .AddIngredient(GetSparkType(), 1)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }

    // --- Concrete variants ---

    public class DeterminationGun : ImbuedDissonanceBase
    {
        protected override ImbuedDissonanceVariant Variant => ImbuedDissonanceVariant.Determination;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfDetermination>();
    }

    public class IntegrityGun : ImbuedDissonanceBase
    {
        protected override ImbuedDissonanceVariant Variant => ImbuedDissonanceVariant.Integrity;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfIntegrity>();
    }

    public class PatienceGun : ImbuedDissonanceBase
    {
        protected override ImbuedDissonanceVariant Variant => ImbuedDissonanceVariant.Patience;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPatience>();
    }

    public class PerseveranceGun : ImbuedDissonanceBase
    {
        protected override ImbuedDissonanceVariant Variant => ImbuedDissonanceVariant.Perseverance;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPerseverance>();
    }

    public class KindnessGun : ImbuedDissonanceBase
    {
        protected override ImbuedDissonanceVariant Variant => ImbuedDissonanceVariant.Kindness;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfKindness>();
    }

    public class JusticeGun : ImbuedDissonanceBase
    {
        protected override ImbuedDissonanceVariant Variant => ImbuedDissonanceVariant.Justice;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfJustice>();
    }

    public class BraveryGun : ImbuedDissonanceBase
    {
        protected override ImbuedDissonanceVariant Variant => ImbuedDissonanceVariant.Bravery;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfBravery>();
    }
}
