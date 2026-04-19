using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;

namespace DeterministicChaos.Content.Items
{
    // Tracks whether the player currently has the Titansbane imbue active.
    public class TitansBaneImbuePlayer : ModPlayer
    {
        public bool imbueActive = false;

        public override void ResetEffects()
        {
            imbueActive = false;
        }
    }
}
