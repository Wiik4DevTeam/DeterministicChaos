using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
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
using DeterministicChaos.Content.Items.Imbued;
using DeterministicChaos.Content.Items.Prefixes;
using DeterministicChaos.Content.SoulTraits.Armor;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class RoaringTomeBigProjectile : ModProjectile
    {
        // Track absorbed stars
        public int absorbedStars = 0;
        public int bonusDamage = 10;
        private bool hasExploded = false;

        // Kindness: tracked ally index
        private int kindnessAllyIndex = -1;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            Main.projFrames[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.aiStyle = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
        }

        public void AbsorbStar(int starDamage)
        {
            absorbedStars++;
            bonusDamage += 1;

            // Check variant for reduced contribution
            Player owner = Main.player[Projectile.owner];
            var tomePlayer = owner.GetModPlayer<RoaringTomePlayer>();
            float scaleGain = 0.15f;
            int damageGain = starDamage;

            // Perseverance: 30% less contribution to orb size and damage
            if (tomePlayer.imbuedClarityVariant == ImbuedClarityVariant.Perseverance)
            {
                scaleGain *= 0.7f;
                damageGain = (int)(damageGain * 0.7f);
            }

            // Increase size
            Projectile.scale += scaleGain;
            if (Projectile.scale > 6f)
                Projectile.scale = 6f;

            // Increase speed slightly, ensure we maintain direction
            float currentSpeed = Projectile.velocity.Length();
            if (currentSpeed < 0.1f)
            {
                // If nearly stopped, give it a push in a random direction
                Projectile.velocity = Main.rand.NextVector2Unit() * 3f;
            }
            else
            {
                float newSpeed = currentSpeed + 0.5f;
                if (newSpeed > 12f)
                    newSpeed = 12f;
                Projectile.velocity = Vector2.Normalize(Projectile.velocity) * newSpeed;
            }

            // Update damage
            Projectile.damage += damageGain;
            if(absorbedStars < 8)
            {
                Projectile.damage += (int)(Projectile.damage * 0.07);
            }

            // Kindness: heal tracked ally on each absorption
            if (tomePlayer.imbuedClarityVariant == ImbuedClarityVariant.Kindness && kindnessAllyIndex >= 0 && kindnessAllyIndex < Main.maxPlayers)
            {
                Player ally = Main.player[kindnessAllyIndex];
                if (ally.active && !ally.dead && ally.statLife < ally.statLifeMax2)
                {
                    int healAmount = 2;

                    var prefixPlayer = owner.GetModPlayer<PrefixEffectPlayer>();
                    healAmount = prefixPlayer.ScaleHeal(healAmount);

                    var emblemPlayer = owner.GetModPlayer<ImbuedEmblemPlayer>();
                    if (emblemPlayer.hasKindnessEmblem)
                        healAmount = (int)(healAmount * 1.25f);

                    if (healAmount > 0)
                    {
                        ally.statLife = Math.Min(ally.statLife + healAmount, ally.statLifeMax2);
                        ally.HealEffect(healAmount);
                    }

                    RoaringGunPlayer.NotifyAllyHealed(Projectile.owner);
                }
            }

            // Update hitbox size based on scale
            int newSize = (int)(32 * Projectile.scale);
            Projectile.Resize(newSize, newSize);

            // Sound effect
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = 0.5f + absorbedStars * 0.05f }, Projectile.Center);
        }

        public override void AI()
        {
            // static rotation
            Projectile.rotation = 0f;

            Player owner = Main.player[Projectile.owner];
            var tomePlayer = owner.GetModPlayer<RoaringTomePlayer>();
            var variant = tomePlayer.imbuedClarityVariant;

            // Kindness/Bravery: no tile collision
            if (variant == ImbuedClarityVariant.Kindness || variant == ImbuedClarityVariant.Bravery)
                Projectile.tileCollide = false;
            else
                Projectile.tileCollide = true;

            // Frame animation, faster based on speed
            float speed = Projectile.velocity.Length();
            int frameSpeed = System.Math.Max(2, 12 - (int)(speed * 0.8f));
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= frameSpeed)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= Main.projFrames[Projectile.type])
                    Projectile.frame = 0;
            }

            // Aggressively seek nearby stars from very far away
            float seekRadius = 1200f;
            Projectile nearestStar = null;
            float nearestDist = seekRadius;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile other = Main.projectile[i];
                if (other.active && other.owner == Projectile.owner && other.type == ModContent.ProjectileType<RoaringTomeStarProjectile>())
                {
                    float distance = Vector2.Distance(Projectile.Center, other.Center);
                    if (distance < nearestDist)
                    {
                        nearestDist = distance;
                        nearestStar = other;
                    }
                }
            }

            // Steer toward nearest star
            if (nearestStar != null)
            {
                Vector2 direction = (nearestStar.Center - Projectile.Center);
                if (direction.Length() > 0)
                {
                    direction.Normalize();
                    float currentSpeed = System.Math.Max(Projectile.velocity.Length(), 3f);
                    Vector2 desiredVelocity = direction * currentSpeed;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.3f);
                }
            }

            float enemySeekRadius = 2600f;

            // Bravery: always follow the player instead of enemies
            if (variant == ImbuedClarityVariant.Bravery)
            {
                Vector2 direction = (owner.Center - Projectile.Center);
                if (direction.Length() > 40f)
                {
                    direction.Normalize();
                    float turnStr = 0.18f + (Projectile.scale - 1f) * 0.50f;
                    float currentSpeed = System.Math.Max(Projectile.velocity.Length(), 3f);
                    Vector2 desiredVelocity = direction * currentSpeed;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, turnStr);
                }
            }
            // Kindness: track to one nearby ally; fall back to the owner if no ally is around
            else if (variant == ImbuedClarityVariant.Kindness)
            {
                // Pick ally if none tracked
                if (kindnessAllyIndex < 0 || kindnessAllyIndex >= Main.maxPlayers ||
                    !Main.player[kindnessAllyIndex].active || Main.player[kindnessAllyIndex].dead)
                {
                    kindnessAllyIndex = -1;
                    float closestDist = 2600f;
                    for (int i = 0; i < Main.maxPlayers; i++)
                    {
                        if (i == Projectile.owner) continue;
                        Player ally = Main.player[i];
                        if (!ally.active || ally.dead) continue;
                        if (ally.team != owner.team || owner.team == 0) continue;

                        float dist = Vector2.Distance(Projectile.Center, ally.Center);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            kindnessAllyIndex = i;
                        }
                    }
                }

                // Steer toward tracked ally, or fall back to the owner
                Player followTarget = (kindnessAllyIndex >= 0) ? Main.player[kindnessAllyIndex] : owner;
                Vector2 direction = (followTarget.Center - Projectile.Center);
                if (direction.Length() > 40f)
                {
                    direction.Normalize();
                    float turnStr = 0.18f + (Projectile.scale - 1f) * 0.50f;
                    float currentSpeed = System.Math.Max(Projectile.velocity.Length(), 3f);
                    Vector2 desiredVelocity = direction * currentSpeed;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, turnStr);
                }
            }
            else
            {
                // Default enemy seeking
                NPC nearestEnemy = null;
                float nearestEnemyDist = enemySeekRadius;

                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.active && !npc.friendly && !npc.dontTakeDamage && npc.CanBeChasedBy())
                    {
                        float distance = Vector2.Distance(Projectile.Center, npc.Center);
                        if (distance < nearestEnemyDist)
                        {
                            nearestEnemyDist = distance;
                            nearestEnemy = npc;
                        }
                    }
                }

                if (nearestEnemy != null)
                {
                    Vector2 direction = (nearestEnemy.Center - Projectile.Center);
                    if (direction.Length() > 0)
                    {
                        direction.Normalize();
                        float enemyTurnStrength = 0.18f + (Projectile.scale - 1f) * 0.50f;
                        float currentSpeed = System.Math.Max(Projectile.velocity.Length(), 3f);
                        Vector2 desiredVelocity = direction * currentSpeed;
                        Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, enemyTurnStrength);
                    }
                }
            }

            // Integrity: grant 10% DR to allies near the orb
            if (variant == ImbuedClarityVariant.Integrity)
            {
                float drRange = 300f;
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    if (i == Projectile.owner) continue;
                    Player ally = Main.player[i];
                    if (!ally.active || ally.dead) continue;
                    if (ally.team != owner.team || owner.team == 0) continue;

                    if (Vector2.Distance(Projectile.Center, ally.Center) < drRange)
                        ally.endurance += 0.10f;
                }
                // Also grant to owner
                if (Vector2.Distance(Projectile.Center, owner.Center) < drRange)
                    owner.endurance += 0.10f;
            }

            // Pulsing effect, faster based on size and speed
            float pulseSpeed = 0.1f + Projectile.scale * 0.05f + speed * 0.02f;
            float pulse = 1f + 0.1f * (float)System.Math.Sin(Projectile.ai[0] * pulseSpeed);
            Projectile.ai[0]++;

            // Dust effects, more dust when bigger
            int dustFrequency = System.Math.Max(1, 5 - absorbedStars);
            if (Main.rand.NextBool(dustFrequency))
            {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(Projectile.width * 0.4f, Projectile.height * 0.4f);
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.Shadowflame, 0f, 0f, 100, default, 1f + Projectile.scale * 0.3f);
                dust.noGravity = true;
                dust.velocity = (dustPos - Projectile.Center).SafeNormalize(Vector2.Zero) * 2f;
            }

            // Orbiting particles when stars absorbed
            if (absorbedStars > 0)
            {
                for (int i = 0; i < absorbedStars && i < 10; i++)
                {
                    float angle = Projectile.ai[0] * 0.05f + i * MathHelper.TwoPi / System.Math.Min(absorbedStars, 10);
                    float orbitRadius = Projectile.width * 0.6f;
                    Vector2 orbitPos = Projectile.Center + new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle)) * orbitRadius;

                    if (Main.rand.NextBool(3))
                    {
                        Dust dust = Dust.NewDustDirect(orbitPos, 1, 1, DustID.PurpleTorch, 0f, 0f, 100, default, 0.6f);
                        dust.noGravity = true;
                        dust.velocity = Vector2.Zero;
                    }
                }
            }

            // Light based on size
            float lightIntensity = 0.5f + Projectile.scale * 0.2f;
            Lighting.AddLight(Projectile.Center, lightIntensity * 0.5f, lightIntensity * 0.2f, lightIntensity * 0.6f);

            // Hard speed cap — prevent the orb from moving so fast it clips through the world.
            const float MaxSpeed = 16f;
            float curSpeed = Projectile.velocity.Length();
            if (curSpeed > MaxSpeed)
                Projectile.velocity = Projectile.velocity * (MaxSpeed / curSpeed);
        }

        // Justice: guaranteed crit + hypercrit chance conversion
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Direct contact damage is suppressed; the explosion (in OnHitNPC) is the sole damage source.
            // This prevents the same target from being hit twice (contact + AoE).
            modifiers.FinalDamage *= 0f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Explode on hit (explosion handles all damage, crits, and Justice hypercrit)
            Explode();
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            // Explode on tile collision
            Explode();
            return true;
        }

        public override void OnKill(int timeLeft)
        {
            // Explode on timeout if we haven't already
            if (!hasExploded)
            {
                Explode();
            }
        }

        private void Explode()
        {
            if (hasExploded) return;
            hasExploded = true;
            // Explosion radius based on scale
            float explosionRadius = 80f * Projectile.scale;

            // Sound
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f + Projectile.scale * 0.2f, Pitch = -0.3f - Projectile.scale * 0.1f }, Projectile.Center);

            // Multiplayer safety check, ensure owner index is valid
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
            {
                CreateExplosionVisuals(explosionRadius);
                return;
            }
            
            // Deal area damage
            Player player = Main.player[Projectile.owner];
            
            // Multiplayer safety check, ensure player reference is valid
            if (player == null || !player.active)
            {
                CreateExplosionVisuals(explosionRadius);
                return;
            }
            
            int explosionDamage = Projectile.damage;

            // Check for Justice variant
            var tomePlayer = player.GetModPlayer<RoaringTomePlayer>();
            bool isJustice = tomePlayer.imbuedClarityVariant == ImbuedClarityVariant.Justice;
            bool isPatience = tomePlayer.imbuedClarityVariant == ImbuedClarityVariant.Patience;
            int totalDamageDealt = 0;
            bool didJusticeHypercrit = false;

            // Only the owner deals damage to prevent duplicates in multiplayer
            if (Projectile.owner == Main.myPlayer)
            {
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.active && !npc.friendly && !npc.dontTakeDamage)
                    {
                        float distance = Vector2.Distance(Projectile.Center, npc.Center);
                        if (distance < explosionRadius)
                        {
                            // Damage falloff
                            float damageMult = 1f - (distance / explosionRadius) * 0.5f;
                            int finalDamage = (int)(explosionDamage * damageMult);

                            bool isCrit;
                            if (isJustice)
                            {
                                isCrit = true;
                                // Roll hypercrit
                                float hypercritChance = player.GetTotalCritChance(DamageClass.Magic);
                                if (Main.rand.Next(100) < (int)hypercritChance)
                                {
                                    finalDamage = (int)(finalDamage * 1.5f);
                                    didJusticeHypercrit = true;
                                }
                            }
                            else
                            {
                                isCrit = Main.rand.Next(100) < player.GetCritChance(DamageClass.Magic);
                            }

                            NPC.HitInfo hitInfo = new NPC.HitInfo
                            {
                                Damage = finalDamage,
                                Knockback = 8f * Projectile.scale,
                                HitDirection = Projectile.Center.X < npc.Center.X ? 1 : -1,
                                Crit = isCrit
                            };
                            npc.StrikeNPC(hitInfo);
                            totalDamageDealt += finalDamage;

                            // Sync the hit to other clients in multiplayer
                            if (Main.netMode != NetmodeID.SinglePlayer)
                            {
                                NetMessage.SendStrikeNPC(npc, hitInfo);
                            }
                        }
                    }
                }

                // Justice hypercrit VFX/SFX
                if (didJusticeHypercrit)
                {
                    for (int i = 0; i < 25; i++)
                    {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
                        Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.YellowTorch, vel, 0, default, 2f);
                        dust.noGravity = true;
                    }
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/Hypercrit") { Volume = 0.6f }, Projectile.Center);

                    var hatPlayer = player.GetModPlayer<CowboyHatPlayer>();
                    if (hatPlayer.hasSheriffHat)
                        hatPlayer.hypercritAttackSpeedTimer = 36;
                }

                // Patience: grant rogue stealth based on damage dealt by the explosion
                if (isPatience && totalDamageDealt > 0)
                {
                    tomePlayer.GrantPatienceStealth(totalDamageDealt);
                }
            }

            // Release absorbed stars as projectiles, only spawn for the owner
            if (absorbedStars > 0 && Projectile.owner == Main.myPlayer)
            {
                int starDamage = bonusDamage;
                
                for (int i = 0; i < absorbedStars; i++)
                {
                    float angle = i * MathHelper.TwoPi / absorbedStars;
                    Vector2 starVelocity = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle)) * 10f;
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        starVelocity,
                        ModContent.ProjectileType<RoaringTomeStarProjectile>(),
                        starDamage,
                        2f,
                        Projectile.owner
                    );
                }
            }
            
            CreateExplosionVisuals(explosionRadius);
        }
        
        private void CreateExplosionVisuals(float explosionRadius)
        {
            // Visual effects
            int dustAmount = (int)(40 * Projectile.scale);
            for (int i = 0; i < dustAmount; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(10f * Projectile.scale, 10f * Projectile.scale);
                Dust dust = Dust.NewDustDirect(Projectile.Center, 1, 1, DustID.Shadowflame, velocity.X, velocity.Y, 100, default, 2f * Projectile.scale);
                dust.noGravity = true;
            }

            // Purple smoke
            for (int i = 0; i < dustAmount / 2; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(6f * Projectile.scale, 6f * Projectile.scale);
                Dust dust = Dust.NewDustDirect(Projectile.Center, 1, 1, DustID.Smoke, velocity.X, velocity.Y, 150, Color.Purple, 2.5f * Projectile.scale);
                dust.noGravity = true;
            }

            // Star burst effect
            for (int i = 0; i < 8 + absorbedStars * 2; i++)
            {
                float angle = i * MathHelper.TwoPi / (8 + absorbedStars * 2);
                Vector2 velocity = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle)) * (5f + Projectile.scale * 2f);
                Dust dust = Dust.NewDustDirect(Projectile.Center, 1, 1, DustID.PurpleTorch, velocity.X, velocity.Y, 0, default, 1.5f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            Rectangle frameRect = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = new Vector2(texture.Width * 0.5f, frameHeight * 0.5f);

            // Imbued Clarity trait tint
            Color traitTint = Color.White;
            Player owner = Main.player[Projectile.owner];
            if (owner != null && owner.active)
            {
                var tp = owner.GetModPlayer<RoaringTomePlayer>();
                if (tp.isHoldingClarity)
                    traitTint = ImbuedTraitColor.FromNoneFirst((int)tp.imbuedClarityVariant);
            }

            // Draw proper afterimages, offset to sprite center (66x66 frames)
            // since Resize() keeps center consistent but changes Projectile.Size
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                
                Vector2 drawPos = Projectile.oldPos[i] + new Vector2(33, 33) - Main.screenPosition;
                float progress = i / (float)Projectile.oldPos.Length;
                float trailScale = Projectile.scale * (1f - progress * 0.5f);
                Color trailColor = ImbuedTraitColor.Multiply(Color.White, traitTint) * (1f - progress) * 0.6f;
                Main.EntitySpriteDraw(texture, drawPos, frameRect, trailColor, Projectile.oldRot[i], origin, trailScale, SpriteEffects.None, 0);
            }

            // Draw main sprite with same offset
            Vector2 mainDrawPos = Projectile.position + new Vector2(33, 33) - Main.screenPosition;
            Main.EntitySpriteDraw(texture, mainDrawPos, frameRect, traitTint, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }

        public override void PostDraw(Color lightColor)
        {
        }

        public override Color? GetAlpha(Color lightColor)
        {
            return Color.White;
        }
    }
}
