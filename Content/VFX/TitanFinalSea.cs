using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using System;
using System.Collections.Generic;
using DeterministicChaos.Content.NPCs.Bosses;

namespace DeterministicChaos.Content.VFX
{
    // Inverted dark sea for the Titan final stand sequence.
    // The lava ceiling descends from above the tower downward, forcing players to escape to the bottom.
    // Black fill occupies everything ABOVE surfaceWorldY.
    // Sphere stalactites hang at the surface edge pointing downward.
    // Players whose Center.Y < surfaceWorldY are inside the lava and take damage.
    public class TitanFinalSea : ModSystem
    {
        // ======== Surface Sphere Configuration ========
        private const int SURFACE_SPHERE_COUNT = 300;
        private const float SPHERE_BASE_HEIGHT = 60f;
        private const float SPHERE_BASE_WIDTH = 150f;
        private const float SPHERE_PULSE_AMOUNT = 12f;
        private const float SPHERE_HEIGHT_PULSE = 8f;
        private const float OUTLINE_THICKNESS = 3f;

        // ======== Scattered Orbs (inside the black fill) ========
        private const int SCATTERED_ORB_COUNT = 150;
        private const float SCATTERED_ORB_MIN_SIZE = 100f;
        private const float SCATTERED_ORB_MAX_SIZE = 180f;

        // ======== Falling Particles ========
        private const int MAX_PARTICLES = 40;
        private const float PARTICLE_SPAWN_INTERVAL = 0.12f;
        private const float PARTICLE_FALL_SPEED = 45f;   // Falls downward from surface
        private const float PARTICLE_LIFETIME = 2.5f;
        private const float PARTICLE_START_SIZE = 35f;
        private const float PARTICLE_END_SIZE = 5f;

        // ======== State ========
        private static bool active = false;
        private static float surfaceWorldY;     // Bottom edge of the descending lava ceiling
        private static float centerWorldX;
        private static float totalWidthPixels;

        // Per-surface-sphere random offsets
        private static float[] spherePulseOffsets;
        private static float[] spherePulseSpeedVariance;
        private static float[] sphereYOffsets;

        // Scattered orb data (inside the black region, above surface)
        private static float[] scatteredOrbX;
        private static float[] scatteredOrbYOffset; // Negative = above surface (inside the black)
        private static float[] scatteredOrbSize;
        private static float[] scatteredOrbPulseOffset;

        // Falling particles
        private struct SeaParticle
        {
            public float WorldX;
            public float WorldY;
            public float Life;
            public float MaxLife;
            public float Size;
            public float PulseOffset;
        }
        private static List<SeaParticle> particles = new List<SeaParticle>();
        private static float particleSpawnTimer = 0f;

        public static float SurfaceWorldY => surfaceWorldY;
        public static bool IsActive => active;

        public override void Load()
        {
            if (Main.dedServ)
                return;
            Terraria.On_Main.DrawDust += DrawFinalSea;
        }

        public override void Unload()
        {
            Terraria.On_Main.DrawDust -= DrawFinalSea;
            particles?.Clear();
        }

        public static void Activate(float centerX, float startSurfaceY, float totalWidth)
        {
            active = true;
            centerWorldX = centerX;
            surfaceWorldY = startSurfaceY;
            totalWidthPixels = totalWidth;
            particles.Clear();
            particleSpawnTimer = 0f;

            spherePulseOffsets = new float[SURFACE_SPHERE_COUNT];
            spherePulseSpeedVariance = new float[SURFACE_SPHERE_COUNT];
            sphereYOffsets = new float[SURFACE_SPHERE_COUNT];

            for (int i = 0; i < SURFACE_SPHERE_COUNT; i++)
            {
                spherePulseOffsets[i] = Main.rand.NextFloat() * MathHelper.TwoPi;
                spherePulseSpeedVariance[i] = 0.8f + Main.rand.NextFloat() * 0.4f;
                sphereYOffsets[i] = Main.rand.NextFloat(-10f, 10f);
            }

            scatteredOrbX = new float[SCATTERED_ORB_COUNT];
            scatteredOrbYOffset = new float[SCATTERED_ORB_COUNT];
            scatteredOrbSize = new float[SCATTERED_ORB_COUNT];
            scatteredOrbPulseOffset = new float[SCATTERED_ORB_COUNT];

            float seaLeft = centerX - totalWidth / 2f;
            for (int i = 0; i < SCATTERED_ORB_COUNT; i++)
            {
                scatteredOrbX[i] = seaLeft + Main.rand.NextFloat() * totalWidth;
                scatteredOrbYOffset[i] = -Main.rand.NextFloat(20f, 300f); // above surface (negative)
                scatteredOrbSize[i] = Main.rand.NextFloat(SCATTERED_ORB_MIN_SIZE, SCATTERED_ORB_MAX_SIZE);
                scatteredOrbPulseOffset[i] = Main.rand.NextFloat() * MathHelper.TwoPi;
            }
        }

