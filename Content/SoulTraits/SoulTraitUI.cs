using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace DeterministicChaos.Content.SoulTraits
{
    public static class TextWrapUtility
    {
        public static string WrapText(string text, float maxWidth, float scale = 1f)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var font = FontAssets.MouseText.Value;
            string[] words = text.Split(' ');
            StringBuilder wrappedText = new StringBuilder();
            StringBuilder currentLine = new StringBuilder();

            foreach (string word in words)
            {
                string testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
                Vector2 size = font.MeasureString(testLine) * scale;

                if (size.X > maxWidth && currentLine.Length > 0)
                {
                    wrappedText.AppendLine(currentLine.ToString());
                    currentLine.Clear();
                    currentLine.Append(word);
                }
                else
                {
                    if (currentLine.Length > 0)
                        currentLine.Append(" ");
                    currentLine.Append(word);
                }
            }

            if (currentLine.Length > 0)
                wrappedText.Append(currentLine.ToString());

            return wrappedText.ToString();
        }
    }

    public class SoulTraitUIState : UIState
    {
        private SoulTraitSlot soulTraitSlot;
        private UIPanel tooltipPanel;
        private bool showingTooltip = false;

        public override void OnInitialize()
        {
            soulTraitSlot = new SoulTraitSlot();
            soulTraitSlot.Width.Set(44, 0f);
            soulTraitSlot.Height.Set(44, 0f);
            Append(soulTraitSlot);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Main.LocalPlayer == null || !Main.LocalPlayer.active)
                return;

            // Position the slot directly to the right of the last ammo slot
            int xPos = 572;
            int yPos = 105;

            soulTraitSlot.Left.Set(xPos, 0f);
            soulTraitSlot.Top.Set(yPos, 0f);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);
        }
    }

    public class SoulTraitSlot : UIElement
    {
        private bool isHovering = false;

        public SoulTraitSlot()
        {
            Width.Set(44, 0f);
            Height.Set(44, 0f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dimensions = GetDimensions();
            Vector2 position = new Vector2(dimensions.X, dimensions.Y);
            
            Player player = Main.LocalPlayer;
            SoulTraitPlayer traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            // Draw slot background
            Texture2D slotTexture = TextureAssets.InventoryBack.Value;
            Color slotColor = Color.White;

            if (traitPlayer.CurrentTrait != SoulTraitType.None)
            {
                slotColor = SoulTraitData.GetTraitColor(traitPlayer.CurrentTrait) * 0.5f;
                slotColor.A = 255;
            }

            spriteBatch.Draw(slotTexture, position, null, slotColor, 0f, Vector2.Zero, Main.inventoryScale, SpriteEffects.None, 0f);

            // Draw soul icon (always draw, including None state)
            DrawSoulIcon(spriteBatch, position, traitPlayer.CurrentTrait);

            // Draw visibility toggle button (only if player has a trait)
            if (traitPlayer.CurrentTrait != SoulTraitType.None)
            {
                DrawVisibilityToggle(spriteBatch, position, traitPlayer);
            }

            // Check for hover
            Rectangle slotRect = new Rectangle((int)position.X, (int)position.Y, (int)(slotTexture.Width * Main.inventoryScale), (int)(slotTexture.Height * Main.inventoryScale));
            isHovering = slotRect.Contains(Main.mouseX, Main.mouseY);

            if (isHovering)
            {
                Main.LocalPlayer.mouseInterface = true;
                DrawTooltip(spriteBatch, traitPlayer);
            }
        }

        private void DrawVisibilityToggle(SpriteBatch spriteBatch, Vector2 slotPosition, SoulTraitPlayer traitPlayer)
        {
            float slotSize = 52 * Main.inventoryScale;
            
            // Position toggle at bottom-right of the slot
            Vector2 togglePos = slotPosition + new Vector2(slotSize - 14, slotSize - 14);
            Rectangle toggleRect = new Rectangle((int)togglePos.X, (int)togglePos.Y, 14, 14);
            
            // Check for click on toggle
            if (toggleRect.Contains(Main.mouseX, Main.mouseY))
            {
                Main.LocalPlayer.mouseInterface = true;
                
                if (Main.mouseLeft && Main.mouseLeftRelease)
                {
                    traitPlayer.SoulVisible = !traitPlayer.SoulVisible;
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }
            
            // Draw the eye icon (visible) or crossed eye (hidden)
            Texture2D eyeTexture = TextureAssets.InventoryTickOn.Value;
            if (!traitPlayer.SoulVisible)
            {
                eyeTexture = TextureAssets.InventoryTickOff.Value;
            }
            
            Color eyeColor = traitPlayer.SoulVisible ? SoulTraitData.GetTraitColor(traitPlayer.CurrentTrait) : Color.Gray;
            spriteBatch.Draw(eyeTexture, togglePos, null, eyeColor, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
        }

        private void DrawSoulIcon(SpriteBatch spriteBatch, Vector2 slotPosition, SoulTraitType trait)
        {
            Color traitColor = trait == SoulTraitType.None ? Color.White : SoulTraitData.GetTraitColor(trait);
            
            // Load the appropriate soul texture
            string texturePath = "DeterministicChaos/Content/SoulTraits/" + trait.ToString();
            Texture2D soulTexture = ModContent.Request<Texture2D>(texturePath, AssetRequestMode.ImmediateLoad).Value;
            
            // Calculate center of the slot and scale to fit nicely
            float slotSize = 52 * Main.inventoryScale;
            Vector2 center = slotPosition + new Vector2(slotSize / 2, slotSize / 2);
            Vector2 origin = new Vector2(soulTexture.Width / 2f, soulTexture.Height / 2f);
            
            // Scale the icon to fill most of the slot (about 70% of slot size)
            float targetSize = slotSize * 0.7f;
            float scale = targetSize / System.Math.Max(soulTexture.Width, soulTexture.Height);

            // Pulsing effect (only for active traits)
            float pulse = trait == SoulTraitType.None ? 1f : 1f + (float)System.Math.Sin(Main.GameUpdateCount * 0.05f) * 0.1f;

            spriteBatch.Draw(soulTexture, center, null, traitColor, 0f, origin, scale * pulse, SpriteEffects.None, 0f);
        }

        private void DrawTooltip(SpriteBatch spriteBatch, SoulTraitPlayer traitPlayer)
        {
            if (traitPlayer.CurrentTrait == SoulTraitType.None)
            {
                // Draw simple tooltip for empty slot
                string noTraitText = "No Soul Trait";
                string hintText = "Visit Gerson to obtain a Soul Trait";

                Vector2 mousePos = new Vector2(Main.mouseX + 20, Main.mouseY + 20);
                
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, noTraitText, 
                    mousePos.X, mousePos.Y, Color.White, Color.Black, Vector2.Zero);
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, hintText, 
                    mousePos.X, mousePos.Y + 24, Color.Gray, Color.Black, Vector2.Zero);

                if (traitPlayer.TraitLocked)
                {
                    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, "[Locked - Hardmode]", 
                        mousePos.X, mousePos.Y + 48, Color.Red, Color.Black, Vector2.Zero);
                }
                return;
            }

            // Draw full trait tooltip
            DrawTraitTooltip(spriteBatch, traitPlayer);
        }

        private void DrawTraitTooltip(SpriteBatch spriteBatch, SoulTraitPlayer traitPlayer)
        {
            Color traitColor = SoulTraitData.GetTraitColor(traitPlayer.CurrentTrait);
            string traitName = SoulTraitData.GetTraitName(traitPlayer.CurrentTrait);
            string[] bonusDescriptions = SoulTraitData.GetTraitBonusDescriptions(traitPlayer.CurrentTrait);
            int[] thresholds = SoulTraitData.GetInvestmentThresholds();
            int investment = traitPlayer.TotalInvestment;

            // Position tooltip near the mouse cursor
            float tooltipWidth = 400;
            float tooltipHeight = 280;
            
            // Default position to the right of cursor
            Vector2 mousePos = new Vector2(Main.mouseX + 20, Main.mouseY + 20);

            // Clamp tooltip to screen bounds
            if (mousePos.X + tooltipWidth > Main.screenWidth)
                mousePos.X = Main.mouseX - tooltipWidth - 10;
            if (mousePos.Y + tooltipHeight > Main.screenHeight)
                mousePos.Y = Main.screenHeight - tooltipHeight - 10;
            if (mousePos.X < 10)
                mousePos.X = 10;
            if (mousePos.Y < 10)
                mousePos.Y = 10;

            float yOffset = 0;

            // Draw trait name
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, traitName, 
                mousePos.X, mousePos.Y + yOffset, traitColor, Color.Black, Vector2.Zero);
            yOffset += 28;

            // Draw investment points
            string investmentText = $"Investment Points: {investment}/20";
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, investmentText, 
                mousePos.X, mousePos.Y + yOffset, Color.White, Color.Black, Vector2.Zero);
            yOffset += 24;

            // Draw investment breakdown
            string breakdownText = $"(Armor: {traitPlayer.ArmorInvestment} | Weapon: {traitPlayer.WeaponInvestment} | Potion: {traitPlayer.PotionInvestment})";
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, breakdownText, 
                mousePos.X, mousePos.Y + yOffset, Color.Gray, Color.Black, Vector2.Zero, 0.8f);
            yOffset += 24;

            // Draw bonus descriptions with text wrapping
            float maxTextWidth = tooltipWidth - 20;
            for (int i = 0; i < bonusDescriptions.Length && i < thresholds.Length; i++)
            {
                bool unlocked = investment >= thresholds[i];
                Color textColor = unlocked ? traitColor : Color.Gray;
                string prefix = unlocked ? "[Active] " : $"[{thresholds[i]} pts] ";
                string fullText = prefix + bonusDescriptions[i];
                
                // Wrap the text
                string wrappedText = TextWrapUtility.WrapText(fullText, maxTextWidth, 0.85f);
                string[] lines = wrappedText.Split('\n');
                
                foreach (string line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, line.Trim(), 
                            mousePos.X, mousePos.Y + yOffset, textColor, Color.Black, Vector2.Zero, 0.85f);
                        yOffset += 18;
                    }
                }
                yOffset += 4;
            }

            // Draw active marks
            yOffset += 8;
            DrawActiveMarks(spriteBatch, mousePos, yOffset, traitPlayer);

            // Draw lock status
            if (traitPlayer.TraitLocked)
            {
                yOffset += 24;
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, "[Locked - Hardmode]", 
                    mousePos.X, mousePos.Y + yOffset, Color.Red, Color.Black, Vector2.Zero);
            }
        }

        private void DrawActiveMarks(SpriteBatch spriteBatch, Vector2 mousePos, float yOffset, SoulTraitPlayer traitPlayer)
        {
            List<string> activeMarks = new List<string>();

            if (traitPlayer.JusticeMarkActive)
                activeMarks.Add("Justice Mark: Ready!");

            if (traitPlayer.KindnessMarkTimer > 0)
                activeMarks.Add($"Kindness Mark: {traitPlayer.KindnessMarkTimer / 60}s");

            if (traitPlayer.BraveryMarkActive)
                activeMarks.Add("Bravery Mark: Active");

            if (traitPlayer.PatienceMarkStacks > 0)
                activeMarks.Add($"Patience Marks: {traitPlayer.PatienceMarkStacks}");

            if (traitPlayer.IntegrityMarkStacks > 0)
                activeMarks.Add($"Integrity Mark Stacks: {traitPlayer.IntegrityMarkStacks}");

            if (traitPlayer.PerseveranceMarkActive)
                activeMarks.Add("Perseverance Mark: Ready!");

            if (traitPlayer.DeterminationMarkActive)
                activeMarks.Add("Determination Mark: Active!");
            else if (traitPlayer.DeterminationCooldown > 0)
                activeMarks.Add($"Determination Cooldown: {traitPlayer.DeterminationCooldown / 60}s");

            Color traitColor = SoulTraitData.GetTraitColor(traitPlayer.CurrentTrait);
            foreach (string mark in activeMarks)
            {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, mark, 
                    mousePos.X, mousePos.Y + yOffset, traitColor * 0.9f, Color.Black, Vector2.Zero, 0.8f);
                yOffset += 20;
            }
        }
    }
}
