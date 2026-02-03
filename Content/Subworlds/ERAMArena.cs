using SubworldLibrary;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using DeterministicChaos.Content.Tiles;
using DeterministicChaos.Content.Walls;
using DeterministicChaos.Content.NPCs.Bosses;
using DeterministicChaos.Content.Systems;

namespace DeterministicChaos.Content.Subworlds
{
    public class SubworldGenPass : GenPass
    {
        private System.Action<GenerationProgress> action;

        public SubworldGenPass(string name, System.Action<GenerationProgress> action) : base(name, 1f)
        {
            this.action = action;
        }

        protected override void ApplyPass(GenerationProgress progress, Terraria.IO.GameConfiguration configuration)
        {
            action(progress);
        }
    }

    public class ERAMArena : Subworld
    {
        private static bool bossSpawned = false;
        private static bool dialogueStarted = false;
        private static bool dialogueComplete = false;
        private static int dialogueTimer = 0; // Timer to wait for dialogue to finish
        
        // Persistent flag, tracks if player has ever entered this arena before
        // This persists across game sessions via ModSystem saving
        public static bool hasEnteredBefore = false;
        
        // Track the player who is currently in the arena (multiplayer support)
        public static int currentArenaPlayer = -1;
        
        public override int Width => 200;
        public override int Height => 200;
        
        public override bool ShouldSave => false;
        public override bool NoPlayerSaving => false;
        
        public override List<GenPass> Tasks => new List<GenPass>()
        {
            new SubworldGenPass("ERAM Arena", GenerateArena)
        };

        private void GenerateArena(GenerationProgress progress)
        {
            progress.Message = "";
            
            int centerX = Width / 2;
            int centerY = Height / 2;
            int arenaWidth = 40;  // 10 columns × 4 tiles
            int arenaHeight = 24; // 6 rows × 4 tiles
            int wallThickness = 2;
            int surroundingArea = 50;
            
            int innerStartX = centerX - arenaWidth / 2;
            int innerStartY = centerY - arenaHeight / 2;
            int innerEndX = innerStartX + arenaWidth;
            int innerEndY = innerStartY + arenaHeight;
            
            int outerStartX = innerStartX - wallThickness;
            int outerStartY = innerStartY - wallThickness;
            int outerEndX = innerEndX + wallThickness;
            int outerEndY = innerEndY + wallThickness;
            
            int blackStartX = outerStartX - surroundingArea;
            int blackStartY = outerStartY - surroundingArea;
            int blackEndX = outerEndX + surroundingArea;
            int blackEndY = outerEndY + surroundingArea;
            
            ushort eramTile = (ushort)ModContent.TileType<ERAMUnbreakableTile>();
            ushort blackTile = (ushort)ModContent.TileType<PitchBlackTile>();
            ushort eramWall = (ushort)ModContent.WallType<ERAMUnbreakableWall>();
            
            // ASCII layout (10 columns × 6 rows, each cell is 4×4 tiles):
            // OXXXXXXXXX
            // XXOXOXOXOX
            // XOXXXXOXXX
            // XXXOXXXXOX
            // XOXOXOXOXX
            // XXXXXXXXXO
            // O = obstacle (block), X = empty (wall only)
            string[] layout = new string[]
            {
                "OXXXXXXXXX",
                "XXOXOXOXOX",
                "XOXXXXOXXX",
                "XXXOXXXXOX",
                "XOXOXOXOXX",
                "XXXXXXXXXO"
            };
            
            int cellSize = 4;
            
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Tile tile = Main.tile[x, y];
                    tile.ClearEverything();
                    
                    bool inInnerArea = x >= innerStartX && x < innerEndX && y >= innerStartY && y < innerEndY;
                    bool inOuterArea = x >= outerStartX && x < outerEndX && y >= outerStartY && y < outerEndY;
                    bool inBlackArea = x >= blackStartX && x < blackEndX && y >= blackStartY && y < blackEndY;
                    
                    if (inOuterArea && !inInnerArea)
                    {
                        // Arena border wall
                        tile.TileType = eramTile;
                        tile.HasTile = true;
                    }
                    else if (inBlackArea && !inOuterArea)
                    {
                        // Surrounding pitch black area
                        tile.TileType = blackTile;
                        tile.HasTile = true;
                    }
                    else if (inInnerArea)
                    {
                        // Inside arena, check layout
                        int cellX = (x - innerStartX) / cellSize;
                        int cellY = (y - innerStartY) / cellSize;
                        
                        // Bounds check
                        if (cellY >= 0 && cellY < layout.Length && cellX >= 0 && cellX < layout[cellY].Length)
                        {
                            char cell = layout[cellY][cellX];
                            if (cell == 'O')
                            {
                                // Obstacle block
                                tile.TileType = eramTile;
                                tile.HasTile = true;
                            }
                            else
                            {
                                // Empty with wall background
                                tile.WallType = eramWall;
                            }
                        }
                        else
                        {
                            tile.WallType = eramWall;
                        }
                    }
                }
            }
            
