using SubworldLibrary;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using DeterministicChaos.Content.Tiles;
using StructureHelper.API;

namespace DeterministicChaos.Content.Subworlds
{
    public class DarkDimension : Subworld
    {
        public enum BiomeType
        {
            Forest,
            Corruption,
            Crimson,
            Hallow,
            Jungle,
            Desert,
            Snow,
            Underworld,
            Dungeon,
            Underground,
            Ocean
        }
        
        // Origin point in the main world
        public static int OriginX = 0;
        public static int OriginY = 0;
        public static int WorldSeed = 0;
        public static BiomeType SourceBiome = BiomeType.Forest;
        
        public override int Width => 800;
        public override int Height => 800;
        
        public override bool ShouldSave => false;
        public override bool NoPlayerSaving => false;
        
        public override List<GenPass> Tasks => new List<GenPass>()
        {
            new SubworldGenPass("Dark World", GenerateDarkDimension)
        };

        // Store structure position for height map generation
        private static int seamHouseX = -1;
        private static int seamHouseWidth = 0;

        private void GenerateDarkDimension(GenerationProgress progress)
        {
            progress.Message = "Entering the Dark World...";
            
            Terraria.Utilities.UnifiedRandom rand = new Terraria.Utilities.UnifiedRandom(WorldSeed + (int)SourceBiome);
            
            // Get biome-specific tiles
            GetBiomeTiles(out ushort surfaceTile, out ushort dirtTile, out ushort stoneTile, out ushort wallType);
            
            // Base surface height
            int baseSurfaceY = (Height / 3) - 40;
            
            // Spawn is always at center
            int spawnX = Width / 2;
            
            // Determine structure position first (so we can flatten terrain there)
            DetermineSeamHousePosition(rand);
            
            // Generate height map with flat zones around spawn and structure
            int[] heightMap = GenerateHeightMap(rand, baseSurfaceY, spawnX);
            
            // Fill terrain based on height map
            for (int x = 0; x < Width; x++)
            {
                progress.Value = (float)x / Width * 0.5f;
                
                int surfaceY = heightMap[x];
                
                for (int y = 0; y < Height; y++)
                {
                    Tile tile = Main.tile[x, y];
                    tile.ClearEverything();
                    
                    // Above surface = air
                    if (y < surfaceY)
                        continue;
                    
                    // Surface layer
                    if (y == surfaceY)
                    {
                        tile.TileType = surfaceTile;
                        tile.HasTile = true;
                    }
                    // Dirt/sand layer
                    else if (y < surfaceY + 8)
                    {
                        tile.TileType = dirtTile;
                        tile.HasTile = true;
                    }
                    // Stone layer with caves
                    else
                    {
                        // Simple cave generation
                        float caveNoise = GetCaveNoise(x, y, rand);
                        if (caveNoise > 0.35f)
                        {
                            tile.TileType = stoneTile;
                            tile.HasTile = true;
                            
                            // Add some ore
                            if (rand.NextFloat() < 0.01f)
                                tile.TileType = TileID.Iron;
                            else if (rand.NextFloat() < 0.01f)
                                tile.TileType = TileID.Gold;
                        }
                    }
                    
                    // Add wall underground
                    if (y > surfaceY + 3)
                    {
                        tile.WallType = wallType;
                    }
                }
            }
            
            // Find spawn and place portal (use height at spawn location)
            int spawnY = heightMap[spawnX] - 3;
            
            ClearSpawnArea(spawnX, spawnY);
            PlaceReturnPortal(spawnX, spawnY);
            
            Main.spawnTileX = spawnX;
            Main.spawnTileY = spawnY;
            
            // Generate water for Ocean biome (above the sand, not below)
            if (SourceBiome == BiomeType.Ocean)
            {
                progress.Message = "Filling ocean...";
                GenerateOceanWater(heightMap, spawnX, baseSurfaceY);
            }
            
            // Add foliage based on biome (pass height map for correct placement)
            progress.Message = "Growing dark plants...";
            GenerateFoliage(rand, heightMap, surfaceTile);
            
            // Place SeamHouse structure
            progress.Message = "Placing structures...";
            PlaceSeamHouse(rand, heightMap);
            
            progress.Value = 1f;
        }

