using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class RoaringSwordHomingSlash : ModProjectile
    {
        private const float HomingStrength = 0.15f;
        private const float MaxSpeed = 18f;
        private const float ChainRadius = 500f;
        
        private ref float SlashAngle => ref Projectile.ai[0];
        private ref float TargetNPC => ref Projectile.ai[1];
        
        public override void SetDefaults()
        {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.alpha = 50;
        }

        public override void AI()
        {
            int targetIndex = (int)TargetNPC;
            NPC target = null;
            
            if (targetIndex >= 0 && targetIndex < Main.maxNPCs)
            {
                NPC potentialTarget = Main.npc[targetIndex];
                if (potentialTarget.active && !potentialTarget.friendly && !potentialTarget.dontTakeDamage)
                {
                    target = potentialTarget;
                }
            }
            
            if (target == null)
            {
                float closestDist = 600f;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal)
                        continue;
                        
                    if (npc.HasBuff(ModContent.BuffType<EyeDebuff>()))
                    {
                        float dist = Vector2.Distance(Projectile.Center, npc.Center);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            target = npc;
                            TargetNPC = i;
                        }
                    }
                }
            }
            
            if (target != null)
            {
                Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * MaxSpeed, HomingStrength);
            }
            
            Projectile.rotation = Projectile.velocity.ToRotation();
            SlashAngle = Projectile.rotation;
            
            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 100, default, 1.2f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
            
            Dust whiteDust = Dust.NewDustDirect(Projectile.Center, 1, 1, DustID.WhiteTorch, -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f, 0, Color.White, 0.8f);
            whiteDust.noGravity = true;
            
            Lighting.AddLight(Projectile.Center, 0.6f, 0.6f, 0.8f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            RoaringSwordMarkGlobalNPC markNPC = target.GetGlobalNPC<RoaringSwordMarkGlobalNPC>();
            bool hadMarks = markNPC.markStacks > 0;
            
            if (hadMarks)
            {
                for (int i = 0; i < 15; i++)
                {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                    Dust dust = Dust.NewDustPerfect(target.Center, DustID.Shadowflame, vel, 0, default, 1.5f);
                    dust.noGravity = true;
                }
                
                markNPC.ClearMarks(target);
                
                // Chain to next marked target
                if (Projectile.owner == Main.myPlayer)
                {
                    ChainToNextMarkedTarget(target);
                }
            }
            
            Player player = Main.player[Projectile.owner];
            player.itemAnimation = 0;
            player.itemTime = 0;
        }
        
        private void ChainToNextMarkedTarget(NPC hitTarget)
        {
            Player player = Main.player[Projectile.owner];
            
            // Find the next closest marked target
            float closestDist = ChainRadius;
            NPC nextTarget = null;
            
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal)
                    continue;
                
                // Skip the target we just hit
                if (npc.whoAmI == hitTarget.whoAmI)
                    continue;
                
                // Must have marks
                if (!npc.HasBuff(ModContent.BuffType<EyeDebuff>()))
                    continue;
                    
                float dist = Vector2.Distance(hitTarget.Center, npc.Center);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    nextTarget = npc;
                }
            }
            
            // Spawn a new homing slash toward the next target
            if (nextTarget != null)
            {
                Vector2 direction = (nextTarget.Center - hitTarget.Center).SafeNormalize(Vector2.UnitX);
                float angle = direction.ToRotation();
                
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    hitTarget.Center,
                    direction * 12f,
                    ModContent.ProjectileType<RoaringSwordHomingSlash>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner,
                    angle,
                    nextTarget.whoAmI
                );
                
                // Visual chain effect
                for (int i = 0; i < 8; i++)
                {
                    Vector2 vel = direction.RotatedByRandom(0.3f) * Main.rand.NextFloat(4f, 8f);
                    Dust dust = Dust.NewDustPerfect(hitTarget.Center, DustID.WhiteTorch, vel, 0, Color.White, 1.5f);
                    dust.noGravity = true;
                }
            }
        }
        
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            RoaringSwordMarkGlobalNPC markNPC = target.GetGlobalNPC<RoaringSwordMarkGlobalNPC>();
            if (markNPC.markStacks > 0)
            {
                float damageMultiplier = 1f + (markNPC.markStacks / (float)RoaringSwordMarkGlobalNPC.MaxStacks) * 4f;
                modifiers.SourceDamage *= damageMultiplier;
                
                if (markNPC.markStacks >= RoaringSwordMarkGlobalNPC.MaxStacks)
                {
                    modifiers.SetCrit();
                }
            }
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame, vel, 100, default, 1.2f);
                dust.noGravity = true;
            }
            
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() * 0.5f;
            
            for (int i = 0; i < 5; i++)
            {
                Vector2 drawPos = Projectile.Center - Projectile.velocity * (i * 0.5f) - Main.screenPosition;
                float alpha = 1f - (i * 0.2f);
                Color drawColor = Color.Lerp(Color.White, Color.Purple, i * 0.15f) * alpha * 0.6f;
                
                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    null,
                    drawColor,
                    Projectile.rotation + MathHelper.PiOver4,
                    origin,
                    Projectile.scale * (1f - i * 0.05f),
                    SpriteEffects.None,
                    0
                );
            }
            
            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                Color.White,
                Projectile.rotation + MathHelper.PiOver4,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );
            
            return false;
        }
    }
}
