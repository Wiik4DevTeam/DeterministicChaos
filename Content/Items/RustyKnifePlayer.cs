using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Items
{
    public class RustyKnifePlayer : ModPlayer
    {
        public int swingCombo = 0;

        // ---- Attack Prompt State ----
        public bool PromptActive = false;
        public bool MarkerLocked = false;

        private int promptTimer = 0;         // Ticks since prompt started
        internal int lockTimer = 0;           // Ticks since marker was locked
        internal float markerProgress = 0f;   // 0.0 (left) to 1.0 (right)
        private int storedDamage = 0;

        private const int PromptTravelTime = 90; // 1.5 seconds at 60fps
        private const int LockFlashDuration = 60; // 1 second of flash + despawn

        // Prompt dimensions (matches AttackPrompt.png: 236 x 66)
        private const int PromptWidth = 236;
        private const int PromptHeight = 66;
        private const int MarkerWidth = 6;

        public override void ResetEffects()
        {
            if (Player.itemAnimation <= 0)
            {
                swingCombo = 0;
            }
        }

        public void StartPrompt(int damage)
        {
            PromptActive = true;
            MarkerLocked = false;
            promptTimer = 0;
            lockTimer = 0;
            markerProgress = 0f;
            storedDamage = damage;
        }

        public void LockMarker()
        {
            if (!PromptActive || MarkerLocked)
                return;

            MarkerLocked = true;
            lockTimer = 0;

            // Sound is handled in FireDeterminationSlash based on timing quality
        }

        public override void PreUpdate()
        {
            if (!PromptActive)
                return;

            if (!MarkerLocked)
            {
                // Move marker from left to right over PromptTravelTime
                promptTimer++;
                markerProgress = (float)promptTimer / PromptTravelTime;

                if (markerProgress >= 1f)
                {
                    // Reached the right side — auto-fire
                    markerProgress = 1f;
                    LockMarker();
                }
            }
            else
            {
                lockTimer++;

                // Fire the slash on the first tick of locking
                if (lockTimer == 1)
                {
                    FireDeterminationSlash();
                }

                // Despawn after flash duration
                if (lockTimer >= LockFlashDuration)
                {
                    PromptActive = false;
                    MarkerLocked = false;
                }
            }
        }

        private void FireDeterminationSlash()
        {
            // Only the owning client spawns the projectile
            if (Player.whoAmI != Main.myPlayer)
                return;

            // Calculate damage multiplier: 1x at edges, 10x at center (mirrored)
            // markerProgress: 0 = left edge, 0.5 = center, 1.0 = right edge
            float distFromCenter = Math.Abs(markerProgress - 0.5f) * 2f; // 0 at center, 1 at edges
            float multiplier = MathHelper.Lerp(10f, 1f, distFromCenter);

            // Aim toward mouse
            Vector2 toMouse = (Main.MouseWorld - Player.Center).SafeNormalize(Vector2.UnitX);
            float aimAngle = toMouse.ToRotation();

            Projectile.NewProjectile(
                Player.GetSource_ItemUse(Player.HeldItem),
                Player.Center,
                Vector2.Zero, // velocity set in AI via ai[1]
                ModContent.ProjectileType<DeterminationSlash>(),
                storedDamage,
                4f,
                Player.whoAmI,
                multiplier,  // ai[0] = damage multiplier
                aimAngle     // ai[1] = aim angle
            );

            // Visual feedback based on multiplier
            if (multiplier >= 8f)
            {
                // Perfect or near-perfect hit
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/DTHeavyHit"), Player.Center);
                for (int i = 0; i < 20; i++)
                {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                    Dust dust = Dust.NewDustPerfect(Player.Center, DustID.RedTorch, vel, 0, default, 1.8f);
                    dust.noGravity = true;
                }
            }
            else
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/DTHit"), Player.Center);
            }
        }
    }


    public class RustyKnifePromptDrawSystem : ModSystem
    {
        private static Asset<Texture2D> promptTexture;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            promptTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/Items/AttackPrompt");
        }

        public override void Unload()
        {
            promptTexture = null;
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead)
                return;

            var knifePlayer = player.GetModPlayer<RustyKnifePlayer>();
            if (!knifePlayer.PromptActive)
                return;

            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1)
            {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "DeterministicChaos: Attack Prompt",
                    delegate
                    {
                        DrawAttackPrompt(player, knifePlayer);
                        return true;
                    },
                    InterfaceScaleType.Game
                ));
            }
        }

        private void DrawAttackPrompt(Player player, RustyKnifePlayer knifePlayer)
        {
            if (promptTexture == null || !promptTexture.IsLoaded)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D tex = promptTexture.Value;

            const int PromptWidth = 236;
            const int PromptHeight = 66;
            const int MarkerWidth = 6;

            // Position centered under the player in world space
            Vector2 promptWorldPos = player.Center + new Vector2(0, 50);
            Vector2 promptScreenPos = promptWorldPos - Main.screenPosition;
            Vector2 promptOrigin = new Vector2(PromptWidth / 2f, PromptHeight / 2f);

            // Use reflection of RustyKnifePlayer fields
            // Access via the public fields
            float markerProgress = GetMarkerProgress(knifePlayer);
            bool markerLocked = knifePlayer.MarkerLocked;

            // Draw the prompt background
            float promptAlpha = 1f;
            if (markerLocked)
            {
                // Fade out during lock phase
                int lockTimer = GetLockTimer(knifePlayer);
                float fadeProgress = (float)lockTimer / 60f;
                promptAlpha = MathHelper.Lerp(1f, 0f, fadeProgress);
            }

            spriteBatch.Draw(
                tex,
                promptScreenPos,
                null,
                Color.White * promptAlpha,
                0f,
                promptOrigin,
                1f,
                SpriteEffects.None,
                0f
            );

            // Draw the marker (white rectangle, same height as the prompt)
            float markerX = -PromptWidth / 2f + (markerProgress * PromptWidth);
            Vector2 markerScreenPos = promptScreenPos + new Vector2(markerX, 0);

            Color markerColor;
            if (markerLocked)
            {
                // Flash between white and black
                int lockTimer = GetLockTimer(knifePlayer);
                bool flashWhite = (lockTimer / 4) % 2 == 0; // Flash every 4 ticks
                markerColor = flashWhite ? Color.White : Color.Black;
                markerColor *= promptAlpha;
            }
            else
            {
                markerColor = Color.White;
            }

            // Draw marker as a filled rectangle
            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            Rectangle markerRect = new Rectangle(
                (int)(markerScreenPos.X - MarkerWidth / 2f),
                (int)(promptScreenPos.Y - PromptHeight / 2f),
                MarkerWidth,
                PromptHeight
            );

            spriteBatch.Draw(pixel, markerRect, markerColor);

            // Draw a center line indicator (subtle guide)
            Rectangle centerLine = new Rectangle(
                (int)(promptScreenPos.X - 1),
                (int)(promptScreenPos.Y - PromptHeight / 2f),
                2,
                PromptHeight
            );
            spriteBatch.Draw(pixel, centerLine, Color.Yellow * 0.3f * promptAlpha);

            // Draw damage multiplier text above the prompt
            float distFromCenter = Math.Abs(markerProgress - 0.5f) * 2f;
            float multiplier = MathHelper.Lerp(10f, 1f, distFromCenter);
            string multText = $"x{multiplier:F1}";
            
            Color multColor;
            if (multiplier >= 8f)
                multColor = Color.Red;
            else if (multiplier >= 4f)
                multColor = Color.Yellow;
            else
                multColor = Color.White;

            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(multText);
            Vector2 textPos = promptScreenPos + new Vector2(-textSize.X / 2f, -PromptHeight / 2f - 24);

            // Black outline + text using Utils.DrawBorderString
            Utils.DrawBorderString(spriteBatch, multText, textPos, multColor * promptAlpha, 1f);
        }

        private float GetMarkerProgress(RustyKnifePlayer knifePlayer)
        {
            return knifePlayer.markerProgress;
        }

        private int GetLockTimer(RustyKnifePlayer knifePlayer)
        {
            return knifePlayer.lockTimer;
        }
    }
}
