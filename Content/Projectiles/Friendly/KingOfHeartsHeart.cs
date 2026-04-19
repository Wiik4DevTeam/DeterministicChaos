using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class KingOfHeartsHeart : ModProjectile
    {
        private const float OrbitRadius = 100f;
        private const int HitCooldown = 12; // 0.2 seconds

        // ai[0] = heart index (0-9), ai[1] = anchor projectile whoAmI
        private ref float HeartIndex => ref Projectile.ai[0];
        private ref float AnchorID => ref Projectile.ai[1];

        private int _age;

        public override string Texture => "DeterministicChaos/Content/Projectiles/Friendly/FriendlyHeartProjectile";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 999999;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = HitCooldown;
        }

        public override void AI()
        {
            _age++;

            Projectile anchor = Main.projectile[(int)AnchorID];
            if (!anchor.active || anchor.type != ModContent.ProjectileType<KingOfHeartsProjectile>() || anchor.owner != Projectile.owner)
            {
                Projectile.Kill();
                return;
            }

            // Read shared state from anchor
            float globalAngle = anchor.localAI[0];
            int totalHearts = Math.Max((int)anchor.ai[1], 1);

            float angle = globalAngle + HeartIndex * MathHelper.TwoPi / totalHearts;
            Vector2 targetPos = anchor.Center + OrbitRadius * angle.ToRotationVector2();

            // Smooth follow for visual interpolation
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.25f);
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = 0f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 pos = Projectile.Center - Main.screenPosition;

            const int TweenFrames = 8;
            float spawnT = MathHelper.Clamp(_age / (float)TweenFrames, 0f, 1f);
            float despawnT = MathHelper.Clamp(Projectile.timeLeft / (float)TweenFrames, 0f, 1f);
            float tweenScale = Math.Min(spawnT, despawnT);
            float drawScale = Projectile.scale * tweenScale;

            Color tint = Color.Lerp(Color.White, new Color(210, 40, 40), 0.7f);
            Color glowColor = new Color(210, 40, 40) * (0.5f * tweenScale);
            Main.EntitySpriteDraw(tex, pos, null, glowColor, 0f, origin, drawScale * 1.3f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, tint * tweenScale, 0f, origin, drawScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
