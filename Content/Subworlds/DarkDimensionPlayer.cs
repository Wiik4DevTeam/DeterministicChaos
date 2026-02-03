using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using SubworldLibrary;
using DeterministicChaos.Content.Subworlds;

namespace DeterministicChaos.Content.Subworlds
{
    /// <summary>
    /// ModPlayer to handle Dark World related player state.
    /// </summary>
    public class DarkDimensionPlayer : ModPlayer
    {
        /// <summary>
        /// Flag to mark this player should enter the Dark World.
        /// Used for multiplayer synchronization.
        /// </summary>
        public bool shouldEnterDarkDimension = false;
        
        /// <summary>
        /// Position in main world before entering Dark World.
        /// Used to return player to correct location.
        /// </summary>
        public float returnX = 0f;
        public float returnY = 0f;
        
        public override void ResetEffects()
        {
            // Prevent placing tiles in Dark World by setting tile range to 0
            if (SubworldSystem.IsActive<DarkDimension>())
            {
                Player.tileRangeX = 0;
                Player.tileRangeY = 0;
                Player.blockRange = 0;
            }
        }
        
        public override void PreUpdate()
        {
            // Handle entering Dark World
            if (shouldEnterDarkDimension && !SubworldSystem.IsActive<DarkDimension>())
            {
                // Store return position
                returnX = Player.Center.X;
                returnY = Player.Center.Y;
                
                // Reset flag
                shouldEnterDarkDimension = false;
            }
        }
        
        public override void OnEnterWorld()
        {
            // Reset flags when entering any world
            shouldEnterDarkDimension = false;
        }
        
        public override void PostUpdate()
        {
            if (SubworldSystem.IsActive<DarkDimension>())
            {
                // Check if player is near the portal for interaction
                if (Tiles.DarkPortal.PortalX > 0)
                {
                    float portalWorldX = Tiles.DarkPortal.PortalX * 16 + 8;
                    float horizontalDist = System.Math.Abs(Player.Center.X - portalWorldX);
                    
                    if (horizontalDist < 80f)
                    {
                        Player.cursorItemIconEnabled = true;
                        Player.cursorItemIconID = Terraria.ID.ItemID.MagicMirror;
                        
                        // Handle right-click to exit, check if player just right-clicked
                        if (Player.whoAmI == Main.myPlayer && Main.mouseRight && Main.mouseRightRelease)
                        {
                            Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item6, Player.Center);
                            SubworldSystem.Exit();
                        }
                    }
                }
                
                // Force the biome zone based on the source biome
                // Using PostUpdate so it runs AFTER Terraria's zone calculation
                switch (DarkDimension.SourceBiome)
                {
                    case DarkDimension.BiomeType.Corruption:
                        Player.ZoneCorrupt = true;
                        break;
                    case DarkDimension.BiomeType.Crimson:
                        Player.ZoneCrimson = true;
                        break;
                    case DarkDimension.BiomeType.Hallow:
                        Player.ZoneHallow = true;
                        break;
                    case DarkDimension.BiomeType.Jungle:
                        Player.ZoneJungle = true;
                        break;
                    case DarkDimension.BiomeType.Desert:
                        Player.ZoneDesert = true;
                        break;
                    case DarkDimension.BiomeType.Snow:
                        Player.ZoneSnow = true;
                        break;
                    case DarkDimension.BiomeType.Underworld:
                        Player.ZoneUnderworldHeight = true;
                        break;
                    case DarkDimension.BiomeType.Dungeon:
                        Player.ZoneDungeon = true;
                        break;
                    case DarkDimension.BiomeType.Underground:
                        Player.ZoneDirtLayerHeight = true;
                        break;
                    case DarkDimension.BiomeType.Ocean:
                        Player.ZoneBeach = true;
                        break;
                    default: // Forest, no zone override needed, it's the default
                        break;
                }
            }
        }
    }
}
