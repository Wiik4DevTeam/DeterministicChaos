using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    // Friendly version of the Roaring Knight's Seeking Knife
    // Spawns around a target NPC, follows it, then dashes through dealing damage
    public class RogueSeekingKnife : ModProjectile
    {
        public override string Texture => "DeterministicChaos/Content/Projectiles/Enemy/SeekingKnife";
        
        private const int FollowTime = 45; // 0.75 seconds following
        private const int StopTime = 10; // Brief pause before dash
        private const int DashSpeed = 80;
        private const int TotalLifetime = FollowTime + StopTime + 45;
        private const float OrbitDistance = 200f; // Closer orbit for smaller targets
        private const int GrowthTime = 12;
        
        private Vector2 lockedPosition;
        private bool hasStopped = false;
        private bool hasDashed = false;
        private bool prevHasDashed = false;
        private float orbitAngle;
        private bool isVisible = true;
        
        // ai[0] = target NPC index
        // ai[1] = spawn delay (optional)
        
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }
        
        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false; // Knife itself does not damage, the slash does
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1; // Does not despawn on hit since it does not hit
            Projectile.timeLeft = TotalLifetime;
            Projectile.scale = 1.4f;
            
            // Use rogue/throwing damage for the spawned slash
            Projectile.DamageType = DamageClass.Throwing;
        }
        
        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.WriteVector2(lockedPosition);
            writer.Write(hasStopped);
            writer.Write(hasDashed);
            writer.Write(orbitAngle);
            writer.Write(Projectile.rotation);
            writer.Write(isVisible);
        }
        
        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            lockedPosition = reader.ReadVector2();
            hasStopped = reader.ReadBoolean();
            hasDashed = reader.ReadBoolean();
            orbitAngle = reader.ReadSingle();
            Projectile.rotation = reader.ReadSingle();
            isVisible = reader.ReadBoolean();
        }
        
        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            int targetIndex = (int)Projectile.ai[0];
            
            // Assign a random orbit position
            orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            
            if (targetIndex >= 0 && targetIndex < Main.maxNPCs)
            {
                NPC target = Main.npc[targetIndex];
                if (target.active && !target.friendly)
                {
                    // Position at orbit around target
                    Vector2 orbitPos = target.Center + new Vector2(OrbitDistance, 0f).RotatedBy(orbitAngle);
                    Projectile.Center = orbitPos;
                    
                    // Point towards target center
                    Projectile.rotation = (target.Center - Projectile.Center).ToRotation();
                }
            }
            
            // Play spawn sound
            if (Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = 0.8f }, Projectile.Center);
            }
        }
        
        public override void AI()
        {
            // Check if hasDashed just changed (for sound sync)
            if (hasDashed && !prevHasDashed && Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnifeLaunch")
                {
                    Volume = 0.5f,
                    Pitch = 0.3f
                }, Projectile.Center);
            }
            prevHasDashed = hasDashed;
            
            int elapsed = TotalLifetime - Projectile.timeLeft;
            int targetIndex = (int)Projectile.ai[0];
            
            // Phase 1: Follow target at fixed orbit position
            if (elapsed < FollowTime)
            {
                if (targetIndex >= 0 && targetIndex < Main.maxNPCs)
                {
                    NPC target = Main.npc[targetIndex];
                    if (target.active && !target.friendly)
                    {
                        // Maintain position at orbit angle
                        Vector2 targetPos = target.Center + new Vector2(OrbitDistance, 0f).RotatedBy(orbitAngle);
                        
                        // Smoothly move to orbit position
                        Vector2 toTarget = targetPos - Projectile.Center;
                        float speed = 18f;
                        
                        if (toTarget.Length() > speed)
                            Projectile.velocity = toTarget.SafeNormalize(Vector2.Zero) * speed;
                        else
                        {
                            Projectile.Center = targetPos;
                            Projectile.velocity = Vector2.Zero;
                        }
                        
                        // Point towards target center
                        Projectile.rotation = (target.Center - Projectile.Center).ToRotation();
                    }
                    else
                    {
                        // Target died, find new target or despawn
                        FindNewTarget();
                    }
                }
                
                Lighting.AddLight(Projectile.Center, 0.6f, 0.6f, 0.6f);
            }
            // Phase 2: Stop and turn white
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
            // Phase 3: Dash towards target
            else
            {
                if (!hasDashed)
                {
                    hasDashed = true;
                    
                    // Dash in the direction we're pointing
                    Vector2 dashDir = new Vector2(1f, 0f).RotatedBy(Projectile.rotation);
                    Projectile.velocity = dashDir * DashSpeed;
                    
                    // Spawn slash attack at the target position
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
            int targetIndex = (int)Projectile.ai[0];
            Vector2 slashCenter = Projectile.Center;
            
            // If we have a valid target, center the slash on them
            if (targetIndex >= 0 && targetIndex < Main.maxNPCs)
            {
                NPC target = Main.npc[targetIndex];
                if (target.active)
                {
                    slashCenter = target.Center;
                }
            }
            
            int type = ModContent.ProjectileType<RogueSlashAttack>();
            
            // Slash angle is perpendicular to the knife direction
            float slashAngle = Projectile.rotation + MathHelper.PiOver2;
            
            // Slash deals half the knife's damage
            int slashDamage = Projectile.damage / 2;
            
            int p = Projectile.NewProjectile(
                Projectile.GetSource_FromAI(),
                slashCenter,
                Vector2.Zero,
                type,
                slashDamage,
                Projectile.knockBack,
                Projectile.owner,
                slashAngle,
                0f,
                0f
            );
            
            if (p >= 0 && p < Main.maxProjectiles)
                Main.projectile[p].netUpdate = true;
        }
        
        private void FindNewTarget()
        {
            // Look for nearest enemy
            float closestDist = 600f;
            int closestNPC = -1;
            
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && !npc.dontTakeDamage && npc.lifeMax > 5)
                {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestNPC = i;
                    }
                }
            }
            
            if (closestNPC >= 0)
            {
                Projectile.ai[0] = closestNPC;
                Projectile.netUpdate = true;
            }
            else
            {
                // No targets, despawn
                Projectile.Kill();
            }
        }
        
        public override bool PreDraw(ref Color lightColor)
        {
            if (!isVisible)
                return false;
            
            Texture2D tex = TextureAssets.Projectile[ModContent.ProjectileType<Enemy.SeekingKnife>()].Value;
            if (tex == null)
                return false;
            
            int elapsed = TotalLifetime - Projectile.timeLeft;
            
            // Color based on phase
            Color drawColor;
            if (elapsed >= FollowTime)
            {
                // White during stop and dash
                drawColor = Color.White;
            }
            else
            {
                // Gray/silver during follow
                drawColor = new Color(200, 200, 220) * 0.9f;
            }
            
            // Growth animation
            float drawScale = Projectile.scale;
            if (elapsed < GrowthTime)
            {
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
                    
                    Main.EntitySpriteDraw(tex, trailPos, null, drawColor * trailAlpha * 0.5f,
                        Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
                }
            }
            
            // Draw main sprite
            Main.EntitySpriteDraw(tex, pos, null, drawColor, Projectile.rotation, origin,
                drawScale, SpriteEffects.None, 0);
            
            return false;
        }
    }
}
