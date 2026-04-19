using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.DarkWorldEnemies
{
    public class DarkEyeLaserDust : ModDust
    {
        public override void OnSpawn(Dust dust)
        {
            var tex = Texture2D;
            dust.frame = new Rectangle(0, 0, tex.Width(), tex.Height());
            dust.rotation = dust.velocity.ToRotation();
        }

        public override bool Update(Dust dust)
        {
            dust.velocity *= 0.98f;
            dust.position += dust.velocity;
            dust.scale -= 0.05f;
            if (dust.scale < 0.1f)
            {
                dust.active = false;
            }
            return false;
        }
    }
}
