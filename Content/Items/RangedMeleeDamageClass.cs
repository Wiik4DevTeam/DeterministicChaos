using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    /// <summary>
    /// A hybrid damage class that benefits from both Ranged and Melee modifiers.
    /// Used by the Frying Pan.
    /// </summary>
    public class RangedMeleeDamageClass : DamageClass
    {
        public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
        {
            // Inherit damage/crit/speed bonuses from both Ranged and Melee
            if (damageClass == Ranged || damageClass == Melee)
                return StatInheritanceData.Full;

            // Also inherit from Generic (all-class bonuses)
            if (damageClass == Generic)
                return StatInheritanceData.Full;

            return StatInheritanceData.None;
        }

        public override bool GetEffectInheritance(DamageClass damageClass)
        {
            // Counts as both Ranged and Melee for effect purposes (e.g. armor set checks)
            return damageClass == Ranged || damageClass == Melee;
        }

        public override bool UseStandardCritCalcs => true;
    }
}