        private void DetermineSeamHousePosition(Terraria.Utilities.UnifiedRandom rand)
        {
            // Decide left or right side
            bool placeOnLeft = rand.NextBool();
            
            if (placeOnLeft)
            {
                seamHouseX = 50 + rand.Next(50);
            }
            else
            {
                seamHouseX = Width - 150 - rand.Next(50);
            }
            
            // Try to get structure width
            try
            {
                Mod mod = ModContent.GetInstance<DeterministicChaos>();
                Point16 dimensions = Generator.GetStructureDimensions("Assets/Structures/SeamHouse", mod);
                seamHouseWidth = dimensions.X;
            }
            catch
            {
                seamHouseWidth = 30; // Default fallback
            }
        }

        private int[] GenerateHeightMap(Terraria.Utilities.UnifiedRandom rand, int baseSurface, int spawnX)
        {
            int[] heights = new int[Width];
            
            // First pass: generate hilly terrain
            float currentHeight = baseSurface;
            float velocity = 0f;
            
            for (int x = 0; x < Width; x++)
            {
                velocity += (rand.NextFloat() - 0.5f) * 1.2f;
                velocity *= 0.92f;
                velocity = MathHelper.Clamp(velocity, -1.5f, 1.5f);
                
                currentHeight += velocity;
                // Limit terrain to go at most 10 tiles below base, 15 tiles above
                currentHeight = MathHelper.Clamp(currentHeight, baseSurface - 10, baseSurface + 15);
                
                heights[x] = (int)currentHeight;
            }
            
            // Second pass: flatten areas around spawn (20 block radius) and structure (5 blocks on each side)
            // with smooth transitions
            
            int spawnFlatRadius = 20;
            int spawnTransitionRadius = 10; // How many blocks to smoothly transition
            int structureFlatRadius = 5;
            int structureTransitionRadius = 8;
            
            // Get the target flat height at spawn (use base surface)
            int spawnFlatHeight = baseSurface;
            
            // Get the target flat height at structure (use base surface)
            int structureFlatHeight = baseSurface;
            
            // Apply flattening with smooth transitions
            for (int x = 0; x < Width; x++)
            {
                // Check spawn area
                int distFromSpawn = System.Math.Abs(x - spawnX);
                if (distFromSpawn <= spawnFlatRadius + spawnTransitionRadius)
                {
                    if (distFromSpawn <= spawnFlatRadius)
                    {
                        // Fully flat zone
                        heights[x] = spawnFlatHeight;
                    }
                    else
                    {
                        // Transition zone, smooth curve from flat to hilly
                        float t = (float)(distFromSpawn - spawnFlatRadius) / spawnTransitionRadius;
                        // Use smooth step for natural curve
                        t = t * t * (3f - 2f * t);
                        heights[x] = (int)MathHelper.Lerp(spawnFlatHeight, heights[x], t);
                    }
                }
                
                // Check structure area
                if (seamHouseX > 0)
                {
                    int structureLeft = seamHouseX - structureFlatRadius;
                    int structureRight = seamHouseX + seamHouseWidth + structureFlatRadius;
                    
                    if (x >= structureLeft - structureTransitionRadius && x <= structureRight + structureTransitionRadius)
                    {
                        if (x >= structureLeft && x <= structureRight)
                        {
                            // Fully flat zone under and around structure
                            heights[x] = structureFlatHeight;
                        }
                        else if (x < structureLeft)
                        {
                            // Left transition
                            float t = (float)(structureLeft - x) / structureTransitionRadius;
                            t = t * t * (3f - 2f * t);
                            heights[x] = (int)MathHelper.Lerp(structureFlatHeight, heights[x], t);
                        }
                        else
                        {
                            // Right transition
                            float t = (float)(x - structureRight) / structureTransitionRadius;
                            t = t * t * (3f - 2f * t);
                            heights[x] = (int)MathHelper.Lerp(structureFlatHeight, heights[x], t);
                        }
                    }
                }
            }
            
            return heights;
        }

