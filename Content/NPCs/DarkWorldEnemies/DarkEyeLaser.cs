using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace DeterministicChaos.Content.NPCs.DarkWorldEnemies
{
    public class DarkEyeLaser: ModProjectile
    {
        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();
        }

        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
        }

        public override void AI()
        {
            base.AI();
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnKill(int timeLeft)
        {
            base.OnKill(timeLeft);
            if (!Main.dedServ)
            {
                for(int i = 0; i < 10; i++)
                {
                    Dust.NewDust(Projectile.Center, 1, 1, ModContent.DustType<DarkEyeLaserDust>(), Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-10f, 10f));
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            var texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            var effects = Projectile.spriteDirection == 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            var offset = new Vector2(texture.Width - Projectile.width, 0);

            if (Projectile.spriteDirection == 0)
            {
                offset *= -1;
            }

            Main.EntitySpriteDraw(texture, Projectile.position - Main.screenPosition, null, Color.White, Projectile.rotation, offset, Projectile.scale, effects);
            return false;
        }
    }
}
