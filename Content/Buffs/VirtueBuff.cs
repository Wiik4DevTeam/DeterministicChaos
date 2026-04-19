using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Buffs
{
    public class VirtueBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoTimeDisplay[Type] = false;
            Main.buffNoSave[Type] = true;
            Main.debuff[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            // +15% damage, attack speed, and crit chance
            player.GetDamage(DamageClass.Generic) += 0.15f;
            player.GetAttackSpeed(DamageClass.Generic) += 0.15f;
            player.GetCritChance(DamageClass.Generic) += 15;
        }
    }
}
