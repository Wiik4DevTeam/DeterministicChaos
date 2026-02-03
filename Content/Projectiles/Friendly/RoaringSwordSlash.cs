using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;
using DeterministicChaos.Content.Items.Armor;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class RoaringSwordSlash : ModProjectile
    {
        private const int Frames = 4;
        private const int TotalLife = 20;
        private const int TicksPerFrame = 5;
        
        private const float WidthScale = 3.5f;
        private const float HeightScale = 2.5f;
        private const float MarkRadius = 1200f;
        
        private bool hasPlayedSound = false;
        private bool hasTriggeredMarkSpread = false;
        private bool hitAnyTarget = false;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = Frames;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;
        }

        public override void SetDefaults()
        {
            Projectile.width = 150;
            Projectile.height = 150;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            Projectile.localAI[0] = Projectile.Center.X;
            Projectile.localAI[1] = Projectile.Center.Y;
            Projectile.rotation = Projectile.ai[0];
            
            // Apply armor set bonus sword scale
            Player player = Main.player[Projectile.owner];
            if (player != null && player.active)
            {
                float armorBonus = player.GetModPlayer<RoaringArmorPlayer>().swordScaleBonus;
                Projectile.scale = 1f + armorBonus;
            }
        }

        public override void AI()
        {
            if (!hasPlayedSound && Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.8f, Pitch = 0.2f }, Projectile.Center);
                hasPlayedSound = true;
            }
            
            Projectile.velocity = Vector2.Zero;
            Projectile.Center = new Vector2(Projectile.localAI[0], Projectile.localAI[1]);

            Projectile.frameCounter++;
            if (Projectile.frameCounter >= TicksPerFrame)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= Frames)
                    Projectile.frame = Frames - 1;
            }

            Lighting.AddLight(Projectile.Center, 1.0f, 1.0f, 1.0f);

            if (Projectile.timeLeft <= 2)
            {
                // When slash ends, trigger chain lunge if we hit a fully marked target
                if (hasTriggeredMarkSpread && Projectile.owner == Main.myPlayer)
                {
                    TriggerChainLunge();
                }
                Projectile.Kill();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (target == null || !target.active)
                return;
                
            hitAnyTarget = true;
            
            RoaringSwordMarkGlobalNPC markNPC = target.GetGlobalNPC<RoaringSwordMarkGlobalNPC>();
            bool hadFullMarks = markNPC.markStacks >= RoaringSwordMarkGlobalNPC.MaxStacks;
            
            markNPC.ClearMarks(target);
            
            bool isWormSegment = target.realLife >= 0 && target.realLife != target.whoAmI;
            
            if (hadFullMarks && !hasTriggeredMarkSpread && !isWormSegment)
            {
                hasTriggeredMarkSpread = true;
                MarkNearbyEnemies(target);
            }
        }
        
        private void MarkNearbyEnemies(NPC sourceNPC)
        {
            if (sourceNPC == null || !sourceNPC.active)
                return;
            
            // Break sound effect for clearing full marks
            SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/Break") { Volume = 0.9f }, sourceNPC.Center);
            SpawnAuraEffect(sourceNPC.Center);
            
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal)
                    continue;
                
                if (npc.whoAmI == sourceNPC.whoAmI)
                    continue;
                
                if (npc.realLife >= 0 && npc.realLife != npc.whoAmI)
                    continue;
                    
                float dist = Vector2.Distance(sourceNPC.Center, npc.Center);
                if (dist <= MarkRadius)
                {
                    RoaringSwordMarkGlobalNPC markNPC = npc.GetGlobalNPC<RoaringSwordMarkGlobalNPC>();
                    markNPC.markStacks = RoaringSwordMarkGlobalNPC.MaxStacks;
                    npc.AddBuff(ModContent.BuffType<EyeDebuff>(), 360);
                    
                    // Play sound for full mark application
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.6f, Pitch = 0.8f }, npc.Center);
                    
                    for (int d = 0; d < 10; d++)
                    {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                        Dust dust = Dust.NewDustPerfect(npc.Center, DustID.WhiteTorch, vel, 0, Color.White, 1.2f);
                        dust.noGravity = true;
                    }
                }
            }
        }
        
        private void SpawnAuraEffect(Vector2 center)
        {
            for (int frame = 0; frame < 20; frame++)
            {
                float progress = frame / 20f;
                float radius = MarkRadius * progress;
                int dustCount = (int)(32 * (1f - progress * 0.5f));
                
                for (int i = 0; i < dustCount; i++)
                {
                    float angle = MathHelper.TwoPi * i / dustCount;
                    Vector2 dustPos = center + new Vector2(radius, 0f).RotatedBy(angle);
                    Dust dust = Dust.NewDustPerfect(dustPos, DustID.WhiteTorch, Vector2.Zero, 0, Color.White, 1.5f - progress);
                    dust.noGravity = true;
                    dust.noLight = false;
                }
            }
            
            for (int i = 0; i < 16; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
                Dust dust = Dust.NewDustPerfect(center, DustID.WhiteTorch, vel, 0, Color.White, 2f);
                dust.noGravity = true;
            }
        }
        
        private void TriggerChainLunge()
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active || player.dead)
                return;
            
            float closestDist = MarkRadius;
            NPC closestTarget = null;
            
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal)
                    continue;
                
                // Only target enemies with FULL mark stacks (5)
                RoaringSwordMarkGlobalNPC markNPC = npc.GetGlobalNPC<RoaringSwordMarkGlobalNPC>();
                if (markNPC.markStacks < RoaringSwordMarkGlobalNPC.MaxStacks)
                    continue;
                
                // Skip worm segments
                if (npc.realLife >= 0 && npc.realLife != npc.whoAmI)
                    continue;
                    
                float dist = Vector2.Distance(player.Center, npc.Center);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestTarget = npc;
                }
            }
            
            if (closestTarget != null && closestTarget.active)
            {
                // Reset player cooldown and spawn chain lunge
                player.itemAnimation = 0;
                player.itemTime = 0;
                
                Vector2 direction = (closestTarget.Center - player.Center).SafeNormalize(Vector2.UnitX);
                
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    player.Center,
                    direction,
                    ModContent.ProjectileType<RoaringSwordChainLunge>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner,
                    0f,
                    closestTarget.whoAmI
                );
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            RoaringSwordMarkGlobalNPC markNPC = target.GetGlobalNPC<RoaringSwordMarkGlobalNPC>();
            if (markNPC.markStacks > 0)
            {
                float damageMultiplier = 1f + (markNPC.markStacks / (float)RoaringSwordMarkGlobalNPC.MaxStacks) * 6f;
                modifiers.SourceDamage *= damageMultiplier;
                
                if (markNPC.markStacks >= RoaringSwordMarkGlobalNPC.MaxStacks)
                {
                    modifiers.SetCrit();
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            // Calculate the current scale based on lifetime
            float lifeT = (TotalLife - Projectile.timeLeft) / (float)TotalLife;
            float curHeightScale = MathHelper.Lerp(HeightScale, 0f, lifeT);
            
            if (curHeightScale < 0.1f)
                return false;

            // The slash is a long thin rectangle
            float actualWidth = 350f * WidthScale;
            float actualHeight = 30f * curHeightScale;

            Vector2 center = new Vector2(Projectile.localAI[0], Projectile.localAI[1]);
            
            // Get the direction vector from rotation
            Vector2 direction = new Vector2(1f, 0f).RotatedBy(Projectile.rotation);
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);

            // Convert target rectangle to center
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            Vector2 targetHalfSize = new Vector2(targetHitbox.Width * 0.5f, targetHitbox.Height * 0.5f);

            // Vector from slash center to target center
            Vector2 toTarget = targetCenter - center;

            // Project onto slash's local axes
            float alongSlash = Vector2.Dot(toTarget, direction);
            float perpToSlash = System.Math.Abs(Vector2.Dot(toTarget, perpendicular));

            float halfWidth = actualWidth * 0.5f;
            float halfHeight = actualHeight * 0.5f;
            float targetRadius = System.Math.Max(targetHalfSize.X, targetHalfSize.Y);

            bool withinWidth = System.Math.Abs(alongSlash) <= (halfWidth + targetRadius);
            bool withinHeight = perpToSlash <= (halfHeight + targetRadius);

            return withinWidth && withinHeight;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            if (tex == null)
                return false;

            int frameWidth = tex.Width;
            int frameHeight = tex.Height / Frames;

            Rectangle src = new Rectangle(0, Projectile.frame * frameHeight, frameWidth, frameHeight);
            Vector2 origin = new Vector2(frameWidth * 0.5f, frameHeight * 0.5f);

            Vector2 pos = new Vector2(Projectile.localAI[0], Projectile.localAI[1]) - Main.screenPosition;

            float lifeT = (TotalLife - Projectile.timeLeft) / (float)TotalLife;
            float curHeightScale = MathHelper.Lerp(HeightScale, 0f, lifeT);
            Vector2 drawScale = new Vector2(WidthScale * Projectile.scale, System.Math.Max(0.001f, curHeightScale * Projectile.scale));

            Vector2 direction = new Vector2(1f, 0f).RotatedBy(Projectile.rotation);
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
            
            Vector2 offset1 = -direction * 25f + perpendicular * 8f;
            Vector2 trailScale1 = drawScale * 0.85f;
            Main.EntitySpriteDraw(tex, pos + offset1, src, Color.White * 0.4f, Projectile.rotation, origin, trailScale1, SpriteEffects.None, 0);
            
            Vector2 offset2 = -direction * 20f - perpendicular * 6f;
            Vector2 trailScale2 = drawScale * 0.75f;
            Main.EntitySpriteDraw(tex, pos + offset2, src, Color.White * 0.3f, Projectile.rotation, origin, trailScale2, SpriteEffects.None, 0);
            
            Vector2 offset3 = -direction * 40f;
            Vector2 trailScale3 = drawScale * 0.6f;
            Main.EntitySpriteDraw(tex, pos + offset3, src, Color.White * 0.2f, Projectile.rotation, origin, trailScale3, SpriteEffects.None, 0);
            
            Vector2 offset4 = -direction * 35f + perpendicular * 12f;
            Vector2 trailScale4 = drawScale * 0.5f;
            Main.EntitySpriteDraw(tex, pos + offset4, src, Color.White * 0.15f, Projectile.rotation, origin, trailScale4, SpriteEffects.None, 0);

            Color drawColor = Color.White;
            Main.EntitySpriteDraw(tex, pos, src, drawColor, Projectile.rotation, origin, drawScale, SpriteEffects.None, 0);
            
            return false;
        }
    }
}
