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
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Buffs
{
    public class TitanStarBuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_" + BuffID.Shine;

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
            Main.lightPet[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            if (!IsStarEquippedAndVisible(player))
            {
                player.DelBuff(buffIndex);
                buffIndex--;
                return;
            }

            player.buffTime[buffIndex] = 18000;
        }

        private static bool IsStarEquippedAndVisible(Player player)
        {
            int starType = ModContent.ItemType<TitanStar>();
            int slotCount = player.miscEquips?.Length ?? 0;

            for (int i = 0; i < slotCount; i++)
            {
                Item equipped = player.miscEquips[i];
                if (equipped == null || equipped.type != starType)
                    continue;

                bool hidden = player.hideMisc[i];
                return !hidden;
            }

            return false;
        }
    }
}
