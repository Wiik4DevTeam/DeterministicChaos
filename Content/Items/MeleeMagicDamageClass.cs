using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    /// <summary>
    /// A hybrid damage class that benefits from both Melee and Magic modifiers.
    /// Used by the Rusty Knife.
    /// </summary>
    public class MeleeMagicDamageClass : DamageClass
    {
        public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
        {
            // Inherit damage/crit/speed bonuses from both Melee and Magic
            if (damageClass == Melee || damageClass == Magic)
                return StatInheritanceData.Full;

            // Also inherit from Generic (all-class bonuses)
            if (damageClass == Generic)
                return StatInheritanceData.Full;

            return StatInheritanceData.None;
        }

        public override bool GetEffectInheritance(DamageClass damageClass)
        {
            // Counts as both Melee and Magic for effect purposes (e.g. armor set checks)
            return damageClass == Melee || damageClass == Magic;
        }

        public override bool UseStandardCritCalcs => true;
    }
}
