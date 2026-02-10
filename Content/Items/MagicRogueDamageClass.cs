using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class MagicRogueDamageClass : DamageClass
    {
        private DamageClass calamityRogueClass = null;
        private bool initialized = false;

        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            // Try to get Calamity's RogueDamageClass
            if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
            {
                if (calamity.TryFind<DamageClass>("RogueDamageClass", out var rogueClass))
                {
                    calamityRogueClass = rogueClass;
                }
            }
        }

        public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
        {
            EnsureInitialized();

            // Inherit damage/crit/speed bonuses from Magic and Throwing
            if (damageClass == Magic || damageClass == Throwing)
                return StatInheritanceData.Full;

            // Inherit from Calamity's RogueDamageClass at half effectiveness for damage
            if (calamityRogueClass != null && damageClass == calamityRogueClass)
                return new StatInheritanceData(
                    damageInheritance: 0.5f,  // Half damage bonus
                    critChanceInheritance: 1f,
                    attackSpeedInheritance: 1f,
                    armorPenInheritance: 1f,
                    knockbackInheritance: 1f
                );

            // Also inherit from Generic (all-class bonuses)
            if (damageClass == Generic)
                return StatInheritanceData.Full;

            return StatInheritanceData.None;
        }

        public override bool GetEffectInheritance(DamageClass damageClass)
        {
            EnsureInitialized();

            // Counts as Magic, Throwing, and Calamity Rogue for effect purposes
            if (damageClass == Magic || damageClass == Throwing)
                return true;

            if (calamityRogueClass != null && damageClass == calamityRogueClass)
                return true;

            return false;
        }

        public override bool UseStandardCritCalcs => true;
    }
}
