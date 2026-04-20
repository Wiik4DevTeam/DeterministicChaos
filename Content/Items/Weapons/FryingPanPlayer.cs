using Terraria;
using Terraria.ID;
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

namespace DeterministicChaos.Content.Items.Weapons
{
    public class FryingPanPlayer : ModPlayer
    {
        public bool hasFryingPan = false;

        public override void ResetEffects()
        {
            hasFryingPan = false;
        }

        public override void PostUpdateEquips()
        {
            for (int i = 0; i < Player.inventory.Length; i++)
            {
                if (Player.inventory[i].type == ModContent.ItemType<FryingPan>())
                {
                    hasFryingPan = true;
                    break;
                }
            }
        }
    }
}
