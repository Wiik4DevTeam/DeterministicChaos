using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    public class AerodynamicBoots : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 30;
            Item.damage = 43;
            Item.knockBack = 5f;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = false;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(gold: 10);
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<AerodynamicBootsProjectile>();
            Item.shootSpeed = 20f;
            Item.DamageType = ModContent.GetInstance<SummonerMeleeDamageClass>();
        }

        public override void SetStaticDefaults()
        {
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 6, SoulTraitType.Integrity);
        }

        public override bool CanUseItem(Player player)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait != SoulTraitType.Integrity)
                return false;

            var bootsPlayer = player.GetModPlayer<AerodynamicBootsPlayer>();
            if (bootsPlayer.IsOnCooldown)
                return false;

            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            var bootsPlayer = player.GetModPlayer<AerodynamicBootsPlayer>();
            int finalDamage = damage + (bootsPlayer.GraceStacks * 8);

            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);

            bootsPlayer.StartDash(direction);
            // Player velocity is set by StartDash via PostUpdate

            Projectile.NewProjectile(source, player.Center, direction, type, finalDamage, knockback, player.whoAmI, bootsPlayer.GraceStacks);

            SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Pitch = 0.5f, Volume = 0.7f }, player.Center);

            return false;
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
            var bootsPlayer = player.GetModPlayer<AerodynamicBootsPlayer>();
            if (bootsPlayer.GraceStacks > 0)
            {
                var stackLine = new TooltipLine(Mod, "GraceStacks", $"Grace Stacks: {bootsPlayer.GraceStacks}/{AerodynamicBootsPlayer.MAX_GRACE_STACKS} (halves next damage taken)");
                stackLine.OverrideColor = new Color(200, 150, 255);
                tooltips.Add(stackLine);
            }
        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            var player = Main.LocalPlayer;
            var bootsPlayer = player.GetModPlayer<AerodynamicBootsPlayer>();

            if (!bootsPlayer.IsOnCooldown)
                return;

            float cooldownProgress = bootsPlayer.CooldownProgress;

            Texture2D itemTexture = Terraria.GameContent.TextureAssets.Item[Item.type].Value;

            float overlayAlpha = 0.5f * cooldownProgress;
            Color overlayColor = Color.Black * overlayAlpha;

            spriteBatch.Draw(
                itemTexture,
                position,
                frame,
                overlayColor,
                0f,
                origin,
                scale,
                SpriteEffects.None,
                0f
            );

            float maxCooldown = bootsPlayer.CurrentMaxCooldownSeconds;
            float secondsRemaining = cooldownProgress * maxCooldown;
            string timeText = secondsRemaining.ToString("0.0");
            Vector2 textSize = Terraria.GameContent.FontAssets.ItemStack.Value.MeasureString(timeText);
            Vector2 textPos = position + (frame.Size() * scale / 2f) - (textSize / 2f);

            Terraria.UI.Chat.ChatManager.DrawColorCodedStringWithShadow(
                spriteBatch,
                Terraria.GameContent.FontAssets.ItemStack.Value,
                timeText,
                textPos,
                Color.White,
                0f,
                Vector2.Zero,
                Vector2.One * 0.8f
            );
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<BalletShoes>(), 1)
                .AddIngredient(ModContent.ItemType<SoulCatalyst>(), 1)
                .AddIngredient(ItemID.CobaltBar, 10)
                .AddTile(TileID.Anvils)
                .Register();

            CreateRecipe()
                .AddIngredient(ModContent.ItemType<BalletShoes>(), 1)
                .AddIngredient(ModContent.ItemType<SoulCatalyst>(), 1)
                .AddIngredient(ItemID.PalladiumBar, 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
