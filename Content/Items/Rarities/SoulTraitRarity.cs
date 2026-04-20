using Microsoft.Xna.Framework;
using Terraria.ModLoader;
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

namespace DeterministicChaos.Content.Items.Rarities
{
    public class IntegrityRarity : ModRarity
    {
        public static readonly Color InnerColor   = new Color(0, 0, 255);
        public static readonly Color OutlineColor  = new Color(0, 0, 140);

        public override Color RarityColor => InnerColor;
        public override int GetPrefixedRarity(int offset, float valueMult) => Type;
    }

    public class PerseveranceRarity : ModRarity
    {
        public static readonly Color InnerColor   = new Color(255, 80, 255);
        public static readonly Color OutlineColor  = new Color(100, 0, 130);

        public override Color RarityColor => InnerColor;
        public override int GetPrefixedRarity(int offset, float valueMult) => Type;
    }

    public class BraveryRarity : ModRarity
    {
        public static readonly Color InnerColor   = new Color(255, 190, 50);
        public static readonly Color OutlineColor  = new Color(160, 50, 0);

        public override Color RarityColor => InnerColor;
        public override int GetPrefixedRarity(int offset, float valueMult) => Type;
    }

    public class DeterminationRarity : ModRarity
    {
        public static readonly Color InnerColor   = new Color(255, 80, 80);
        public static readonly Color OutlineColor  = new Color(140, 10, 10);

        public override Color RarityColor => InnerColor;
        public override int GetPrefixedRarity(int offset, float valueMult) => Type;
    }
}
