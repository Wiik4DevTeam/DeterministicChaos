using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using DeterministicChaos.Content.NPCs.Bosses;
using Terraria.Audio;
using System;
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
    // Yellow orb that arcs upward then homes toward TitanBody.
    // On contact, increases the Titan's damage multiplier.
    // +0.1x normally, +0.02x if multiplier is already 3x or above.
    public class TitanMultiplierOrb : ModProjectile
    {
        // Use vanilla Falling Star as placeholder texture
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FallingStar;

        private const float RISE_DURATION = 40f;      // Ticks of upward arc
        private const float HOME_SPEED = 14f;          // Max homing speed
        private const float HOME_LERP = 0.06f;         // Homing turn rate
        private const float HIT_DISTANCE = 30f;        // Distance to trigger multiplier increase

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600; // 10 seconds max
            Projectile.light = 0.6f;
            Projectile.alpha = 50;
        }

        public override void AI()
        {
            // Yellow dust trail
            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.YellowTorch, Scale: 1.2f);
                d.noGravity = true;
                d.velocity *= 0.3f;
            }

            Projectile.rotation += 0.15f;

            // Phase 1: Rise upward with spread
            if (Projectile.ai[0] < RISE_DURATION)
            {
                Projectile.ai[0]++;
                Projectile.velocity.Y -= 0.15f;
                Projectile.velocity.X *= 0.98f;
                return;
            }

            // Phase 2: Home toward TitanBody
            int titanType = ModContent.NPCType<TitanBody>();
            int targetIndex = -1;
            float closestDist = float.MaxValue;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == titanType)
                {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        targetIndex = i;
                    }
                }
            }

            if (targetIndex >= 0)
            {
                NPC target = Main.npc[targetIndex];
                Vector2 direction = target.Center - Projectile.Center;
                if (direction.LengthSquared() > 0)
                {
                    direction.Normalize();
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * HOME_SPEED, HOME_LERP);
                }

                // Check if reached the Titan
                if (closestDist < HIT_DISTANCE)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient && target.ModNPC is TitanBody titan)
                    {
                        float increment = titan.DamageMultiplier >= 3f ? 0.02f : 0.1f;
                        titan.DamageMultiplier += increment;
                        target.netUpdate = true;

                        // Play hit sound with pitch increasing per multiplier
                        float pitch = MathHelper.Clamp((titan.DamageMultiplier - 1f) * 0.15f, 0f, 1f);
                        SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/TitanStarHit") { Pitch = pitch }, Projectile.Center);

                        // Shake the TitanStar
                        int starType = ModContent.NPCType<NPCs.Bosses.TitanStar>();
                        for (int s = 0; s < Main.maxNPCs; s++)
                        {
                            NPC n = Main.npc[s];
                            if (n.active && n.type == starType && (int)n.ai[0] == target.whoAmI
                                && n.ModNPC is NPCs.Bosses.TitanStar star)
                            {
                                star.TriggerShake();
                                break;
                            }
                        }
                    }

                    // Impact VFX
                    for (int d = 0; d < 20; d++)
                    {
                        Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.YellowTorch, Scale: 1.8f);
                        dust.noGravity = true;
                        dust.velocity = Main.rand.NextVector2Circular(5f, 5f);
                    }

                    Projectile.Kill();
                }
            }
            else
            {
                // No titan found, drift upward and fade
                Projectile.velocity.Y -= 0.03f;
                Projectile.alpha += 3;
                if (Projectile.alpha >= 255)
                    Projectile.Kill();
            }
        }

        public override Color? GetAlpha(Color lightColor)
        {
            return new Color(255, 220, 50, 100); // Bright yellow
        }

        // Spawns multiple multiplier orbs with spread, arcing upward then homing to TitanBody.
        // Call this from any enemy's OnKill to create the orb effect.
        public static void SpawnOrbs(IEntitySource source, Vector2 position, int count)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Main.rand.NextVector2Circular(16f, 16f);
                float angle = MathHelper.ToRadians(-90f + (i - count / 2f) * 20f);
                Vector2 velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * (3f + Main.rand.NextFloat(2f));

                Projectile.NewProjectile(
                    source,
                    position + offset,
                    velocity,
                    ModContent.ProjectileType<TitanMultiplierOrb>(),
                    0, 0f, Main.myPlayer
                );
            }
        }
    }
}
