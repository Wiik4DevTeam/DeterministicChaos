using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.ID;
using SubworldLibrary;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using DeterministicChaos.Content.Subworlds;
using DeterministicChaos.Content.Projectiles.Enemy;
using DeterministicChaos.Content.Systems;
using Terraria.DataStructures;

namespace DeterministicChaos.Content.VFX
{
    public class DarkWorldCutscene : ModSystem
    {
        private static bool isPlaying = false;
        private static float cutsceneTimer = 0f;
        private static Vector2 originPosition;
        private static Player originPlayer;
        
        // Star spawning
        private static float starSpawnTimer = 0f;
        private static int starsSpawned = 0;
        private static List<CutsceneStar> activeStars = new List<CutsceneStar>();
        
        // Sphere tracking
        private static int mainOvalId = -1;
        private static List<SmokeSphere> smokeSpheres = new List<SmokeSphere>();
        private static float smokeSpawnTimer = 0f;
        private static float shockwaveSpawnTimer = 0f;
        
        // Screen fade
        private static float screenFadeAlpha = 0f;
        
        // Camera effects
        private static float originalZoom = 1f;
        private static float targetZoom = 1.15f;
        
        // Phase timing
        private const float STAR_PHASE_DURATION = 1f;      // First 1 second, stars
        private const float WHITE_TO_BLACK_START = 1f;      // When oval appears
        private const float WHITE_TO_BLACK_END = 4f;        // 3 seconds of transition
        private const float BLACK_DURATION = 5f;            // 5 seconds of black
        private const float TOTAL_DURATION = 9f;            // Total cutscene length
        
        // Textures
        private static Asset<Texture2D> starTexture;
        
        private struct CutsceneStar
        {
            public Vector2 Position;
            public float Rotation;
            public float Scale;
            public float Life;
            public float MaxLife;
        }
        
        private struct SmokeSphere
        {
            public int SphereId;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Acceleration;
            public float Size;
            public float StartSize;
            public float TargetSize;
            public float Life;
            public float MaxLife;
        }

        public override void Load()
        {
            if (Main.dedServ)
                return;
                
            starTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/VFX/FountainStarSpawn");
        }

        public override void Unload()
        {
            starTexture = null;
            activeStars?.Clear();
            smokeSpheres?.Clear();
        }

        public static void StartCutscene(Player player)
        {
            if (isPlaying)
                return;
            
            // In multiplayer, send packet to sync across all clients
            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                ERAMNetworkHandler.SendDarkWorldCutscenePacket(player.Center, player.whoAmI);
            }
            
            // Start locally
            StartCutsceneAtPosition(player.Center, player);
        }
        
