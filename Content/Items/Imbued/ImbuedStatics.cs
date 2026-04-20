using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
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
    public enum ImbuedStaticVariant
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

    public abstract class ImbuedStaticBase : ModItem
    {
        protected abstract ImbuedStaticVariant Variant { get; }
        protected abstract int GetSparkType();

        public override string Texture => "DeterministicChaos/Content/Items/Weapons/RoaringYoyo";

        private SoulTraitType GetSoulTraitType()
        {
            return Variant switch
            {
                ImbuedStaticVariant.Determination => SoulTraitType.Determination,
                ImbuedStaticVariant.Integrity => SoulTraitType.Integrity,
                ImbuedStaticVariant.Patience => SoulTraitType.Patience,
                ImbuedStaticVariant.Perseverance => SoulTraitType.Perseverance,
                ImbuedStaticVariant.Kindness => SoulTraitType.Kindness,
                ImbuedStaticVariant.Justice => SoulTraitType.Justice,
                ImbuedStaticVariant.Bravery => SoulTraitType.Bravery,
                _ => SoulTraitType.None
            };
        }

        protected Color GetTraitColor()
        {
            return Variant switch
            {
                ImbuedStaticVariant.Determination => new Color(255, 60, 60),
                ImbuedStaticVariant.Integrity => new Color(0, 0, 255),
                ImbuedStaticVariant.Patience => new Color(80, 255, 255),
                ImbuedStaticVariant.Perseverance => new Color(255, 80, 255),
                ImbuedStaticVariant.Kindness => new Color(80, 230, 80),
                ImbuedStaticVariant.Justice => new Color(255, 255, 80),
                ImbuedStaticVariant.Bravery => new Color(255, 190, 60),
                _ => Color.White
            };
        }

        public override void SetStaticDefaults()
        {
            ItemID.Sets.Yoyo[Type] = true;
            ItemID.Sets.GamepadExtraRange[Type] = 15;
            ItemID.Sets.GamepadSmartQuickReach[Type] = true;
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 6, GetSoulTraitType());
        }

        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 26;

            Item.DamageType = DamageClass.MeleeNoSpeed;
            Item.damage = 70;
            Item.knockBack = 2.5f;
            Item.crit = 4;

            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.autoReuse = true;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = SoundID.Item1;
            Item.channel = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;

            Item.shoot = ModContent.ProjectileType<RoaringYoyoProjectile>();
            Item.shootSpeed = 16f;

            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.value = Item.buyPrice(gold: 8);
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
        {
            if (Variant == ImbuedStaticVariant.Determination)
                damage *= 1.08f;
        }

        public override void HoldItem(Player player)
        {
            var yp = player.GetModPlayer<RoaringYoyoPlayer>();
            yp.isHoldingStatic = true;
            yp.imbuedStaticVariant = Variant;

            Vector3 light = GetTraitColor().ToVector3() * 0.6f;
            Lighting.AddLight(player.Center, light);

            // Perseverance: right-click pulls stars with no cooldown for 70 mana per click.
            // Other variants get the base 2-second cooldown / no-mana pull from RoaringYoyo.HoldItem,
            // but ImbuedStaticBase doesn't inherit from RoaringYoyo, so we reimplement it here.
            if (Variant == ImbuedStaticVariant.Perseverance)
            {
                if (player.whoAmI == Main.myPlayer && Main.mouseRight && Main.mouseRightRelease)
                {
                    if (player.CheckMana(70, true))
                    {
                        yp.PerseverancePullStars();
                        SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.7f, Pitch = 0.8f }, player.Center);
                    }
                }
            }
            else if (player.whoAmI == Main.myPlayer
                && Main.mouseRight && Main.mouseRightRelease
                && yp.perseverancePullCooldown <= 0)
            {
                yp.perseverancePullCooldown = 120; // 2 seconds
                yp.PerseverancePullStars();
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.7f, Pitch = 0.4f }, player.Center);
            }
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
                .AddIngredient(ModContent.ItemType<RoaringYoyo>(), 1)
                .AddIngredient(GetSparkType(), 1)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }

    // --- Concrete variants ---

    public class DeterminationStatic : ImbuedStaticBase
    {
        protected override ImbuedStaticVariant Variant => ImbuedStaticVariant.Determination;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfDetermination>();
    }

    public class IntegrityStatic : ImbuedStaticBase
    {
        protected override ImbuedStaticVariant Variant => ImbuedStaticVariant.Integrity;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfIntegrity>();
    }

    public class PatienceStatic : ImbuedStaticBase
    {
        protected override ImbuedStaticVariant Variant => ImbuedStaticVariant.Patience;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPatience>();
    }

    public class PerseveranceStatic : ImbuedStaticBase
    {
        protected override ImbuedStaticVariant Variant => ImbuedStaticVariant.Perseverance;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPerseverance>();
    }

    public class KindnessStatic : ImbuedStaticBase
    {
        protected override ImbuedStaticVariant Variant => ImbuedStaticVariant.Kindness;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfKindness>();
    }

    public class JusticeStatic : ImbuedStaticBase
    {
        protected override ImbuedStaticVariant Variant => ImbuedStaticVariant.Justice;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfJustice>();
    }

    public class BraveryStatic : ImbuedStaticBase
    {
        protected override ImbuedStaticVariant Variant => ImbuedStaticVariant.Bravery;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfBravery>();
    }
}
