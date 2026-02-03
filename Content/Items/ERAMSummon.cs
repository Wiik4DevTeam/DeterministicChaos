using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SubworldLibrary;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Subworlds;
using DeterministicChaos.Content.Systems;

namespace DeterministicChaos.Content.Items
{
    public class ERAMSummon : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.value = 0;
            Item.rare = ItemRarityID.Red;
            Item.useAnimation = 45;
            Item.useTime = 45;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = false;
            Item.scale = 0.5f;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Item[Item.type].Value;
            Vector2 position = Item.position - Main.screenPosition + new Vector2(Item.width / 2, Item.height - texture.Height / 2);
            Vector2 origin = texture.Size() * 0.5f;
            
            float smallScale = scale * 0.5f;
            spriteBatch.Draw(texture, position, null, Color.White, rotation, origin, smallScale, SpriteEffects.None, 0f);
            return false;
        }

        public override bool CanUseItem(Player player)
        {
            // Cannot use if already in arena or currently transitioning
            if (SubworldSystem.IsActive<ERAMArena>() || ERAMTransitionSystem.IsTransitioning)
                return false;
            
            // In multiplayer, check if someone else is already in the arena
            if (Main.netMode != NetmodeID.SinglePlayer && ERAMArena.currentArenaPlayer >= 0)
            {
                // Someone is already in the arena
                Main.NewText("Another player is currently in the ERAM Arena. Please wait.", Microsoft.Xna.Framework.Color.Yellow);
                return false;
            }
            
            return true;
        }

        public override bool? UseItem(Player player)
        {
            // Mark this player as the one entering the arena
            ERAMArena.currentArenaPlayer = player.whoAmI;
            
            // Start the transition effect, the system will handle teleporting after the effect completes
            ERAMTransitionSystem.StartTransition(player.whoAmI);
            return true;
        }
    }
}
