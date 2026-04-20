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
    public class ToughGauntletProjectile : ModProjectile
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
            Projectile.penetrate = -1;
            Projectile.timeLeft = 40;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = ModContent.GetInstance<RangedMeleeDamageClass>();

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI()
        {
            // Set base scale on first frame
            if (Projectile.localAI[0] == 0)
            {
                baseScale = IsLargePunch ? 2.2f : 1.1f;
                Projectile.scale = baseScale;
                Projectile.localAI[0] = 1;
            }

            // Scale for large punch hitbox
            if (IsLargePunch)
            {
                Projectile.width = 70;
                Projectile.height = 70;
            }

            // Start shrinking when close to despawn
            if (Projectile.timeLeft < 15)
            {
                isShrinking = true;
                float shrinkProgress = Projectile.timeLeft / 15f;
                Projectile.scale = baseScale * shrinkProgress;

                if (Projectile.timeLeft < 10)
                    Projectile.damage = 0;
            }

            // Animate frames
            frameCounter++;
            if (frameCounter >= 4)
            {
                frameCounter = 0;
                currentFrame++;
                if (currentFrame >= FRAME_COUNT)
                    currentFrame = 0;
            }
            Projectile.frame = currentFrame;

            // Rotate to face movement direction
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver4 - MathHelper.PiOver2;

            // Slow down slightly
            Projectile.velocity *= 0.97f;

            // Orange-red fire trail (more intense than ToughGlove)
            if (!isShrinking && Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch);
                dust.noGravity = true;
                dust.scale = IsLargePunch ? 2.2f : 1.4f;
                dust.velocity = Projectile.velocity * 0.2f;
            }

            // Extra hellfire-colored particles
            if (!isShrinking && Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.SolarFlare);
                dust.noGravity = true;
                dust.scale = IsLargePunch ? 1.5f : 0.8f;
                dust.velocity = Projectile.velocity * 0.1f;
            }

            // Lighting
            Lighting.AddLight(Projectile.Center, 1.2f * Projectile.scale, 0.5f * Projectile.scale, 0.1f * Projectile.scale);

            // Spawn orange speed lines behind the projectile
            if (!isShrinking && Main.rand.NextBool(2))
            {
                Vector2 projDir = Projectile.velocity.SafeNormalize(Vector2.Zero);
                Vector2 perpendicular = new Vector2(-projDir.Y, projDir.X);
                float sideOffset = Main.rand.NextFloat(-10f, 10f);
                Vector2 spawnPos = Projectile.Center + perpendicular * sideOffset;

                Vector2 lineVel = -projDir * Main.rand.NextFloat(3f, 7f) + perpendicular * Main.rand.NextFloat(-0.5f, 0.5f);
                float lineLength = Main.rand.NextFloat(18f, 40f) * (IsLargePunch ? 1.6f : 1f);
                float lineThickness = Main.rand.NextFloat(1.5f, 3.5f) * (IsLargePunch ? 1.4f : 1f);

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
                // Large projectiles create massive explosions on Hellfire-afflicted targets
                if (target.HasBuff(BuffID.OnFire3))
                {
                    // Remove the Hellfire (detonated)
                    target.DelBuff(target.FindBuffIndex(BuffID.OnFire3));

                    Vector2 explosionCenter = target.Center;

                    // Play massive explosion sound
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.1f, Volume = 1f }, explosionCenter);

                    // Massive explosion visual, much larger than ToughGlove
                    for (int i = 0; i < 50; i++)
                    {
                        Vector2 dustVel = Main.rand.NextVector2Circular(16f, 16f);
                        Dust dust = Dust.NewDustDirect(explosionCenter - new Vector2(24, 24), 48, 48, DustID.SolarFlare, dustVel.X, dustVel.Y);
                        dust.noGravity = true;
                        dust.scale = Main.rand.NextFloat(2.5f, 4.5f);
                    }

                    // Fire ring
                    for (int i = 0; i < 40; i++)
                    {
                        Vector2 dustVel = Main.rand.NextVector2Circular(12f, 12f);
                        Dust dust = Dust.NewDustDirect(explosionCenter - new Vector2(30, 30), 60, 60, DustID.Torch, dustVel.X, dustVel.Y);
                        dust.noGravity = true;
                        dust.scale = Main.rand.NextFloat(2f, 3.5f);
                    }

                    // Smoke
                    for (int i = 0; i < 20; i++)
                    {
                        Vector2 dustVel = Main.rand.NextVector2Circular(8f, 8f);
                        Dust dust = Dust.NewDustDirect(explosionCenter - new Vector2(30, 30), 60, 60, DustID.Smoke, dustVel.X, dustVel.Y);
                        dust.scale = Main.rand.NextFloat(2f, 3f);
                    }

                    // Deal massive explosion damage, 75% bonus to primary target
                    float explosionRadius = 140f; // Much larger than ToughGlove's 80
                    int explosionDamage = (int)(damageDone * 0.75f);

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
                                Main.player[Projectile.owner].ApplyDamageToNPC(npc, explosionDamage, 0f, 0, false);
                            }
                        }
                    }

                    // Screen shake effect via camera
                    if (Main.myPlayer == Projectile.owner)
                    {
                        // Minor screen shake via player velocity nudge (reset next frame)
                    }

                    Projectile.Kill();
                }
            }
            else
            {
                // Small punches apply Hellfire instead of OnFire
                target.AddBuff(BuffID.OnFire3, 180); // 3 seconds of Hellfire

                // Incandescent punches (ai[1] == 1) also apply OnFire and increment combo
                if (Projectile.ai[1] == 1f)
                {
                    target.AddBuff(BuffID.OnFire, 180);

                    Player owner = Main.player[Projectile.owner];
                    if (owner.TryGetModPlayer(out IncandescentPlayer ip))
                    {
                        ip.OnPunchHitEnemy();
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.scale < 0.05f)
                return false;

            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            Rectangle sourceRect = new Rectangle(0, currentFrame * FRAME_HEIGHT, FRAME_WIDTH, FRAME_HEIGHT);

            Vector2 origin = new Vector2(FRAME_WIDTH / 2, FRAME_HEIGHT / 2);

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
            if (Projectile.scale > 0.1f)
            {
                for (int i = 0; i < 10; i++)
                {
                    Vector2 dustVel = Main.rand.NextVector2Circular(4f, 4f);
                    Dust dust = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Torch, dustVel.X, dustVel.Y);
                    dust.noGravity = true;
                    dust.scale = IsLargePunch ? 2.5f : 1.5f;
                }
            }
        }

        public override Color? GetAlpha(Color lightColor)
        {
            // Bright orange-red tint (more intense than ToughGlove)
            return new Color(255, 180, 120) * ((255 - Projectile.alpha) / 255f);
        }
    }
}
