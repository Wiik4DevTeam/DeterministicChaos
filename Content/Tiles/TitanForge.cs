using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.DataStructures;

namespace DeterministicChaos.Content.Tiles
{
    public class TitanForge : ModTile
    {
        
        public override void SetStaticDefaults()
        {
            // Properties
            Main.tileTable[Type] = true;
            Main.tileSolidTop[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileFrameImportant[Type] = true;
            TileID.Sets.DisableSmartCursor[Type] = true;
            TileID.Sets.IgnoredByNpcStepUp[Type] = true;

            // Placement, 3 tiles wide, 2 tiles high
            // Each frame is 16x18 with 2 pixels padding on right and bottom
            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 2;
            TileObjectData.newTile.Origin = new Terraria.DataStructures.Point16(1, 1);
            TileObjectData.newTile.CoordinateHeights = new int[] { 18, 18 };
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.UsesCustomCanPlace = true;
            TileObjectData.addTile(Type);

            // Localization
            AddMapEntry(new Color(75, 50, 90), Language.GetText("Mods.DeterministicChaos.Tiles.TitanForge.MapEntry"));

            // This tile is a crafting station
            AdjTiles = new int[] { TileID.WorkBenches, TileID.Anvils };

            // Dust type when broken
            DustType = DustID.PurpleTorch;
        }

        public override void NumDust(int i, int j, bool fail, ref int num)
        {
            num = fail ? 1 : 3;
        }

        public override void KillMultiTile(int i, int j, int frameX, int frameY)
        {
            Item.NewItem(new Terraria.DataStructures.EntitySource_TileBreak(i, j), i * 16, j * 16, 48, 32, ModContent.ItemType<Items.TitanForgeItem>());
        }
        
        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
        {
            // Emit a subtle red glow
            float pulse = 0.5f + 0.3f * (float)System.Math.Sin(Main.GameUpdateCount * 0.05f);
            r = 0.4f * pulse;
            g = 0.05f * pulse;
            b = 0.05f * pulse;
        }
        
        public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
        {
            // Only draw the aura on the top-left tile of the multi-tile
            Tile tile = Main.tile[i, j];
            if (tile.TileFrameX != 0 || tile.TileFrameY != 0)
                return;
            
            // Calculate draw position for the center of the tile (3x2 tiles = 48x32 pixels)
            Vector2 zero = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 tileCenter = new Vector2(i * 16 + 24, j * 16 + 16) - Main.screenPosition + zero;
            
            // Pulsing effect
            float pulse = 0.5f + 0.3f * (float)System.Math.Sin(Main.GameUpdateCount * 0.05f);
            
            // Use Extra[91] which is a soft circular glow texture
            Texture2D glowTexture = Terraria.GameContent.TextureAssets.Extra[91].Value;
            Vector2 origin = new Vector2(glowTexture.Width / 2f, glowTexture.Height / 2f);
            
            // End the current sprite batch and start with additive blending
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            
            // Draw multiple glow layers for a soft aura effect
            for (int layer = 4; layer >= 1; layer--)
            {
                float scale = 0.8f + (layer * 0.4f);
                float alpha = pulse * (0.5f / layer);
                Color layerColor = new Color(1f, 0.15f, 0.1f) * alpha;
                
                spriteBatch.Draw(
                    glowTexture,
                    tileCenter,
                    null,
                    layerColor,
                    0f,
                    origin,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
            
            // Draw a brighter core glow
            float coreAlpha = pulse * 0.6f;
            Color coreColor = new Color(1f, 0.3f, 0.2f) * coreAlpha;
            spriteBatch.Draw(
                glowTexture,
                tileCenter,
                null,
                coreColor,
                0f,
                origin,
                0.5f,
                SpriteEffects.None,
                0f
            );
            
            // Restore normal blending
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
