using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class CookingPotCone : ModProjectile
    {
        public override string Texture => "Terraria/Images/MagicPixel";

        // ai[0] = cone half-angle in radians
        // ai[1] = cone range
        private float ConeHalfAngle => Projectile.ai[0];
        private float ConeRange => Projectile.ai[1];

        private bool hasHit = false;
        private HashSet<int> hitNPCs = new HashSet<int>();
        private bool spawnedPickup = false;

        public override void SetDefaults()
        {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 12; // Longer for liquid visual
            Projectile.DamageType = ModContent.GetInstance<RangedMeleeDamageClass>();
            Projectile.aiStyle = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI()
        {
            // Only process on the first frame
            if (!hasHit)
            {
                hasHit = true;
                ProcessCone();
            }

            // Spawn green particle cone each frame for visual effect
            SpawnConeParticles();
        }

        private void ProcessCone()
        {
            if (Main.myPlayer != Projectile.owner)
                return;

            Player owner = Main.player[Projectile.owner];
            var potPlayer = owner.GetModPlayer<CookingPotPlayer>();

            Vector2 coneDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float coneAngle = coneDir.ToRotation();
            float range = ConeRange > 0 ? ConeRange : 450f;
            float halfAngle = ConeHalfAngle > 0 ? ConeHalfAngle : MathHelper.ToRadians(30f);

            // Hit enemies in cone
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal)
                    continue;

                if (hitNPCs.Contains(i))
                    continue;

                Vector2 toNPC = npc.Center - owner.Center;
                float dist = toNPC.Length();

                if (dist > range)
                    continue;

                float angleToNPC = toNPC.ToRotation();
                float angleDiff = MathHelper.WrapAngle(angleToNPC - coneAngle);

                if (System.Math.Abs(angleDiff) <= halfAngle)
                {
                    hitNPCs.Add(i);

                    // Deal damage
                    int hitDir = (npc.Center.X > owner.Center.X) ? 1 : -1;
                    owner.ApplyDamageToNPC(npc, Projectile.damage, Projectile.knockBack, hitDir, false);

                    // Apply flask debuff
                    int debuffID = potPlayer.GetFlaskDebuffID();
                    if (debuffID > 0)
                    {
                        npc.AddBuff(debuffID, 180); // 3 seconds
                    }

                    // Spawn kindness pickup on the first enemy hit only
                    if (!spawnedPickup)
                    {
                        SpawnKindnessPickup(npc.Center);
                        spawnedPickup = true;
                    }

                    // Hit effect particles
                    for (int d = 0; d < 6; d++)
                    {
                        Vector2 dustVel = Main.rand.NextVector2Circular(4f, 4f);
                        Dust dust = Dust.NewDustDirect(npc.Center, 0, 0, DustID.GreenTorch, dustVel.X, dustVel.Y);
                        dust.noGravity = true;
                        dust.scale = 1.3f;
                    }
                }
            }

            // Heal allies in cone if food is slotted
            int healAmount = potPlayer.GetFoodHealAmount();
            int alcoholBuff = potPlayer.GetAlcoholBuffID();

            if (healAmount > 0 || alcoholBuff > 0)
            {
                // Heal players in cone
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player target = Main.player[i];
                    if (!target.active || target.dead)
                        continue;

                    // Must be on same team (or self)
                    if (i != Projectile.owner && (owner.team == 0 || target.team != owner.team))
                        continue;

                    // Skip self for healing (can still get alcohol buff)
                    bool isSelf = (i == Projectile.owner);

                    Vector2 toPlayer = target.Center - owner.Center;
                    float dist = toPlayer.Length();

                    if (dist > range && !isSelf)
                        continue;

                    float angleToPlayer = toPlayer.ToRotation();
                    float angleDiff = MathHelper.WrapAngle(angleToPlayer - coneAngle);

                    if (isSelf || System.Math.Abs(angleDiff) <= halfAngle)
                    {
                        // Heal ally
                        if (healAmount > 0 && !isSelf && target.statLife < target.statLifeMax2)
                        {
                            target.HealEffect(healAmount);
                            target.statLife += healAmount;
                            if (target.statLife > target.statLifeMax2)
                                target.statLife = target.statLifeMax2;
                        }

                        // Apply Calamity alcohol buff (1 second)
                        if (alcoholBuff > 0)
                        {
                            target.AddBuff(alcoholBuff, 60); // 1 second
                        }
                    }
                }

                // Heal town NPCs in cone (works with Stained Apron heal mirroring)
                if (healAmount > 0)
                {
                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        NPC npc = Main.npc[i];
                        if (!npc.active || !npc.friendly || !npc.townNPC)
                            continue;

                        Vector2 toNPC = npc.Center - owner.Center;
                        float dist = toNPC.Length();

                        if (dist > range)
                            continue;

                        float angleToNPC = toNPC.ToRotation();
                        float angleDiff = MathHelper.WrapAngle(angleToNPC - coneAngle);

                        if (System.Math.Abs(angleDiff) <= halfAngle)
                        {
                            if (npc.life < npc.lifeMax)
                            {
                                npc.life += healAmount;
                                if (npc.life > npc.lifeMax)
                                    npc.life = npc.lifeMax;
                                npc.HealEffect(healAmount);
                            }
                        }
                    }
                }
            }
        }

        private void SpawnKindnessPickup(Vector2 position)
        {
            if (Main.myPlayer != Projectile.owner)
                return;

            int pickup = Projectile.NewProjectile(
                Projectile.GetSource_FromAI(),
                position,
                new Vector2(Main.rand.NextFloat(-2f, 2f), -3f),
                ModContent.ProjectileType<KindnessPickup>(),
                0, 0f,
                Projectile.owner
            );

            if (pickup >= 0 && pickup < Main.maxProjectiles)
            {
                Main.projectile[pickup].netUpdate = true;
            }
        }

        private void SpawnConeParticles()
        {
            Player owner = Main.player[Projectile.owner];
            Vector2 coneDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float coneAngle = coneDir.ToRotation();
            // Cap visual range so particles don't extend beyond the liquid look
            float range = System.Math.Min(ConeRange > 0 ? ConeRange : 450f, 450f);
            float halfAngle = ConeHalfAngle > 0 ? ConeHalfAngle : MathHelper.ToRadians(30f);

            float lifeProgress = 1f - (Projectile.timeLeft / 12f);

            // Liquid pour: dense stream of droplets that arc outward with gravity
            int particleCount = 20;
            for (int i = 0; i < particleCount; i++)
            {
                float angle = coneAngle + Main.rand.NextFloat(-halfAngle, halfAngle);
                // Particles spawn closer at start, further as the pour continues
                float minDist = 20f + lifeProgress * 80f;
                float maxDist = range * (0.4f + lifeProgress * 0.6f);
                float dist = Main.rand.NextFloat(minDist, maxDist);
                Vector2 particlePos = owner.Center + angle.ToRotationVector2() * dist;

                // Liquid velocity: outward + downward (gravity-like droop)
                float outSpeed = Main.rand.NextFloat(4f, 10f);
                Vector2 vel = angle.ToRotationVector2() * outSpeed;
                vel.Y += Main.rand.NextFloat(1f, 3f); // Gravity droop makes it look like liquid

                // Alternate between thick green droplets and thin streaks
                if (Main.rand.NextBool(2))
                {
                    // Thick green droplet
                    Dust drop = Dust.NewDustDirect(particlePos, 0, 0, DustID.GreenTorch,
                        vel.X, vel.Y, 80, default, 1.4f + Main.rand.NextFloat(0.6f));
                    drop.noGravity = false; // Let gravity pull droplets down
                    drop.noLight = false;
                }
                else
                {
                    // Thin liquid streak
                    Dust streak = Dust.NewDustDirect(particlePos, 0, 0, DustID.GreenFairy,
                        vel.X * 1.2f, vel.Y * 1.2f, 60, default, 1.0f + Main.rand.NextFloat(0.4f));
                    streak.noGravity = false;
                    streak.fadeIn = 1.3f;
                }
            }

            // Splatter particles at the edges, bigger, slower, more chaotic
            for (int i = 0; i < 6; i++)
            {
                // Pick angles at the outer edges of the cone
                float edgeSign = Main.rand.NextBool() ? 1f : -1f;
                float angle = coneAngle + halfAngle * edgeSign * Main.rand.NextFloat(0.7f, 1.0f);
                float dist = Main.rand.NextFloat(range * 0.3f, range * 0.9f);
                Vector2 splatPos = owner.Center + angle.ToRotationVector2() * dist;

                Vector2 splatVel = Main.rand.NextVector2Circular(3f, 3f);
                splatVel.Y += 2f;

                Dust splat = Dust.NewDustDirect(splatPos, 0, 0, DustID.GreenTorch,
                    splatVel.X, splatVel.Y, 100, default, 1.8f + Main.rand.NextFloat(0.5f));
                splat.noGravity = false;
            }

            // Steam/mist particles near the source (coming from the pot)
            for (int i = 0; i < 4; i++)
            {
                float angle = coneAngle + Main.rand.NextFloat(-halfAngle * 0.5f, halfAngle * 0.5f);
                Vector2 steamPos = owner.Center + angle.ToRotationVector2() * Main.rand.NextFloat(15f, 50f);
                Vector2 steamVel = angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f);
                steamVel.Y -= 1f; // Steam rises

                Dust steam = Dust.NewDustDirect(steamPos, 0, 0, DustID.Cloud,
                    steamVel.X, steamVel.Y, 180, Color.LimeGreen, 0.8f + Main.rand.NextFloat(0.4f));
                steam.noGravity = true;
            }

            // Emit light along the cone
            for (int i = 0; i < 8; i++)
            {
                float angle = coneAngle + Main.rand.NextFloat(-halfAngle, halfAngle);
                float dist = range * (i + 1) / 9f;
                Vector2 lightPos = owner.Center + angle.ToRotationVector2() * dist;
                Lighting.AddLight(lightPos, 0.15f, 0.5f, 0.15f);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // No visible sprite, purely particle-based
            return false;
        }

        // This projectile shouldn't collide normally, we handle hits manually in ProcessCone
        public override bool? CanHitNPC(NPC target)
        {
            return false;
        }
    }
}
