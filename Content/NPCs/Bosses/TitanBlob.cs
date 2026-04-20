using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Terraria.GameContent;
using DeterministicChaos.Content.VFX;
using System;
using DeterministicChaos.Content.Items.Accessories;
using DeterministicChaos.Content.Items.BossBags;
using DeterministicChaos.Content.Items.BossSummons;
using DeterministicChaos.Content.Items.Consumables;
using DeterministicChaos.Content.Items.DamageClasses;
using DeterministicChaos.Content.Items.Globals;
using DeterministicChaos.Content.Items.Materials;
using DeterministicChaos.Content.Items.Placeable;
using DeterministicChaos.Content.Items.Rarities;
using DeterministicChaos.Content.Items.Weapons;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    // Titan Blob, invulnerable hazard that drifts across the screen during EnemyClear.
    // Rotates slowly at 60°/sec. Spawns a TitanEye and TitanPupil on its center.
    public class TitanBlob : ModNPC
    {
        private const float ROTATION_SPEED = MathHelper.Pi / 180f; // 60°/sec → ~1.047 rad/sec ÷ 60 ticks
        private const float MOVE_SPEED = 3f;
        private const float MIN_SPEED = 0.8f;            // Slowest speed when right next to a player
        private const float SLOWDOWN_RANGE = 300f;        // X distance within which slowdown starts
        private const float Y_TOLERANCE = 60f;            // Max Y difference to count as "near" a player
        private const float DESPAWN_DISTANCE = 1500f;

        // ai[0] = horizontal direction (-1 or 1)
        // ai[1] = 1 if eye/pupil children have been spawned
        // ai[2] = eye NPC whoAmI
        // ai[3] = pupil NPC whoAmI

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NoMultiplayerSmoothingByType[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 34;
            NPC.height = 34;
            NPC.damage = 70;
            NPC.defense = 0;
            NPC.lifeMax = 1;
            NPC.dontTakeDamage = true;
            NPC.immortal = true;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.npcSlots = 0f;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.damage = 70;
        }

        public override void AI()
        {
            float direction = NPC.ai[0]; // -1 or 1

            // Determine speed, slow down when approaching a nearby player
            float speed = MOVE_SPEED;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;

                float dx = p.Center.X - NPC.Center.X;
                float dy = Math.Abs(p.Center.Y - NPC.Center.Y);

                // Only slow down if within Y tolerance and moving toward the player
                if (dy > Y_TOLERANCE) continue;
                bool movingToward = (direction > 0 && dx > 0) || (direction < 0 && dx < 0);
                if (!movingToward) continue;

                float absDx = Math.Abs(dx);
                if (absDx < SLOWDOWN_RANGE)
                {
                    float factor = absDx / SLOWDOWN_RANGE; // 0 at player, 1 at range edge
                    float playerSpeed = MathHelper.Lerp(MIN_SPEED, MOVE_SPEED, factor);
                    if (playerSpeed < speed)
                        speed = playerSpeed;
                }
            }

            // Move horizontally
            NPC.velocity = new Vector2(direction * speed, 0f);

            // Rotate at 60°/sec
            NPC.rotation += ROTATION_SPEED;

            // Spawn eye and pupil children on first tick (server/singleplayer only)
            if (NPC.ai[1] != 1f && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int eyeIdx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y,
                    ModContent.NPCType<TitanEye>());

                if (eyeIdx >= 0 && eyeIdx < Main.maxNPCs)
                {
                    Main.npc[eyeIdx].ai[0] = NPC.whoAmI;
                    NPC.ai[2] = eyeIdx;

                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, eyeIdx);

                    int pupilIdx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y,
                        ModContent.NPCType<TitanPupil>());

                    if (pupilIdx >= 0 && pupilIdx < Main.maxNPCs)
                    {
                        Main.npc[pupilIdx].ai[0] = eyeIdx;
                        NPC.ai[3] = pupilIdx;

                        if (Main.netMode == NetmodeID.Server)
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, pupilIdx);
                    }
                }

                NPC.ai[1] = 1f;
                NPC.netUpdate = true;
            }

            // Despawn when far from ALL players
            bool anyNearby = false;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead)
                {
                    if (Math.Abs(NPC.Center.X - p.Center.X) < DESPAWN_DISTANCE)
                    {
                        anyNearby = true;
                        break;
                    }
                }
            }

            if (!anyNearby)
            {
                DespawnWithChildren();
            }
        }

        // Removes this blob and its eye/pupil children.
        public void DespawnWithChildren()
        {
            // Despawn pupil
            int pupilIdx = (int)NPC.ai[3];
            if (pupilIdx >= 0 && pupilIdx < Main.maxNPCs && Main.npc[pupilIdx].active
                && Main.npc[pupilIdx].type == ModContent.NPCType<TitanPupil>())
            {
                Main.npc[pupilIdx].active = false;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, pupilIdx);
            }

            // Despawn eye
            int eyeIdx = (int)NPC.ai[2];
            if (eyeIdx >= 0 && eyeIdx < Main.maxNPCs && Main.npc[eyeIdx].active
                && Main.npc[eyeIdx].type == ModContent.NPCType<TitanEye>())
            {
                Main.npc[eyeIdx].active = false;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, eyeIdx);
            }

            NPC.active = false;
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
        }

        public override bool CheckActive() => false;

        public override void DrawBehind(int index)
        {
            // Draw blobs on top of other NPCs (including TitanStar)
            Main.instance.DrawCacheNPCsOverPlayers.Add(index);
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            // Draw eye on top of blob
            int eyeIdx = (int)NPC.ai[2];
            if (eyeIdx >= 0 && eyeIdx < Main.maxNPCs)
            {
                NPC eye = Main.npc[eyeIdx];
                if (eye.active && eye.type == ModContent.NPCType<TitanEye>())
                {
                    Texture2D eyeTex = TextureAssets.Npc[eye.type].Value;
                    Vector2 eyeOrigin = new Vector2(eyeTex.Width / 2f, eyeTex.Height / 2f);
                    Vector2 eyeDrawPos = eye.Center - screenPos;
                    spriteBatch.Draw(eyeTex, eyeDrawPos, null, drawColor, 0f, eyeOrigin, eye.scale, SpriteEffects.None, 0f);
                }
            }

            // Draw pupil on top of eye
            int pupilIdx = (int)NPC.ai[3];
            if (pupilIdx >= 0 && pupilIdx < Main.maxNPCs)
            {
                NPC pupil = Main.npc[pupilIdx];
                if (pupil.active && pupil.type == ModContent.NPCType<TitanPupil>())
                {
                    Texture2D pupilTex = TextureAssets.Npc[pupil.type].Value;
                    Vector2 pupilOrigin = new Vector2(pupilTex.Width / 2f, pupilTex.Height / 2f);
                    Vector2 pupilDrawPos = pupil.Center - screenPos;
                    spriteBatch.Draw(pupilTex, pupilDrawPos, null, drawColor, 0f, pupilOrigin, pupil.scale, SpriteEffects.None, 0f);
                }
            }
        }

        // Spawns a TitanBlob offscreen from a random active player, traveling across the screen.
        public static void SpawnBlob(IEntitySource source)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // Pick a random active player to spawn near
            Player player = null;
            int attempts = 0;
            while (attempts < 50)
            {
                int idx = Main.rand.Next(Main.maxPlayers);
                if (Main.player[idx].active && !Main.player[idx].dead)
                {
                    player = Main.player[idx];
                    break;
                }
                attempts++;
            }

            // Fallback: find first active player
            if (player == null)
            {
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    if (Main.player[i].active && !Main.player[i].dead)
                    {
                        player = Main.player[i];
                        break;
                    }
                }
            }

            if (player == null) return;

            bool fromLeft = Main.rand.NextBool();

            // Use tower geometry for spawn position instead of Main.screenWidth (which is 0 on dedicated server)
            float spawnX;
            if (TitanSpawnCutscene.TowerPlaced)
            {
                var towerTop = TitanSpawnCutscene.TowerTopLeft;
                float towerLeftX = towerTop.X * 16f;
                float towerRightX = (towerTop.X + TitanSpawnCutscene.TOWER_WIDTH) * 16f;
                spawnX = fromLeft ? towerLeftX - 100f : towerRightX + 100f;
            }
            else
            {
                // Fallback if tower not placed (shouldn't happen)
                spawnX = player.Center.X + (fromLeft ? -1200f : 1200f);
            }
            float spawnY = player.Center.Y - Main.rand.Next(3, 23) * 16f; // 3–22 tiles above player
            float direction = fromLeft ? 1f : -1f;

            int npcIdx = NPC.NewNPC(source, (int)spawnX, (int)spawnY, ModContent.NPCType<TitanBlob>());
            if (npcIdx >= 0 && npcIdx < Main.maxNPCs)
            {
                Main.npc[npcIdx].ai[0] = direction;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIdx);
            }
        }

        // Spawns a blob that may also appear slightly below the player (used outside Parkour phase).
        public static void SpawnBlobNearPlayer(IEntitySource source)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Player player = null;
            int attempts = 0;
            while (attempts < 50)
            {
                int idx = Main.rand.Next(Main.maxPlayers);
                if (Main.player[idx].active && !Main.player[idx].dead)
                {
                    player = Main.player[idx];
                    break;
                }
                attempts++;
            }
            if (player == null)
            {
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    if (Main.player[i].active && !Main.player[i].dead) { player = Main.player[i]; break; }
                }
            }
            if (player == null) return;

            bool fromLeft = Main.rand.NextBool();
            float spawnX;
            if (TitanSpawnCutscene.TowerPlaced)
            {
                var towerTop = TitanSpawnCutscene.TowerTopLeft;
                float towerLeftX  = towerTop.X * 16f;
                float towerRightX = (towerTop.X + TitanSpawnCutscene.TOWER_WIDTH) * 16f;
                spawnX = fromLeft ? towerLeftX - 100f : towerRightX + 100f;
            }
            else
            {
                spawnX = player.Center.X + (fromLeft ? -1200f : 1200f);
            }

            // 70% chance above player (3–22 tiles), 30% chance slightly below (1–6 tiles)
            float spawnY;
            if (Main.rand.NextFloat() < 0.3f)
                spawnY = player.Center.Y + Main.rand.Next(1, 7) * 16f;
            else
                spawnY = player.Center.Y - Main.rand.Next(3, 23) * 16f;

            float direction = fromLeft ? 1f : -1f;

            int npcIdx = NPC.NewNPC(source, (int)spawnX, (int)spawnY, ModContent.NPCType<TitanBlob>());
            if (npcIdx >= 0 && npcIdx < Main.maxNPCs)
            {
                Main.npc[npcIdx].ai[0] = direction;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIdx);
            }
        }
    }
}
