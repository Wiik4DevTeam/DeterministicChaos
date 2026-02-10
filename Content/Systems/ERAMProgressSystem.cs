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
        
        public override void SaveWorldData(TagCompound tag)
        {
            tag["ERAMHasEnteredBefore"] = ERAMArena.hasEnteredBefore;
            tag["ERAMDefeated"] = ERAMDefeated;
            tag["RoaringKnightDefeated"] = RoaringKnightDefeated;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            ERAMArena.hasEnteredBefore = tag.GetBool("ERAMHasEnteredBefore");
            ERAMDefeated = tag.GetBool("ERAMDefeated");
            RoaringKnightDefeated = tag.GetBool("RoaringKnightDefeated");
        }

        public override void OnWorldLoad()
        {
            // Reset to false when loading a new world (will be overwritten by LoadWorldData if saved)
            ERAMArena.hasEnteredBefore = false;
            ERAMDefeated = false;
            RoaringKnightDefeated = false;
        }

        public override void OnWorldUnload()
        {
            // Reset when unloading
            ERAMArena.hasEnteredBefore = false;
            ERAMDefeated = false;
            RoaringKnightDefeated = false;
        }

        public override void NetSend(BinaryWriter writer)
        {
            BitsByte flags = new BitsByte();
            flags[0] = ERAMDefeated;
            flags[1] = RoaringKnightDefeated;
            flags[2] = ERAMArena.hasEnteredBefore;
            writer.Write(flags);
        }

        public override void NetReceive(BinaryReader reader)
        {
            BitsByte flags = reader.ReadByte();
            ERAMDefeated = flags[0];
            RoaringKnightDefeated = flags[1];
            ERAMArena.hasEnteredBefore = flags[2];
        }
    }
}
