using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Systems
{
    // Manages animated background during Roaring Knight boss fight
    public class RoaringKnightBackgroundSystem : ModSystem
    {
        private static Asset<Texture2D> backgroundTexture;
        private static float fadeAlpha = 0f;
        private const float FadeSpeed = 0.01f;
        
        public static bool ShowBackground { get; set; } = false;
        
        public override void Load()
        {
            if (!Main.dedServ)
            {
                backgroundTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/NPCs/Bosses/BossBG");
            }
            
            On_Main.DoDraw_DrawNPCsOverTiles += DrawBackgroundLayer;
        }

        public override void Unload()
        {
            backgroundTexture = null;
            ShowBackground = false;
        }

        // Handles fading in and out based on boss activity
        public override void PostUpdateEverything()
        {
            bool bossActive = false;
            if (ShowBackground)
            {
                int roaringKnightType = ModContent.NPCType<NPCs.Bosses.RoaringKnight>();
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.active && npc.type == roaringKnightType)
                    {
                        bossActive = true;
                        break;
                    }
                }
            }
            
            if (ShowBackground && bossActive)
            {
                fadeAlpha = MathHelper.Min(fadeAlpha + FadeSpeed, 0.6f);
            }
            else
            {
                fadeAlpha = MathHelper.Max(fadeAlpha - FadeSpeed * 2f, 0f);
                if (!bossActive)
                {
                    ShowBackground = false;
                }
            }
        }

        // Draws the background texture covering the entire screen
        private void DrawBackgroundLayer(On_Main.orig_DoDraw_DrawNPCsOverTiles orig, Main self)
        {
            if (!Main.gameMenu && fadeAlpha > 0f && backgroundTexture != null)
            {
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                
                Texture2D texture = backgroundTexture.Value;
                int screenWidth = Main.screenWidth;
                int screenHeight = Main.screenHeight;
                
                float scaleX = screenWidth / (float)texture.Width;
                float scaleY = screenHeight / (float)texture.Height;
                float scale = MathHelper.Max(scaleX, scaleY);
                
                Vector2 position = new Vector2(screenWidth / 2f, screenHeight / 2f);
                Vector2 origin = new Vector2(texture.Width / 2f, texture.Height / 2f);
                
                Main.spriteBatch.Draw(
                    texture,
                    position,
                    null,
                    Color.White * fadeAlpha,
                    0f,
                    origin,
                    scale,
                    SpriteEffects.None,
                    0f
                );
                
                Main.spriteBatch.End();
            }
            
            orig(self);
        }
    }
}
