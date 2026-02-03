using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
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