        public static void StartCutsceneAtPosition(Vector2 position, Player player)
        {
            if (isPlaying)
                return;
                
            isPlaying = true;
            cutsceneTimer = 0f;
            originPosition = position;
            originPlayer = player;
            starsSpawned = 0;
            starSpawnTimer = 0f;
            smokeSpawnTimer = 0f;
            shockwaveSpawnTimer = 0f;
            mainOvalId = -1;
            screenFadeAlpha = 0f;
            
            // Save original zoom
            originalZoom = Main.GameZoomTarget;
            
            activeStars.Clear();
            smokeSpheres.Clear();
            BlackSphereSystem.ClearAll();
            
            // Play the opening sound
            SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/OpenDarkWorld")
            {
                Volume = 1f,
                Pitch = 0f
            }, position);
        }

        public static bool IsPlaying => isPlaying;

        public override void PostUpdateEverything()
        {
            if (!isPlaying || Main.dedServ)
                return;

            float deltaTime = 1f / 60f;
            cutsceneTimer += deltaTime;

            if (cutsceneTimer < STAR_PHASE_DURATION)
            {
                UpdateStarPhase(deltaTime);
            }
            else if (cutsceneTimer < WHITE_TO_BLACK_END + BLACK_DURATION)
            {
                UpdateOvalPhase(deltaTime);
            }
            
            // Update all stars
            UpdateStars(deltaTime);
            
            // Update smoke spheres
            UpdateSmokeSpheres(deltaTime);
            
            // Calculate interior color (white -> black transition)
            UpdateSphereColors();
            
            // Apply player effects
            ApplyPlayerEffects(deltaTime);
            
            // Apply camera effects (zoom and screenshake)
            ApplyCameraEffects(deltaTime);
            
            // Update screen fade (fade to black 1 second before teleport)
            UpdateScreenFade(deltaTime);

            // === Final Phase: Teleport ===
            if (cutsceneTimer >= TOTAL_DURATION)
            {
                EndCutscene();
            }
        }

        private void UpdateStarPhase(float deltaTime)
        {
            starSpawnTimer += deltaTime;
            
            // Spawn a star every 0.1 seconds
            if (starSpawnTimer >= 0.1f && starsSpawned < 10)
            {
                starSpawnTimer = 0f;
                starsSpawned++;
                
                // Random position around player
                Vector2 offset = new Vector2(
                    Main.rand.NextFloat(-100f, 100f),
                    Main.rand.NextFloat(-100f, 100f)
                );
                
                activeStars.Add(new CutsceneStar
                {
                    Position = originPosition + offset,
                    Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    Scale = Main.rand.NextFloat(0.5f, 1.5f),
                    Life = 0.1f,
                    MaxLife = 0.1f
                });
            }
        }

        private void UpdateOvalPhase(float deltaTime)
        {
            // Create main oval if not yet created
            if (mainOvalId < 0 && cutsceneTimer >= STAR_PHASE_DURATION)
            {
                // Large oval, bottom at player position
                // The oval extends upward, so center is above the player
                float ovalHeight = 800f;
                float ovalWidth = 150f;
                
                Vector2 ovalCenter = originPosition - new Vector2(0, ovalHeight / 2);
                
                mainOvalId = BlackSphereSystem.AddSphere(
                    ovalCenter, 
                    ovalWidth, 
                    ovalHeight, 
                    widthPulse: 40f, 
                    pulseSpeed: 2f
                );
            }
            
            // Spawn smoke spheres
            smokeSpawnTimer += deltaTime;
            if (smokeSpawnTimer >= 0.15f && cutsceneTimer < WHITE_TO_BLACK_END + BLACK_DURATION - 1f)
            {
                smokeSpawnTimer = 0f;
                SpawnSmokeSphere();
            }
        }

        private void SpawnSmokeSphere()
        {
            // Spawn at the bottom-center of the oval (at player's feet) and expand outward
            Vector2 spawnPos = originPosition + new Vector2(
                Main.rand.NextFloat(-30f, 30f),
                Main.rand.NextFloat(-5f, 5f)
            );
            
            float startSize = Main.rand.NextFloat(1f, 5f);
            float targetSize = Main.rand.NextFloat(160f, 300f);
            
            // Move in a 22.5 degree cone upward (left and right)
            // Angle range: -22.5 to +22.5 degrees from straight up
            // Straight up is -Pi/2, so range is -Pi/2 - Pi/8 to -Pi/2 + Pi/8
            float angle = Main.rand.NextFloat(-MathHelper.PiOver4 / 2f, MathHelper.PiOver4 / 2f) - MathHelper.PiOver2;
            float speed = Main.rand.NextFloat(20f, 40f);
            Vector2 velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;
            
            int sphereId = BlackSphereSystem.AddSphere(spawnPos, startSize, startSize);
            
            smokeSpheres.Add(new SmokeSphere
            {
                SphereId = sphereId,
                Position = spawnPos,
                Velocity = velocity,
                Acceleration = Main.rand.NextFloat(15f, 30f),
                Size = startSize,
                StartSize = startSize,
                TargetSize = targetSize,
                Life = 10f,
                MaxLife = 10f
            });
        }

        private void UpdateSmokeSpheres(float deltaTime)
        {
            for (int i = smokeSpheres.Count - 1; i >= 0; i--)
            {
                var smoke = smokeSpheres[i];
                
                // Accelerate velocity
                Vector2 direction = smoke.Velocity;
                if (direction != Vector2.Zero)
                {
                    direction.Normalize();
                    smoke.Velocity += direction * smoke.Acceleration * deltaTime;
                }
                
                // Move based on velocity
                smoke.Position += smoke.Velocity * deltaTime;
                
                // Grow to max size over 5 seconds, then stay at max size
                float timeElapsed = smoke.MaxLife - smoke.Life;
                float growthDuration = 5f;
                float growthProgress = MathHelper.Clamp(timeElapsed / growthDuration, 0f, 1f);
                float currentSize = MathHelper.Lerp(smoke.StartSize, smoke.TargetSize, growthProgress);
                
                // Update the sphere
                BlackSphereSystem.UpdateSphere(smoke.SphereId, 
                    position: smoke.Position, 
                    width: currentSize, 
                    height: currentSize);
                
                smoke.Life -= deltaTime;
                smokeSpheres[i] = smoke;
                
                // Remove dead spheres
                if (smoke.Life <= 0)
                {
                    BlackSphereSystem.RemoveSphere(smoke.SphereId);
                    smokeSpheres.RemoveAt(i);
                }
            }
        }

        private void UpdateSphereColors()
        {
            // Calculate the interior color based on cutscene progress
            // White from 1s to 4s, then black from 4s to 9s
            float whiteAmount;
            
            if (cutsceneTimer < WHITE_TO_BLACK_START)
            {
                whiteAmount = 1f; // Pure white
            }
            else if (cutsceneTimer < WHITE_TO_BLACK_END)
            {
                // Transition from white to black
                float progress = (cutsceneTimer - WHITE_TO_BLACK_START) / (WHITE_TO_BLACK_END - WHITE_TO_BLACK_START);
                whiteAmount = 1f - progress;
            }
            else
            {
                whiteAmount = 0f; // Pure black
            }
            
            // Update the sphere system's interior color
            BlackSphereSystem.InteriorBrightness = whiteAmount;
        }

        private void UpdateStars(float deltaTime)
        {
            for (int i = activeStars.Count - 1; i >= 0; i--)
            {
                var star = activeStars[i];
                star.Life -= deltaTime;
                activeStars[i] = star;
                
                if (star.Life <= 0)
                {
                    activeStars.RemoveAt(i);
                }
            }
        }

        private void ApplyPlayerEffects(float deltaTime)
        {
            if (originPlayer == null || !originPlayer.active)
                return;
                
            // Apply slowness to the player who used the dark shard
            originPlayer.velocity *= 0.85f;
            originPlayer.slowFall = true;
        }
        
        private void UpdateScreenFade(float deltaTime)
        {
            // Start fading to black 1 second before teleport
            float fadeStartTime = TOTAL_DURATION - 1f;
            
            if (cutsceneTimer >= fadeStartTime)
            {
                // Fade from 0 to 1 over 1 second
                float fadeProgress = (cutsceneTimer - fadeStartTime) / 1f;
                screenFadeAlpha = MathHelper.Clamp(fadeProgress, 0f, 1f);
            }
            else
            {
                screenFadeAlpha = 0f;
            }
        }
        
        private void ApplyCameraEffects(float deltaTime)
        {
            // Only apply effects when oval is visible
            if (cutsceneTimer >= STAR_PHASE_DURATION)
            {
                // Gradually zoom in
                float zoomProgress = Math.Min(1f, (cutsceneTimer - STAR_PHASE_DURATION) / 2f);
                Main.GameZoomTarget = MathHelper.Lerp(originalZoom, targetZoom, zoomProgress);
                
                // Constant screenshake
                float shakeIntensity = 2f;
                Main.screenPosition += new Vector2(
                    Main.rand.NextFloat(-shakeIntensity, shakeIntensity),
                    Main.rand.NextFloat(-shakeIntensity, shakeIntensity)
                );
                
                // Spawn shockwave particles at bottom of oval
                shockwaveSpawnTimer += deltaTime;
                if (shockwaveSpawnTimer >= 0.05f) // Spawn every 0.05 seconds
                {
                    shockwaveSpawnTimer = 0f;
                    SpawnShockwaveParticle();
                }
            }
        }
        
        private void SpawnShockwaveParticle()
        {
            if (Main.netMode == Terraria.ID.NetmodeID.Server)
                return;
                
            // Spawn at bottom of oval (player's feet position)
            Vector2 spawnPos = originPosition + new Vector2(
                Main.rand.NextFloat(-60f, 60f),
                Main.rand.NextFloat(-10f, 10f)
            );
            
            // Random downward direction (between 45 degrees down-left and 45 degrees down-right)
            // Down is Pi/2 (90 degrees), so we want range from Pi/4 to 3*Pi/4
            float angle = Main.rand.NextFloat(MathHelper.PiOver4, MathHelper.Pi - MathHelper.PiOver4);
            float speed = Main.rand.NextFloat(8f, 16f);
            Vector2 velocity = new Vector2(speed, 0f).RotatedBy(angle);
            
            // Spawn the shockwave projectile
            int projType = ModContent.ProjectileType<ShockwaveLine>();
            var source = new EntitySource_Misc("DarkWorldCutscene");
            
            Projectile.NewProjectile(
                source,
                spawnPos,
                velocity,
                projType,
                0,
                0f,
                Main.myPlayer,
                ai0: speed,
                ai1: Main.rand.NextFloat(15f, 30f)
            );
        }

        private void EndCutscene()
        {
            isPlaying = false;
            
            // Reset camera zoom
            Main.GameZoomTarget = originalZoom;
            
            // Clear all VFX
            BlackSphereSystem.ClearAll();
            activeStars.Clear();
            smokeSpheres.Clear();
            
            // Teleport nearby players to Dark World
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (!player.active || player.dead)
                    continue;
                    
                float distance = Vector2.Distance(player.Center, originPosition);
                if (distance < 400f)
                {
                    // Set up Dark World parameters
                    DarkDimension.OriginX = (int)(originPosition.X / 16);
                    DarkDimension.OriginY = (int)(originPosition.Y / 16);
                    DarkDimension.WorldSeed = Main.rand.Next();
                    
                    // Enter the Dark World
                    SubworldSystem.Enter<DarkDimension>();
                    break;
                }
            }
        }

        // Draw the stars
        public override void PostDrawTiles()
        {
            if (!isPlaying || Main.dedServ)
                return;
                
            if (starTexture == null || !starTexture.IsLoaded)
                return;
                
            if (activeStars.Count == 0)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Texture2D tex = starTexture.Value;
            Vector2 origin = new Vector2(tex.Width / 2, tex.Height / 2);

            foreach (var star in activeStars)
            {
                Vector2 screenPos = star.Position - Main.screenPosition;
                float alpha = star.Life / star.MaxLife;
                
                spriteBatch.Draw(
                    tex,
                    screenPos,
                    null,
                    Color.White * alpha,
                    star.Rotation,
                    origin,
                    star.Scale,
                    SpriteEffects.None,
                    0f
                );
            }

            spriteBatch.End();
            
            // Draw screen fade to black
            if (screenFadeAlpha > 0f)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
                
                // Draw a full-screen black rectangle
                Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
                Rectangle screenRect = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
                spriteBatch.Draw(pixel, screenRect, Color.Black * screenFadeAlpha);
                
                spriteBatch.End();
            }
        }
    }
}
