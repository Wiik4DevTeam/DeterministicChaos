using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    public class SeekingKnife : ModProjectile
    {
        private const int FollowTime = 60; // 1 second at 60fps
        private const int StopTime = 12; // 0.2 seconds
        private const int DashSpeed = 105;
        private const int TotalLifetime = FollowTime + StopTime + 60; // Total duration
        private const float OrbitDistance = 400f; // Distance from player (like clock radius)
        private const int GrowthTime = 18; // 0.3 seconds at 60fps

        private Vector2 lockedPosition;
        private Vector2 lockedPlayerCenter; // Store player center when stopping
        private bool hasStopped = false;
        private bool hasDashed = false;
        private bool prevHasDashed = false; // Track previous frame value for sound
        private float orbitAngle; // The "clock position" angle
        private int spawnDelay; // Delay before knife appears
        private bool isVisible = false;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false; // Knife itself does no damage
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLifetime;
            Projectile.scale = 1.8f; // Make knives bigger
        }

        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.WriteVector2(lockedPosition);
            writer.WriteVector2(lockedPlayerCenter);
            writer.Write(hasStopped);
            writer.Write(hasDashed);
            writer.Write(orbitAngle);
            writer.Write(Projectile.rotation);
            writer.Write(spawnDelay);
            writer.Write(isVisible);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            lockedPosition = reader.ReadVector2();
            lockedPlayerCenter = reader.ReadVector2();
            hasStopped = reader.ReadBoolean();
            hasDashed = reader.ReadBoolean();
            orbitAngle = reader.ReadSingle();
            Projectile.rotation = reader.ReadSingle();
            spawnDelay = reader.ReadInt32();
            isVisible = reader.ReadBoolean();
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            // ai[0] = target player index
            // ai[1] = spawn delay in ticks (if > 0, knife is invisible until delay expires)
            int targetIndex = (int)Projectile.ai[0];
            spawnDelay = (int)Projectile.ai[1];
            
            // If there's a spawn delay, extend lifetime and start invisible
            if (spawnDelay > 0)
            {
                Projectile.timeLeft += spawnDelay;
                isVisible = false;
            }
            else
            {
                isVisible = true;
            }
            
            // Only assign random position on server, then sync to clients
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // Assign a random clock position
                int clockPosition = Main.rand.Next(12);
                
                // Calculate angle: 0 = right, rotating counter-clockwise
                orbitAngle = MathHelper.ToRadians(clockPosition * 30f);
                
                if (targetIndex >= 0 && targetIndex < Main.maxPlayers)
                {
                    Player target = Main.player[targetIndex];
                    if (target.active && !target.dead)
                    {
                        // Position at the clock position
                        Vector2 orbitPos = target.Center + new Vector2(OrbitDistance, 0f).RotatedBy(orbitAngle);
                        Projectile.Center = orbitPos;
                        
                        // Point towards player center
                        Projectile.rotation = (target.Center - Projectile.Center).ToRotation();
                    }
                }
                
                Projectile.netUpdate = true;
            }
        }

        public override void AI()
        {
            // Handle spawn delay
            if (spawnDelay > 0)
            {
                spawnDelay--;
                if (spawnDelay == 0)
                {
                    isVisible = true;
                    Projectile.netUpdate = true;
                }
                return; // Don't do anything else while delayed
            }
            
            // Check if hasDashed just changed from false to true (on clients via sync)
            if (hasDashed && !prevHasDashed && Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnifeLaunch")
                {
                    Volume = 0.7f
                }, Projectile.Center);
            }
            prevHasDashed = hasDashed;
            
            int elapsed = TotalLifetime - Projectile.timeLeft;

            // Phase 1: Follow player at fixed orbit position (1 second)
            if (elapsed < FollowTime)
            {
                int targetIndex = (int)Projectile.ai[0];
                if (targetIndex >= 0 && targetIndex < Main.maxPlayers)
                {
                    Player target = Main.player[targetIndex];
                    if (target.active && !target.dead)
                    {
                        // Maintain position at the fixed clock angle
                        Vector2 targetPos = target.Center + new Vector2(OrbitDistance, 0f).RotatedBy(orbitAngle);
                        
                        // Smoothly move to maintain orbit position
                        Vector2 toTarget = targetPos - Projectile.Center;
                        float speed = 20f;
                        
                        if (toTarget.Length() > speed)
                            Projectile.velocity = toTarget.SafeNormalize(Vector2.Zero) * speed;
                        else
                        {
                            Projectile.Center = targetPos;
                            Projectile.velocity = Vector2.Zero;
                        }
                        
                        // Always point towards player center
                        Projectile.rotation = (target.Center - Projectile.Center).ToRotation();
                    }
                }
                
                Lighting.AddLight(Projectile.Center, 0.8f, 0.2f, 0.2f);
            }
            // Phase 2: Stop and turn white (0.2 seconds)
            else if (elapsed < FollowTime + StopTime)
            {
                if (!hasStopped)
                {
                    hasStopped = true;
                    lockedPosition = Projectile.Center;
                    
                    // Store player center position when knife turns white
                    int targetIndex = (int)Projectile.ai[0];
                    if (targetIndex >= 0 && targetIndex < Main.maxPlayers)
                    {
                        Player target = Main.player[targetIndex];
                        if (target.active && !target.dead)
                        {
                            lockedPlayerCenter = target.Center;
                        }
                    }
                    
                    Projectile.velocity = Vector2.Zero;
                    Projectile.netUpdate = true;
                }
                
                Projectile.Center = lockedPosition;
                Lighting.AddLight(Projectile.Center, 1.0f, 1.0f, 1.0f);
            }
            // Phase 3: Dash and spawn slash
            else
            {
                if (!hasDashed)
                {
                    hasDashed = true;
                    
                    // Dash in the direction we're pointing (towards player)
                    Vector2 dashDir = new Vector2(1f, 0f).RotatedBy(Projectile.rotation);
                    Projectile.velocity = dashDir * DashSpeed;
                    
                    // Spawn slash attack
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        SpawnSlashAttack();
                    }
                    
                    Projectile.netUpdate = true;
                }
                
                Lighting.AddLight(Projectile.Center, 1.0f, 1.0f, 1.0f);
            }
        }

        private void SpawnSlashAttack()
        {
            int type = ModContent.ProjectileType<SlashAttack>();
            int damage = 80;
            
            // The slash is centered on the player's position when knife turned white
            Vector2 slashCenter = lockedPlayerCenter;
            
            // The slash texture has inverted width/height, so add 90 degrees
            float slashAngle = Projectile.rotation + MathHelper.PiOver2;
            
            // ai[0] = angle, ai[1] = flip flags, ai[2] = color mode (1 = white)
            int p = Projectile.NewProjectile(
                Projectile.GetSource_FromAI(),
                slashCenter,
                Vector2.Zero,
                type,
                damage,
                0f,
                Main.myPlayer,
                slashAngle,
                0,
                1f // White color mode
            );
            
            if (p >= 0 && p < Main.maxProjectiles)
                Main.projectile[p].netUpdate = true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Don't draw if still delayed
            if (!isVisible)
                return false;
            
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            if (tex == null)
                return false;
            
            int elapsed = TotalLifetime - Projectile.timeLeft;
            
            // Determine color based on phase
            Color drawColor;
            if (elapsed >= FollowTime)
            {
                // White during stop and dash phases
                drawColor = Color.White;
            }
            else
            {
                // Red during follow phase
                drawColor = Color.Red * 0.9f;
            }
            
            // Calculate scale with growth animation
            float drawScale = Projectile.scale;
            if (elapsed < GrowthTime)
            {
                // Start at 0.1 height (very short), grow to full height
                float growthProgress = elapsed / (float)GrowthTime;
                float verticalScale = MathHelper.Lerp(0.1f, 1f, growthProgress);
                drawScale = Projectile.scale * verticalScale;
            }
            
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            
            // Draw trail during dash
            if (hasDashed)
            {
                for (int i = 0; i < Projectile.oldPos.Length; i++)
                {
                    if (Projectile.oldPos[i] == Vector2.Zero)
                        continue;
                    
                    float trailAlpha = 1f - (i / (float)Projectile.oldPos.Length);
                    Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    
                    // Emissive trail, full brightness
                    Main.EntitySpriteDraw(tex, trailPos, null, drawColor * trailAlpha * 0.5f, 
                        Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
                }
            }
            
            // Draw main sprite, emissive (full brightness, ignore lighting)
            Main.EntitySpriteDraw(tex, pos, null, drawColor, Projectile.rotation, origin, 
                drawScale, SpriteEffects.None, 0);
            
            return false;
        }
    }
}