        public static void SetSurfaceY(float worldY)
        {
            surfaceWorldY = worldY;
        }

        public static void Deactivate()
        {
            active = false;
            particles.Clear();
        }

        public override void PostUpdateEverything()
        {
            if (!active || Main.dedServ)
                return;

            // Safety: deactivate if Titan is gone
            bool titanAlive = false;
            int titanType = ModContent.NPCType<TitanBody>();
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == titanType)
                {
                    titanAlive = true;
                    break;
                }
            }
            if (!titanAlive)
            {
                Deactivate();
                return;
            }

            float dt = 1f / 60f;

            // Spawn particles near the surface that fall downward
            particleSpawnTimer += dt;
            if (particleSpawnTimer >= PARTICLE_SPAWN_INTERVAL && particles.Count < MAX_PARTICLES)
            {
                particleSpawnTimer = 0f;
                float seaLeft = centerWorldX - totalWidthPixels / 2f;
                particles.Add(new SeaParticle
                {
                    WorldX = seaLeft + Main.rand.NextFloat() * totalWidthPixels,
                    WorldY = surfaceWorldY + Main.rand.NextFloat(-10f, 20f),
                    Life = PARTICLE_LIFETIME,
                    MaxLife = PARTICLE_LIFETIME,
                    Size = PARTICLE_START_SIZE,
                    PulseOffset = Main.rand.NextFloat() * MathHelper.TwoPi
                });
            }

            // Update particles (fall downward)
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.Life -= dt;
                p.WorldY += PARTICLE_FALL_SPEED * dt; // falls down

                float lifeRatio = MathHelper.Clamp(p.Life / p.MaxLife, 0f, 1f);
                p.Size = MathHelper.Lerp(PARTICLE_END_SIZE, PARTICLE_START_SIZE, lifeRatio);

                if (p.Life <= 0f)
                {
                    particles.RemoveAt(i);
                    continue;
                }

