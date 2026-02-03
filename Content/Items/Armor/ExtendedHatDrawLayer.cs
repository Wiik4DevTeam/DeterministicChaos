using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items.Armor
{
    // PlayerDrawLayer that renders helmet extensions above the player's head
    // Handles helmets that implement IExtendedHat interface
    public class ExtendedHatDrawLayer : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Head);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
        {
            // Do not draw if the player is invisible
            if (drawInfo.drawPlayer.invis)
                return false;

            // Check vanity slot first
            Item headItem = drawInfo.drawPlayer.armor[10];
            if (headItem.IsAir)
                headItem = drawInfo.drawPlayer.armor[0];

            if (headItem.IsAir)
                return false;

            // Check if the helmet implements IExtendedHat
            return headItem.ModItem is IExtendedHat;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Player player = drawInfo.drawPlayer;
            
            // Check vanity slot first, then armor slot
            Item headItem = player.armor[10];
            if (headItem.IsAir)
                headItem = player.armor[0];

            if (headItem.ModItem is not IExtendedHat extendedHat)
                return;

            // Load the extension texture
            string texturePath = extendedHat.ExtensionTexture;
            if (!ModContent.HasAsset(texturePath))
                return;

            Texture2D texture = ModContent.Request<Texture2D>(texturePath).Value;
            Vector2 offset = extendedHat.ExtensionSpriteOffset(drawInfo);

            // Calculate frame for sprite sheet (20 frames, 60 pixels each)
            int totalFrames = 20;
            int frameHeight = texture.Height / totalFrames;
            int frameWidth = texture.Width;
            
            // Use the same frame as the player's body animation
            int frameY = player.bodyFrame.Y / player.bodyFrame.Height;
            if (frameY >= totalFrames)
                frameY = 0;
            
            Rectangle sourceRect = new Rectangle(0, frameY * frameHeight, frameWidth, frameHeight);

            // Calculate position matching the head draw position
            Vector2 position = drawInfo.Position - Main.screenPosition;
            position += new Vector2(
                (player.width / 2f) - (frameWidth / 2f),
                player.height - player.bodyFrame.Height + 2f
            );
            position = new Vector2((int)position.X, (int)position.Y);
            position += player.headPosition;
            
            // Apply the custom offset
            position += offset;

            // Handle player direction flip
            SpriteEffects spriteEffects = player.direction == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // Apply gravity flip if needed
            if (player.gravDir == -1f)
            {
                spriteEffects |= SpriteEffects.FlipVertically;
            }

            // Origin for flipping, use center of frame for proper flip behavior
            Vector2 origin = new Vector2(frameWidth / 2f, frameHeight / 2f);
            
            // Adjust position to account for centered origin
            position += origin;

            // Get color, respect player opacity, shadow, and stealth effects
            Color drawColor = drawInfo.colorArmorHead;
            
            // Apply shadow effect
            if (drawInfo.shadow != 0f)
            {
                drawColor *= (1f - drawInfo.shadow);
            }
            
            // Apply stealth/opacity from player
            drawColor *= player.stealth;

            // Draw the extension with proper frame
            DrawData drawData = new DrawData(
                texture,
                position,
                sourceRect,
                drawColor,
                player.headRotation,
                origin,
                1f,
                spriteEffects,
                0
            );

            drawInfo.DrawDataCache.Add(drawData);
        }
    }
}
