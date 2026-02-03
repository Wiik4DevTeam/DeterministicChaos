using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SubworldLibrary;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace DeterministicChaos.Content.Tiles
{
    /// <summary>
    /// A return portal that sends players back to the main world.
    /// Invisible tile with large interaction hitbox, visual handled by FountainVisualSystem.
    /// </summary>
    public class DarkPortal : ModTile
    {
        // Portal X position for the visual system to use
        public static int PortalX = -1;

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = false; // No light from tile itself
            Main.tileSolid[Type] = false;
            
            // Simple 1x3 vertical tile
            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2Top);
            TileObjectData.newTile.Width = 1;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.Origin = new Point16(0, 2);
            TileObjectData.newTile.CoordinateHeights = new int[] { 16, 16, 16 };
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, 1, 0);
            TileObjectData.newTile.AnchorTop = AnchorData.Empty;
            TileObjectData.addTile(Type);
            
            AddMapEntry(new Color(150, 50, 200), CreateMapEntryName());
            
            // Cannot be destroyed
            MinPick = int.MaxValue;
        }

        public override bool CanExplode(int i, int j)
        {
            return false;
        }

        public override bool CanKillTile(int i, int j, ref bool blockDamaged)
        {
            return false;
        }

        public override void NearbyEffects(int i, int j, bool closer)
        {
            // Store portal X position for the visual system
            PortalX = i;
        }

        public override bool RightClick(int i, int j)
        {
            if (SubworldSystem.IsActive<Subworlds.DarkDimension>())
            {
                SoundEngine.PlaySound(SoundID.Item6, Main.LocalPlayer.Center);
                SubworldSystem.Exit();
                return true;
            }
            return false;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
        {
            // Don't draw anything, completely invisible
            return false;
        }
    }
}
