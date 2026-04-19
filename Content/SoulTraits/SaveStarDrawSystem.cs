using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits
{
    // Draws the SaveStar sprite at the Determination saved position when the mark is active.
    // The sprite is 80x40 with two 40x40 frames that alternate for animation.
    public class SaveStarDrawSystem : ModSystem
    {
        private static Asset<Texture2D> saveStarTexture;

        private const int FrameWidth = 40;
        private const int FrameHeight = 40;
        private const int FrameCount = 2;
        private const float AnimationSpeed = 4f; // Frames per second

        public override void Load()
        {
            if (!Main.dedServ)
            {
                saveStarTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/SoulTraits/SaveStar");
            }
        }

        public override void Unload()
        {
            saveStarTexture = null;
        }

        public override void PostDrawTiles()
        {
            if (Main.dedServ)
                return;

            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead)
                return;

            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait != SoulTraitType.Determination)
                return;

            if (traitPlayer.TotalInvestment < 20)
                return;

            if (!traitPlayer.DeterminationMarkActive)
                return;

            if (saveStarTexture == null || !saveStarTexture.IsLoaded)
                return;

            Texture2D tex = saveStarTexture.Value;

            // Animation frame selection
            int frame = (int)(Main.GameUpdateCount / (60f / AnimationSpeed)) % FrameCount;
            Rectangle sourceRect = new Rectangle(frame * FrameWidth, 0, FrameWidth, FrameHeight);

            // World position to screen position
            Vector2 worldPos = traitPlayer.DeterminationSavedPosition;
            Vector2 drawPos = worldPos - Main.screenPosition;
            Vector2 origin = new Vector2(FrameWidth / 2f, FrameHeight / 2f);

            // Pulsing scale
            float pulse = 1f + 0.08f * (float)System.Math.Sin(Main.GameUpdateCount * 0.06f);

            // Gentle bobbing
            float bob = (float)System.Math.Sin(Main.GameUpdateCount * 0.04f) * 3f;
            drawPos.Y += bob;

            // Gentle rotation
            float rotation = (float)System.Math.Sin(Main.GameUpdateCount * 0.03f) * 0.1f;

            SpriteBatch spriteBatch = Main.spriteBatch;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // Yellow glow behind the star (additive blend would need a separate batch, so we draw a soft yellow underlay)
            float glowPulse = 0.4f + 0.15f * (float)System.Math.Sin(Main.GameUpdateCount * 0.08f);
            Color glowColor = new Color(255, 255, 100) * glowPulse;
            float glowScale = pulse * 1.6f;
            spriteBatch.Draw(tex, drawPos, sourceRect, glowColor, rotation, origin, glowScale, SpriteEffects.None, 0f);

            // Main star sprite
            Color starColor = Color.White;
            spriteBatch.Draw(tex, drawPos, sourceRect, starColor, rotation, origin, pulse, SpriteEffects.None, 0f);

            spriteBatch.End();

            // Additive pass for bright yellow glow
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            float additiveGlowPulse = 0.25f + 0.15f * (float)System.Math.Sin(Main.GameUpdateCount * 0.08f + 1f);
            Color additiveColor = new Color(255, 255, 50) * additiveGlowPulse;
            spriteBatch.Draw(tex, drawPos, sourceRect, additiveColor, rotation, origin, pulse * 1.3f, SpriteEffects.None, 0f);

            spriteBatch.End();

            // Emit yellow light at the save position
            Lighting.AddLight(worldPos, 1.0f, 0.95f, 0.3f);
        }
    }
}
