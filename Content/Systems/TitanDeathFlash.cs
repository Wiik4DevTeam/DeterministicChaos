using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.NPCs.Bosses;

namespace DeterministicChaos.Content.Systems
{
    // Handles two final-stand screen events:
    //   1. Plays TitanDeath sound the moment IsInFinalStand becomes true (client-side).
    //   2. Fades the screen to white in the last 1 second before the Titan dies,
    //      then fades back from white after it dies.
    public class TitanDeathFlash : ModSystem
    {
        private enum FlashState { Inactive, FadeIn, Hold, FadeOut }

        private static FlashState state   = FlashState.Inactive;
        private static float      alpha   = 0f;
        private static float      timer   = 0f;
        private static bool       prevFinalStand = false;

        private const float FADE_IN_DURATION  = 1f; // Seconds to reach full white
        private const float FADE_OUT_DURATION = 1f; // Seconds to fade back

        public override void Unload()
        {
            state          = FlashState.Inactive;
            alpha          = 0f;
            timer          = 0f;
            prevFinalStand = false;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            // Find the TitanBody NPC
            TitanBody titan = null;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].ModNPC is TitanBody tb)
                {
                    titan = tb;
                    break;
                }
            }

            bool inFinalStand = titan?.IsInFinalStand ?? false;

            // ── Sound: play TitanDeath on the first tick of final stand ──
            if (inFinalStand && !prevFinalStand)
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/TitanDeath"));
                // Reset flash state in case of re-entry
                state = FlashState.Inactive;
                alpha = 0f;
                timer = 0f;
            }

            // ── Trigger fade-in when 1 second remains ──
            if (inFinalStand && state == FlashState.Inactive
                && titan.finalStandTimer >= TitanBody.FINAL_STAND_DURATION - 1f)
            {
                state = FlashState.FadeIn;
                timer = 0f;
            }

            // ── Titan just died: switch to fade-out ──
            if (!inFinalStand && prevFinalStand
                && (state == FlashState.FadeIn || state == FlashState.Hold))
            {
                state = FlashState.FadeOut;
                timer = 0f;
                // alpha stays at current value (likely 1f)
            }

            prevFinalStand = inFinalStand;

            // ── Advance state ──
            switch (state)
            {
                case FlashState.FadeIn:
                    timer += 1f / 60f;
                    alpha = MathHelper.Clamp(timer / FADE_IN_DURATION, 0f, 1f);
                    if (timer >= FADE_IN_DURATION)
                    {
                        alpha = 1f;
                        state = FlashState.Hold;
                    }
                    break;

                case FlashState.Hold:
                    alpha = 1f; // Stay white until titan dies
                    break;

                case FlashState.FadeOut:
                    timer += 1f / 60f;
                    alpha = 1f - MathHelper.Clamp(timer / FADE_OUT_DURATION, 0f, 1f);
                    if (timer >= FADE_OUT_DURATION)
                    {
                        alpha = 0f;
                        state = FlashState.Inactive;
                    }
                    break;
            }
        }

        public override void PostDrawTiles()
        {
            if (Main.dedServ || alpha <= 0.001f)
                return;

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, null, null);

            sb.Draw(
                TextureAssets.MagicPixel.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                new Rectangle(0, 0, 1, 1),
                Color.White * alpha);

            sb.End();
        }
    }
}
