using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;
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
using DeterministicChaos.Content.Items.Prefixes;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class AbstractLetter : ModProjectile
    {
        // Sprite sheet dimensions (same layout as TornNotebookLetter)
        private const int SPRITE_COLUMNS = 9;
        private const int SPRITE_ROWS = 3;
        private const int CELL_SIZE = 20;
        private const int TOTAL_LETTERS = 27;

        // AI slots
        // ai[0] = letter index for sprite
        // ai[1] = effect flags (LetterEffects enum)
        private int LetterIndex => (int)Projectile.ai[0];
        private LetterEffects Effects => (LetterEffects)(int)Projectile.ai[1];

        // Seeking behavior
        private int targetIndex = -1;
        private const float SEEK_STRENGTH = 0.18f;
        private const float MAX_SEEK_DISTANCE = 700f;

        // Aura orbit state
        private float orbitAngle = 0f;
        private const float ORBIT_RADIUS = 80f;
        private const float ORBIT_SPEED = 0.06f;

        // Initialization tracking
        private bool initialized = false;

        // Letter color variation (set once on spawn)
        private int colorVariant = -1;

        // Purple/pink/blue palette for non-HEAL letters
        private static readonly Color[] LetterPalette = new Color[]
        {
            new Color(180, 60, 255),   // bright purple
            new Color(140, 40, 220),   // deep purple
            new Color(255, 50, 160),   // hot pink
            new Color(230, 70, 200),   // magenta pink
            new Color(120, 80, 255),   // purplish blue
            new Color(100, 60, 230),   // indigo blue
            new Color(200, 80, 255),   // light purple
            new Color(255, 80, 220),   // rose pink
        };

        public override string Texture => "DeterministicChaos/Content/Projectiles/Friendly/TornNotebookLetter";

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 360;
            Projectile.DamageType = ModContent.GetInstance<MagicRogueDamageClass>();
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
            Projectile.netImportant = true;
        }

        public override void AI()
        {
            // Assign color variant on first frame
            if (colorVariant < 0)
                colorVariant = Projectile.whoAmI % LetterPalette.Length;

            // First-frame setup
            if (!initialized)
            {
                initialized = true;

                if ((Effects & LetterEffects.Pierce) != 0)
                {
                    Projectile.penetrate = -1;
                    Projectile.usesLocalNPCImmunity = true;
                    Projectile.localNPCHitCooldown = 10;
                }
                if ((Effects & LetterEffects.Burrow) != 0)
                {
                    Projectile.tileCollide = false;
                }
                if ((Effects & LetterEffects.Aura) != 0)
                {
                    Projectile.tileCollide = false;
                    Projectile.timeLeft = 720; // 12 seconds for aura letters
                    // Read the starting orbit angle stored by SpawnAuraLetter in localAI[0]
                    orbitAngle = Projectile.localAI[0];
                }
                if ((Effects & LetterEffects.Rain) != 0)
                {
                    Projectile.tileCollide = true; // Rain letters collide with tiles
                }
            }

            bool isAura = (Effects & LetterEffects.Aura) != 0;

            // Scale
            float scaleMult = Projectile.localAI[1] != 0 ? 0.25f : 1f;
            if ((Effects & LetterEffects.Big) != 0)
                Projectile.scale = 3.7f * scaleMult;
            else
                Projectile.scale = 2f * scaleMult;

            if (isAura)
            {
                // Aura letters handle their own seek suppression countdown
                if (Projectile.localAI[1] > 0)
                {
                    Projectile.localAI[1]--;
                    if (Projectile.localAI[1] <= 0)
                    {
                        Projectile.localAI[1] = -1f;
                        Projectile.friendly = true;
                    }
                }
                AuraAI();
            }
            else
            {
                NormalAI();
            }

            // Dust trail
            int dustType = GetDustType();
            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, dustType);
                dust.velocity *= 0.3f;
                dust.scale = 0.8f;
                dust.noGravity = true;
            }

            // Heal glow
            if ((Effects & LetterEffects.Heal) != 0 && Main.rand.NextBool(4))
            {
                Dust heal = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GreenFairy);
                heal.velocity *= 0.2f;
                heal.scale = 0.6f;
                heal.noGravity = true;
            }

            Vector3 lightColor = GetLightColor();
            Lighting.AddLight(Projectile.Center, lightColor);
        }

        private void AuraAI()
        {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            orbitAngle += ORBIT_SPEED;
            if (orbitAngle > MathHelper.TwoPi)
                orbitAngle -= MathHelper.TwoPi;

            float radius = ORBIT_RADIUS;
            if ((Effects & LetterEffects.Big) != 0)
                radius *= 1.4f;

            Vector2 targetPos = owner.Center + new Vector2(
                (float)System.Math.Cos(orbitAngle) * radius,
                (float)System.Math.Sin(orbitAngle) * radius
            );

            // Smoothly move toward orbit position
            Projectile.velocity = (targetPos - Projectile.Center) * 0.15f;
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.2f);
            Projectile.rotation = orbitAngle + MathHelper.PiOver2;
        }

        private void NormalAI()
        {
            bool hasHeal = (Effects & LetterEffects.Heal) != 0;
            bool hasSeek = (Effects & LetterEffects.Seek) != 0;

            // HEAL + SEEK: seek allies instead of enemies
            if (hasSeek && hasHeal && Projectile.localAI[1] <= 0)
            {
                SeekNearestAlly();
            }
            else if (hasSeek && !hasHeal && Projectile.localAI[1] <= 0)
            {
                SeekNearestEnemy();
            }

            // Count down seek suppression timer (for split projectiles)
            if (Projectile.localAI[1] > 0)
            {
                Projectile.localAI[1]--;
                if (Projectile.localAI[1] <= 0)
                {
                    Projectile.localAI[1] = -1f;
                    Projectile.friendly = true;
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        private void SeekNearestEnemy()
        {
            if (targetIndex < 0 || targetIndex >= Main.maxNPCs || !Main.npc[targetIndex].active || !Main.npc[targetIndex].CanBeChasedBy())
            {
                targetIndex = FindNearestEnemy();
            }

            float maxSpeed = (Effects & LetterEffects.Fast) != 0 ? 16f : 8f;

            if (targetIndex >= 0)
            {
                NPC target = Main.npc[targetIndex];
                Vector2 toTarget = target.Center - Projectile.Center;
                float distance = toTarget.Length();

                if (distance < MAX_SEEK_DISTANCE && distance > 0)
                {
                    toTarget.Normalize();
                    float homingStrength = SEEK_STRENGTH * (1f + (1f - distance / MAX_SEEK_DISTANCE));
                    Vector2 targetVelocity = toTarget * maxSpeed;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVelocity, homingStrength);

                    float currentSpeed = Projectile.velocity.Length();
                    if (currentSpeed < maxSpeed && currentSpeed > 0)
                    {
                        float newSpeed = MathHelper.Min(currentSpeed + 0.5f, maxSpeed);
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * newSpeed;
                    }
                }
            }
            else
            {
                Projectile.velocity.Y += 0.02f;
            }

            if (Projectile.velocity.Length() > maxSpeed)
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
        }

        private void SeekNearestAlly()
        {
            // Find nearest player (not self) who is missing health
            float maxSpeed = (Effects & LetterEffects.Fast) != 0 ? 16f : 8f;
            int closestPlayer = -1;
            float closestDist = MAX_SEEK_DISTANCE;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active || p.dead || i == Projectile.owner)
                    continue;
                if (p.statLife >= p.statLifeMax2)
                    continue;

                float dist = Vector2.Distance(Projectile.Center, p.Center);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestPlayer = i;
                }
            }

            // Also seek owner if they're missing health and no allies are closer
            Player owner = Main.player[Projectile.owner];
            if (owner.active && !owner.dead && owner.statLife < owner.statLifeMax2)
            {
                float ownerDist = Vector2.Distance(Projectile.Center, owner.Center);
                if (ownerDist < closestDist)
                {
                    closestDist = ownerDist;
                    closestPlayer = Projectile.owner;
                }
            }

            if (closestPlayer >= 0)
            {
                Player target = Main.player[closestPlayer];
                Vector2 toTarget = target.Center - Projectile.Center;
                float distance = toTarget.Length();

                if (distance < MAX_SEEK_DISTANCE && distance > 0)
                {
                    toTarget.Normalize();
                    float homingStrength = SEEK_STRENGTH * (1f + (1f - distance / MAX_SEEK_DISTANCE));
                    Vector2 targetVelocity = toTarget * maxSpeed;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVelocity, homingStrength);
                }
            }

            if (Projectile.velocity.Length() > maxSpeed)
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
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

        // HEAL: check if touching any player each frame
        public override bool? CanHitNPC(NPC target)
        {
            // Aura letters in orbit still hit enemies
            return null;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if ((Effects & LetterEffects.Boom) != 0)
                CreateExplosion();

            if ((Effects & LetterEffects.Pierce) != 0)
                Projectile.ai[1] = (float)((int)Projectile.ai[1] & ~(int)LetterEffects.Seek);

            if ((Effects & LetterEffects.Split) != 0)
                SpawnSplitProjectiles(target);

            // HEAL on enemy hit: heal the owner for 2 HP
            if ((Effects & LetterEffects.Heal) != 0)
            {
                Player owner = Main.player[Projectile.owner];
                if (owner.active && !owner.dead)
                {
                    int heal = owner.GetModPlayer<PrefixEffectPlayer>().ScaleHeal(2);
                    owner.Heal(heal);
                }
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if ((Effects & LetterEffects.Crit) != 0)
                modifiers.SetCrit();
        }

        // HEAL: check collision with players each frame for ally healing
        public override bool PreAI()
        {
            if ((Effects & LetterEffects.Heal) != 0)
            {
                CheckAllyHealing();
            }
            return true;
        }

        private void CheckAllyHealing()
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active || p.dead)
                    continue;

                // Don't heal owner unless SEEK+HEAL is actively seeking them
                // Always heal other players on contact
                if (i == Projectile.owner)
                    continue;

                if (Projectile.Hitbox.Intersects(p.Hitbox))
                {
                    Player owner = Main.player[Projectile.owner];
                    int heal = owner.GetModPlayer<PrefixEffectPlayer>().ScaleHeal(2);
                    p.Heal(heal);

                    // VFX
                    for (int d = 0; d < 8; d++)
                    {
                        Dust dust = Dust.NewDustDirect(p.Center, 0, 0, DustID.GreenFairy,
                            Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f));
                        dust.noGravity = true;
                        dust.scale = 1.2f;
                    }

                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.4f, Pitch = 0.5f }, p.Center);
                    Projectile.Kill();
                    return;
                }
            }
        }

        private void SpawnSplitProjectiles(NPC target)
        {
            if (Projectile.owner != Main.myPlayer)
                return;

            int inheritedEffects = (int)Projectile.ai[1] & ~(int)LetterEffects.Split;
            int splitDamage = Projectile.damage / 4;
            if (splitDamage < 1) splitDamage = 1;

            Vector2 awayFromEnemy = (Projectile.Center - target.Center).SafeNormalize(Vector2.UnitX);
            float randomOffset = Main.rand.NextFloat(-MathHelper.PiOver2, MathHelper.PiOver2);
            awayFromEnemy = awayFromEnemy.RotatedBy(randomOffset);

            for (int i = 0; i < 2; i++)
            {
                float angle = (i == 0 ? -1f : 1f) * MathHelper.PiOver4;
                Vector2 splitVel = awayFromEnemy.RotatedBy(angle) * (Projectile.velocity.Length() * 0.7f);

                int proj = Projectile.NewProjectile(
                    Projectile.GetSource_OnHit(target),
                    Projectile.Center,
                    splitVel,
                    Projectile.type,
                    splitDamage,
                    Projectile.knockBack * 0.5f,
                    Projectile.owner,
                    Projectile.ai[0],
                    (float)inheritedEffects
                );

                if (proj >= 0 && proj < Main.maxProjectiles)
                {
                    Main.projectile[proj].localAI[1] = 30f;
                    Main.projectile[proj].timeLeft = 120;
                    Main.projectile[proj].friendly = false;
                    Main.projectile[proj].netUpdate = true;
                }
            }
        }

        private void CreateExplosion()
        {
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
                        float damageMult = 1f - (dist / explosionRadius) * 0.5f;
                        int finalDamage = (int)(explosionDamage * damageMult);

                        Player owner = Main.player[Projectile.owner];
                        owner.ApplyDamageToNPC(npc, finalDamage, 0f, (npc.Center.X > Projectile.Center.X) ? 1 : -1, false);
                    }
                }
            }
        }

        private int GetDustType()
        {
            if ((Effects & LetterEffects.Heal) != 0) return DustID.GreenFairy;
            if ((Effects & LetterEffects.Boom) != 0) return DustID.Smoke;
            if ((Effects & LetterEffects.Crit) != 0) return DustID.GoldFlame;
            if ((Effects & LetterEffects.Aura) != 0) return DustID.PinkFairy;
            if ((Effects & LetterEffects.Rain) != 0) return DustID.PurpleTorch;
            return DustID.PinkFairy;
        }

        private Vector3 GetLightColor()
        {
            if ((Effects & LetterEffects.Heal) != 0) return new Vector3(0.3f, 0.8f, 0.2f);
            if ((Effects & LetterEffects.Boom) != 0) return new Vector3(1f, 0.3f, 0f);
            if ((Effects & LetterEffects.Crit) != 0) return new Vector3(1f, 1f, 0f);
            // Default purple/pink light for Abstract letters
            Color c = GetLetterTint();
            return new Vector3(c.R / 255f * 0.6f, c.G / 255f * 0.6f, c.B / 255f * 0.6f);
        }

        private Color GetLetterTint()
        {
            if ((Effects & LetterEffects.Heal) != 0)
                return new Color(120, 255, 80); // lime green

            int idx = colorVariant >= 0 ? colorVariant : 0;
            return LetterPalette[idx % LetterPalette.Length];
        }

        private Color GetGlowColor()
        {
            if ((Effects & LetterEffects.Heal) != 0) return new Color(80, 255, 100, 0);
            if ((Effects & LetterEffects.Boom) != 0) return new Color(255, 100, 50, 0);
            if ((Effects & LetterEffects.Crit) != 0) return new Color(255, 255, 100, 0);
            // Use the letter tint as the glow base
            Color tint = GetLetterTint();
            return new Color(tint.R, tint.G, tint.B, 0);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;

            int index = LetterIndex;
            if (index < 0 || index >= TOTAL_LETTERS)
                index = 0;

            int column = index % SPRITE_COLUMNS;
            int row = index / SPRITE_COLUMNS;

            Rectangle sourceRect = new Rectangle(
                column * CELL_SIZE,
                row * CELL_SIZE,
                CELL_SIZE,
                CELL_SIZE
            );

            Vector2 origin = new Vector2(CELL_SIZE / 2f, CELL_SIZE / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Color glowColor = GetGlowColor();

            SpriteEffects spriteEffect = Projectile.velocity.X >= 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // Glow
            for (int i = 0; i < 4; i++)
            {
                Vector2 offset = Main.rand.NextVector2Circular(2f, 2f);
                Main.EntitySpriteDraw(
                    texture,
                    drawPos + offset,
                    sourceRect,
                    glowColor * 0.5f,
                    0f,
                    origin,
                    Projectile.scale * 1.1f,
                    spriteEffect,
                    0
                );
            }

            // Main letter - tinted purple/pink/blue (lime green for HEAL)
            Color letterColor = GetLetterTint();
            Main.EntitySpriteDraw(
                texture,
                drawPos,
                sourceRect,
                letterColor,
                0f,
                origin,
                Projectile.scale,
                spriteEffect,
                0
            );

            return false;
        }

        public override void OnKill(int timeLeft)
        {
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
            writer.Write((int)Projectile.ai[0]);
            writer.Write((int)Projectile.ai[1]);
            writer.Write(targetIndex);
            writer.Write(orbitAngle);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            Projectile.ai[0] = reader.ReadInt32();
            Projectile.ai[1] = reader.ReadInt32();
            targetIndex = reader.ReadInt32();
            orbitAngle = reader.ReadSingle();
        }
    }
}
