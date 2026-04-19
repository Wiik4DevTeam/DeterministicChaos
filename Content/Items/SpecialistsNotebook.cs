using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;
using DeterministicChaos.Content.SoulTraits;

namespace DeterministicChaos.Content.Items
{
    public class SpecialistsNotebook : ModItem
    {
        public const int MANA_PER_LETTER = 8;

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 32;
            Item.damage = 12;
            Item.knockBack = 1.5f;
            Item.useTime = 8;
            Item.useAnimation = 8;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = false;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(gold: 10);
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<TornNotebookLetter>();
            Item.shootSpeed = 6f;
            Item.mana = 0; // We handle mana manually per letter

            // Set to hybrid Magic/Rogue damage class
            SetRogueDamageClass();
        }

        private void SetRogueDamageClass()
        {
            try
            {
                if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
                {
                    if (calamity.TryFind<DamageClass>("RogueDamageClass", out var rogueClass))
                    {
                        Item.DamageType = ModContent.GetInstance<MagicRogueDamageClass>();
                        return;
                    }
                }
            }
            catch { }

            Item.DamageType = DamageClass.Magic;
        }

        public override void SetStaticDefaults()
        {
            // Register +6 Perseverance weapon investment (upgraded from +3)
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 6, SoulTraitType.Perseverance);
        }

        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        public override bool CanUseItem(Player player)
        {
            // Requires Perseverance trait to use
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait != SoulTraitType.Perseverance)
                return false;

            if (player.altFunctionUse == 2)
                return true;

            var notebookPlayer = player.GetModPlayer<SpecialistsNotebookPlayer>();

            if (notebookPlayer.IsAutoFiring || notebookPlayer.IsOnCooldown)
                return false;

            return notebookPlayer.HasLettersToFire() && player.statMana >= MANA_PER_LETTER;
        }

        public override bool? UseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                // Open the UI (shared with TornNotebook)
                var uiSystem = ModContent.GetInstance<SpecialistsNotebookUISystem>();
                if (uiSystem != null)
                {
                    uiSystem.ToggleUI();
                }
                return true;
            }

            return null;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.altFunctionUse == 2)
                return false;

            if (player.whoAmI != Main.myPlayer)
                return false;

            var notebookPlayer = player.GetModPlayer<SpecialistsNotebookPlayer>();

            if (notebookPlayer.IsAutoFiring)
                return false;

            notebookPlayer.StartAutoFireSequence(source, velocity, type, damage, knockback);

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            var player = Main.LocalPlayer;
            var notebookPlayer = player.GetModPlayer<SpecialistsNotebookPlayer>();

            string currentText = string.IsNullOrEmpty(notebookPlayer.StoredText) ? "[Empty]" : notebookPlayer.StoredText;
            var wordLine = new TooltipLine(Mod, "StoredWord", $"Current word: {currentText}");
            wordLine.OverrideColor = new Color(255, 0, 255);
            tooltips.Add(wordLine);
        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            var player = Main.LocalPlayer;
            var notebookPlayer = player.GetModPlayer<SpecialistsNotebookPlayer>();

            if (!notebookPlayer.IsOnCooldown)
                return;

            float cooldownProgress = notebookPlayer.CooldownProgress;

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

            float secondsRemaining = cooldownProgress * 5f;
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
                .AddIngredient(ModContent.ItemType<TornNotebook>(), 1)
                .AddIngredient(ModContent.ItemType<SoulCatalyst>(), 1)
                .AddIngredient(ItemID.CobaltBar, 10)
                .AddTile(TileID.Anvils)
                .Register();

            CreateRecipe()
                .AddIngredient(ModContent.ItemType<TornNotebook>(), 1)
                .AddIngredient(ModContent.ItemType<SoulCatalyst>(), 1)
                .AddIngredient(ItemID.PalladiumBar, 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
