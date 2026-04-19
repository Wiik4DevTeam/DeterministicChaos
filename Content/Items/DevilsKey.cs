using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class DevilsKey : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.value = Item.buyPrice(gold: 2);
            Item.rare = ItemRarityID.LightPurple;
            Item.useAnimation = 45;
            Item.useTime = 45;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = false;
        }

        public override bool CanUseItem(Player player)
        {
            return !NPC.AnyNPCs(ModContent.NPCType<NPCs.Bosses.Jevil>());
        }

        public override bool? UseItem(Player player)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int npc = NPC.NewNPC(
                    player.GetSource_ItemUse(Item),
                    (int)player.Center.X,
                    (int)player.Center.Y - 700,
                    ModContent.NPCType<NPCs.Bosses.Jevil>());

                if (npc >= 0 && npc < Main.maxNPCs)
                    Main.npc[npc].netUpdate = true;
            }

            return true;
        }

        public override void OnCraft(Recipe recipe)
        {
            // Return the Shadow Key so it is effectively kept when crafting.
            Main.LocalPlayer.QuickSpawnItem(Main.LocalPlayer.GetSource_Misc("DevilsKeyCraftRefund"), ItemID.ShadowKey);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.GoldenKey)
                .AddIngredient(ItemID.ShadowKey)
                .AddTile(TileID.DemonAltar)
                .Register();
        }
    }
}