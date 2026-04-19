using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    // Disables wings and teleport items (Rod of Discord, Rod of Harmony) during the Titan boss fight.
    public class TitanFightPlayer : ModPlayer
    {
        private static bool titanAlive = false;
        private static int checkTimer = 0;

        // Periodically checks if a TitanBody NPC is alive, caching the result.
        private static bool IsTitanAlive()
        {
            // Re-check every 30 ticks to avoid scanning every frame
            if (++checkTimer >= 30)
            {
                checkTimer = 0;
                titanAlive = NPC.AnyNPCs(ModContent.NPCType<TitanBody>());
            }
            return titanAlive;
        }

        public override void PostUpdateEquips()
        {
            if (!IsTitanAlive())
                return;

            // Disable wing flight
            Player.wingTimeMax = 0;
            Player.wingTime = 0;
            Player.rocketBoots = 0;
        }

        public override bool CanUseItem(Item item)
        {
            if (!IsTitanAlive())
                return base.CanUseItem(item);

            // Block Rod of Discord and Rod of Harmony
            if (item.type == ItemID.RodofDiscord || item.type == ItemID.RodOfHarmony)
                return false;

            return base.CanUseItem(item);
        }
    }
}
