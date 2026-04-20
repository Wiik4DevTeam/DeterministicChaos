using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Buffs
{
    public abstract class ImbuedSummonBuffBase : ModBuff
    {
        protected abstract int GetProjectileType();

        public override string Texture => "DeterministicChaos/Content/Buffs/RoaringSummonBuff";

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            if (player.ownedProjectileCounts[GetProjectileType()] > 0)
            {
                player.buffTime[buffIndex] = 18000;
            }
            else
            {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }

    public class DeterminationSummonBuff : ImbuedSummonBuffBase
    {
        protected override int GetProjectileType() => ModContent.ProjectileType<DeterminationSummonProjectile>();
    }

    public class IntegritySummonBuff : ImbuedSummonBuffBase
    {
        protected override int GetProjectileType() => ModContent.ProjectileType<IntegritySummonProjectile>();
    }

    public class PatienceSummonBuff : ImbuedSummonBuffBase
    {
        protected override int GetProjectileType() => ModContent.ProjectileType<PatienceSummonProjectile>();
    }

    public class PerseveranceSummonBuff : ImbuedSummonBuffBase
    {
        protected override int GetProjectileType() => ModContent.ProjectileType<PerseveranceSummonProjectile>();
    }

    public class KindnessSummonBuff : ImbuedSummonBuffBase
    {
        protected override int GetProjectileType() => ModContent.ProjectileType<KindnessSummonProjectile>();
    }

    public class JusticeSummonBuff : ImbuedSummonBuffBase
    {
        protected override int GetProjectileType() => ModContent.ProjectileType<JusticeSummonProjectile>();
    }

    public class BraverySummonBuff : ImbuedSummonBuffBase
    {
        protected override int GetProjectileType() => ModContent.ProjectileType<BraverySummonProjectile>();
    }
}
