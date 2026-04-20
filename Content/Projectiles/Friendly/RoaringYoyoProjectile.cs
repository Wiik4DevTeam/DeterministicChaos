using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
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

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class RoaringYoyoProjectile : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 12f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 280f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 14f;
            
            // Trail for afterimages
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(Projectile.localAI[1]);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            Projectile.localAI[1] = reader.ReadSingle();
        }

        public override void AI()
        {
            // Kill if too far from player
            if ((Projectile.position - Main.player[Projectile.owner].position).Length() > 3200f)
            {
                Projectile.Kill();
                return;
            }

            // Add light effect
            Lighting.AddLight(Projectile.Center, 0.5f, 0.4f, 0.2f);

            // Spawn stars periodically, faster rate (every 10 ticks instead of 20)
            Projectile.localAI[1]++;

            // Determine the imbued Static variant of the owning player (if any)
            ImbuedStaticVariant variant = ImbuedStaticVariant.None;
            var owner = Main.player[Projectile.owner];
            if (owner != null && owner.active)
            {
                var yp = owner.GetModPlayer<RoaringYoyoPlayer>();
                if (yp.isHoldingStatic)
                    variant = yp.imbuedStaticVariant;
            }

            if (Main.myPlayer != Projectile.owner)
                return;

            // Kindness: no stars; spawn a KindnessPickup every 30 ticks (~0.5s)
            if (variant == ImbuedStaticVariant.Kindness)
            {
                if (Projectile.localAI[1] % 30f == 0f)
                {
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), -3f);
                    int p = Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        vel,
                        ModContent.ProjectileType<KindnessPickup>(),
                        0,
                        0f,
                        Projectile.owner
                    );
                    if (p >= 0 && p < Main.maxProjectiles)
                        Main.projectile[p].netUpdate = true;
                }
                return;
            }

            // Justice: no stars; fire a guaranteed-hypercrit JusticeBeam at a random nearby enemy every 20 ticks
            if (variant == ImbuedStaticVariant.Justice)
            {
                if (Projectile.localAI[1] % 20f == 0f)
                {
                    NPC target = PickRandomNearbyEnemy(Projectile.Center, 700f);
                    if (target != null)
                    {
                        Vector2 startPos = Projectile.Center + Main.rand.NextVector2Circular(15f, 15f);
                        int beam = Projectile.NewProjectile(
                            Projectile.GetSource_FromThis(),
                            startPos,
                            Vector2.Zero,
                            ModContent.ProjectileType<JusticeBeam>(),
                            (int)(Projectile.damage * 0.6f),
                            Projectile.knockBack * 0.5f,
                            Projectile.owner,
                            target.whoAmI,
                            startPos.X
                        );
                        if (beam >= 0 && beam < Main.maxProjectiles)
                        {
                            Main.projectile[beam].localAI[0] = startPos.Y;
                            Main.projectile[beam].localAI[1] = 1f; // Hypercrit flag
                            Main.projectile[beam].netUpdate = true;
                        }
                    }
                }
                return;
            }

            // Patience: stars spawn less frequently (every 20 ticks)
            float spawnInterval = (variant == ImbuedStaticVariant.Patience) ? 20f : 10f;

            if (Projectile.localAI[1] % spawnInterval == 0f)
            {
                // Calculate direction based on timer (rotating pattern)
                int patternIndex = (int)(Projectile.localAI[1] / spawnInterval) % 8;

                Vector2 velocity = patternIndex switch
                {
                    0 => new Vector2(0f, -8f),
                    1 => new Vector2(5.6f, -5.6f),
                    2 => new Vector2(8f, 0f),
                    3 => new Vector2(5.6f, 5.6f),
                    4 => new Vector2(0f, 8f),
                    5 => new Vector2(-5.6f, 5.6f),
                    6 => new Vector2(-8f, 0f),
                    7 => new Vector2(-5.6f, -5.6f),
                    _ => new Vector2(0f, -8f)
                };

                // Add slight randomness
                Vector2 vel1 = velocity.RotatedByRandom(0.15f) * Main.rand.NextFloat(0.9f, 1.1f);
                Vector2 vel2 = (-velocity).RotatedByRandom(0.15f) * Main.rand.NextFloat(0.9f, 1.1f);

                // Play sound
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.4f, Pitch = 0.5f }, Projectile.Center);

                int starType = ModContent.ProjectileType<RoaringMiniStar>();
                int damage = (int)(Projectile.damage * 0.6f);
                float kb = Projectile.knockBack * 0.5f;

                SpawnMiniStar(starType, vel1, damage, kb, variant);
                SpawnMiniStar(starType, vel2, damage, kb, variant);

                // Reset pattern after full rotation
                if (patternIndex == 7)
                {
                    Projectile.localAI[1] = 0f;
                }
            }
        }

        private void SpawnMiniStar(int starType, Vector2 velocity, int damage, float knockBack, ImbuedStaticVariant variant)
        {
            int p = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                velocity,
                starType,
                damage,
                knockBack,
                Projectile.owner,
                (float)(int)variant
            );
            if (p < 0 || p >= Main.maxProjectiles)
                return;

            // Patience / Perseverance / Bravery: stars last longer
            if (variant == ImbuedStaticVariant.Patience
                || variant == ImbuedStaticVariant.Perseverance
                || variant == ImbuedStaticVariant.Bravery)
            {
                Main.projectile[p].timeLeft = 540;
            }
            Main.projectile[p].netUpdate = true;
        }

        private static NPC PickRandomNearbyEnemy(Vector2 center, float range)
        {
            float rangeSq = range * range;
            var candidates = new System.Collections.Generic.List<int>();
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (!n.active || n.friendly || n.dontTakeDamage || n.lifeMax <= 5 || n.CountsAsACritter)
                    continue;
                if (Vector2.DistanceSquared(n.Center, center) > rangeSq)
                    continue;
                candidates.Add(i);
            }
            if (candidates.Count == 0)
                return null;
            return Main.npc[candidates[Main.rand.Next(candidates.Count)]];
        }
        
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            
            // Fullbright golden color (multiplied by trait color when imbued)
            Color baseColor = new Color(255, 220, 150);
            Color traitTint = Color.White;
            Player owner = Main.player[Projectile.owner];
            if (owner != null && owner.active)
            {
                var yp = owner.GetModPlayer<RoaringYoyoPlayer>();
                if (yp.isHoldingStatic)
                    traitTint = ImbuedTraitColor.FromNoneFirst((int)yp.imbuedStaticVariant);
            }
            Color drawColor = ImbuedTraitColor.Multiply(baseColor, traitTint);
            
            // Draw afterimage trail
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                float t = i / (float)Projectile.oldPos.Length;
                float alpha = (1f - t) * 0.5f;
                float scale = Projectile.scale * (1f - t * 0.3f);
                
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                
                Main.spriteBatch.Draw(
                    tex,
                    pos,
                    null,
                    drawColor * alpha,
                    Projectile.oldRot[i],
                    origin,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
            
            // Draw main yoyo (fullbright, ignores lighting)
            Main.spriteBatch.Draw(
                tex,
                Projectile.Center - Main.screenPosition,
                null,
                drawColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );
            
            return false;
        }
    }
}
