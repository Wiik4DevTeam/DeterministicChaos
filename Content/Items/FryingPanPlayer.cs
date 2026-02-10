using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
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
