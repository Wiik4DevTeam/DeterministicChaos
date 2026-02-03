using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Walls
{
    public class ERAMUnbreakableWall : ModWall
    {
        public override void SetStaticDefaults()
        {
            Main.wallHouse[Type] = false;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
        {
            r = 1f;
            g = 1f;
            b = 1f;
        }

        public override bool CanExplode(int i, int j)
        {
            return false;
        }

        public override void KillWall(int i, int j, ref bool fail)
        {
            fail = true;
        }
    }
}
