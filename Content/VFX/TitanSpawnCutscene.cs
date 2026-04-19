using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SubworldLibrary;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using ReLogic.Content;
using Terraria.DataStructures;
using StructureHelper.API;
using System;
using System.Collections.Generic;
using DeterministicChaos.Content.NPCs.Bosses;
using DeterministicChaos.Content.Subworlds;
using DeterministicChaos.Content.Tiles;
using DeterministicChaos.Content.Systems;

namespace DeterministicChaos.Content.VFX
{
    public class TitanSpawnCutscene : ModSystem
    {
        public enum Phase
        {
            Inactive,
            Stars,
            SpawnAnimation,
            DarkerFountain
        }

        private static Phase currentPhase = Phase.Inactive;
        private static float phaseTimer = 0f;

        // Star phase (reused from DarkWorldCutscene)
        private static List<CutsceneStar> activeStars = new List<CutsceneStar>();
        private static float starSpawnTimer = 0f;
        private static int starsSpawned = 0;

        // Fountain position (world coords, center of the portal tile)
        public static Vector2 FountainWorldPosition => fountainWorldPosition;
        private static Vector2 fountainWorldPosition = Vector2.Zero;

        // Spawn animation
        private static int spawnAnimFrame = 0;
        private static float spawnAnimTimer = 0f;
        private const int SPAWN_FRAME_COUNT = 15;
        private const int SPAWN_FRAME_WIDTH = 320;
        private const int SPAWN_FRAME_HEIGHT = 900;
        private const float SPAWN_FPS = 12f;

        // Darker fountain (looping)
        private static int darkerFountainFrame = 0;
        private static float darkerFountainTimer = 0f;
        private const int DARKER_FRAME_COUNT = 8;
        private const int DARKER_FRAME_WIDTH = 320;
        private const int DARKER_FRAME_HEIGHT = 440;
        private const float DARKER_FPS = 12f;
        private static float darkerScrollOffset = 0f;

        // TitanTower structure
        public const int TOWER_WIDTH = 75;
        public const int TOWER_HEIGHT = 118;
        public static bool TowerPlaced => towerPlaced;
        public static Point16 TowerTopLeft => towerTopLeft;
        private static bool towerPlaced = false;
        private static Point16 towerTopLeft = Point16.Zero;
        // Saved tile data so we can restore when the tower is removed
        private static TileSaveData[,] savedTiles = null;

        // Titan boss spawn delay (seconds into DarkerFountain phase)
        private const float TITAN_SPAWN_DELAY = 5f;
        private static bool titanSpawned = false;

        // Textures
        private static Asset<Texture2D> starTexture;
        private static Asset<Texture2D> spawnTexture;
        private static Asset<Texture2D> darkerFountainTexture;

        // Star phase timing
        private const float STAR_PHASE_DURATION = 1f;

        private struct CutsceneStar
        {
            public Vector2 Position;
            public float Rotation;
            public float Scale;
            public float Life;
            public float MaxLife;
        }

        public static bool IsActive => currentPhase != Phase.Inactive;
        public static bool IsDarkerFountainActive => currentPhase == Phase.DarkerFountain;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            starTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/VFX/FountainStarSpawn");
            spawnTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/VFX/DarkerFountainSpawn");
            darkerFountainTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/VFX/DarkerFountain");

            // Hook to draw behind tiles
            Terraria.On_Main.DrawBackgroundBlackFill += DrawBehindTiles;
        }

        public override void Unload()
        {
            Terraria.On_Main.DrawBackgroundBlackFill -= DrawBehindTiles;
            starTexture = null;
            spawnTexture = null;
            darkerFountainTexture = null;
            activeStars?.Clear();
        }

        public static void StartCutscene()
        {
            if (currentPhase != Phase.Inactive)
                return;

            // Get fountain position from portal tile
            if (DarkPortal.PortalX < 0)
                return;

            Vector2 position = FindFountainPosition();

            // In multiplayer, sync to all clients
            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                ERAMNetworkHandler.SendTitanCutscenePacket(position);
            }

