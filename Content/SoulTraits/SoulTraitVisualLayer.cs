using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using SubworldLibrary;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.UI;
using DeterministicChaos.Content.Subworlds;

namespace DeterministicChaos.Content.SoulTraits
{
    public class SoulTraitVisualLayer : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.LastVanillaLayer);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
        {
            Player player = drawInfo.drawPlayer;
            SoulTraitPlayer traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            
            // Only visible if player has a trait, visibility is enabled, and NOT in Dark World
            // In Dark World, the soul is drawn by the UI system to avoid being inverted
            bool inDarkDimension = SubworldSystem.IsActive<DarkDimension>();
            return traitPlayer.CurrentTrait != SoulTraitType.None && traitPlayer.SoulVisible && !inDarkDimension;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Player player = drawInfo.drawPlayer;
            SoulTraitPlayer traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            if (traitPlayer.CurrentTrait == SoulTraitType.None || !traitPlayer.SoulVisible)
                return;

            DrawSoulForPlayer(player, traitPlayer, ref drawInfo);
        }
        
        public static void DrawSoulForPlayer(Player player, SoulTraitPlayer traitPlayer, ref PlayerDrawSet drawInfo)
        {
            // Load the soul texture
            string texturePath = "DeterministicChaos/Content/SoulTraits/" + traitPlayer.CurrentTrait.ToString();
            Texture2D soulTexture = ModContent.Request<Texture2D>(texturePath, AssetRequestMode.ImmediateLoad).Value;

            // Get trait color
            Color traitColor = SoulTraitData.GetTraitColor(traitPlayer.CurrentTrait);

            // Calculate position, floating above player's head with slight bob
            float bobOffset = (float)System.Math.Sin(Main.GameUpdateCount * 0.05f) * 3f;
            Vector2 soulPosition = player.Center + new Vector2(0, -40 + bobOffset);
            
            // Convert to screen position
            Vector2 drawPosition = soulPosition - Main.screenPosition;

            // Scale and origin
            float scale = 0.8f;
            Vector2 origin = new Vector2(soulTexture.Width / 2f, soulTexture.Height / 2f);

            // Pulsing transparency for main soul
            float pulse = 0.7f + (float)System.Math.Sin(Main.GameUpdateCount * 0.03f) * 0.2f;
            Color drawColor = traitColor * pulse;

            // Soft glow outline that fades in and out
            float glowPulse = 0.3f + (float)System.Math.Sin(Main.GameUpdateCount * 0.04f) * 0.15f;
            
            // Draw multiple glow layers behind the soul (larger, more transparent)
            for (int i = 3; i >= 1; i--)
            {
                float glowScale = scale + (i * 0.15f);
                float glowAlpha = glowPulse * (0.4f / i);
                Color layerGlow = traitColor * glowAlpha;
                
                DrawData glowData = new DrawData(
                    soulTexture,
                    drawPosition,
                    null,
                    layerGlow,
                    0f,
                    origin,
                    glowScale,
                    SpriteEffects.None,
                    0
                );
                drawInfo.DrawDataCache.Add(glowData);
            }

            // Create main soul draw data
            DrawData data = new DrawData(
                soulTexture,
                drawPosition,
                null,
                drawColor,
                0f,
                origin,
                scale,
                SpriteEffects.None,
                0
            );

            drawInfo.DrawDataCache.Add(data);

            // Emit colored light at the soul's position matching the trait color
            float lightPulse = 0.4f + (float)System.Math.Sin(Main.GameUpdateCount * 0.04f) * 0.1f;
            Vector3 lightColor = traitColor.ToVector3() * lightPulse;
            Lighting.AddLight(soulPosition, lightColor);
        }
    }
    
    public class DarkDimensionSoulDrawSystem : ModSystem
    {
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            // Only draw in Dark World
            if (!SubworldSystem.IsActive<DarkDimension>())
                return;
            
            // Find the index of the inventory layer to insert before it
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1)
            {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "DeterministicChaos: Soul Visual",
                    delegate
                    {
                        DrawSoulsForAllPlayers();
                        return true;
                    },
                    InterfaceScaleType.Game
                ));
            }
        }
        
        private void DrawSoulsForAllPlayers()
        {
            SpriteBatch spriteBatch = Main.spriteBatch;
            
            // Draw soul for each active player
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (!player.active || player.dead)
                    continue;
                
                SoulTraitPlayer traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
                if (traitPlayer.CurrentTrait == SoulTraitType.None || !traitPlayer.SoulVisible)
                    continue;
                
                DrawSoulDirect(spriteBatch, player, traitPlayer);
            }
        }
        
        private void DrawSoulDirect(SpriteBatch spriteBatch, Player player, SoulTraitPlayer traitPlayer)
        {
            // Load the soul texture
            string texturePath = "DeterministicChaos/Content/SoulTraits/" + traitPlayer.CurrentTrait.ToString();
            Texture2D soulTexture = ModContent.Request<Texture2D>(texturePath, AssetRequestMode.ImmediateLoad).Value;

            // Get trait color (normal, not inverted)
            Color traitColor = SoulTraitData.GetTraitColor(traitPlayer.CurrentTrait);

            // Calculate position, floating above player's head with slight bob
            float bobOffset = (float)System.Math.Sin(Main.GameUpdateCount * 0.05f) * 3f;
            Vector2 soulPosition = player.Center + new Vector2(0, -40 + bobOffset);
            
            // Convert to screen position
            Vector2 drawPosition = soulPosition - Main.screenPosition;

            // Scale and origin
            float scale = 0.8f;
            Vector2 origin = new Vector2(soulTexture.Width / 2f, soulTexture.Height / 2f);

            // Pulsing transparency for main soul
            float pulse = 0.7f + (float)System.Math.Sin(Main.GameUpdateCount * 0.03f) * 0.2f;
            Color drawColor = traitColor * pulse;

            // Soft glow outline that fades in and out
            float glowPulse = 0.3f + (float)System.Math.Sin(Main.GameUpdateCount * 0.04f) * 0.15f;
            
            // Draw multiple glow layers behind the soul (larger, more transparent)
            for (int i = 3; i >= 1; i--)
            {
                float glowScale = scale + (i * 0.15f);
                float glowAlpha = glowPulse * (0.4f / i);
                Color layerGlow = traitColor * glowAlpha;
                
                spriteBatch.Draw(
                    soulTexture,
                    drawPosition,
                    null,
                    layerGlow,
                    0f,
                    origin,
                    glowScale,
                    SpriteEffects.None,
                    0f
                );
            }

            // Draw main soul
            spriteBatch.Draw(
                soulTexture,
                drawPosition,
                null,
                drawColor,
                0f,
                origin,
                scale,
                SpriteEffects.None,
                0f
            );

            // Emit colored light at the soul's position
            float lightPulse = 0.4f + (float)System.Math.Sin(Main.GameUpdateCount * 0.04f) * 0.1f;
            Vector3 lightColor = traitColor.ToVector3() * lightPulse;
            Lighting.AddLight(soulPosition, lightColor);
        }
    }
}
