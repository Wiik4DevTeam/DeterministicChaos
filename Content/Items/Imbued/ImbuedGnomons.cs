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
    public enum ImbuedGnomonVariant
    {
        None,
        Determination,
        Integrity,
        Patience,
        Perseverance,
        Kindness,
        Justice,
        Bravery
    }

    public abstract class ImbuedGnomonBase : ModItem
    {
        protected abstract ImbuedGnomonVariant Variant { get; }
        protected abstract int GetSparkType();

        public override string Texture => "DeterministicChaos/Content/Items/Weapons/RoaringWhip";

        private SoulTraitType GetSoulTraitType()
        {
            return Variant switch
            {
                ImbuedGnomonVariant.Determination => SoulTraitType.Determination,
                ImbuedGnomonVariant.Integrity => SoulTraitType.Integrity,
                ImbuedGnomonVariant.Patience => SoulTraitType.Patience,
                ImbuedGnomonVariant.Perseverance => SoulTraitType.Perseverance,
                ImbuedGnomonVariant.Kindness => SoulTraitType.Kindness,
                ImbuedGnomonVariant.Justice => SoulTraitType.Justice,
                ImbuedGnomonVariant.Bravery => SoulTraitType.Bravery,
                _ => SoulTraitType.None
            };
        }

        protected Color GetTraitColor()
        {
            return Variant switch
            {
                ImbuedGnomonVariant.Determination => new Color(255, 60, 60),
                ImbuedGnomonVariant.Integrity => new Color(0, 0, 255),
                ImbuedGnomonVariant.Patience => new Color(80, 255, 255),
                ImbuedGnomonVariant.Perseverance => new Color(255, 80, 255),
                ImbuedGnomonVariant.Kindness => new Color(80, 230, 80),
                ImbuedGnomonVariant.Justice => new Color(255, 255, 80),
                ImbuedGnomonVariant.Bravery => new Color(255, 190, 60),
                _ => Color.White
            };
        }

        public override void SetStaticDefaults()
        {
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 6, GetSoulTraitType());
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.DefaultToWhip(ModContent.ProjectileType<RoaringWhipProjectile>(), RoaringWhip.BaseDamage, RoaringWhip.BaseKnockback, RoaringWhip.ShootSpeed);
            Item.autoReuse = true;
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.value = Item.buyPrice(gold: 8);
        }

        public override void HoldItem(Player player)
        {
            var whipPlayer = player.GetModPlayer<RoaringWhipPlayer>();
            whipPlayer.isHoldingGnomon = true;
            whipPlayer.imbuedGnomonVariant = Variant;

            Vector3 light = GetTraitColor().ToVector3() * 0.6f;
            Lighting.AddLight(player.Center, light);
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
        {
            if (Variant == ImbuedGnomonVariant.Determination)
                damage *= 1.1f;
            else if (Variant == ImbuedGnomonVariant.Patience)
                damage *= 0.5f;
        }

        public override float UseSpeedMultiplier(Player player)
        {
            float mult = 1f;

            // Bravery: +40% attack speed
            if (Variant == ImbuedGnomonVariant.Bravery)
                mult *= 1.4f;

            // Perseverance: +35% attack speed for 1 hit while speed buff is active
            if (Variant == ImbuedGnomonVariant.Perseverance)
            {
                var whipPlayer = player.GetModPlayer<RoaringWhipPlayer>();
                if (whipPlayer.perseveranceSpeedTimer > 0)
                    mult *= 1.35f;
            }

            return mult;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Perseverance: consume 25 mana on use to activate the speed buff,
            // but only when the buff is not already active (prevents per-swing mana drain and any perceived stacking).
            if (Variant == ImbuedGnomonVariant.Perseverance)
            {
                var whipPlayer = player.GetModPlayer<RoaringWhipPlayer>();
                // Consume the speed buff (this swing was the fast one)
                if (whipPlayer.perseveranceSpeedTimer > 0)
                    whipPlayer.perseveranceSpeedTimer = 0;

                // Activate speed buff for next swing if we have mana
                if (whipPlayer.perseveranceSpeedTimer <= 0 && player.CheckMana(25, pay: true))
                {
                    whipPlayer.perseveranceSpeedTimer = 1;
                }
            }

            return true;
        }

        public override bool MeleePrefix() => true;

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
                .AddIngredient(ModContent.ItemType<RoaringWhip>(), 1)
                .AddIngredient(GetSparkType(), 1)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }

    // --- Concrete variants ---

    public class DeterminationGnomon : ImbuedGnomonBase
    {
        protected override ImbuedGnomonVariant Variant => ImbuedGnomonVariant.Determination;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfDetermination>();
    }

    public class IntegrityGnomon : ImbuedGnomonBase
    {
        protected override ImbuedGnomonVariant Variant => ImbuedGnomonVariant.Integrity;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfIntegrity>();
    }

    public class PatienceGnomon : ImbuedGnomonBase
    {
        protected override ImbuedGnomonVariant Variant => ImbuedGnomonVariant.Patience;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPatience>();
    }

    public class PerseveranceGnomon : ImbuedGnomonBase
    {
        protected override ImbuedGnomonVariant Variant => ImbuedGnomonVariant.Perseverance;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPerseverance>();
    }

    public class KindnessGnomon : ImbuedGnomonBase
    {
        protected override ImbuedGnomonVariant Variant => ImbuedGnomonVariant.Kindness;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfKindness>();
    }

    public class JusticeGnomon : ImbuedGnomonBase
    {
        protected override ImbuedGnomonVariant Variant => ImbuedGnomonVariant.Justice;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfJustice>();
    }

    public class BraveryGnomon : ImbuedGnomonBase
    {
        protected override ImbuedGnomonVariant Variant => ImbuedGnomonVariant.Bravery;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfBravery>();
    }
}
