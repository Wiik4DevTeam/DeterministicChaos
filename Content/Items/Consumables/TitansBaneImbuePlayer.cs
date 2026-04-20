using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;
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

namespace DeterministicChaos.Content.Items.Consumables
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
