using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;

namespace DeterministicChaos.Content.Buffs
{
    public class RoaringGunBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoTimeDisplay[Type] = false; // Show the "timer" which we use for stacks
            Main.buffNoSave[Type] = true;
            Main.debuff[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            var gunPlayer = player.GetModPlayer<RoaringGunPlayer>();

            // Remove buff if not holding gun or no stacks
            if (!gunPlayer.isHoldingRoaringGun || gunPlayer.gunStacks <= 0)
            {
                player.DelBuff(buffIndex);
                buffIndex--;
                return;
            }

            // Set the buff time to display the stack count as the "timer"
            // Multiply by 60 so it displays as the stack number (60 frames = 1 second displayed)
            player.buffTime[buffIndex] = gunPlayer.gunStacks * 60;
        }

        public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
        {
            Player player = Main.LocalPlayer;
            var gunPlayer = player.GetModPlayer<RoaringGunPlayer>();
            int stacks = gunPlayer.gunStacks;

            buffName = $"Ameva Charge: {stacks}";
            
            // Calculate current fire rate multiplier
            float speedMult;
            if (stacks == 0)
                speedMult = 1f;
            else if (stacks <= 18)
                speedMult = 1f + (stacks / 18f) * 1f;
            else if (stacks == 19)
                speedMult = 3.5f;
            else
                speedMult = 5f;

            tip = $"Fire rate: {speedMult:F1}x\nMax stacks: {RoaringGun.MaxStacks}";
            
            // Change rarity color based on stacks
            if (stacks >= 20)
                rare = 11; // Cyan/Master
            else if (stacks >= 19)
                rare = 10; // Red/Expert
            else if (stacks >= 10)
                rare = 5; // Pink
            else
                rare = 1; // Blue
        }
    }
}
