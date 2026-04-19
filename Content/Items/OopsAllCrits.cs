using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class OopsAllCrits : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.accessory = true;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(gold: 6);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.luck += 0.15f;
            player.GetCritChance(DamageClass.Generic) += 45f;
            player.GetDamage(DamageClass.Generic) *= 0.78f;
        }
    }
}