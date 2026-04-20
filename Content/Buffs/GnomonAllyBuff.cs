using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Buffs
{
    public class GnomonAllyBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoTimeDisplay[Type] = false;
            Main.buffNoSave[Type] = true;
            Main.debuff[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            // +20% movement speed and control, +20% weapon damage
            player.moveSpeed += 0.2f;
            player.runAcceleration *= 1.2f;
            player.GetDamage(DamageClass.Generic) += 0.2f;
        }
    }
}