                particles[i] = p;
            }
        }

        private void DrawFinalSea(Terraria.On_Main.orig_DrawDust orig, Main self)
        {
            orig(self);

            if (!active || Main.dedServ)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float time = (float)Main.GameUpdateCount * 0.05f;

            float seaLeftWorld = centerWorldX - totalWidthPixels / 2f;
            float seaRightWorld = centerWorldX + totalWidthPixels / 2f;

            float screenSurfaceY = surfaceWorldY - Main.screenPosition.Y;
            float screenLeft = seaLeftWorld - Main.screenPosition.X;
            float screenRight = seaRightWorld - Main.screenPosition.X;

            // Early out if the entire ceiling is off the bottom of the screen
            if (screenSurfaceY > Main.screenHeight + SPHERE_BASE_HEIGHT + 200)
                return;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 1) Solid black region above the surface (from top of screen to just above sphere edge)
            float solidTop = -200f; // well off top of screen
            float solidBottom = screenSurfaceY - SPHERE_BASE_HEIGHT * 0.3f;
            float solidWidth = screenRight - screenLeft;

            if (solidBottom > solidTop && solidWidth > 0)
            {
                Rectangle blackRect = new Rectangle(
                    (int)screenLeft,
                    (int)solidTop,
                    (int)solidWidth,
                    (int)(solidBottom - solidTop)
                );
                spriteBatch.Draw(pixel, blackRect, Color.Black);
            }

            // ======== TWO-PASS RENDERING: All outlines first, then all interiors ========

            float sphereSpacing = totalWidthPixels / SURFACE_SPHERE_COUNT;

            // === PASS 1: All white outlines ===

            // Scattered orb outlines
            if (scatteredOrbX != null)
            {
                for (int i = 0; i < SCATTERED_ORB_COUNT; i++)
                {
                    float orbScreenX = scatteredOrbX[i] - Main.screenPosition.X;
                    float orbScreenY = screenSurfaceY + scatteredOrbYOffset[i]; // above surface (negative offset)
                    if (orbScreenY < -SCATTERED_ORB_MAX_SIZE - 100 || orbScreenY > Main.screenHeight + 100) continue;

                    float pulse = (float)Math.Sin(time * 1.2f + scatteredOrbPulseOffset[i]) * 8f;
                    float size = scatteredOrbSize[i] + pulse;
                    Vector2 center = new Vector2(orbScreenX, orbScreenY);
                    DrawFilledEllipse(spriteBatch, pixel, center, size + OUTLINE_THICKNESS * 2, size + OUTLINE_THICKNESS * 2, Color.White);
                }
            }

            // Surface sphere outlines (at surface boundary, forming stalactite edge)
            for (int i = 0; i < SURFACE_SPHERE_COUNT; i++)
            {
                float worldX = seaLeftWorld + sphereSpacing * (i + 0.5f);
                float screenX = worldX - Main.screenPosition.X;
                float pulseTime = time * spherePulseSpeedVariance[i] + spherePulseOffsets[i];
                float currentWidth = SPHERE_BASE_WIDTH + (float)Math.Sin(pulseTime) * SPHERE_PULSE_AMOUNT;
                float currentHeight = SPHERE_BASE_HEIGHT + (float)Math.Cos(pulseTime * 0.7f) * SPHERE_HEIGHT_PULSE;
                float sphereScreenY = screenSurfaceY + sphereYOffsets[i];
                Vector2 center = new Vector2(screenX, sphereScreenY);
                DrawFilledEllipse(spriteBatch, pixel, center, currentWidth + OUTLINE_THICKNESS * 2, currentHeight + OUTLINE_THICKNESS * 2, Color.White);
            }

            // Falling particle outlines
            foreach (var p in particles)
            {
                float screenX = p.WorldX - Main.screenPosition.X;
                float screenY = p.WorldY - Main.screenPosition.Y;
                if (screenY < -50 || screenY > Main.screenHeight + 50) continue;
                float size = p.Size;
                Vector2 center = new Vector2(screenX, screenY);
                DrawFilledEllipse(spriteBatch, pixel, center, size + OUTLINE_THICKNESS * 2, size + OUTLINE_THICKNESS * 2, Color.White);
            }

            // === PASS 2: All black interiors ===

            if (scatteredOrbX != null)
            {
                for (int i = 0; i < SCATTERED_ORB_COUNT; i++)
                {
                    float orbScreenX = scatteredOrbX[i] - Main.screenPosition.X;
                    float orbScreenY = screenSurfaceY + scatteredOrbYOffset[i];
                    if (orbScreenY < -SCATTERED_ORB_MAX_SIZE - 100 || orbScreenY > Main.screenHeight + 100) continue;

                    float pulse = (float)Math.Sin(time * 1.2f + scatteredOrbPulseOffset[i]) * 8f;
                    float size = scatteredOrbSize[i] + pulse;
                    Vector2 center = new Vector2(orbScreenX, orbScreenY);
                    DrawFilledEllipse(spriteBatch, pixel, center, size, size, Color.Black);
                }
            }

            for (int i = 0; i < SURFACE_SPHERE_COUNT; i++)
            {
                float worldX = seaLeftWorld + sphereSpacing * (i + 0.5f);
                float screenX = worldX - Main.screenPosition.X;
                float pulseTime = time * spherePulseSpeedVariance[i] + spherePulseOffsets[i];
                float currentWidth = SPHERE_BASE_WIDTH + (float)Math.Sin(pulseTime) * SPHERE_PULSE_AMOUNT;
                float currentHeight = SPHERE_BASE_HEIGHT + (float)Math.Cos(pulseTime * 0.7f) * SPHERE_HEIGHT_PULSE;
                float sphereScreenY = screenSurfaceY + sphereYOffsets[i];
                Vector2 center = new Vector2(screenX, sphereScreenY);
                DrawFilledEllipse(spriteBatch, pixel, center, currentWidth, currentHeight, Color.Black);
            }

            foreach (var p in particles)
            {
                float screenX = p.WorldX - Main.screenPosition.X;
                float screenY = p.WorldY - Main.screenPosition.Y;
                if (screenY < -50 || screenY > Main.screenHeight + 50) continue;
                float size = p.Size;
                Vector2 center = new Vector2(screenX, screenY);
                DrawFilledEllipse(spriteBatch, pixel, center, size, size, Color.Black);
            }

            spriteBatch.End();
        }

        private static void DrawFilledEllipse(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float width, float height, Color color)
        {
            float radiusX = width / 2f;
            float radiusY = height / 2f;

            for (int y = -(int)radiusY; y <= (int)radiusY; y++)
            {
                float normalizedY = y / radiusY;
                if (Math.Abs(normalizedY) > 1f)
                    continue;

                float xExtent = radiusX * (float)Math.Sqrt(1f - normalizedY * normalizedY);
                if (xExtent < 0.5f)
                    continue;

                Rectangle rect = new Rectangle(
                    (int)(center.X - xExtent),
                    (int)(center.Y + y),
                    (int)(xExtent * 2),
                    1
                );
                spriteBatch.Draw(pixel, rect, color);
            }
        }
    }
}
