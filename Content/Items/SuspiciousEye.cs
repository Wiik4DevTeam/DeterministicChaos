using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class SuspiciousEye : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.SuspiciousLookingEye;
        
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.value = 0;
            Item.rare = ItemRarityID.Blue;
            Item.useAnimation = 45;
            Item.useTime = 45;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = false;
        }

        public override bool CanUseItem(Player player)
        {
            return !NPC.AnyNPCs(ModContent.NPCType<NPCs.Bosses.FakeEyeOfCthulhu>()) 
                && !NPC.AnyNPCs(ModContent.NPCType<NPCs.Bosses.RoaringKnight>());
        }

        public override bool? UseItem(Player player)
        {
            // Play roar sound on all clients
            // WHY THE FUCK DOES THIS NOT WORK
            if (Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(SoundID.Roar, player.Center);
            }
            
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int npc = NPC.NewNPC(player.GetSource_ItemUse(Item), 
                    (int)player.Center.X, 
                    (int)player.Center.Y - 800, 
                    ModContent.NPCType<NPCs.Bosses.FakeEyeOfCthulhu>());
                
                if (npc < Main.maxNPCs)
                {
                    Main.npc[npc].netUpdate = true;
                }
            }
            
            return true;
        }
        
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.BlackLens, 6)
                .AddTile(TileID.DemonAltar)
                .Register();
        }
    }
}
