using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    public class Projectile_Star : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;

            Projectile.hostile = true;
            Projectile.friendly = false;

            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;

            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;

            Projectile.scale = 1.1f;
        }

        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            // Sync explosion counter and mode
            writer.Write(Projectile.ai[0]);
            writer.Write(Projectile.ai[1]);
            writer.Write(Projectile.ai[2]);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            Projectile.ai[0] = reader.ReadSingle();
            Projectile.ai[1] = reader.ReadSingle();
            Projectile.ai[2] = reader.ReadSingle();
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            // Play star emit sound with random pitch
            if (Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightStarEmit")
                {
                    Volume = 0.8f,
                    PitchVariance = 0.3f
                }, Projectile.Center);
            }
        }

        public override void AI()
        {
            // ai[0] = sphere NPC index (if in absorb mode) OR explosion timer
            // ai[1] = behavior mode (-1 = absorb mode, 0 = normal, 2 = explode)
            // ai[2] = fuse time for explosion
            
            // Don't rotate the star
            // Projectile.rotation += 0.08f;  // Removed rotation
            
            // Absorb mode: despawn when near sphere
            if (Projectile.ai[1] == -1f)
            {
                int sphereIdx = (int)Projectile.ai[0];
                if (sphereIdx >= 0 && sphereIdx < Main.maxNPCs && Main.npc[sphereIdx].active)
                {
                    NPC sphere = Main.npc[sphereIdx];
                    float distToSphere = Vector2.Distance(Projectile.Center, sphere.Center);
                    
                    // Home towards sphere to ensure they don't miss
                    Vector2 toSphere = sphere.Center - Projectile.Center;
                    float currentSpeed = Projectile.velocity.Length();
                    
                    if (distToSphere > 10f && currentSpeed > 0f)
                    {
                        Vector2 desiredDirection = toSphere.SafeNormalize(Vector2.Zero);
                        Vector2 desiredVelocity = desiredDirection * currentSpeed;
                        
                        float turnSpeed = 0.15f;
                        Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, turnSpeed);
                    }
                    
                    // Despawn when very close to sphere
                    if (distToSphere < 80f)
                    {
                        Projectile.Kill();
                        return;
                    }
                }
            }
            
            // Explode mode: count down and spawn seekers
            if (Projectile.ai[1] == 2f)
            {
                // Only increment counter on server/singleplayer
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Projectile.ai[0]++;
                    
                    // Sync the counter periodically
                    if ((int)Projectile.ai[0] % 10 == 0)
                        Projectile.netUpdate = true;
                }

                float fuse = Projectile.ai[2];
                if (fuse <= 0f)
                    fuse = 75f;

                float remaining = fuse - Projectile.ai[0];
                if (remaining <= 180f)
                {
                    float slowT = (180f - remaining) / 180f;
                    float easedT = slowT * slowT * slowT;
                    
                    float drag = MathHelper.Lerp(0.98f, 0.75f, easedT);
                    
                    if (remaining <= 110f)
                    {
                        float pullbackT = (110f - remaining) / 110f;
                        Vector2 pullback = -Projectile.velocity.SafeNormalize(Vector2.Zero) * pullbackT * 0.6f;
                        Projectile.velocity += pullback;
                    }
                    
                    Projectile.velocity *= drag;
                }

                if (Projectile.ai[0] >= fuse)
                {
                    // Play explosion sound on all clients
                    if (Main.netMode != NetmodeID.Server)
                    {
                        SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightExplosion")
                        {
                            Volume = 0.8f
                        }, Projectile.Center);
                    }
                    
                    // Spawn seeking projectiles server-side
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        int seekerType = ModContent.ProjectileType<Projectile_Seeking>();
                        int count = 3;

                        for (int i = 0; i < count; i++)
                        {
                            float angle = (MathHelper.TwoPi * i) / count;
                            Vector2 dir = new Vector2(1f, 0f).RotatedBy(angle);
                            float speed = 9f;

                            int p = Projectile.NewProjectile(
                                Projectile.GetSource_FromAI(),
                                Projectile.Center,
                                dir * speed,
                                seekerType,
                                Projectile.damage,
                                0f,
                                Main.myPlayer
                            );
                            
                            if (p >= 0 && p < Main.maxProjectiles)
                                Main.projectile[p].netUpdate = true;
                        }
                        
                        // Spawn 5 shockwave lines at explosion
                        int shockwaveType = ModContent.ProjectileType<ShockwaveLine>();
                        for (int i = 0; i < 5; i++)
                        {
                            float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                            Vector2 dir = new Vector2(1f, 0f).RotatedBy(angle);
                            float speed = Main.rand.NextFloat(12f, 20f); // Medium speed
                            
                            int p = Projectile.NewProjectile(
                                Projectile.GetSource_FromAI(),
                                Projectile.Center,
                                dir * speed,
                                shockwaveType,
                                0,
                                0f,
                                Main.myPlayer,
                                speed,
                                Main.rand.NextFloat(15f, 30f) // Short to medium length
                            );
                            
                            if (p >= 0 && p < Main.maxProjectiles)
                                Main.projectile[p].netUpdate = true;
                        }
                    }

                    Projectile.Kill();
                }
            }
        }




        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            // Only apply pulsing/color change when in explode mode
            float redT = 0f;
            float drawScale = Projectile.scale;
            
            if (Projectile.ai[1] == 2f)
            {
                int fuse = (int)Projectile.ai[2];
                if (fuse <= 0) fuse = 75;

                float remaining = fuse - Projectile.ai[0];

                if (remaining <= 150f)
                    redT = (150f - remaining) / 150f;

                if (remaining <= 150f)
                {
                    float pulseSpeed = MathHelper.Lerp(8f, 35f, redT);
                    float pulse = (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * pulseSpeed) * 0.5f + 0.5f;
                    float pulseAmount = MathHelper.Lerp(0f, 0.85f, redT);
                    drawScale = Projectile.scale * (1f + pulse * pulseAmount);
                }
            }

            Color tint = Color.Lerp(Color.White, new Color(255, 60, 60), MathHelper.Clamp(redT, 0f, 1f));

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                float t = i / (float)Projectile.oldPos.Length;
                float a = (1f - t) * 0.45f;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;

                Main.spriteBatch.Draw(
                    tex,
                    pos,
                    null,
                    tint * a,
                    Projectile.rotation,
                    origin,
                    Projectile.scale, // Trail uses base scale
                    SpriteEffects.None,
                    0f
                );
            }

            Main.spriteBatch.Draw(
                tex,
                Projectile.Center - Main.screenPosition,
                null,
                tint,
                Projectile.rotation,
                origin,
                drawScale, // Main star uses pulsing scale
                SpriteEffects.None,
                0f
            );

            return false;
        }

    }
}
