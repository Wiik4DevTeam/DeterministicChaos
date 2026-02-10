using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    /// <summary>
    /// A hybrid damage class that benefits from both Ranged and Summoner modifiers.
    /// Used by the HollowGun.
    /// </summary>
    public class RangedSummonDamageClass : DamageClass
    {
        public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
        {
            // Inherit damage/crit/speed bonuses from both Ranged and Summon
            if (damageClass == Ranged || damageClass == Summon)
                return StatInheritanceData.Full;

            // Also inherit from Generic (all-class bonuses)
            if (damageClass == Generic)
                return StatInheritanceData.Full;

            return StatInheritanceData.None;
        }

        public override bool GetEffectInheritance(DamageClass damageClass)
        {
            // Counts as both Ranged and Summon for effect purposes (e.g. armor set checks)
            return damageClass == Ranged || damageClass == Summon;
        }

        public override bool UseStandardCritCalcs => true;
    }
}