        private void GenerateFoliage(Terraria.Utilities.UnifiedRandom rand, int[] heightMap, ushort surfaceTile)
        {
            // First pass: place larger objects like trees and rocks
            for (int x = 15; x < Width - 15; x++)
            {
                // Skip the spawn area
                if (x > Width / 2 - 20 && x < Width / 2 + 20)
                    continue;
                
                // Skip structure area
                if (seamHouseX > 0 && x >= seamHouseX - 8 && x <= seamHouseX + seamHouseWidth + 8)
                    continue;
                
                int surfaceY = heightMap[x];
                Tile groundTile = Main.tile[x, surfaceY];
                Tile aboveTile = Main.tile[x, surfaceY - 1];
                
                if (!groundTile.HasTile || aboveTile.HasTile)
                    continue;
                
                float chance = rand.NextFloat();
                
                switch (SourceBiome)
                {
                    case BiomeType.Forest:
                        if (chance < 0.06f)
                        {
                            WorldGen.GrowTree(x, surfaceY - 1);
                        }
                        else if (chance < 0.09f)
                        {
                            PlaceRock(x, surfaceY - 1, rand, TileID.Stone);
                        }
                        else if (chance < 0.11f)
                        {
                            WorldGen.PlaceSmallPile(x, surfaceY - 1, rand.Next(6), 0);
                        }
                        break;
                        
                    case BiomeType.Hallow:
                        if (chance < 0.06f)
                        {
                            WorldGen.GrowTree(x, surfaceY - 1);
                        }
                        else if (chance < 0.09f)
                        {
                            PlaceRock(x, surfaceY - 1, rand, TileID.Pearlstone);
                        }
                        else if (chance < 0.12f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.DyePlants, mute: true, style: rand.Next(8, 12));
                        }
                        break;
                        
                    case BiomeType.Corruption:
                        if (chance < 0.05f)
                        {
                            WorldGen.GrowTree(x, surfaceY - 1);
                        }
                        else if (chance < 0.08f)
                        {
                            PlaceRock(x, surfaceY - 1, rand, TileID.Ebonstone);
                        }
                        else if (chance < 0.12f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.CorruptThorns, mute: true);
                        }
                        break;
                        
                    case BiomeType.Crimson:
                        if (chance < 0.05f)
                        {
                            WorldGen.GrowTree(x, surfaceY - 1);
                        }
                        else if (chance < 0.08f)
                        {
                            PlaceRock(x, surfaceY - 1, rand, TileID.Crimstone);
                        }
                        else if (chance < 0.12f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.CrimsonThorns, mute: true);
                        }
                        break;
                        
                    case BiomeType.Jungle:
                        if (chance < 0.07f)
                        {
                            WorldGen.GrowTree(x, surfaceY - 1);
                        }
                        else if (chance < 0.10f)
                        {
                            PlaceRock(x, surfaceY - 1, rand, TileID.Mud);
                        }
                        else if (chance < 0.14f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.JungleThorns, mute: true);
                        }
                        break;
                        
