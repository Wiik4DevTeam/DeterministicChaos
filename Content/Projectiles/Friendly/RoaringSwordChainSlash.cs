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
    public class RoaringSwordChainSlash : ModProjectile
    {
        private const int Frames = 4;
        private const int TotalLife = 15;
        private const int TicksPerFrame = 4;
        
        private const float WidthScale = 3.5f;
        private const float HeightScale = 2.5f;
        
        private bool hasPlayedSound = false;

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
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.7f, Pitch = 0.4f }, Projectile.Center);
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
                Projectile.Kill();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (target == null || !target.active)
                return;
                
            RoaringSwordMarkGlobalNPC markNPC = target.GetGlobalNPC<RoaringSwordMarkGlobalNPC>();
            markNPC.ClearMarks(target);
            
            // Mark consumption visual
            for (int i = 0; i < 12; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                Dust dust = Dust.NewDustPerfect(target.Center, DustID.WhiteTorch, vel, 0, Color.White, 1.5f);
                dust.noGravity = true;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (target == null)
                return;
                
            RoaringSwordMarkGlobalNPC markNPC = target.GetGlobalNPC<RoaringSwordMarkGlobalNPC>();
            if (markNPC.markStacks > 0)
            {
                float damageMultiplier = 1f + (markNPC.markStacks / (float)RoaringSwordMarkGlobalNPC.MaxStacks) * 3f;
                modifiers.SourceDamage *= damageMultiplier;
                
                if (markNPC.markStacks >= RoaringSwordMarkGlobalNPC.MaxStacks)
                {
                    modifiers.SetCrit();
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float lifeT = (TotalLife - Projectile.timeLeft) / (float)TotalLife;
            float curHeightScale = MathHelper.Lerp(HeightScale, 0f, lifeT);
            
            if (curHeightScale < 0.1f)
                return false;

            float actualWidth = 350f * WidthScale;
            float actualHeight = 30f * curHeightScale;

            Vector2 center = new Vector2(Projectile.localAI[0], Projectile.localAI[1]);
            Vector2 direction = new Vector2(1f, 0f).RotatedBy(Projectile.rotation);
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);

            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            Vector2 targetHalfSize = new Vector2(targetHitbox.Width * 0.5f, targetHitbox.Height * 0.5f);

            Vector2 toTarget = targetCenter - center;

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
            Texture2D tex = TextureAssets.Projectile[ModContent.ProjectileType<RoaringSwordSlash>()].Value;
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

            Main.EntitySpriteDraw(tex, pos, src, Color.White, Projectile.rotation, origin, drawScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
