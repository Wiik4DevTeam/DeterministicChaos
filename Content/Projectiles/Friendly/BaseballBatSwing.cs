using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class BaseballBatSwing : ModProjectile
    {
        private const int SwingDuration = 14;
        private const int LingerDuration = 4;
        private const int TotalDuration = SwingDuration + LingerDuration;
        private const float SwingArc = MathHelper.Pi * 0.9f;
        private const float BaseReach = 30f;
        
        private ref float SwingDirection => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];
        
        private float startAngle;
        private float aimAngle;
        private int playerDir;
        private bool initialized = false;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults()
        {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalDuration;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            if (!initialized)
            {
                initialized = true;
                Vector2 toMouse = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
                playerDir = toMouse.X >= 0 ? 1 : -1;
                player.direction = playerDir;
                
                aimAngle = toMouse.ToRotation();
                
                float actualSwingDir = SwingDirection;
                if (playerDir == -1)
                {
                    actualSwingDir = -SwingDirection;
                }
                
                startAngle = aimAngle - (SwingArc * 0.5f * actualSwingDir);
                
                SoundEngine.PlaySound(SoundID.Item1, player.Center);
            }

            player.direction = playerDir;

            float currentReach;
            float currentRotation;
            
            float actualSwingDirection = SwingDirection;
            if (playerDir == -1)
            {
                actualSwingDirection = -SwingDirection;
            }
            
            if (Timer < SwingDuration)
            {
                float swingProgress = Timer / SwingDuration;
                float easedProgress = EaseOutQuad(swingProgress);
                
                currentRotation = startAngle + (SwingArc * easedProgress * actualSwingDirection);
                currentReach = BaseReach;
                Projectile.scale = 1f;
            }
            else
            {
                float lingerProgress = (Timer - SwingDuration) / LingerDuration;
                float easedLinger = EaseInQuad(lingerProgress);
                
                float endSwingRotation = startAngle + (SwingArc * actualSwingDirection);
                currentRotation = endSwingRotation;
                currentReach = MathHelper.Lerp(BaseReach, BaseReach * 0.5f, easedLinger);
                Projectile.scale = MathHelper.Lerp(1f, 0.5f, easedLinger);
            }

            Projectile.Center = player.Center + new Vector2(currentReach, 0f).RotatedBy(currentRotation);
            Projectile.rotation = currentRotation + MathHelper.PiOver4;

            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentRotation - MathHelper.PiOver2);
            player.heldProj = Projectile.whoAmI;

            Timer++;
            if (Timer >= TotalDuration)
            {
                Projectile.Kill();
            }
        }

        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }
        
        private float EaseInQuad(float t)
        {
            return t * t;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/BatHit") { Volume = 0.8f }, target.Center);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active)
                return false;
            
            Vector2 batBase = player.Center;
            Vector2 batDirection = new Vector2(1f, 0f).RotatedBy(Projectile.rotation - MathHelper.PiOver4);
            Vector2 batTip = player.Center + batDirection * 80f;
            
            float collisionPoint = 0f;
            
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(),
                targetHitbox.Size(),
                batBase,
                batTip,
                30f * Projectile.scale,
                ref collisionPoint
            );
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active)
                return false;
                
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            if (texture == null)
                return false;
            
            Vector2 origin = new Vector2(0, texture.Height);
            
            SpriteEffects effects = playerDir == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float drawRotation = Projectile.rotation;
            if (playerDir == -1)
            {
                drawRotation += MathHelper.PiOver2;
                origin = new Vector2(texture.Width, texture.Height);
            }
            
            Main.EntitySpriteDraw(
                texture,
                player.Center - Main.screenPosition,
                null,
                lightColor,
                drawRotation,
                origin,
                Projectile.scale,
                effects,
                0
            );
            
            return false;
        }
    }
}
