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
    public class TornNotebook : ModItem
    {
        public const int MANA_PER_LETTER = 20;

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 32;
            Item.damage = 8;
            Item.knockBack = 1f;
            Item.useTime = 10;
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = false; // Single click fires entire sequence
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 2);
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<TornNotebookLetter>();
            Item.shootSpeed = 5f;
            Item.mana = 0; // We handle mana manually per letter
            
            // Set to hybrid Magic/Rogue damage class
            SetRogueDamageClass();
        }

        private void SetRogueDamageClass()
        {
            // Try to use Calamity's Rogue class if available
            try
            {
                if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
                {
                    if (calamity.TryFind<DamageClass>("RogueDamageClass", out var rogueClass))
                    {
                        // Use our custom hybrid class
                        Item.DamageType = ModContent.GetInstance<MagicRogueDamageClass>();
                        return;
                    }
                }
            }
            catch { }

            // Fallback to Magic if Calamity not available
            Item.DamageType = DamageClass.Magic;
        }

        public override void SetStaticDefaults()
        {
            // Register +3 Perseverance weapon investment
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 3);
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
            {
                return false;
            }

            if (player.altFunctionUse == 2)
            {
                // Alt-fire opens the UI
                return true;
            }

            // Normal attack: check if not already firing a sequence and we have text
            var notebookPlayer = player.GetModPlayer<TornNotebookPlayer>();
            
            // Don't allow if already in a firing sequence or on cooldown
            if (notebookPlayer.IsAutoFiring || notebookPlayer.IsOnCooldown)
                return false;
            
            return notebookPlayer.HasLettersToFire() && player.statMana >= MANA_PER_LETTER;
        }

        public override bool? UseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                // Open the UI
                var uiSystem = ModContent.GetInstance<TornNotebookUISystem>();
                if (uiSystem != null)
                {
                    uiSystem.ToggleUI();
                }
                return true;
            }

            return null; // Let Shoot handle normal attack
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Don't shoot on alt-fire
            if (player.altFunctionUse == 2)
                return false;

            // Only the local player handles firing
            if (player.whoAmI != Main.myPlayer)
                return false;

            var notebookPlayer = player.GetModPlayer<TornNotebookPlayer>();
            
            // Don't start new sequence if already firing
            if (notebookPlayer.IsAutoFiring)
                return false;
            
            // Start the auto-firing sequence
            notebookPlayer.StartAutoFireSequence(source, velocity, type, damage, knockback);

            return false;
        }

        public static int GetLetterIndex(char c)
        {
            c = char.ToUpper(c);
            if (c >= 'A' && c <= 'Z')
                return c - 'A';
            if (c == ' ')
                return 26; // Space is last cell
            return -1; // Invalid character
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            var player = Main.LocalPlayer;
            var notebookPlayer = player.GetModPlayer<TornNotebookPlayer>();
            
            // Add current word info
            string currentText = string.IsNullOrEmpty(notebookPlayer.StoredText) ? "[Empty]" : notebookPlayer.StoredText;
            var wordLine = new TooltipLine(Mod, "StoredWord", $"Current word: {currentText}");
            wordLine.OverrideColor = new Color(255, 0, 255); // Purple for Perseverance
            tooltips.Add(wordLine);
        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            var player = Main.LocalPlayer;
            var notebookPlayer = player.GetModPlayer<TornNotebookPlayer>();
            
            if (!notebookPlayer.IsOnCooldown)
                return;

            // Draw cooldown overlay
            float cooldownProgress = notebookPlayer.CooldownProgress;
            
            // Get item texture
            Texture2D itemTexture = Terraria.GameContent.TextureAssets.Item[Item.type].Value;
            
            // Draw semi-transparent dark overlay on the item (darkens as cooldown is active)
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
            
            // Draw remaining cooldown time as text
            float secondsRemaining = cooldownProgress * 5f; // 5 second max cooldown
            string timeText = secondsRemaining.ToString("0.0");
            Vector2 textSize = Terraria.GameContent.FontAssets.ItemStack.Value.MeasureString(timeText);
            Vector2 textPos = position + (frame.Size() * scale / 2f) - (textSize / 2f);
            
            // Draw text with shadow
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
    }
}
