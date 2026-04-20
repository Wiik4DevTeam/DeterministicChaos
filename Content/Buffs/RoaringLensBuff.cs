using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;
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

namespace DeterministicChaos.Content.Buffs
{
    public class RoaringLensBuff : ModBuff
    {
        // Uses a vanilla icon so this buff doesn't require an extra texture file.
        public override string Texture => "Terraria/Images/Buff_" + BuffID.Shine;

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
            Main.lightPet[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            RoaringLensPlayer lensPlayer = player.GetModPlayer<RoaringLensPlayer>();
            if (!IsLensEquippedAndVisible(player))
            {
                lensPlayer.RoaringLensActive = false;
                player.DelBuff(buffIndex);
                buffIndex--;
                return;
            }

            lensPlayer.RoaringLensActive = true;
            player.buffTime[buffIndex] = 18000;
        }

        private static bool IsLensEquippedAndVisible(Player player)
        {
            int lensType = ModContent.ItemType<RoaringLens>();
            int slotCount = player.miscEquips?.Length ?? 0;

            for (int i = 0; i < slotCount; i++)
            {
                Item equipped = player.miscEquips[i];
                if (equipped == null || equipped.type != lensType)
                    continue;

                bool hidden = player.hideMisc[i];
                return !hidden;
            }

            return false;
        }
    }
}
