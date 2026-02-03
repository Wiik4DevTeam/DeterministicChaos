using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class RoaringBulletProjectile : ModProjectile
    {
        // Constants
        public const int ExplosionThreshold = 5;
        public const int AttachDuration = 120;
        public const int ExplosionWindow = 120;


        public bool IsAttached => Projectile.ai[0] == 1f;
        public int AttachedNPC => (int)Projectile.ai[1];
        public int StoredDamage => (int)Projectile.ai[2];

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.aiStyle = -1;
            Projectile.extraUpdates = 1;
            Projectile.ai[1] = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }
        
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(Projectile.localAI[0]);
            writer.Write(Projectile.localAI[1]);
            writer.Write(Projectile.localAI[2]);
        }
        
        public override void ReceiveExtraAI(BinaryReader reader)
        {
            Projectile.localAI[0] = reader.ReadSingle();
            Projectile.localAI[1] = reader.ReadSingle();
            Projectile.localAI[2] = reader.ReadSingle();
        }

        public override void AI()
        {
            if (IsAttached)
            {
                AttachedBehavior();
            }
            else
            {
                FlyingBehavior();
            }

            // Always emit light
            Lighting.AddLight(Projectile.Center, 0.3f, 0.1f, 0.3f);
        }

        private void FlyingBehavior()
        {
            // Rotate to face velocity
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // Spawn dust trail
            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 100, default, 0.8f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }

        private void AttachedBehavior()
        {
            Projectile.localAI[0]++;

            // Despawn after 2 seconds
            if (Projectile.localAI[0] >= AttachDuration)
            {
                Projectile.Kill();
                return;
            }

            // Check if target NPC is still valid
            int npcIndex = AttachedNPC;
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs || !Main.npc[npcIndex].active)
            {
                Projectile.Kill();
                return;
            }

            NPC target = Main.npc[npcIndex];

            // Follow the NPC with offset
            Vector2 offset = new Vector2(Projectile.localAI[1], Projectile.localAI[2]);
            Projectile.Center = target.Center + offset;
            Projectile.velocity = Vector2.Zero;

            // Pulse effect
            float pulse = 0.5f + 0.5f * (float)System.Math.Sin(Projectile.localAI[0] * 0.2f);
            Lighting.AddLight(Projectile.Center, 0.4f * pulse, 0.1f * pulse, 0.5f * pulse);

            // Small dust particles
            if (Main.rand.NextBool(10))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 100, default, 0.5f);
                dust.noGravity = true;
                dust.velocity = Main.rand.NextVector2Circular(1f, 1f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Attach to the enemy instead of despawning
            if (!IsAttached && target.active && !target.friendly)
            {
                Projectile.ai[0] = 1f;
                Projectile.ai[1] = target.whoAmI;
                Projectile.localAI[0] = 0f;

                // Store random offset from center
                Projectile.localAI[1] = Main.rand.NextFloat(-target.width * 0.4f, target.width * 0.4f);
                Projectile.localAI[2] = Main.rand.NextFloat(-target.height * 0.4f, target.height * 0.4f);

                // Store the original damage before zeroing it
                Projectile.ai[2] = Projectile.damage;

                // Keep the projectile alive
                Projectile.penetrate = -1;
                Projectile.damage = 0;
                Projectile.timeLeft = AttachDuration + 10;
                
                // Sync state to other clients
                Projectile.netUpdate = true;

                // Play attach sound
                if (Main.netMode != NetmodeID.Server)
                {
                    SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.5f, Pitch = 0.5f }, Projectile.Center);
                }

                // Check for explosion
                CheckForExplosion(target);
            }
        }

        private void CheckForExplosion(NPC target)
        {
            // Count attached bullets on this NPC
            List<Projectile> attachedBullets = new List<Projectile>();

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == Projectile.type && proj.owner == Projectile.owner)
                {
                    if (proj.ModProjectile is RoaringBulletProjectile bullet && bullet.IsAttached && bullet.AttachedNPC == target.whoAmI)
                    {
                        // Only count bullets attached within the explosion window
                        if (proj.localAI[0] < ExplosionWindow)
                        {
                            attachedBullets.Add(proj);
                        }
                    }
                }
            }

            // Trigger explosion if threshold met
            if (attachedBullets.Count >= ExplosionThreshold)
            {
                TriggerExplosion(target, attachedBullets);
            }
        }

        private void TriggerExplosion(NPC target, List<Projectile> bullets)
        {
            // Calculate explosion damage from all attached bullets
            int explosionDamage = 0;
            foreach (Projectile bullet in bullets)
            {
                explosionDamage += (int)bullet.ai[2];
            }
            explosionDamage /= 2;

            // Deal damage to the target, only the owner deals damage to prevent duplicates
            // Also ensure valid owner index and active player
            if (Projectile.owner == Main.myPlayer && 
                Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers &&
                target.active && !target.friendly)
            {
                Player player = Main.player[Projectile.owner];
                if (player != null && player.active)
                {
                    NPC.HitInfo hitInfo = new NPC.HitInfo
                    {
                        Damage = explosionDamage,
                        Knockback = 8f,
                        HitDirection = player.direction,
                        Crit = Main.rand.Next(100) < player.GetCritChance(DamageClass.Ranged)
                    };
                    target.StrikeNPC(hitInfo);
                    
                    // Sync the hit to other clients in multiplayer
                    if (Main.netMode != NetmodeID.SinglePlayer)
                    {
                        NetMessage.SendStrikeNPC(target, hitInfo);
                    }
                }
            }

            // Visual and sound effects
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f, Pitch = -0.3f }, target.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.8f }, target.Center);

            // Explosion dust
            for (int i = 0; i < 40; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust dust = Dust.NewDustDirect(target.Center, 1, 1, DustID.Shadowflame, velocity.X, velocity.Y, 100, default, 2f);
                dust.noGravity = true;
            }

            // Purple/black smoke
            for (int i = 0; i < 20; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);
                Dust dust = Dust.NewDustDirect(target.Center, 1, 1, DustID.Smoke, velocity.X, velocity.Y, 150, Color.Purple, 2f);
                dust.noGravity = true;
            }

            // Gore-like particles
            for (int i = 0; i < 10; i++)
            {
                Vector2 pos = target.Center + Main.rand.NextVector2Circular(target.width * 0.5f, target.height * 0.5f);
                Vector2 velocity = (pos - target.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 6f);
                Dust dust = Dust.NewDustDirect(pos, 1, 1, DustID.PurpleTorch, velocity.X, velocity.Y, 0, default, 1.5f);
                dust.noGravity = false;
            }

            // Kill all attached bullets
            foreach (Projectile bullet in bullets)
            {
                // Create small explosion at each bullet position
                for (int i = 0; i < 5; i++)
                {
                    Dust dust = Dust.NewDustDirect(bullet.Center, 1, 1, DustID.Shadowflame, 
                        Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, default, 1f);
                    dust.noGravity = true;
                }
                bullet.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Draw afterimages when flying
            if (!IsAttached)
            {
                Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
                Vector2 origin = texture.Size() * 0.5f;

                for (int i = 0; i < Projectile.oldPos.Length; i++)
                {
                    Vector2 drawPos = Projectile.oldPos[i] - Main.screenPosition + new Vector2(Projectile.width / 2, Projectile.height / 2);
                    Color trailColor = Color.Purple * (1f - i / (float)Projectile.oldPos.Length) * 0.5f;
                    Main.EntitySpriteDraw(texture, drawPos, null, trailColor, Projectile.rotation, origin, Projectile.scale * (1f - i * 0.1f), SpriteEffects.None, 0);
                }
            }

            return true;
        }

        public override void PostDraw(Color lightColor)
        {
            // Draw emissive glow
            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;

            float glowIntensity = IsAttached ? 0.5f + 0.3f * (float)System.Math.Sin(Projectile.localAI[0] * 0.2f) : 0.6f;
            Color glowColor = Color.Purple * glowIntensity;

            Main.EntitySpriteDraw(texture, drawPos, null, glowColor, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);
        }

        public override Color? GetAlpha(Color lightColor)
        {
            // Always fully visible
            return Color.White;
        }
    }
}
