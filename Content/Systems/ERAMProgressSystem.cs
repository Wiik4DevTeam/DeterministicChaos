using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using DeterministicChaos.Content.Subworlds;

namespace DeterministicChaos.Content.Systems
{
    public class ERAMProgressSystem : ModSystem
    {
        // World-persistent boss defeat flags
        public static bool ERAMDefeated = false;
        public static bool RoaringKnightDefeated = false;
        public static bool TitanDefeated = false;
        public static bool JevilDefeated = false;
        
        // Set to true before any SubworldSystem.Enter() or SubworldSystem.Exit() call.
        // Prevents OnWorldLoad/OnWorldUnload/LoadWorldData from resetting progress flags
        // during subworld transitions (static fields survive the transition in-process).
        public static bool IsTransitioningSubworld = false;
        
        public override void SaveWorldData(TagCompound tag)
        {
            tag["ERAMHasEnteredBefore"] = ERAMArena.hasEnteredBefore;
            tag["ERAMDefeated"] = ERAMDefeated;
            tag["RoaringKnightDefeated"] = RoaringKnightDefeated;
            tag["TitanDefeated"] = TitanDefeated;
            tag["JevilDefeated"] = JevilDefeated;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            if (IsTransitioningSubworld)
            {
                // During subworld transitions, keep the current static values
                // rather than loading stale data from the world save
                IsTransitioningSubworld = false;
                return;
            }
            
            ERAMArena.hasEnteredBefore = tag.GetBool("ERAMHasEnteredBefore");
            ERAMDefeated = tag.GetBool("ERAMDefeated");
            RoaringKnightDefeated = tag.GetBool("RoaringKnightDefeated");
            TitanDefeated = tag.GetBool("TitanDefeated");
            JevilDefeated = tag.GetBool("JevilDefeated");
        }

        public override void OnWorldLoad()
        {
            if (IsTransitioningSubworld)
            {
                // Don't reset flags during subworld transitions
                return;
            }
            
            // Reset to false when loading a new world (will be overwritten by LoadWorldData if saved)
            ERAMArena.hasEnteredBefore = false;
            ERAMDefeated = false;
            RoaringKnightDefeated = false;
            TitanDefeated = false;
            JevilDefeated = false;
        }

        public override void OnWorldUnload()
        {
            if (IsTransitioningSubworld)
            {
                // Don't reset flags during subworld transitions
                return;
            }
            
            // Reset when unloading
            ERAMArena.hasEnteredBefore = false;
            ERAMDefeated = false;
            RoaringKnightDefeated = false;
            TitanDefeated = false;
            JevilDefeated = false;
        }

        public override void NetSend(BinaryWriter writer)
        {
            BitsByte flags = new BitsByte();
            flags[0] = ERAMDefeated;
            flags[1] = RoaringKnightDefeated;
            flags[2] = ERAMArena.hasEnteredBefore;
            flags[3] = TitanDefeated;
            flags[4] = JevilDefeated;
            writer.Write(flags);
        }

        public override void NetReceive(BinaryReader reader)
        {
            BitsByte flags = reader.ReadByte();
            ERAMDefeated = flags[0];
            RoaringKnightDefeated = flags[1];
            ERAMArena.hasEnteredBefore = flags[2];
            TitanDefeated = flags[3];
            JevilDefeated = flags[4];
        }
    }
}
