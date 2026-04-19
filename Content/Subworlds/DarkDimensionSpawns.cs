using System.Collections.Generic;
using SubworldLibrary;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.NPCs.Bosses;
using DeterministicChaos.Content.NPCs.DarkWorldEnemies;

namespace DeterministicChaos.Content.Subworlds
{
    // Controls enemy spawns in the Dark World subworld
    public class DarkDimensionSpawns : GlobalNPC
    {
        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
        {
            if (!DarkDimension.IsInDarkWorld)
                return;

            // Clear all vanilla spawns
            pool.Clear();

            // Don't spawn enemies during the Titan fight
            if (NPC.AnyNPCs(ModContent.NPCType<TitanBody>()))
                return;

            // Add Dark World enemies
            pool.Add(ModContent.NPCType<ArmoredZombie>(), 1.0f);
            pool.Add(ModContent.NPCType<DarkEye>(), 0.8f);
            pool.Add(ModContent.NPCType<MetalSlime>(), 0.6f);
        }

        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            if (!DarkDimension.IsInDarkWorld)
                return;

            // No spawns during Titan fight
            if (NPC.AnyNPCs(ModContent.NPCType<TitanBody>()))
            {
                spawnRate = int.MaxValue;
                maxSpawns = 0;
                return;
            }

            spawnRate = 400;
            maxSpawns = 8;
        }
    }
}
