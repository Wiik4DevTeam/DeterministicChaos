using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using SubworldLibrary;
using Microsoft.Xna.Framework;
using DeterministicChaos.Content.Subworlds;
using DeterministicChaos.Content.NPCs.Bosses;
using DeterministicChaos.Content.Systems;

namespace DeterministicChaos.Content.Subworlds
{
    public class DarkDimensionPlayer : ModPlayer
    {
        public bool shouldEnterDarkDimension = false;
        
        public float returnX = 0f;
        public float returnY = 0f;

        // Guards against multiple SubworldSystem.Exit() calls on consecutive frames.
        private static bool exitPending = false;
        
        public override void ResetEffects()
        {
            // Prevent placing tiles in Dark World by setting tile range to 0
            if (DarkDimension.IsInDarkWorld)
            {
                Player.tileRangeX = 0;
                Player.tileRangeY = 0;
                Player.blockRange = 0;
            }
        }
        
        public override void PreUpdate()
        {
            // Handle entering Dark World
            if (shouldEnterDarkDimension && !DarkDimension.IsInDarkWorld)
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
            exitPending = false;
        }
        
        public override void PostUpdate()
        {
            if (DarkDimension.IsInDarkWorld)
            {
                // Check if player is near the portal for interaction
                if (Tiles.DarkPortal.PortalX > 0)
                {
                    float portalWorldX = Tiles.DarkPortal.PortalX * 16 + 8;
                    float horizontalDist = System.Math.Abs(Player.Center.X - portalWorldX);
                    
                    if (horizontalDist < 80f)
                    {
                        // Don't show portal exit UI or handle exit if the player is holding a Dark Shard
                        bool holdingDarkShard = Player.HeldItem != null && Player.HeldItem.type == ModContent.ItemType<Items.DarkShard>();
                        
                        if (!holdingDarkShard)
                        {
                            bool titanAlive = NPC.AnyNPCs(ModContent.NPCType<TitanBody>());

                            if (titanAlive)
                            {
                                // Show blocked message on right-click attempt
                                if (Player.whoAmI == Main.myPlayer && Main.mouseRight && Main.mouseRightRelease)
                                {
                                    CombatText.NewText(Player.Hitbox, Color.Red, "Cannot leave while the Titan lives!");
                                }
                            }
                            else
                            {
                                Player.cursorItemIconEnabled = true;
                                Player.cursorItemIconID = Terraria.ID.ItemID.MagicMirror;

                                // Handle right-click to exit, check if player just right-clicked
                                if (Player.whoAmI == Main.myPlayer && Main.mouseRight && Main.mouseRightRelease && !exitPending)
                                {
                                    exitPending = true;
                                    Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item6, Player.Center);
                                    ERAMProgressSystem.IsTransitioningSubworld = true;
                                    SubworldSystem.Exit();
                                }
                            }
                        }
                    }
                }
                
                // Force the biome zone based on the source biome
                // Using PostUpdate so it runs AFTER Terraria's zone calculation
                switch (DarkDimension.CurrentBiome)
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
