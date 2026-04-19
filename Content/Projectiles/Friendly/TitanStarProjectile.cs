using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class TitanStarProjectile : ModProjectile
    {
        private const int FrameWidth = 40;
        private const int FrameHeight = 40;
        private const int FrameCount = 2;
        private const float AnimationSpeed = 4f;
        private const float HoverOffsetY = -48f;

        public override string Texture => "DeterministicChaos/Content/Projectiles/Friendly/WhiteSaveStar";

        public override void SetStaticDefaults()
        {
            Main.projPet[Type] = true;
            ProjectileID.Sets.LightPet[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead || !player.HasBuff(ModContent.BuffType<TitanStarBuff>()))
            {
                Projectile.Kill();
                return;
            }

            // Keep the buff alive as long as the projectile exists
            int buffIndex = player.FindBuffIndex(ModContent.BuffType<TitanStarBuff>());
            if (buffIndex >= 0)
                player.buffTime[buffIndex] = 18000;

            Projectile.timeLeft = 2;

            // Gentle bobbing
            float bob = (float)System.Math.Sin(Main.GameUpdateCount * 0.04f) * 3f;
            Projectile.Center = player.Top + new Vector2(0f, HoverOffsetY + bob);

            // Strong white light
            Lighting.AddLight(Projectile.Center, 1.8f, 1.8f, 1.8f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;

            int frame = (int)(Main.GameUpdateCount / (60f / AnimationSpeed)) % FrameCount;
            Rectangle sourceRect = new Rectangle(frame * FrameWidth, 0, FrameWidth, FrameHeight);
            Vector2 origin = new Vector2(FrameWidth / 2f, FrameHeight / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + 0.08f * (float)System.Math.Sin(Main.GameUpdateCount * 0.06f);
            float rotation = (float)System.Math.Sin(Main.GameUpdateCount * 0.03f) * 0.1f;

            // White glow underlay
            float glowPulse = 0.4f + 0.15f * (float)System.Math.Sin(Main.GameUpdateCount * 0.08f);
            Color glowColor = new Color(220, 220, 255) * glowPulse;
            Main.EntitySpriteDraw(tex, drawPos, sourceRect, glowColor, rotation, origin, pulse * 1.6f, SpriteEffects.None, 0);

            // Main star sprite
            Main.EntitySpriteDraw(tex, drawPos, sourceRect, Color.White, rotation, origin, pulse, SpriteEffects.None, 0);

            // Bright additive glow
            float addPulse = 0.3f + 0.15f * (float)System.Math.Sin(Main.GameUpdateCount * 0.08f + 1f);
            Color addColor = new Color(200, 200, 255) * addPulse;
            Main.EntitySpriteDraw(tex, drawPos, sourceRect, addColor, rotation, origin, pulse * 1.3f, SpriteEffects.None, 0);

            return false;
        }
    }
}
