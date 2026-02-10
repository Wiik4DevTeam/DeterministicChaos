using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class JusticeBeam : ModProjectile
    {
        private ref float TargetIndex => ref Projectile.ai[0];
        private ref float StartX => ref Projectile.ai[1];
        private ref float StartY => ref Projectile.localAI[0];
        
        private bool hasHit = false;
        private int fadeTimer = 0;
        private const int FadeDuration = 15; // Beam fades over 15 ticks
        private Vector2 targetPos;

        public override void SetDefaults()
        {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = ModContent.GetInstance<RangedSummonDamageClass>();
            Projectile.penetrate = -1; // Stays alive for visual fade
            Projectile.timeLeft = FadeDuration + 5;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.hide = true; // Don't draw normally, we draw custom
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // Only hit each NPC once ever
        }

        public override void AI()
        {
            if (!hasHit)
            {
                hasHit = true;
                
                // Get target
                int targetIdx = (int)TargetIndex;
                if (targetIdx >= 0 && targetIdx < Main.maxNPCs)
                {
                    NPC target = Main.npc[targetIdx];
                    if (target != null && target.active && !target.friendly)
                    {
                        targetPos = target.Center;
                        
                        // Move projectile to target for hit detection
                        Projectile.Center = target.Center;
                        
                        // Play sound
                        SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.8f }, targetPos);
                        
                        // Spawn dust along the line
                        Vector2 start = new Vector2(StartX, StartY);
                        float dist = Vector2.Distance(start, targetPos);
                        Vector2 dir = (targetPos - start).SafeNormalize(Vector2.Zero);
                        
                        for (float d = 0; d < dist; d += 8f)
                        {
                            Vector2 pos = start + dir * d;
                            Dust dust = Dust.NewDustPerfect(pos, DustID.YellowTorch, Vector2.Zero, 0, default, 1f);
                            dust.noGravity = true;
                            dust.velocity *= 0.1f;
                        }
                    }
                    else
                    {
                        targetPos = new Vector2(StartX, StartY) + Vector2.UnitX * 100f;
                        Projectile.Kill();
                        return;
                    }
                }
            }

            fadeTimer++;
            if (fadeTimer >= FadeDuration)
            {
                Projectile.Kill();
            }

            // Keep projectile at target
            Projectile.Center = targetPos;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Guaranteed critical hit
            modifiers.SetCrit();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Disable further damage after first hit
            Projectile.friendly = false;
            
            // Justice impact effect
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.5f, Volume = 0.8f }, target.Center);

            // Yellow burst
            for (int i = 0; i < 15; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                Dust dust = Dust.NewDustPerfect(target.Center, DustID.YellowTorch, vel, 0, default, 1.5f);
                dust.noGravity = true;
            }

            // Apply whip-style minion targeting (makes all summons attack this target)
            Player owner = Main.player[Projectile.owner];
            owner.MinionAttackTargetNPC = target.whoAmI;
            
            // Also apply the tag debuff for visual effect and bonus damage
            target.AddBuff(ModContent.BuffType<HollowGunTagDebuff>(), 300);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Don't draw if we haven't hit yet
            if (!hasHit)
                return false;

            Vector2 start = new Vector2(StartX, StartY);
            Vector2 end = targetPos;
            
            float alpha = 1f - (fadeTimer / (float)FadeDuration);
            
            // Draw the beam line
            DrawBeamLine(start, end, alpha);

            return false;
        }

        private void DrawBeamLine(Vector2 start, Vector2 end, float alpha)
        {
            // Use the JusticeBeam sprite (1 pixel tall, stretched to length)
            Texture2D tex = ModContent.Request<Texture2D>("DeterministicChaos/Content/Projectiles/Friendly/JusticeBeam").Value;
            if (tex == null)
                return;

            Vector2 diff = end - start;
            float length = diff.Length();
            float rotation = diff.ToRotation();

            // Add slight buffer to ensure beam reaches the target visually
            float visualLength = length + 10f;
            float scaleX = visualLength;
            
            // Width starts large and decreases over time (alpha goes from 1 to 0)
            float widthMultiplier = 15f + (alpha * 25f); // Starts at 40, ends at 15
            
            Vector2 drawStart = start - Main.screenPosition;
            Vector2 origin = new Vector2(0f, 0.5f);

            // Use a 1x1 source rectangle for consistent stretching
            Rectangle source = new Rectangle(0, 0, 1, 1);

            // Outer glow (scaled up Y)
            Main.EntitySpriteDraw(
                tex,
                drawStart,
                source,
                Color.Yellow * alpha * 0.3f,
                rotation,
                origin,
                new Vector2(scaleX, widthMultiplier * 1.5f),
                SpriteEffects.None,
                0
            );

            // Inner glow
            Main.EntitySpriteDraw(
                tex,
                drawStart,
                source,
                Color.Yellow * alpha * 0.6f,
                rotation,
                origin,
                new Vector2(scaleX, widthMultiplier),
                SpriteEffects.None,
                0
            );

            // Core
            Main.EntitySpriteDraw(
                tex,
                drawStart,
                source,
                Color.White * alpha,
                rotation,
                origin,
                new Vector2(scaleX, widthMultiplier * 0.6f),
                SpriteEffects.None,
                0
            );
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            // Draw over everything
            overPlayers.Add(index);
        }
    }
}
