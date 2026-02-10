using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class TornNotebookLetter : ModProjectile
    {
        // Sprite sheet dimensions
        private const int SPRITE_COLUMNS = 9;
        private const int SPRITE_ROWS = 3;
        private const int CELL_SIZE = 20;
        private const int TOTAL_LETTERS = 27; // A-Z + space

        // AI slots
        // ai[0] = letter index for sprite
        // ai[1] = effect flags (LetterEffects enum)
        private int LetterIndex => (int)Projectile.ai[0];
        private LetterEffects Effects => (LetterEffects)(int)Projectile.ai[1];

        // Seeking behavior
        private int targetNPC = -1;
        private const float SEEK_STRENGTH = 0.15f;
        private const float MAX_SEEK_DISTANCE = 600f;

        public override void SetStaticDefaults()
        {
            // No animation frames since we use spritesheet
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300; // 5 seconds
            Projectile.DamageType = ModContent.GetInstance<MagicRogueDamageClass>();
            Projectile.aiStyle = -1; // Custom AI
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1; // Smoother movement

            // Good multiplayer sync
            Projectile.netImportant = true;
        }

        public override void AI()
        {
            // Set penetrate based on Pierce effect (only on first frame)
            if (Projectile.localAI[0] == 0)
            {
                Projectile.localAI[0] = 1;
                if ((Effects & LetterEffects.Pierce) != 0)
                {
                    Projectile.penetrate = -1; // Infinite pierce
                    Projectile.usesLocalNPCImmunity = true;
                    Projectile.localNPCHitCooldown = 10;
                }
            }

            // Base scale is 2x, BIG effect makes it 3.7x
            if ((Effects & LetterEffects.Big) != 0)
            {
                Projectile.scale = 3.7f;
            }
            else
            {
                Projectile.scale = 2f;
            }

            // Seeking behavior (can be removed by piercing)
            if ((Effects & LetterEffects.Seek) != 0)
            {
                SeekNearestEnemy();
            }
            // No gravity - letters travel in a straight line

            // Rotate based on velocity
            Projectile.rotation = Projectile.velocity.ToRotation();

            // Dust trail
            int dustType = GetDustType();
            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, dustType);
                dust.velocity *= 0.3f;
                dust.scale = 0.8f;
                dust.noGravity = true;
            }

            // Lighting based on effects
            Vector3 lightColor = GetLightColor();
            Lighting.AddLight(Projectile.Center, lightColor);
        }

        private void SeekNearestEnemy()
        {
            // Find or validate target
            if (targetNPC < 0 || targetNPC >= Main.maxNPCs || !Main.npc[targetNPC].active || !Main.npc[targetNPC].CanBeChasedBy())
            {
                targetNPC = FindNearestEnemy();
            }

            // Determine max speed for this projectile
            float maxSpeed = (Effects & LetterEffects.Fast) != 0 ? 14f : 7f;

            if (targetNPC >= 0)
            {
                NPC target = Main.npc[targetNPC];
                Vector2 toTarget = target.Center - Projectile.Center;
                float distance = toTarget.Length();

                if (distance < MAX_SEEK_DISTANCE && distance > 0)
                {
                    toTarget.Normalize();
                    
                    // Stronger homing when closer
                    float homingStrength = SEEK_STRENGTH * (1f + (1f - distance / MAX_SEEK_DISTANCE));
                    
                    // Lerp direction toward target, always use max speed
                    Vector2 targetVelocity = toTarget * maxSpeed;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVelocity, homingStrength);
                    
                    // Accelerate to max speed if below it
                    float currentSpeed = Projectile.velocity.Length();
                    if (currentSpeed < maxSpeed && currentSpeed > 0)
                    {
                        float acceleration = 0.5f;
                        float newSpeed = MathHelper.Min(currentSpeed + acceleration, maxSpeed);
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * newSpeed;
                    }
                }
            }
            else
            {
                // No target, slight gravity
                Projectile.velocity.Y += 0.02f;
            }

            // Cap velocity
            if (Projectile.velocity.Length() > maxSpeed)
            {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
        }

        private int FindNearestEnemy()
        {
            float closestDist = MAX_SEEK_DISTANCE;
            int closest = -1;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy())
                {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = i;
                    }
                }
            }

            return closest;
        }

        private int GetDustType()
        {
            if ((Effects & LetterEffects.Fire) != 0) return DustID.Torch;
            if ((Effects & LetterEffects.Frostburn) != 0) return DustID.IceTorch;
            if ((Effects & LetterEffects.Poison) != 0) return DustID.Poisoned;
            if ((Effects & LetterEffects.Venom) != 0) return DustID.Venom;
            if ((Effects & LetterEffects.Ichor) != 0) return DustID.Ichor;
            if ((Effects & LetterEffects.Cursed) != 0) return DustID.CursedTorch;
            if ((Effects & LetterEffects.Shadowflame) != 0) return DustID.Shadowflame;
            if ((Effects & LetterEffects.Daybreak) != 0) return DustID.SolarFlare;
            if ((Effects & LetterEffects.Boom) != 0) return DustID.Smoke;
            if ((Effects & LetterEffects.Crit) != 0) return DustID.GoldFlame;
            return DustID.PurpleTorch;
        }

        private Vector3 GetLightColor()
        {
            if ((Effects & LetterEffects.Fire) != 0) return new Vector3(1f, 0.5f, 0f);
            if ((Effects & LetterEffects.Frostburn) != 0) return new Vector3(0f, 0.8f, 1f);
            if ((Effects & LetterEffects.Poison) != 0) return new Vector3(0f, 0.8f, 0f);
            if ((Effects & LetterEffects.Ichor) != 0) return new Vector3(1f, 0.8f, 0f);
            if ((Effects & LetterEffects.Cursed) != 0) return new Vector3(0.5f, 1f, 0f);
            if ((Effects & LetterEffects.Shadowflame) != 0) return new Vector3(0.5f, 0f, 0.8f);
            if ((Effects & LetterEffects.Daybreak) != 0) return new Vector3(1f, 0.6f, 0f);
            if ((Effects & LetterEffects.Boom) != 0) return new Vector3(1f, 0.3f, 0f);
            if ((Effects & LetterEffects.Crit) != 0) return new Vector3(1f, 1f, 0f);
            return new Vector3(0.5f, 0f, 0.5f);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // CRIT effect - guaranteed critical
            if ((Effects & LetterEffects.Crit) != 0)
            {
                modifiers.SetCrit();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Apply debuffs based on effects
            ApplyDebuffs(target);

            // BOOM effect - explosion
            if ((Effects & LetterEffects.Boom) != 0)
            {
                CreateExplosion();
            }

            // If piercing, lose seeking ability
            if ((Effects & LetterEffects.Pierce) != 0)
            {
                // Remove Seek from effects
                Projectile.ai[1] = (float)((int)Projectile.ai[1] & ~(int)LetterEffects.Seek);
            }
        }

        private void ApplyDebuffs(NPC target)
        {
            int duration = 180; // 3 seconds

            if ((Effects & LetterEffects.Fire) != 0)
                target.AddBuff(BuffID.OnFire, duration);

            if ((Effects & LetterEffects.Frostburn) != 0)
                target.AddBuff(BuffID.Frostburn, duration);

            if ((Effects & LetterEffects.Poison) != 0)
                target.AddBuff(BuffID.Poisoned, duration);

            if ((Effects & LetterEffects.Venom) != 0)
                target.AddBuff(BuffID.Venom, duration);

            if ((Effects & LetterEffects.Ichor) != 0)
                target.AddBuff(BuffID.Ichor, duration);

            if ((Effects & LetterEffects.Cursed) != 0)
                target.AddBuff(BuffID.CursedInferno, duration);

            if ((Effects & LetterEffects.Shadowflame) != 0)
                target.AddBuff(BuffID.ShadowFlame, duration);

            if ((Effects & LetterEffects.Daybreak) != 0)
                target.AddBuff(BuffID.Daybreak, duration);

            if ((Effects & LetterEffects.Betsy) != 0)
                target.AddBuff(BuffID.BetsysCurse, duration);
        }

        private void CreateExplosion()
        {
            // Visual explosion
            SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);

            for (int i = 0; i < 20; i++)
            {
                Dust dust = Dust.NewDustDirect(Projectile.Center, 1, 1, DustID.Smoke);
                dust.velocity = Main.rand.NextVector2Circular(6f, 6f);
                dust.scale = Main.rand.NextFloat(1f, 2f);
                dust.noGravity = true;

                Dust fireDust = Dust.NewDustDirect(Projectile.Center, 1, 1, DustID.Torch);
                fireDust.velocity = Main.rand.NextVector2Circular(8f, 8f);
                fireDust.scale = Main.rand.NextFloat(1f, 1.5f);
                fireDust.noGravity = true;
            }

            // Deal AOE damage
            float explosionRadius = 80f;
            int explosionDamage = Projectile.damage / 2;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && npc.CanBeChasedBy())
                {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < explosionRadius)
                    {
                        // Deal damage (scaled by distance)
                        float damageMult = 1f - (dist / explosionRadius) * 0.5f;
                        int finalDamage = (int)(explosionDamage * damageMult);

                        Player owner = Main.player[Projectile.owner];
                        owner.ApplyDamageToNPC(npc, finalDamage, 0f, (npc.Center.X > Projectile.Center.X) ? 1 : -1, false);
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Get our custom texture
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;

            // Calculate source rectangle from letter index
            int index = LetterIndex;
            if (index < 0 || index >= TOTAL_LETTERS)
                index = 0; // Default to 'A' if invalid

            int column = index % SPRITE_COLUMNS;
            int row = index / SPRITE_COLUMNS;

            Rectangle sourceRect = new Rectangle(
                column * CELL_SIZE,
                row * CELL_SIZE,
                CELL_SIZE,
                CELL_SIZE
            );

            // Draw the letter
            Vector2 origin = new Vector2(CELL_SIZE / 2f, CELL_SIZE / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // Glow color based on effects
            Color glowColor = GetGlowColor();

            // Flip horizontally when moving right
            SpriteEffects spriteEffect = Projectile.velocity.X >= 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // Add glow effect
            for (int i = 0; i < 4; i++)
            {
                Vector2 offset = Main.rand.NextVector2Circular(2f, 2f);
                Main.EntitySpriteDraw(
                    texture,
                    drawPos + offset,
                    sourceRect,
                    glowColor * 0.5f,
                    0f, // Don't rotate the letter itself
                    origin,
                    Projectile.scale * 1.1f,
                    spriteEffect,
                    0
                );
            }

            // Draw main letter (no rotation for readability)
            Main.EntitySpriteDraw(
                texture,
                drawPos,
                sourceRect,
                Color.White,
                0f,
                origin,
                Projectile.scale,
                spriteEffect,
                0
            );

            return false; // We handled drawing
        }

        private Color GetGlowColor()
        {
            if ((Effects & LetterEffects.Fire) != 0) return new Color(255, 150, 50, 0);
            if ((Effects & LetterEffects.Frostburn) != 0) return new Color(100, 200, 255, 0);
            if ((Effects & LetterEffects.Poison) != 0) return new Color(100, 255, 100, 0);
            if ((Effects & LetterEffects.Venom) != 0) return new Color(150, 50, 200, 0);
            if ((Effects & LetterEffects.Ichor) != 0) return new Color(255, 200, 50, 0);
            if ((Effects & LetterEffects.Cursed) != 0) return new Color(150, 255, 50, 0);
            if ((Effects & LetterEffects.Shadowflame) != 0) return new Color(150, 50, 200, 0);
            if ((Effects & LetterEffects.Daybreak) != 0) return new Color(255, 180, 50, 0);
            if ((Effects & LetterEffects.Boom) != 0) return new Color(255, 100, 50, 0);
            if ((Effects & LetterEffects.Crit) != 0) return new Color(255, 255, 100, 0);
            if ((Effects & LetterEffects.Pierce) != 0) return new Color(180, 180, 255, 0);
            if ((Effects & LetterEffects.Seek) != 0) return new Color(100, 255, 200, 0);
            if ((Effects & LetterEffects.Fast) != 0) return new Color(200, 200, 255, 0);
            if ((Effects & LetterEffects.Big) != 0) return new Color(255, 200, 200, 0);
            return new Color(200, 100, 255, 0);
        }

        public override void OnKill(int timeLeft)
        {
            // Particles on death
            int dustType = GetDustType();
            for (int i = 0; i < 10; i++)
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, dustType);
                dust.velocity = Main.rand.NextVector2Circular(3f, 3f);
                dust.scale = Main.rand.NextFloat(0.8f, 1.2f);
                dust.noGravity = true;
            }
        }

        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.Write((int)Projectile.ai[0]); // Letter index
            writer.Write((int)Projectile.ai[1]); // Effect flags
            writer.Write(targetNPC);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            Projectile.ai[0] = reader.ReadInt32();
            Projectile.ai[1] = reader.ReadInt32();
            targetNPC = reader.ReadInt32();
        }
    }
}
