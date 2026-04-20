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
    // Custom rarity that displays as black text for Dark World items
    public class DarkWorldRarity : ModRarity
    {
        public override Color RarityColor => Color.White;

        public override int GetPrefixedRarity(int offset, float valueMult)
        {
            return Type;
        }
    }
}
