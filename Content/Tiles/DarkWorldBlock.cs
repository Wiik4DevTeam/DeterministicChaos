using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Tiles
{
    public class DarkWorldBlock : ModTile
    {
        // Use vanilla obsidian texture as base
        public override string Texture => "Terraria/Images/Tiles_56";

        public override void SetStaticDefaults()
        {
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = true;
            Main.tileLighted[Type] = false;
            Main.tileMergeDirt[Type] = false;
            
            // Indestructible
            MinPick = int.MaxValue;
            MineResist = float.MaxValue;
            
            // Dark purple dust
            DustType = DustID.PurpleTorch;
            
            AddMapEntry(new Color(30, 10, 50));
        }

        public override bool CanExplode(int i, int j)
        {
            return false;
        }

        public override bool CanKillTile(int i, int j, ref bool blockDamaged)
        {
            return false;
        }

        public override bool CanReplace(int i, int j, int tileTypeBeingPlaced)
        {
            return false;
        }

        public override void NumDust(int i, int j, bool fail, ref int num)
        {
            num = 0;
        }

        public override IEnumerable<Item> GetItemDrops(int i, int j)
        {
            return System.Array.Empty<Item>();
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
        {
            // Faint purple glow
            r = 0.05f;
            g = 0.0f;
            b = 0.1f;
        }
    }
}
