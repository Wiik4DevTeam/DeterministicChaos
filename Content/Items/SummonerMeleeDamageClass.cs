using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class SummonerMeleeDamageClass : DamageClass
    {
        public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
        {
            // Inherit damage/crit/speed bonuses from Summon and Melee
            if (damageClass == Summon || damageClass == Melee)
                return StatInheritanceData.Full;

            // Also inherit from Generic (all-class bonuses)
            if (damageClass == Generic)
                return StatInheritanceData.Full;

            return StatInheritanceData.None;
        }

        public override bool GetEffectInheritance(DamageClass damageClass)
        {
            // Counts as both Summoner and Melee for effect purposes
            return damageClass == Summon || damageClass == Melee;
        }

        public override bool UseStandardCritCalcs => true;
    }
}
