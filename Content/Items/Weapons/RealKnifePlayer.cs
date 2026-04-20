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
    public class RealKnifePlayer : ModPlayer
    {
        public int swingCombo = 0;

        // ---- Attack Prompt State ----
        public bool PromptActive = false;
        public bool MarkerLocked = false;

        private int promptTimer = 0;
        internal int lockTimer = 0;
        internal float markerProgress = 0f;
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
                    // Reached the right side, auto-fire at minimum multiplier
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

        // Calculates the exponential multiplier based on marker position.
        // 15x at exact center, 1x at edges, exponential curve.
        public static float CalculateMultiplier(float markerProgress)
        {
            float distFromCenter = Math.Abs(markerProgress - 0.5f) * 2f; // 0 at center, 1 at edges
            float closeness = 1f - distFromCenter; // 1 at center, 0 at edges
            return (float)Math.Pow(15.0, closeness); // 15^1 = 15 at center, 15^0 = 1 at edges
        }

        private void FireDeterminationSlash()
        {
            // Only the owning client spawns the projectile
            if (Player.whoAmI != Main.myPlayer)
                return;

            float multiplier = CalculateMultiplier(markerProgress);

            // Aim toward mouse
            Vector2 toMouse = (Main.MouseWorld - Player.Center).SafeNormalize(Vector2.UnitX);
            float aimAngle = toMouse.ToRotation();

            Projectile.NewProjectile(
                Player.GetSource_ItemUse(Player.HeldItem),
                Player.Center,
                Vector2.Zero,
                ModContent.ProjectileType<DeterminationSlash>(),
                storedDamage,
                4f,
                Player.whoAmI,
                multiplier,  // ai[0] = damage multiplier
                aimAngle     // ai[1] = aim angle
            );

            // Visual feedback based on multiplier
            if (multiplier >= 15f)
            {
                // Near-perfect or perfect hit, intense feedback
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/DTHeavyHit"), Player.Center);
                for (int i = 0; i < 25; i++)
                {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                    Dust dust = Dust.NewDustPerfect(Player.Center, DustID.RedTorch, vel, 0, default, 2f);
                    dust.noGravity = true;
                }
            }
            else
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/DTHit"), Player.Center);
            }
        }
    }


    public class RealKnifePromptDrawSystem : ModSystem
    {
        private static Asset<Texture2D> promptTexture;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            promptTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/Items/Weapons/AttackPrompt");
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

            var knifePlayer = player.GetModPlayer<RealKnifePlayer>();
            if (!knifePlayer.PromptActive)
                return;

            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1)
            {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "DeterministicChaos: Real Knife Attack Prompt",
                    delegate
                    {
                        DrawAttackPrompt(player, knifePlayer);
                        return true;
                    },
                    InterfaceScaleType.Game
                ));
            }
        }

        private void DrawAttackPrompt(Player player, RealKnifePlayer knifePlayer)
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

            float markerProgress = knifePlayer.markerProgress;
            bool markerLocked = knifePlayer.MarkerLocked;

            // Draw the prompt background
            float promptAlpha = 1f;
            if (markerLocked)
            {
                // Fade out during lock phase
                int lockTimer = knifePlayer.lockTimer;
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
                int lockTimer = knifePlayer.lockTimer;
                bool flashWhite = (lockTimer / 4) % 2 == 0;
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

            // Draw damage multiplier text above the prompt (exponential curve)
            float multiplier = RealKnifePlayer.CalculateMultiplier(markerProgress);
            string multText = $"x{multiplier:F1}";

            Color multColor;
            if (multiplier >= 15f)
                multColor = Color.Red;
            else if (multiplier >= 6f)
                multColor = Color.Yellow;
            else
                multColor = Color.White;

            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(multText);
            Vector2 textPos = promptScreenPos + new Vector2(-textSize.X / 2f, -PromptHeight / 2f - 24);

            // Black outline + text
            Utils.DrawBorderString(spriteBatch, multText, textPos, multColor * promptAlpha, 1f);
        }
    }
}