            // Start locally
            StartCutsceneAtPosition(position);
        }

        // Finds the fountain world position from the portal tile.
        private static Vector2 FindFountainPosition()
        {
            Vector2 pos = new Vector2(DarkPortal.PortalX * 16 + 8, Main.LocalPlayer.Center.Y);

            for (int y = 0; y < Main.maxTilesY; y++)
            {
                Tile tile = Framing.GetTileSafely(DarkPortal.PortalX, y);
                if (tile.HasTile && tile.TileType == ModContent.TileType<DarkPortal>())
                {
                    pos.Y = y * 16 + 8;
                    break;
                }
            }

            return pos;
        }

        // Start the cutscene at a specific position (used for multiplayer sync).
        public static void StartCutsceneAtPosition(Vector2 position)
        {
            if (currentPhase != Phase.Inactive)
                return;

            fountainWorldPosition = position;

            currentPhase = Phase.Stars;
            phaseTimer = 0f;
            starSpawnTimer = 0f;
            starsSpawned = 0;
            activeStars.Clear();

            // Hide the normal fountain
            FountainVisualSystem.HideFountain = true;
        }

        // Called when the Titan boss is killed or despawns to stop the darker fountain.
        public static void StopDarkerFountain()
        {
            if (currentPhase == Phase.Inactive)
                return;

            if (towerPlaced)
                RemoveTitanTower();

            currentPhase = Phase.Inactive;
            FountainVisualSystem.HideFountain = false;
        }

        public static void ResetState()
        {
            RemoveTitanTower();
            ResetStateLite();
        }

        // Resets all static cutscene state WITHOUT writing tiles.
        // Safe to call during world transitions where the tile array may be invalid.
        public static void ResetStateLite()
        {
            currentPhase = Phase.Inactive;
            phaseTimer = 0f;
            activeStars?.Clear();
            starSpawnTimer = 0f;
            starsSpawned = 0;
            fountainWorldPosition = Vector2.Zero;
            spawnAnimFrame = 0;
            spawnAnimTimer = 0f;
            darkerFountainFrame = 0;
            darkerFountainTimer = 0f;
            darkerScrollOffset = 0f;
            titanSpawned = false;
            towerPlaced = false;
            savedTiles = null;
            FountainVisualSystem.HideFountain = false;
        }

        // =========== TitanTower Structure ===========

        private struct TileSaveData
        {
            public ushort TileType;
            public bool HasTile;
            public ushort WallType;
            public short TileFrameX;
            public short TileFrameY;
            public byte LiquidAmount;
            public int LiquidType;
            public byte TileColor;
            public byte WallColor;
        }

        // Saves existing tiles, teleports players to bottom-center, then places the TitanTower structure.
        private static void PlaceTitanTower()
        {
            if (towerPlaced)
                return;

            // Calculate top-left: centered on fountain X, bottom at fountain Y
            int fountainTileX = (int)(fountainWorldPosition.X / 16f);
            int fountainTileY = (int)(fountainWorldPosition.Y / 16f);
            int topLeftX = fountainTileX - TOWER_WIDTH / 2;
            int topLeftY = fountainTileY - TOWER_HEIGHT + 5;
            towerTopLeft = new Point16(topLeftX, topLeftY);

            // Save existing tiles in the area
            savedTiles = new TileSaveData[TOWER_WIDTH, TOWER_HEIGHT];
            for (int x = 0; x < TOWER_WIDTH; x++)
            {
                for (int y = 0; y < TOWER_HEIGHT; y++)
                {
                    int wx = topLeftX + x;
                    int wy = topLeftY + y;
                    if (wx >= 0 && wx < Main.maxTilesX && wy >= 0 && wy < Main.maxTilesY)
                    {
                        Tile tile = Main.tile[wx, wy];
                        savedTiles[x, y] = new TileSaveData
                        {
                            TileType = tile.TileType,
                            HasTile = tile.HasTile,
                            WallType = tile.WallType,
                            TileFrameX = tile.TileFrameX,
                            TileFrameY = tile.TileFrameY,
                            LiquidAmount = tile.LiquidAmount,
                            LiquidType = tile.LiquidType,
                            TileColor = tile.TileColor,
                            WallColor = tile.WallColor
                        };
                    }
                }
            }

            // Teleport all active players in the dark world to bottom-center of tower
            int spawnTileX = fountainTileX;
            int spawnTileY = fountainTileY - 3; // A few tiles above the bottom edge
            Vector2 spawnWorldPos = new Vector2(spawnTileX * 16 + 8, spawnTileY * 16);

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead && DarkDimension.IsInDarkWorld)
                {
                    p.Teleport(spawnWorldPos, -1);
                    p.velocity = Vector2.Zero;
                    p.fallStart = spawnTileY;

                    // Sync teleport to the client in multiplayer
                    if (Main.netMode == NetmodeID.Server)
                    {
                        RemoteClient.CheckSection(i, spawnWorldPos);
                        NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null,
                            0, i, spawnWorldPos.X, spawnWorldPos.Y, -1);
                    }
                }
            }

            // Place the structure (server or singleplayer only)
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                try
                {
                    Mod mod = ModContent.GetInstance<DeterministicChaos>();
                    string structureName = Main.rand.NextBool() ? "Assets/Structures/TitanTower1" : "Assets/Structures/TitanTower2";
                    Generator.GenerateStructure(structureName, towerTopLeft, mod);
                }
                catch (Exception ex)
                {
                    ModContent.GetInstance<DeterministicChaos>().Logger.Error("Failed to place TitanTower: " + ex.Message);
                }

                // Sync tile changes to clients
                if (Main.netMode == NetmodeID.Server)
                {
                    NetMessage.SendTileSquare(-1, topLeftX, topLeftY, TOWER_WIDTH, TOWER_HEIGHT);
                }
            }

            towerPlaced = true;
        }

        // Removes the TitanTower by restoring the saved tiles.
        public static void RemoveTitanTower()
        {
            if (!towerPlaced || savedTiles == null)
                return;

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int topLeftX = towerTopLeft.X;
                int topLeftY = towerTopLeft.Y;

                for (int x = 0; x < TOWER_WIDTH; x++)
                {
                    for (int y = 0; y < TOWER_HEIGHT; y++)
                    {
                        int wx = topLeftX + x;
                        int wy = topLeftY + y;
                        if (wx >= 0 && wx < Main.maxTilesX && wy >= 0 && wy < Main.maxTilesY)
                        {
                            Tile tile = Main.tile[wx, wy];
                            var saved = savedTiles[x, y];
                            tile.ClearEverything();
                            tile.TileType = saved.TileType;
                            tile.HasTile = saved.HasTile;
                            tile.WallType = saved.WallType;
                            tile.TileFrameX = saved.TileFrameX;
                            tile.TileFrameY = saved.TileFrameY;
                            tile.LiquidAmount = saved.LiquidAmount;
                            tile.LiquidType = saved.LiquidType;
                            tile.TileColor = saved.TileColor;
                            tile.WallColor = saved.WallColor;
                        }
                    }
                }

                // Sync tile changes in multiplayer
                if (Main.netMode == NetmodeID.Server)
                {
                    NetMessage.SendTileSquare(-1, topLeftX, topLeftY, TOWER_WIDTH, TOWER_HEIGHT);
                }
            }

            towerPlaced = false;
            savedTiles = null;
        }

        // Removes the current tower and places a new random one (TitanTower1 or TitanTower2).
        // Does not teleport players or re-save base tiles, reuses the original saved tiles.
        public static void ReplaceTitanTower()
        {
            if (!towerPlaced || savedTiles == null)
                return;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int topLeftX = towerTopLeft.X;
            int topLeftY = towerTopLeft.Y;

            // Restore saved tiles first
            for (int x = 0; x < TOWER_WIDTH; x++)
            {
                for (int y = 0; y < TOWER_HEIGHT; y++)
                {
                    int wx = topLeftX + x;
                    int wy = topLeftY + y;
                    if (wx >= 0 && wx < Main.maxTilesX && wy >= 0 && wy < Main.maxTilesY)
                    {
                        Tile tile = Main.tile[wx, wy];
                        var saved = savedTiles[x, y];
                        tile.ClearEverything();
                        tile.TileType = saved.TileType;
                        tile.HasTile = saved.HasTile;
                        tile.WallType = saved.WallType;
                        tile.TileFrameX = saved.TileFrameX;
                        tile.TileFrameY = saved.TileFrameY;
                        tile.LiquidAmount = saved.LiquidAmount;
                        tile.LiquidType = saved.LiquidType;
                        tile.TileColor = saved.TileColor;
                        tile.WallColor = saved.WallColor;
                    }
                }
            }

            // Place a new random tower
            try
            {
                Mod mod = ModContent.GetInstance<DeterministicChaos>();
                string structureName = Main.rand.NextBool() ? "Assets/Structures/TitanTower1" : "Assets/Structures/TitanTower2";
                Generator.GenerateStructure(structureName, towerTopLeft, mod);
            }
            catch (Exception ex)
            {
                ModContent.GetInstance<DeterministicChaos>().Logger.Error("Failed to replace TitanTower: " + ex.Message);
            }

            // Sync tile changes in multiplayer
            if (Main.netMode == NetmodeID.Server)
            {
                NetMessage.SendTileSquare(-1, topLeftX, topLeftY, TOWER_WIDTH, TOWER_HEIGHT);
            }
        }

        // Checks if the darker fountain should be stopped, either all players dead (server/SP)
        // or the Titan NPC no longer exists (client-side cleanup).
        public override void PostUpdateNPCs()
        {
            if (currentPhase != Phase.DarkerFountain)
                return;

            // Client: check if the Titan NPC is gone (despawned or killed on server)
            // Wait until well after TITAN_SPAWN_DELAY so the NPC has time to sync
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                if (phaseTimer < TITAN_SPAWN_DELAY + 3f)
                    return; // Too early, Titan may not have spawned/synced yet

                bool titanExists = false;
                int titanType = ModContent.NPCType<TitanBody>();
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    if (Main.npc[i].active && Main.npc[i].type == titanType)
                    {
                        titanExists = true;
                        break;
                    }
                }

                if (!titanExists)
                {
                    StopDarkerFountain();
                }
                return;
            }

            // Server / singleplayer: check if all players are dead
            if (!towerPlaced)
                return;

            bool anyAlive = false;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead)
                {
                    anyAlive = true;
                    break;
                }
            }

            if (!anyAlive)
            {
                StopDarkerFountain();
            }
        }

        public override void OnWorldUnload()
        {
            // Use ResetStateLite to avoid writing tiles or sending NetMessage during unload,
            // which can cause disconnects during subworld transitions
            ResetStateLite();
        }

        public override void PostUpdateEverything()
        {
            if (currentPhase == Phase.Inactive)
                return;

            float deltaTime = 1f / 60f;
            phaseTimer += deltaTime;

            // Server-side: only track phase timer for tower placement and boss spawning
            if (Main.dedServ)
            {
                switch (currentPhase)
                {
                    case Phase.Stars:
                        if (phaseTimer >= STAR_PHASE_DURATION)
                        {
                            currentPhase = Phase.SpawnAnimation;
                            phaseTimer = 0f;
                        }
                        break;
                    case Phase.SpawnAnimation:
                        // Spawn animation lasts SPAWN_FRAME_COUNT / SPAWN_FPS seconds
                        float spawnDuration = SPAWN_FRAME_COUNT / SPAWN_FPS;
                        if (phaseTimer >= spawnDuration)
                        {
                            currentPhase = Phase.DarkerFountain;
                            phaseTimer = 0f;
                            titanSpawned = false;
                        }
                        break;
                    case Phase.DarkerFountain:
                        if (!titanSpawned && phaseTimer >= TITAN_SPAWN_DELAY)
                        {
                            titanSpawned = true;
                            PlaceTitanTower();
                            SpawnTitanBoss();
                        }
                        break;
                }
                return;
            }

            // Client-side: run visual updates

            switch (currentPhase)
            {
                case Phase.Stars:
                    UpdateStarPhase(deltaTime);
                    break;
                case Phase.SpawnAnimation:
                    UpdateSpawnAnimation(deltaTime);
                    break;
                case Phase.DarkerFountain:
                    UpdateDarkerFountain(deltaTime);
                    break;
            }

            // Update existing stars
            for (int i = activeStars.Count - 1; i >= 0; i--)
            {
                var star = activeStars[i];
                star.Life -= deltaTime;
                activeStars[i] = star;

                if (star.Life <= 0)
                    activeStars.RemoveAt(i);
            }
        }

        private void UpdateStarPhase(float deltaTime)
        {
            starSpawnTimer += deltaTime;

            // Spawn a star every 0.1 seconds
            if (starSpawnTimer >= 0.1f && starsSpawned < 10)
            {
                starSpawnTimer = 0f;
                starsSpawned++;

                Vector2 offset = new Vector2(
                    Main.rand.NextFloat(-100f, 100f),
                    Main.rand.NextFloat(-100f, 100f)
                );

                activeStars.Add(new CutsceneStar
                {
                    Position = fountainWorldPosition + offset,
                    Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    Scale = Main.rand.NextFloat(0.5f, 1.5f),
                    Life = 0.1f,
                    MaxLife = 0.1f
                });
            }

            // Transition to spawn animation after star phase
            if (phaseTimer >= STAR_PHASE_DURATION)
            {
                currentPhase = Phase.SpawnAnimation;
                phaseTimer = 0f;
                spawnAnimFrame = 0;
                spawnAnimTimer = 0f;

                // Play DarkFountain sound at 0.7 speed (negative pitch = slower)
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/DarkFountain")
                {
                    Volume = 1f,
                    Pitch = 0.0f // 1.0x speed
                }, fountainWorldPosition);
            }
        }

        private void UpdateSpawnAnimation(float deltaTime)
        {
            spawnAnimTimer += deltaTime;

            float frameDuration = 1f / SPAWN_FPS;
            if (spawnAnimTimer >= frameDuration)
            {
                spawnAnimTimer -= frameDuration;
                spawnAnimFrame++;

                if (spawnAnimFrame >= SPAWN_FRAME_COUNT)
                {
                    // Spawn animation complete, transition to looping darker fountain
                    currentPhase = Phase.DarkerFountain;
                    phaseTimer = 0f;
                    darkerFountainFrame = 0;
                    darkerFountainTimer = 0f;
                    darkerScrollOffset = 0f;
                    titanSpawned = false;
                }
            }
        }

        private void UpdateDarkerFountain(float deltaTime)
        {
            darkerFountainTimer += deltaTime;

            float frameDuration = 1f / DARKER_FPS;
            if (darkerFountainTimer >= frameDuration)
            {
                darkerFountainTimer -= frameDuration;
                darkerFountainFrame = (darkerFountainFrame + 1) % DARKER_FRAME_COUNT;
            }

            // Scroll for vertical tiling
            darkerScrollOffset -= 2f;
            if (darkerScrollOffset < -DARKER_FRAME_HEIGHT)
                darkerScrollOffset += DARKER_FRAME_HEIGHT;

            // After 5 seconds, place tower and spawn the Titan (singleplayer only, server handles its own)
            if (!titanSpawned && phaseTimer >= TITAN_SPAWN_DELAY)
            {
                titanSpawned = true;
                PlaceTitanTower();

                if (Main.netMode == NetmodeID.SinglePlayer)
                {
                    SpawnTitanBoss();
                }
            }
        }

        // Spawns the TitanBody at the top-center of the tower, plus 6 TitanWings around it.
        private static void SpawnTitanBoss()
        {
            // Despawn all regular enemies before spawning the Titan
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && !npc.townNPC && npc.lifeMax > 5)
                {
                    npc.active = false;
                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, i);
                }
            }

            // Body spawns at top-center of tower
            int fountainTileX = (int)(fountainWorldPosition.X / 16f);
            int fountainTileY = (int)(fountainWorldPosition.Y / 16f);
            int topLeftY = fountainTileY - TOWER_HEIGHT + 5;

            float bodyWorldX = fountainWorldPosition.X;
            float bodyWorldY = (topLeftY + 3) * 16f; // 3 tiles below tower top

            int bodyIndex = NPC.NewNPC(
                new Terraria.DataStructures.EntitySource_WorldEvent(),
                (int)bodyWorldX, (int)bodyWorldY,
                ModContent.NPCType<TitanBody>()
            );

            if (bodyIndex < 0 || bodyIndex >= Main.maxNPCs)
                return;

            // Spawn 6 wings (slots 0-5)
            for (int i = 0; i < 6; i++)
            {
                int wingIndex = NPC.NewNPC(
                    new Terraria.DataStructures.EntitySource_Parent(Main.npc[bodyIndex]),
                    (int)bodyWorldX, (int)bodyWorldY,
                    ModContent.NPCType<TitanWing>(),
                    ai0: bodyIndex,
                    ai1: i
                );

                if (wingIndex >= 0 && wingIndex < Main.maxNPCs && Main.netMode == NetmodeID.Server)
                {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, wingIndex);
                }
            }

            if (Main.netMode == NetmodeID.Server)
            {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, bodyIndex);
            }
        }

        private void DrawBehindTiles(Terraria.On_Main.orig_DrawBackgroundBlackFill orig, Main self)
        {
            orig(self);

            if (Main.dedServ)
                return;

            // Don't manipulate SpriteBatch outside of the dark world.
            // During subworld transitions the graphics state can be invalid,
            // causing crashes/disconnects.
            if (!DarkDimension.IsInDarkWorld)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;

            // End current spritebatch, then start our own
            try { spriteBatch.End(); } catch { }

            if (currentPhase == Phase.Inactive)
            {
                // Still draw Titan behind tiles even when cutscene is inactive
                DrawTitanBehindTiles(spriteBatch);

                // Restart spritebatch and return early, no cutscene VFX to draw
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                return;
            }

            // Draw stars (additive blending)
            if (activeStars.Count > 0 && starTexture != null && starTexture.IsLoaded)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                Texture2D starTex = starTexture.Value;
                Vector2 starOrigin = new Vector2(starTex.Width / 2, starTex.Height / 2);

                foreach (var star in activeStars)
                {
                    Vector2 screenPos = star.Position - Main.screenPosition;
                    float alpha = star.Life / star.MaxLife;

                    spriteBatch.Draw(
                        starTex,
                        screenPos,
                        null,
                        Color.White * alpha,
                        star.Rotation,
                        starOrigin,
                        star.Scale,
                        SpriteEffects.None,
                        0f
                    );
                }

                spriteBatch.End();
            }

            // Draw spawn animation
            if (currentPhase == Phase.SpawnAnimation && spawnTexture != null && spawnTexture.IsLoaded)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                Texture2D tex = spawnTexture.Value;
                Rectangle sourceRect = new Rectangle(spawnAnimFrame * SPAWN_FRAME_WIDTH, 0, SPAWN_FRAME_WIDTH, SPAWN_FRAME_HEIGHT);
                // Origin at 3/4 height so the sprite aligns with the fountain block
                Vector2 origin = new Vector2(SPAWN_FRAME_WIDTH / 2f, SPAWN_FRAME_HEIGHT * 0.75f);
                Vector2 screenPos = fountainWorldPosition - Main.screenPosition;

                spriteBatch.Draw(
                    tex,
                    screenPos,
                    sourceRect,
                    Color.White,
                    0f,
                    origin,
                    1f,
                    SpriteEffects.None,
                    0f
                );

                spriteBatch.End();
            }

            // Draw darker fountain (tiled vertically)
            if (currentPhase == Phase.DarkerFountain && darkerFountainTexture != null && darkerFountainTexture.IsLoaded)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                Texture2D tex = darkerFountainTexture.Value;
                Rectangle sourceRect = new Rectangle(darkerFountainFrame * DARKER_FRAME_WIDTH, 0, DARKER_FRAME_WIDTH, DARKER_FRAME_HEIGHT);
                Vector2 origin = new Vector2(DARKER_FRAME_WIDTH / 2f, 0f);

                // The fountain X position in screen space
                float worldX = fountainWorldPosition.X;
                float screenX = worldX - Main.screenPosition.X;

                // Tile vertically across the entire visible screen
                float startY = darkerScrollOffset - Main.screenPosition.Y % DARKER_FRAME_HEIGHT - DARKER_FRAME_HEIGHT;

                for (float y = startY; y < Main.screenHeight + DARKER_FRAME_HEIGHT; y += DARKER_FRAME_HEIGHT)
                {
                    Vector2 drawPos = new Vector2(screenX, y);

                    spriteBatch.Draw(
                        tex,
                        drawPos,
                        sourceRect,
                        Color.White,
                        0f,
                        origin,
                        1f,
                        SpriteEffects.None,
                        0f
                    );
                }

                spriteBatch.End();
            }

            // Draw Titan body + wings behind tiles, on top of fountain VFX
            DrawTitanBehindTiles(spriteBatch);

            // Restart spritebatch in default state for the rest of rendering
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // Draws the TitanBody and TitanWing NPCs behind tiles (called from DrawBehindTiles hook).
        private void DrawTitanBehindTiles(SpriteBatch spriteBatch)
        {
            int titanBodyType = ModContent.NPCType<TitanBody>();
            int titanWingType = ModContent.NPCType<TitanWing>();

            bool hasAny = false;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && (npc.type == titanBodyType || npc.type == titanWingType))
                {
                    hasAny = true;
                    break;
                }
            }

            if (!hasAny)
                return;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // Draw wings first (behind body)
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != titanWingType)
                    continue;

                Texture2D tex = Terraria.GameContent.TextureAssets.Npc[npc.type].Value;
                var wing = npc.ModNPC as TitanWing;
                if (wing == null) continue;

                int wingSlot = (int)npc.ai[1];
                bool isRight = wingSlot >= 3;

                // Read frame from the wing's own randomized animation
                int frameW = 224, frameH = 228;
                int frame = wing.AnimFrame;

                Rectangle sourceRect = new Rectangle(frame * frameW, 0, frameW, frameH);
                Vector2 origin = new Vector2(frameW / 2f, frameH / 2f);
                Vector2 drawPos = npc.Center - Main.screenPosition;
                SpriteEffects effects = isRight ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                float drawRot = isRight ? -npc.rotation : npc.rotation;

                Color drawColor = Lighting.GetColor((int)(npc.Center.X / 16f), (int)(npc.Center.Y / 16f));
                spriteBatch.Draw(tex, drawPos, sourceRect, drawColor, drawRot, origin, 1f, effects, 0f);
            }

            // Draw body on top of wings
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != titanBodyType)
                    continue;

                Texture2D tex = Terraria.GameContent.TextureAssets.Npc[npc.type].Value;
                Vector2 baseDrawPos = npc.Center - Main.screenPosition;

                // Horizontal sprite sheet: 3 frames, each 484x624
                var body = npc.ModNPC as TitanBody;
                int frame = body != null ? body.BodyAnimFrame : 0;
                int frameW = 484, frameH = 624;
                Rectangle sourceRect = new Rectangle(frame * frameW, 0, frameW, frameH);
                Vector2 origin = new Vector2(frameW / 2f, frameH / 2f);
                Color drawColor = Lighting.GetColor((int)(npc.Center.X / 16f), (int)(npc.Center.Y / 16f));

                if (body != null && body.IsInFinalStand)
                {
                    float shakeTime = (float)Main.GameUpdateCount;

                    // Draw 5 ghost afterimages at progressively offset positions
                    for (int g = 0; g < 5; g++)
                    {
                        float phase = shakeTime * 0.12f + g * MathHelper.TwoPi / 5f;
                        float shakeAmt = 10f + (float)Math.Sin(shakeTime * 0.07f + g) * 4f;
                        Vector2 ghostOffset = new Vector2(
                            (float)Math.Sin(phase * 1.9f) * shakeAmt,
                            (float)Math.Cos(phase * 1.4f) * shakeAmt);
                        float ghostAlpha = 0.18f + 0.07f * (float)Math.Sin(shakeTime * 0.3f + g);
                        Color ghostColor = new Color(180, 220, 255) * ghostAlpha;
                        spriteBatch.Draw(tex, baseDrawPos + ghostOffset, sourceRect, ghostColor,
                            npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);
                    }

                    // Violent shake on the main draw position
                    Vector2 shake = new Vector2(
                        (float)Math.Sin(shakeTime * 3.7f) * 7f,
                        (float)Math.Cos(shakeTime * 2.9f) * 6f);
                    spriteBatch.Draw(tex, baseDrawPos + shake, sourceRect, drawColor,
                        npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);
                }
                else
                {
                    spriteBatch.Draw(tex, baseDrawPos, sourceRect, drawColor, npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.End();
        }
    }
}
