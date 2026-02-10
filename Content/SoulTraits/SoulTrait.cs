using Microsoft.Xna.Framework;

namespace DeterministicChaos.Content.SoulTraits
{
    public enum SoulTraitType
    {
        None,
        Justice,
        Kindness,
        Bravery,
        Patience,
        Integrity,
        Perseverance,
        Determination
    }

    public static class SoulTraitData
    {
        public static Color GetTraitColor(SoulTraitType trait)
        {
            return trait switch
            {
                SoulTraitType.Justice => new Color(255, 255, 0),
                SoulTraitType.Kindness => new Color(50, 205, 50),
                SoulTraitType.Bravery => new Color(255, 165, 0),
                SoulTraitType.Patience => new Color(0, 255, 255),
                SoulTraitType.Integrity => new Color(30, 144, 255),
                SoulTraitType.Perseverance => new Color(255, 0, 255),
                SoulTraitType.Determination => new Color(255, 0, 0),
                _ => Color.Gray
            };
        }

        public static string GetTraitName(SoulTraitType trait)
        {
            return trait switch
            {
                SoulTraitType.Justice => "JUSTICE",
                SoulTraitType.Kindness => "KINDNESS",
                SoulTraitType.Bravery => "BRAVERY",
                SoulTraitType.Patience => "PATIENCE",
                SoulTraitType.Integrity => "INTEGRITY",
                SoulTraitType.Perseverance => "PERSEVERANCE",
                SoulTraitType.Determination => "DETERMINATION",
                _ => "No Soul Trait"
            };
        }

        public static int[] GetInvestmentThresholds()
        {
            return new int[] { 3, 5, 12, 20 };
        }

        public static string[] GetTraitBonusDescriptions(SoulTraitType trait)
        {
            return trait switch
            {
                SoulTraitType.Justice => new string[]
                {
                    "Gain a built-in triple jump.",
                    "Increased critical strike chance by 6%.",
                    "Increased projectile damage by 10%.",
                    "Every 5 hits, gain a Justice Mark. Your next hit is guaranteed to critically strike."
                },
                SoulTraitType.Kindness => new string[]
                {
                    "+2 life regeneration to you. Heals nearby allies and town NPCs for 2 HP every second.",
                    "When you or nearby allies take damage, increase your defense by +6 for 3 seconds.",
                    "Potion effects are applied to nearby allies. +20% potion effect duration.",
                    "When you or an ally dies, you and each ally gain a Kindness Mark, increasing total damage by 25% for 10 seconds."
                },
                SoulTraitType.Bravery => new string[]
                {
                    "+10% global damage to enemies within 10 tiles. Take 5% increased damage.",
                    "+20% move speed while in combat.",
                    "Taking damage increases damage dealt by 12% for 6 seconds.",
                    "After dealing damage, gain a Bravery Mark. Increases global attack speed up to 20% based on enemy proximity."
                },
                SoulTraitType.Patience => new string[]
                {
                    "Enemies are less likely to target you.",
                    "Gain double life regen speed while not moving.",
                    "Gain access to Rogue Stealth. If you already have Rogue Stealth, increases maximum by 30.",
                    "Every 30 seconds without taking damage, gain a Patience Mark. The next hit consumes the stack to reduce damage by 50%."
                },
                SoulTraitType.Integrity => new string[]
                {
                    "Increased global critical strike damage by 15%. Decreased critical strike chance by 10%.",
                    "Increased global attack speed by 15%. Decreased attack damage by 10%.",
                    "Removes all negative effects from previous investment bonuses.",
                    "After dealing damage, gain an Integrity Mark. Your next critical strike will deal increased damage."
                },
                SoulTraitType.Perseverance => new string[]
                {
                    "+20% increased defense. -10% total damage dealt.",
                    "Taking damage increases your damage dealt by 5%. Lasts 5 seconds, stacks up to 5 times.",
                    "When a nearby ally takes damage, they take 50% less damage, and you take 50% of the damage.",
                    "After taking damage, gain a Perseverance Mark. Your next hit will add your Defense to your armor penetration."
                },
                SoulTraitType.Determination => new string[]
                {
                    "Reduced respawn time by 5 seconds.",
                    "6% increased global damage.",
                    "Increased immunity frames when taking damage.",
                    "At 50% health, gain a Determination Mark and store your position. On death, revive at half health and return to that position. 2 minute cooldown."
                },
                _ => new string[] { }
            };
        }
    }
}
