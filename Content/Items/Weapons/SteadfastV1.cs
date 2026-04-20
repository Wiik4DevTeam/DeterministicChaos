using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

namespace DeterministicChaos.Content.Items.Weapons
{
    public class SteadfastV1 : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 30;
            Item.damage = 265;
            Item.knockBack = 6f;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = false;
            Item.rare = ModContent.RarityType<IntegrityRarity>();
            Item.value = Item.buyPrice(gold: 25);
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<SteadfastV1DashProjectile>();
            Item.shootSpeed = 24f;
            Item.DamageType = ModContent.GetInstance<SummonerMeleeDamageClass>();
        }

        public override void SetStaticDefaults()
        {
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 8, SoulTraitType.Integrity);
        }

        public override bool CanUseItem(Player player)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait != SoulTraitType.Integrity)
                return false;

            var sfPlayer = player.GetModPlayer<SteadfastV1Player>();

            if (sfPlayer.IsOnCooldown)
                return false;

            if (sfPlayer.AirDashesRemaining <= 0 && player.velocity.Y != 0f)
                return false;

            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            var sfPlayer = player.GetModPlayer<SteadfastV1Player>();

            // Cursor-directed dash
            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            sfPlayer.StartDash(direction);

            Projectile.NewProjectile(source, player.Center, direction, type, damage, knockback, player.whoAmI);
            SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Pitch = 0.3f, Volume = 0.7f }, player.Center);

            return false;
        }

        public override void HoldItem(Player player)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait == SoulTraitType.Integrity)
            {
                // Passive 25% damage reduction while held
                player.endurance += 0.25f;
            }
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            Color integrityColor = new Color(0, 0, 255);
            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                    line.OverrideColor = integrityColor;
            }

            var player = Main.LocalPlayer;
            var sfPlayer = player.GetModPlayer<SteadfastV1Player>();

            var dashLine = new TooltipLine(Mod, "AirDashes", $"Air Dashes: {sfPlayer.AirDashesRemaining}/{SteadfastV1Player.MAX_AIR_DASHES}");
            dashLine.OverrideColor = sfPlayer.AirDashesRemaining > 0 ? new Color(100, 200, 255) : new Color(150, 150, 150);
            tooltips.Add(dashLine);
        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            var player = Main.LocalPlayer;
            var sfPlayer = player.GetModPlayer<SteadfastV1Player>();

            if (!sfPlayer.IsOnCooldown)
                return;

            float cooldownProgress = sfPlayer.CooldownProgress;
            Texture2D itemTexture = Terraria.GameContent.TextureAssets.Item[Item.type].Value;
            Rectangle sourceRect = new Rectangle(0, (int)(frame.Height * (1f - cooldownProgress)), frame.Width, (int)(frame.Height * cooldownProgress));
            Vector2 drawPos = position + new Vector2(0, frame.Height * (1f - cooldownProgress) * scale);
            spriteBatch.Draw(itemTexture, drawPos, sourceRect, Color.Black * 0.55f, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<AerodynamicBoots>(), 1)
                .AddIngredient(ModContent.ItemType<Titansblood>(), 10)
                .AddIngredient(ModContent.ItemType<SparkOfIntegrity>(), 10)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }
}
