using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.NPCs.Bosses;

namespace DeterministicChaos.Content.Systems
{
    public class ERAMOverlaySystem : ModSystem
    {
        private static Asset<Texture2D> overlayTexture1;
        private static Asset<Texture2D> overlayTexture2;
        private static Asset<Texture2D> overlayTexture3;
        
        private static float fadeAlpha = 0f;
        private const float FadeSpeed = 0.015f;
        
        // Scale multiplier for the tiled texture (increase to make tiles larger)
        private const float TileScale = 6f;
        
        // Current overlay level: 0 = none, 1 = 75%, 2 = 50%, 3 = 25%
        public static int OverlayLevel { get; set; } = 0;
        public static bool BossActive { get; set; } = false;
        
        // Smooth transition tracking
        private static int previousOverlayLevel = 0;
        private static int displayedOverlayLevel = 0;
        private static float transitionProgress = 1f;
        private const float TransitionSpeed = 0.02f;
        
        public override void Load()
        {
            if (!Main.dedServ)
            {
                overlayTexture1 = ModContent.Request<Texture2D>("DeterministicChaos/Content/NPCs/Bosses/ERAMTilableBG1");
                overlayTexture2 = ModContent.Request<Texture2D>("DeterministicChaos/Content/NPCs/Bosses/ERAMTilableBG2");
                overlayTexture3 = ModContent.Request<Texture2D>("DeterministicChaos/Content/NPCs/Bosses/ERAMTilableBG3");
            }
            
            On_Main.DrawInterface += DrawOverlay;
        }

        public override void Unload()
        {
            overlayTexture1 = null;
            overlayTexture2 = null;
            overlayTexture3 = null;
            OverlayLevel = 0;
            BossActive = false;
            previousOverlayLevel = 0;
            displayedOverlayLevel = 0;
            transitionProgress = 1f;
        }

        public override void PostUpdateEverything()
        {
            // Check if ERAM boss is active and update overlay level
            bool foundBoss = false;
            int eramType = ModContent.NPCType<ERAM>();
            
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == eramType)
                {
                    foundBoss = true;
                    BossActive = true;
                    
                    float healthPercent = npc.life / (float)npc.lifeMax;
                    
                    if (healthPercent <= 0.25f)
                        OverlayLevel = 3;
                    else if (healthPercent <= 0.50f)
                        OverlayLevel = 2;
                    else if (healthPercent <= 0.75f)
                        OverlayLevel = 1;
                    else
                        OverlayLevel = 0;
                    
                    break;
                }
            }
            
            if (!foundBoss)
            {
                BossActive = false;
                OverlayLevel = 0;
            }
            
            // Handle smooth transitions between overlay levels
            if (OverlayLevel != displayedOverlayLevel && transitionProgress >= 1f)
            {
                // Start a new transition
                previousOverlayLevel = displayedOverlayLevel;
                transitionProgress = 0f;
            }
            
            if (transitionProgress < 1f)
            {
                transitionProgress += TransitionSpeed;
                if (transitionProgress >= 1f)
                {
                    transitionProgress = 1f;
                    displayedOverlayLevel = OverlayLevel;
                }
            }
            
            // Fade based on overlay level
            float targetAlpha = OverlayLevel > 0 ? 0.5f : 0f;
            
            if (fadeAlpha < targetAlpha)
                fadeAlpha = MathHelper.Min(fadeAlpha + FadeSpeed, targetAlpha);
            else if (fadeAlpha > targetAlpha)
                fadeAlpha = MathHelper.Max(fadeAlpha - FadeSpeed, targetAlpha);
        }

        private void DrawOverlay(On_Main.orig_DrawInterface orig, Main self, GameTime gameTime)
        {
            // Draw overlay before interface
            if (!Main.gameMenu && fadeAlpha > 0f)
            {
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, null, null);
                
                int screenWidth = Main.screenWidth;
                int screenHeight = Main.screenHeight;
                
                // Draw previous overlay fading out during transition
                if (transitionProgress < 1f && previousOverlayLevel > 0)
                {
                    Asset<Texture2D> prevTexture = previousOverlayLevel switch
                    {
                        1 => overlayTexture1,
                        2 => overlayTexture2,
                        3 => overlayTexture3,
                        _ => null
                    };
                    
                    if (prevTexture != null && prevTexture.IsLoaded)
                    {
                        Texture2D texture = prevTexture.Value;
                        float prevAlpha = fadeAlpha * (1f - transitionProgress);
                        Color drawColor = Color.White * prevAlpha;
                        int scaledWidth = (int)(texture.Width * TileScale);
                        int scaledHeight = (int)(texture.Height * TileScale);
                        
                        for (int x = 0; x < screenWidth; x += scaledWidth)
                        {
                            for (int y = 0; y < screenHeight; y += scaledHeight)
                            {
                                Main.spriteBatch.Draw(texture, new Vector2(x, y), null, drawColor, 0f, Vector2.Zero, TileScale, SpriteEffects.None, 0f);
                            }
                        }
                    }
                }
                
                // Draw current overlay fading in
                if (OverlayLevel > 0)
                {
                    Asset<Texture2D> currentTexture = OverlayLevel switch
                    {
                        1 => overlayTexture1,
                        2 => overlayTexture2,
                        3 => overlayTexture3,
                        _ => null
                    };
                    
                    if (currentTexture != null && currentTexture.IsLoaded)
                    {
                        Texture2D texture = currentTexture.Value;
                        float currentAlpha = fadeAlpha * transitionProgress;
                        Color drawColor = Color.White * currentAlpha;
                        int scaledWidth = (int)(texture.Width * TileScale);
                        int scaledHeight = (int)(texture.Height * TileScale);
                        
                        for (int x = 0; x < screenWidth; x += scaledWidth)
                        {
                            for (int y = 0; y < screenHeight; y += scaledHeight)
                            {
                                Main.spriteBatch.Draw(texture, new Vector2(x, y), null, drawColor, 0f, Vector2.Zero, TileScale, SpriteEffects.None, 0f);
                            }
                        }
                    }
                }
                
                Main.spriteBatch.End();
            }
            
            orig(self, gameTime);
        }
    }
}