                    case BiomeType.Desert:
                        if (chance < 0.05f)
                        {
                            WorldGen.GrowCactus(x, surfaceY - 1);
                        }
                        else if (chance < 0.08f)
                        {
                            PlaceRock(x, surfaceY - 1, rand, TileID.Sandstone);
                        }
                        else if (chance < 0.11f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.DesertFossil, mute: true);
                        }
                        break;
                        
                    case BiomeType.Snow:
                        if (chance < 0.05f)
                        {
                            WorldGen.GrowTree(x, surfaceY - 1);
                        }
                        else if (chance < 0.09f)
                        {
                            PlaceRock(x, surfaceY - 1, rand, TileID.IceBlock);
                        }
                        break;
                        
                    case BiomeType.Underworld:
                        if (chance < 0.06f)
                        {
                            PlaceRock(x, surfaceY - 1, rand, TileID.Hellstone);
                        }
                        else if (chance < 0.10f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.Obsidian, mute: true);
                        }
                        break;
                        
                    case BiomeType.Ocean:
                        if (chance < 0.04f)
                        {
                            PlaceRock(x, surfaceY - 1, rand, TileID.ShellPile);
                        }
                        else if (chance < 0.08f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.Coral, mute: true, style: rand.Next(6));
                        }
                        break;
                }
            }
            
            // Second pass: place smaller plants and details
            for (int x = 10; x < Width - 10; x++)
            {
                // Skip the spawn area
                if (x > Width / 2 - 15 && x < Width / 2 + 15)
                    continue;
                
                // Skip structure area
                if (seamHouseX > 0 && x >= seamHouseX - 5 && x <= seamHouseX + seamHouseWidth + 5)
                    continue;
                
                int surfaceY = heightMap[x];
                Tile groundTile = Main.tile[x, surfaceY];
                Tile aboveTile = Main.tile[x, surfaceY - 1];
                
                if (!groundTile.HasTile || aboveTile.HasTile)
                    continue;
                
                float chance = rand.NextFloat();
                
                switch (SourceBiome)
                {
                    case BiomeType.Forest:
                        if (chance < 0.20f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.Plants, mute: true, style: rand.Next(27));
                        }
                        else if (chance < 0.28f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.Plants2, mute: true, style: rand.Next(8));
                        }
                        else if (chance < 0.32f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.LargePiles, mute: true, style: rand.Next(18));
                        }
                        break;
                        
                    case BiomeType.Hallow:
                        if (chance < 0.18f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.HallowedPlants, mute: true, style: rand.Next(7));
                        }
                        else if (chance < 0.26f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.HallowedPlants2, mute: true, style: rand.Next(8));
                        }
                        break;
                        
                    case BiomeType.Corruption:
                        if (chance < 0.18f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.CorruptPlants, mute: true, style: rand.Next(8));
                        }
                        else if (chance < 0.24f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.Stalactite, mute: true);
                        }
                        break;
                        
                    case BiomeType.Crimson:
                        if (chance < 0.18f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.CrimsonPlants, mute: true, style: rand.Next(6));
                        }
                        else if (chance < 0.24f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.Stalactite, mute: true);
                        }
                        break;
                        
                    case BiomeType.Jungle:
                        if (chance < 0.25f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.JunglePlants, mute: true, style: rand.Next(15));
                        }
                        else if (chance < 0.35f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.JunglePlants2, mute: true, style: rand.Next(16));
                        }
                        else if (chance < 0.40f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.PlantDetritus, mute: true);
                        }
                        break;
                        
                    case BiomeType.Desert:
                        if (chance < 0.08f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.LargePiles2, mute: true, style: rand.Next(6));
                        }
                        break;
                        
                    case BiomeType.Snow:
                        if (chance < 0.12f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.SmallPiles, mute: true, style: rand.Next(6));
                        }
                        break;
                        
                    case BiomeType.Underworld:
                        if (chance < 0.15f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.AshPlants, mute: true, style: rand.Next(6));
                        }
                        else if (chance < 0.20f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.Pots, mute: true);
                        }
                        break;
                        
                    case BiomeType.Ocean:
                        if (chance < 0.12f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.BeachPiles, mute: true, style: rand.Next(7));
                        }
                        else if (chance < 0.18f)
                        {
                            WorldGen.PlaceTile(x, surfaceY - 1, TileID.Seaweed, mute: true);
                        }
                        break;
                }
            }
        }
        
        private void PlaceRock(int x, int y, Terraria.Utilities.UnifiedRandom rand, ushort tileType)
        {
            // Create a small rock pile (2-3 tiles wide, 1-2 tall)
            int width = rand.Next(2, 4);
            int height = rand.Next(1, 3);
            
            for (int rx = 0; rx < width; rx++)
            {
                for (int ry = 0; ry < height; ry++)
                {
                    int px = x + rx;
                    int py = y - ry;
                    
                    if (px < 0 || px >= Width || py < 0 || py >= Height)
                        continue;
                    
                    // Skip if theres already something
                    if (Main.tile[px, py].HasTile)
                        continue;
                    
                    // Only place on solid ground
                    if (ry == 0 || Main.tile[px, py + 1].HasTile)
                    {
                        WorldGen.PlaceTile(px, py, tileType, mute: true);
                    }
                }
            }
        }
        
        private void GenerateOceanWater(int[] heightMap, int spawnX, int waterLevel)
        {
            // Water level is at the base surface height
            // Water fills from above the sand down to the water level
            int waterDepth = 15; // How many tiles of water above sand
            
            for (int x = 0; x < Width; x++)
            {
                // Skip spawn area (20 block radius)
                if (System.Math.Abs(x - spawnX) <= 25)
                    continue;
                
                // Skip SeamHouse area (with extra padding)
                if (seamHouseX > 0 && x >= seamHouseX - 10 && x <= seamHouseX + seamHouseWidth + 10)
                    continue;
                
                int surfaceY = heightMap[x];
                
                // Place water above the sand surface
                for (int y = surfaceY - waterDepth; y < surfaceY; y++)
                {
                    if (y < 0 || y >= Height)
                        continue;
                    
                    Tile tile = Main.tile[x, y];
                    
                    // Only place water in empty space
                    if (!tile.HasTile)
                    {
                        tile.LiquidAmount = 255;
                        tile.LiquidType = LiquidID.Water;
                    }
                }
            }
            
            // Settle liquids
            Liquid.QuickWater(3);
        }

        private void PlaceSeamHouse(Terraria.Utilities.UnifiedRandom rand, int[] heightMap)
        {
            if (seamHouseX <= 0 || seamHouseX >= Width)
                return;
            
            try
            {
                Mod mod = ModContent.GetInstance<DeterministicChaos>();
                
                // Get structure dimensions to place based on bottom-left corner
                Point16 dimensions = Generator.GetStructureDimensions("Assets/Structures/SeamHouse", mod);
                
                // Use the height at the structure position
                int groundY = heightMap[System.Math.Clamp(seamHouseX, 0, Width - 1)];
                
                // Calculate Y position so bottom of structure is at ground level
                int structureY = groundY - dimensions.Y;
                
                Point16 position = new Point16(seamHouseX, structureY);
                Generator.GenerateStructure("Assets/Structures/SeamHouse", position, mod);
            }
            catch (System.Exception ex)
            {
                // Log the error but don't crash world gen
                ModContent.GetInstance<DeterministicChaos>().Logger.Error("Failed to place SeamHouse structure: " + ex.Message);
            }
        }

        private void GetBiomeTiles(out ushort surface, out ushort dirt, out ushort stone, out ushort wall)
        {
            switch (SourceBiome)
            {
                case BiomeType.Corruption:
                    surface = TileID.CorruptGrass;
                    dirt = TileID.Ebonstone;
                    stone = TileID.Ebonstone;
                    wall = WallID.EbonstoneUnsafe;
                    break;
                case BiomeType.Crimson:
                    surface = TileID.CrimsonGrass;
                    dirt = TileID.Crimstone;
                    stone = TileID.Crimstone;
                    wall = WallID.CrimstoneUnsafe;
                    break;
                case BiomeType.Hallow:
                    surface = TileID.HallowedGrass;
                    dirt = TileID.Pearlstone;
                    stone = TileID.Pearlstone;
                    wall = WallID.PearlstoneBrickUnsafe;
                    break;
                case BiomeType.Jungle:
                    surface = TileID.JungleGrass;
                    dirt = TileID.Mud;
                    stone = TileID.Stone;
                    wall = WallID.JungleUnsafe;
                    break;
                case BiomeType.Desert:
                    surface = TileID.Sand;
                    dirt = TileID.HardenedSand;
                    stone = TileID.Sandstone;
                    wall = WallID.Sandstone;
                    break;
                case BiomeType.Snow:
                    surface = TileID.SnowBlock;
                    dirt = TileID.IceBlock;
                    stone = TileID.IceBlock;
                    wall = WallID.SnowWallUnsafe;
                    break;
                case BiomeType.Underworld:
                    surface = TileID.Ash;
                    dirt = TileID.Ash;
                    stone = TileID.Hellstone;
                    wall = WallID.ObsidianBrickUnsafe;
                    break;
                case BiomeType.Dungeon:
                    surface = TileID.BlueDungeonBrick;
                    dirt = TileID.BlueDungeonBrick;
                    stone = TileID.BlueDungeonBrick;
                    wall = WallID.BlueDungeonUnsafe;
                    break;
                case BiomeType.Underground:
                    surface = TileID.Dirt;
                    dirt = TileID.Dirt;
                    stone = TileID.Stone;
                    wall = WallID.Stone;
                    break;
                case BiomeType.Ocean:
                    surface = TileID.Sand;
                    dirt = TileID.Sand;
                    stone = TileID.Sandstone;
                    wall = WallID.Sandstone;
                    break;
                default: // Forest
                    surface = TileID.Grass;
                    dirt = TileID.Dirt;
                    stone = TileID.Stone;
                    wall = WallID.Stone;
                    break;
            }
        }

        private float GetCaveNoise(int x, int y, Terraria.Utilities.UnifiedRandom rand)
        {
            // Simple pseudo-noise for caves
            int hash = (x * 374761393 + y * 668265263 + WorldSeed) ^ (x * y);
            hash = (hash ^ (hash >> 13)) * 1274126177;
            return (float)(hash & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }

        private int FindSpawnY(int x)
        {
            for (int y = 0; y < Height; y++)
            {
                if (Main.tile[x, y].HasTile)
                    return y - 3;
            }
            return Height / 3;
        }

        private void ClearSpawnArea(int x, int y)
        {
            for (int dx = -5; dx <= 5; dx++)
            {
                for (int dy = -10; dy <= 4; dy++)
                {
                    int tx = x + dx;
                    int ty = y + dy;
                    
                    if (tx >= 0 && tx < Width && ty >= 0 && ty < Height)
                    {
                        Tile tile = Main.tile[tx, ty];
                        tile.HasTile = false;
                        tile.WallType = 0;
                    }
                }
            }
            
            // Create floor
            GetBiomeTiles(out ushort surface, out _, out _, out _);
            for (int dx = -5; dx <= 5; dx++)
            {
                int tx = x + dx;
                int ty = y + 5;
                
                if (tx >= 0 && tx < Width && ty >= 0 && ty < Height)
                {
                    Tile tile = Main.tile[tx, ty];
                    tile.TileType = surface;
                    tile.HasTile = true;
                }
            }
        }

        private void PlaceReturnPortal(int x, int y)
        {
            int portalType = ModContent.TileType<DarkPortal>();
            
            for (int dy = 0; dy < 3; dy++)
            {
                int tx = x;
                int ty = y - 2 + dy;
                
                if (tx >= 0 && tx < Width && ty >= 0 && ty < Height)
                {
                    Tile tile = Main.tile[tx, ty];
                    tile.TileType = (ushort)portalType;
                    tile.HasTile = true;
                    tile.TileFrameX = 0;
                    tile.TileFrameY = (short)(dy * 18);
                }
            }
        }

        public override void OnEnter()
        {
            SubworldSystem.noReturn = true;
        }
        
        public override void OnLoad() { }
        public override void OnExit() { }

        public override void DrawMenu(GameTime gameTime) { }
    }
}
