using System.Collections.Generic;
using SubworldLibrary;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.NPCs.DarkWorldEnemies;

namespace DeterministicChaos.Content.Subworlds
{
    // Controls enemy spawns in the Dark World subworld
    public class DarkDimensionSpawns : GlobalNPC
    {
        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
        {
            if (!SubworldSystem.IsActive<DarkDimension>())
                return;

            // Clear all vanilla spawns
            pool.Clear();

            // Add Dark World enemies to all biomes
            pool.Add(ModContent.NPCType<DarkWorldSlime>(), 1.0f);
            pool.Add(ModContent.NPCType<DarkWorldEye>(), 0.8f);
        }

        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            if (!SubworldSystem.IsActive<DarkDimension>())
                return;

            spawnRate = 400;
            maxSpawns = 8;
        }
    }
}
