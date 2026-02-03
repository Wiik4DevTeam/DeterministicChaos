using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    public class LatticeKnife : ModProjectile
    {
        private const int FollowTime = 60; // 1 second at 60fps
        private const int StopTime = 12; // 0.2 seconds
        private const int DashSpeed = 60;
        private const int TotalLifetime = FollowTime + StopTime + 120; // Total duration
        private const float MinDistance = 300f;
        private const float MaxDistance = 600f;

        private Vector2 lockedPosition;
        private Vector2 dashDirection;
        private bool hasStopped = false;
        private bool hasDashed = false;
        private bool prevHasDashed = false; // Track previous frame value for sound
        private int indicatorId = -1;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = false; // No damage until dash
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLifetime;
            Projectile.scale = 1.3f; // Reduced from 1.8f
        }

        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.WriteVector2(lockedPosition);
            writer.WriteVector2(dashDirection);
            writer.Write(hasStopped);
            writer.Write(hasDashed);
            writer.Write(indicatorId);
            writer.Write(Projectile.rotation);
            writer.Write(Projectile.hostile);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            lockedPosition = reader.ReadVector2();
            dashDirection = reader.ReadVector2();
            hasStopped = reader.ReadBoolean();
            hasDashed = reader.ReadBoolean();
            indicatorId = reader.ReadInt32();
            Projectile.rotation = reader.ReadSingle();
            Projectile.hostile = reader.ReadBoolean();
        }

        private void SpawnIndicator()
        {
            int type = ModContent.ProjectileType<Enemy.SliceIndicator>();
            
            // The indicator shows the path from knife to its target
            Vector2 indicatorPos = Projectile.Center;
            float indicatorAngle = Projectile.rotation;
            
            // ai[0] = damage (negative to prevent spawning attacks)
            // ai[1] = angle
            // ai[2] = scale modifier for lattice (positive value means it's a lattice indicator)
            indicatorId = Projectile.NewProjectile(
                Projectile.GetSource_FromAI(),
                indicatorPos,
                Vector2.Zero,
                type,
                0,
                0f,
                Main.myPlayer,
                -1f, // Negative damage signals "don't spawn attacks"
                indicatorAngle,
                2.5f // Scale modifier: taller (2.5x height) and skinnier (0.4x width in SliceIndicator)
            );
            
            if (indicatorId >= 0 && indicatorId < Main.maxProjectiles)
            {
                // Adjust indicator lifetime to match knife phases
                Main.projectile[indicatorId].timeLeft = FollowTime + StopTime;
                Main.projectile[indicatorId].netUpdate = true;
                
                // Move knife backwards by half the indicator height so it lines up
                Texture2D indicatorTex = TextureAssets.Projectile[type].Value;
                if (indicatorTex != null)
                {
                    float halfIndicatorHeight = indicatorTex.Height * 2.5f * 0.5f; // 2.5 is scale modifier
                    Vector2 moveBack = new Vector2((float)System.Math.Cos(Projectile.rotation), (float)System.Math.Sin(Projectile.rotation));
                    Projectile.Center -= moveBack * halfIndicatorHeight;
                }
            }
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            // ai[0] = target player index
            int targetIndex = (int)Projectile.ai[0];
            
            if (targetIndex >= 0 && targetIndex < Main.maxPlayers)
            {
                Player target = Main.player[targetIndex];
                if (target.active && !target.dead)
                {
                    // Only calculate on server/singleplayer
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // Random angle around player
                        float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                        
                        // Random distance between min and max
                        float distance = Main.rand.NextFloat(MinDistance, MaxDistance);
                        
                        // Position knife at that distance and angle
                        Vector2 spawnPos = target.Center + new Vector2(distance, 0f).RotatedBy(angle);
                        Projectile.Center = spawnPos;
                        
                        // Calculate dash direction (towards player with random variance)
                        Vector2 baseDirection = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                        
                        float angleVariance = Main.rand.NextFloat(-MathHelper.PiOver4, MathHelper.PiOver4);
                        dashDirection = baseDirection.RotatedBy(angleVariance);
                        
                        // Point in dash direction
                        Projectile.rotation = dashDirection.ToRotation();
                        
                        // Spawn indicator line showing the path
                        SpawnIndicator();
                        
                        // CRITICAL: Sync the spawned values to clients
                        Projectile.netUpdate = true;
                    }
                }
            }
            
            // Play knife spawn sound
            if (Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightKnifeSpawn")
                {
                    Volume = 0.5f
                }, Projectile.Center);
            }
        }

        public override void AI()
        {
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

            // Phase 1: Stay still with red indicator (1 second)
            if (elapsed < FollowTime)
            {
                Projectile.velocity = Vector2.Zero;
                Lighting.AddLight(Projectile.Center, 0.8f, 0.2f, 0.2f);
            }
            // Phase 2: Stop and turn white (0.2 seconds)
            else if (elapsed < FollowTime + StopTime)
            {
                if (!hasStopped)
                {
                    hasStopped = true;
                    lockedPosition = Projectile.Center;
                    Projectile.velocity = Vector2.Zero;
                    Projectile.netUpdate = true;
                }
                
                Projectile.Center = lockedPosition;
                Lighting.AddLight(Projectile.Center, 1.0f, 1.0f, 1.0f);
            }
            // Phase 3: Dash forward (becomes hostile)
            else
            {
                if (!hasDashed)
                {
                    hasDashed = true;
                    
                    // Dash in the stored direction
                    Projectile.velocity = dashDirection * DashSpeed;
                    Projectile.hostile = true; // Now deals damage
                    Projectile.netUpdate = true;
                }
                
                Lighting.AddLight(Projectile.Center, 1.0f, 1.0f, 1.0f);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // Kill projectile on hit
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor)
        {
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
                // Red during telegraph phase
                drawColor = Color.Red * 0.9f;
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
                    
                    Main.EntitySpriteDraw(tex, trailPos, null, drawColor * trailAlpha * 0.5f, 
                        Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
                }
            }
            
            // Draw main sprite, emissive
            Main.EntitySpriteDraw(tex, pos, null, drawColor, Projectile.rotation, origin, 
                Projectile.scale, SpriteEffects.None, 0);
            
            return false;
        }
    }
}
