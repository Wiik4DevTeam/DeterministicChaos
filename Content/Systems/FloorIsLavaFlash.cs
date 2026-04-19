using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.VFX;

namespace DeterministicChaos.Content.Systems
{
    // Draws a white flash over the arena ground during the Titan's FloorIsLava attack.
    // Telegraph phase: subtle pulsing white. Danger phase: bright solid white.
    public class FloorIsLavaFlash : ModSystem
    {
        private enum FlashState
        {
            Inactive,
            Telegraph,
            Danger
        }

        private static FlashState state = FlashState.Inactive;
        private static float flashTimer = 0f;

        // Flash covers the arena ground: extra wide + a few tiles tall at ground level
        private const int FLASH_GROUND_TILES = 4;          // Height of the flash in tiles
        private const int FLASH_EXTRA_WIDTH_TILES = 20;    // Extra tiles on each side beyond tower

        public static void StartTelegraph()
        {
            state = FlashState.Telegraph;
            flashTimer = 0f;
        }

        public static void StartDanger()
        {
            state = FlashState.Danger;
            flashTimer = 0f;
        }

        public static void Stop()
        {
            state = FlashState.Inactive;
            flashTimer = 0f;
        }

        public override void Unload()
        {
            state = FlashState.Inactive;
            flashTimer = 0f;
        }

        public override void PostUpdateEverything()
        {
            if (state == FlashState.Inactive)
                return;

            flashTimer += 1f / 60f;

            // Auto-stop after reasonable duration if not manually stopped
            if (flashTimer > 5f)
                Stop();
        }

        public override void PostDrawTiles()
        {
            if (state == FlashState.Inactive || Main.dedServ)
                return;

            if (!TitanSpawnCutscene.TowerPlaced)
                return;

            var towerTop = TitanSpawnCutscene.TowerTopLeft;
            float arenaGroundY = towerTop.Y * 16f + 11f * 16f;

            // Flash rectangle: wider than tower, at ground level
            float flashLeft = (towerTop.X - FLASH_EXTRA_WIDTH_TILES) * 16f;
            float flashWidth = (TitanSpawnCutscene.TOWER_WIDTH + FLASH_EXTRA_WIDTH_TILES * 2) * 16f;
            float flashTop = arenaGroundY + 3f * 16f; // Start at ground level (4 tiles lower)
            float flashHeight = FLASH_GROUND_TILES * 16f;

            // Convert to screen coords
            Vector2 screenPos = new Vector2(flashLeft, flashTop) - Main.screenPosition;
            Rectangle flashRect = new Rectangle(
                (int)screenPos.X, (int)screenPos.Y,
                (int)flashWidth, (int)flashHeight);

            float alpha;
            if (state == FlashState.Telegraph)
            {
                // Pulsing white, 0.2 to 0.5 alpha
                alpha = 0.2f + 0.3f * (float)System.Math.Sin(flashTimer * MathHelper.TwoPi * 2f);
                alpha = MathHelper.Clamp(alpha, 0.1f, 0.5f);
            }
            else // Danger
            {
                // Bright solid white, ramping up
                alpha = MathHelper.Clamp(0.5f + flashTimer * 2f, 0.5f, 0.9f);
            }

            SpriteBatch spriteBatch = Main.spriteBatch;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // Use a 1x1 white pixel texture
            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            spriteBatch.Draw(pixel, flashRect, Color.White * alpha);

            spriteBatch.End();
        }
    }
}