            Main.spawnTileX = centerX;
            Main.spawnTileY = centerY;
        }
        
        public override void OnEnter()
        {
            SubworldSystem.noReturn = true;
            bossSpawned = false;
            dialogueStarted = false;
            dialogueComplete = false;
            dialogueTimer = 0;
        }
        
        public override void OnLoad()
        {
            bossSpawned = false;
            dialogueStarted = false;
            dialogueComplete = false;
            dialogueTimer = 0;
        }
        
        public override void Update()
        {
            // Check if any player is active
            bool playerReady = false;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (Main.player[i].active)
                {
                    playerReady = true;
                    break;
                }
            }
            
            if (!playerReady)
                return;
            
            // Start dialogue if not started yet
            if (!dialogueStarted)
            {
                dialogueStarted = true;
                
                // Calculate total dialogue duration based on whether first entry or not
                // Each dialogue has ~4 seconds linger + typing time (~0.05s per char)
                // First time: 6 dialogues, Subsequent: 1 dialogue
                // Approximate: first time ~30 seconds, subsequent ~6 seconds
                // Using frames (60 fps): first time ~1800 frames, subsequent ~360 frames
                if (!hasEnteredBefore)
                {
                    dialogueTimer = 2800; // ~30 seconds for 6 dialogues
                    StartERAMDialogue();
                }
                else
                {
                    dialogueTimer = 120; // ~6 seconds for 1 dialogue
                }
            
            }
            
            // Count down dialogue timer
            if (dialogueStarted && !dialogueComplete)
            {
                if (dialogueTimer > 0)
                {
                    dialogueTimer--;
                }
                else
                {
                    dialogueComplete = true;
                }
            }
            
            // Spawn boss after dialogue is complete
            if (dialogueComplete && !bossSpawned && Main.netMode != NetmodeID.MultiplayerClient)
            {
                bossSpawned = true;
                
                int centerX = Width / 2;
                int centerY = Height / 2;
                Vector2 spawnPos = new Vector2(centerX * 16, centerY * 16);
                
                int npcIndex = NPC.NewNPC(
                    new Terraria.DataStructures.EntitySource_WorldEvent(),
                    (int)spawnPos.X,
                    (int)spawnPos.Y,
                    ModContent.NPCType<ERAM>()
                );
                
                if (npcIndex >= 0 && npcIndex < Main.maxNPCs)
                {
                    Main.npc[npcIndex].netUpdate = true;
                    
                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);
                }
            }
        }
        
        private void StartERAMDialogue()
        {
            // Use network-synced dialogue for multiplayer support
            if (!hasEnteredBefore)
            {
                // First time, full dialogue
                hasEnteredBefore = true;
                
                string[] texts = new string[]
                {
                    "... Welcome.",
                    "Is it fun? Playing pretend god?",
                    "Your search for real power begins here...",
                    "But before you advance to the next level... you must perfect your skills on your own.",
                    "Hope you haven't relied on those trinkets of yours.",
                    "Ready your knife, before it grows dull."
                };
                float[] lingerTimes = new float[] { 4f, 4f, 4f, 4f, 4f, 4f };
                
                ERAMNetworkHandler.SendDialoguePacket(texts, lingerTimes);
            }
            else
            {
                // Subsequent entries, just the final line
                ERAMNetworkHandler.SendDialoguePacket(
                    new string[] { "Ready your knife, before it grows dull." },
                    new float[] { 4f }
                );
            }
        }
        
        public override void DrawMenu(GameTime gameTime)
        {
            // Empty method, no text or loading screen elements
        }
        
        public override void OnExit()
        {
            // Clear the arena player tracking when exiting
            currentArenaPlayer = -1;
        }
    }
}
