using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
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

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class ToughGloveProjectile : ModProjectile
    {
        private const int FRAME_COUNT = 2;
        private const int FRAME_WIDTH = 74;
        private const int FRAME_HEIGHT = 74;

        public bool IsLargePunch => Projectile.ai[0] == 1;

        private int frameCounter = 0;
        private int currentFrame = 0;
        private float baseScale = 1f;
        private bool isShrinking = false;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = FRAME_COUNT;
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1; // Infinite penetration, controlled by local immunity
            Projectile.timeLeft = 40; // Slightly longer for shrink animation
            Projectile.tileCollide = false; // Go through walls
            Projectile.ignoreWater = true;
            Projectile.DamageType = ModContent.GetInstance<RangedMeleeDamageClass>();
            
            // Each enemy can only be hit once per projectile
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // -1 means hit once and never again
        }

        public override void AI()
        {
            // Set base scale on first frame
            if (Projectile.localAI[0] == 0)
            {
                baseScale = IsLargePunch ? 1.8f : 1f;
                Projectile.scale = baseScale;
                Projectile.localAI[0] = 1;
            }

            // Scale for large punch hitbox
            if (IsLargePunch)
            {
                Projectile.width = 60;
                Projectile.height = 60;
            }

            // Start shrinking when close to despawn
            if (Projectile.timeLeft < 15)
            {
                isShrinking = true;
                float shrinkProgress = Projectile.timeLeft / 15f;
                Projectile.scale = baseScale * shrinkProgress;
                
                // Stop dealing damage while shrinking
                if (Projectile.timeLeft < 10)
                    Projectile.damage = 0;
            }

            // Animate frames
            frameCounter++;
            if (frameCounter >= 4) // Change frame every 4 ticks
            {
                frameCounter = 0;
                currentFrame++;
                if (currentFrame >= FRAME_COUNT)
                    currentFrame = 0;
            }
            Projectile.frame = currentFrame;

            // Rotate to face movement direction
            // Sprite faces bottom-left, adjusted by -135 degrees total
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver4 - MathHelper.PiOver2;

            // Slow down slightly
            Projectile.velocity *= 0.97f;

            // Orange fire trail (less when shrinking)
            if (!isShrinking && Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch);
                dust.noGravity = true;
                dust.scale = IsLargePunch ? 1.8f : 1.2f;
                dust.velocity = Projectile.velocity * 0.2f;
            }

            // Lighting
            Lighting.AddLight(Projectile.Center, 1f * Projectile.scale, 0.6f * Projectile.scale, 0.1f * Projectile.scale);

            // Spawn orange speed lines behind the projectile
            if (!isShrinking && Main.rand.NextBool(2))
            {
                Vector2 projDir = Projectile.velocity.SafeNormalize(Vector2.Zero);
                Vector2 perpendicular = new Vector2(-projDir.Y, projDir.X);
                float sideOffset = Main.rand.NextFloat(-8f, 8f);
                Vector2 spawnPos = Projectile.Center + perpendicular * sideOffset;

                Vector2 lineVel = -projDir * Main.rand.NextFloat(3f, 6f) + perpendicular * Main.rand.NextFloat(-0.5f, 0.5f);
                float lineLength = Main.rand.NextFloat(15f, 35f) * (IsLargePunch ? 1.5f : 1f);
                float lineThickness = Main.rand.NextFloat(1.5f, 3f) * (IsLargePunch ? 1.3f : 1f);

                int proj = Projectile.NewProjectile(
                    Projectile.GetSource_FromAI(),
                    spawnPos,
                    lineVel,
                    ModContent.ProjectileType<SpeedLine>(),
                    0, 0f, Projectile.owner,
                    lineLength, lineThickness);

                if (proj >= 0 && proj < Main.maxProjectiles)
                    Main.projectile[proj].localAI[1] = 1f; // Orange mode
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (IsLargePunch)
            {
                // Large projectiles only explode if target is burning - detonates the burn
                if (target.HasBuff(BuffID.OnFire))
                {
                    // Remove the burn (detonated)
                    target.DelBuff(target.FindBuffIndex(BuffID.OnFire));
                    
                    // Explosion center is at the target
                    Vector2 explosionCenter = target.Center;
                    
                    // Play explosion sound
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.2f, Volume = 0.7f }, explosionCenter);
                    
                    // Create explosion visual at target
                    for (int i = 0; i < 30; i++)
                    {
                        Vector2 dustVel = Main.rand.NextVector2Circular(10f, 10f);
                        Dust dust = Dust.NewDustDirect(explosionCenter - new Vector2(16, 16), 32, 32, DustID.Torch, dustVel.X, dustVel.Y);
                        dust.noGravity = true;
                        dust.scale = Main.rand.NextFloat(2f, 3.5f);
                    }
                    
                    // Extra smoke/fire dust
                    for (int i = 0; i < 15; i++)
                    {
                        Vector2 dustVel = Main.rand.NextVector2Circular(6f, 6f);
                        Dust dust = Dust.NewDustDirect(explosionCenter - new Vector2(20, 20), 40, 40, DustID.Smoke, dustVel.X, dustVel.Y);
                        dust.scale = Main.rand.NextFloat(1.5f, 2.5f);
                    }
                    
                    // Deal explosion damage - extra 50% to the primary target
                    float explosionRadius = 80f;
                    int explosionDamage = damageDone / 2;
                    
                    // Apply explosion damage to the primary target
                    Main.player[Projectile.owner].ApplyDamageToNPC(target, explosionDamage, 0f, 0, false);
                    
                    // Apply explosion damage to nearby enemies
                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        NPC npc = Main.npc[i];
                        if (npc.active && !npc.friendly && !npc.dontTakeDamage && npc.whoAmI != target.whoAmI)
                        {
                            float distance = Vector2.Distance(explosionCenter, npc.Center);
                            if (distance < explosionRadius)
                            {
                                // Apply explosion damage
                                Main.player[Projectile.owner].ApplyDamageToNPC(npc, explosionDamage, 0f, 0, false);
                            }
                        }
                    }
                    
                    // Kill projectile after explosion
                    Projectile.Kill();
                }
                // No burn applied from large punches
            }
            else
            {
                // Small punches ignite enemies
                target.AddBuff(BuffID.OnFire, 180); // 3 seconds
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Don't draw if scale is too small
            if (Projectile.scale < 0.05f)
                return false;

            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            
            // Calculate source rectangle for current frame
            Rectangle sourceRect = new Rectangle(0, currentFrame * FRAME_HEIGHT, FRAME_WIDTH, FRAME_HEIGHT);
            
            Vector2 origin = new Vector2(FRAME_WIDTH / 2, FRAME_HEIGHT / 2);
            
            // Draw with proper rotation - no flip needed, rotation handles direction
            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                sourceRect,
                lightColor * ((255 - Projectile.alpha) / 255f),
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            // Only spawn dust if not fully shrunk
            if (Projectile.scale > 0.1f)
            {
                // Burst of fire dust
                for (int i = 0; i < 8; i++)
                {
                    Vector2 dustVel = Main.rand.NextVector2Circular(4f, 4f);
                    Dust dust = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Torch, dustVel.X, dustVel.Y);
                    dust.noGravity = true;
                    dust.scale = IsLargePunch ? 2f : 1.5f;
                }
            }
        }

        public override Color? GetAlpha(Color lightColor)
        {
            // Bright orange tint
            return new Color(255, 200, 150) * ((255 - Projectile.alpha) / 255f);
        }
    }
}
