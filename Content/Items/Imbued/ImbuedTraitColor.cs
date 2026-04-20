using Microsoft.Xna.Framework;
using DeterministicChaos.Content.Items.Accessories;
using DeterministicChaos.Content.Items.BossBags;
using DeterministicChaos.Content.Items.BossSummons;
using DeterministicChaos.Content.Items.Consumables;
using DeterministicChaos.Content.Items.DamageClasses;
using DeterministicChaos.Content.Items.Globals;
using DeterministicChaos.Content.Items.Materials;
using DeterministicChaos.Content.Items.Placeable;
using DeterministicChaos.Content.Items.Rarities;
using DeterministicChaos.Content.Items.Weapons;

namespace DeterministicChaos.Content.Items.Imbued
{
    /// <summary>
    /// Centralized color palette for the Imbued weapon trait variants, used to tint
    /// projectile sprites, trails, and dust so each imbued weapon visually reflects
    /// its soul trait. Mirrors the per-class GetTraitColor switches in the imbued items.
    /// </summary>
    public static class ImbuedTraitColor
    {
        // Indices 1-7 follow the ImbuedClarity/Static/Gnomon enum order:
        // 1=Determination, 2=Integrity, 3=Patience, 4=Perseverance,
        // 5=Kindness, 6=Justice, 7=Bravery. Index 0 is a sentinel (None).
        private static readonly Color[] Colors =
        {
            Color.White,
            new Color(255, 60, 60),    // Determination
            new Color(0, 0, 255),      // Integrity
            new Color(80, 255, 255),   // Patience
            new Color(255, 80, 255),   // Perseverance
            new Color(80, 230, 80),    // Kindness
            new Color(255, 255, 80),   // Justice
            new Color(255, 190, 60),   // Bravery
        };

        /// <summary>For enums whose first member is None (Gnomon, Static, Clarity).</summary>
        public static Color FromNoneFirst(int variant)
        {
            if (variant <= 0 || variant >= Colors.Length) return Color.White;
            return Colors[variant];
        }

        /// <summary>For ImbuedWillbreakerVariant which starts at 0=Determination (no None entry, -1 = unimbued).</summary>
        public static Color FromZeroDetermination(int variant)
        {
            if (variant < 0 || variant + 1 >= Colors.Length) return Color.White;
            return Colors[variant + 1];
        }

        /// <summary>Multiply two colors (per-channel, 0..255 normalized).</summary>
        public static Color Multiply(Color a, Color b)
        {
            return new Color(
                (byte)(a.R * b.R / 255),
                (byte)(a.G * b.G / 255),
                (byte)(a.B * b.B / 255),
                (byte)(a.A * b.A / 255)
            );
        }
    }
}
