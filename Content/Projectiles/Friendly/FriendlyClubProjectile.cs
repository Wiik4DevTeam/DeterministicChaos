using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    // Friendly club projectile spawned by Devilsknife on hit. Normal damage, no pierce.
    public class FriendlyClubProjectile : ModProjectile
    {
        private const int HitImmunityTicks = 18; // ~0.3 seconds
        private int _age;

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
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
        }

        public override bool? CanDamage()
        {
            bool fromDeckOfCards = Projectile.GetGlobalProjectile<DeckOfCardsGlobalProjectile>().spawnedByDeckOfCards;
            return fromDeckOfCards || _age >= HitImmunityTicks ? null : false;
        }

        public override void AI()
        {
            _age++;
            if (Projectile.velocity.LengthSquared() > 0.1f)
                Projectile.rotation = Projectile.velocity.ToRotation();
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

            Color tint = Color.Lerp(Color.White, new Color(40, 160, 40), 0.7f);
            Color glowColor = new Color(40, 160, 40) * (0.5f * tweenScale);
            Main.EntitySpriteDraw(tex, pos, null, glowColor, Projectile.rotation, origin, drawScale * 1.3f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, tint * tweenScale, Projectile.rotation, origin, drawScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
