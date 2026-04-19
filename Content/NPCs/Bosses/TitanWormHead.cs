using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using DeterministicChaos.Content.Projectiles.Friendly;
using System;
using System.IO;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    // Titan Worm head, spawns during EnemyClear phase.
    // Invulnerable while body segments exist. On death, drops 5 multiplier orbs.
    public class TitanWormHead : ModNPC
    {
        public const int BODY_SEGMENT_COUNT = 5;
        public const float HALF_HEIGHT = 29f;  // 58 / 2
        private const float OVERLAP = 11f;      // 3 + 8 extra to tighten gaps

        private const float STRAIGHT_SPEED = 6f;  // Speed when moving straight
        private const float TURN_SPEED_MIN = 2.5f; // Speed when turning hard
        private const float TURN_SPEED = 0.035f;

        // Overshoot system, worm flies straight past the player, then waits before turning
        private const float OVERSHOOT_COOLDOWN = 0.8f * 60f; // 0.8 seconds in ticks (48 ticks)
        private const float PLAYER_PROXIMITY = 80f;          // Distance to count as "reached" the player
        private float overshootTimer = 0f;                    // Ticks remaining before turning is allowed
        private bool hasPassedPlayer = false;                 // Whether we've reached the player this charge

        // ── Multiplayer sync ──────────────────────────────────────────
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(overshootTimer);
            writer.Write(hasPassedPlayer);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            overshootTimer = reader.ReadSingle();
            hasPassedPlayer = reader.ReadBoolean();
        }

        // Computes center-to-center spacing between two segments given their half-heights.
        public static float GetSpacing(float parentHalfHeight, float childHalfHeight)
        {
            return parentHalfHeight + childHalfHeight - OVERLAP;
        }

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NoMultiplayerSmoothingByType[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 36;
            NPC.height = 34;
            NPC.damage = 70;
            NPC.defense = 10;
            NPC.lifeMax = 500;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.npcSlots = 0.5f;
            NPC.dontTakeDamage = true; // Invulnerable while body segments exist
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            if (Main.masterMode)
                NPC.lifeMax = (int)(NPC.lifeMax * 2f);
            else if (Main.expertMode)
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);

            NPC.lifeMax = (int)(NPC.lifeMax * balance);

            // Prevent Terraria from multiplying damage, keep our base value
            NPC.damage = 70;
        }

        public override void AI()
        {
            NPC.TargetClosest(true);

            // Check if any body segments still exist for this worm
            bool hasBody = false;
            int bodyType = ModContent.NPCType<TitanWormBody>();
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && n.type == bodyType && (int)n.ai[1] == NPC.whoAmI)
                {
                    hasBody = true;
                    break;
                }
            }

            NPC.dontTakeDamage = hasBody;

            // Die immediately when no body segments remain (server/singleplayer only)
            if (!hasBody && Main.netMode != NetmodeID.MultiplayerClient)
            {
                // Despawn tail first
                int tailType = ModContent.NPCType<TitanWormTail>();
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC n = Main.npc[i];
                    if (n.active && n.type == tailType && (int)n.ai[1] == NPC.whoAmI)
                    {
                        n.active = false;
                        if (Main.netMode == NetmodeID.Server)
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, i);
                        break;
                    }
                }

                // Kill the head (triggers OnKill → spawns orbs)
                NPC.life = 0;
                NPC.checkDead();
                return;
            }

            // Worm head movement, charge at player, overshoot, then turn back
            if (NPC.target >= 0 && NPC.target < Main.maxPlayers)
            {
                Player target = Main.player[NPC.target];
                if (target.active && !target.dead)
                {
                    Vector2 direction = target.Center - NPC.Center;
                    float distToPlayer = direction.Length();

                    // Detect when we pass close to the player (or hit them)
                    if (!hasPassedPlayer && distToPlayer < PLAYER_PROXIMITY)
                    {
                        hasPassedPlayer = true;
                        overshootTimer = OVERSHOOT_COOLDOWN;
                    }

                    // Count down the overshoot cooldown
                    if (overshootTimer > 0f)
                        overshootTimer--;

                    // Reset once cooldown expires, allow turning again
                    if (hasPassedPlayer && overshootTimer <= 0f)
                        hasPassedPlayer = false;

                    if (direction.LengthSquared() > 0)
                    {
                        float targetAngle = direction.ToRotation();
                        float currentAngle = NPC.velocity.LengthSquared() > 0
                            ? NPC.velocity.ToRotation()
                            : targetAngle;

                        float angleDiff = Math.Abs(MathHelper.WrapAngle(targetAngle - currentAngle));
                        float newAngle;

                        if (overshootTimer > 0f)
                        {
                            // During overshoot: fly straight, no turning
                            newAngle = currentAngle;
                        }
                        else
                        {
                            // Normal turning toward the player
                            newAngle = currentAngle + MathHelper.WrapAngle(targetAngle - currentAngle) * TURN_SPEED;
                        }

                        // Faster when going straight, slower when turning hard
                        float turnFactor = MathHelper.Clamp(angleDiff / MathHelper.Pi, 0f, 1f);
                        float speed = overshootTimer > 0f
                            ? STRAIGHT_SPEED  // Full speed during overshoot
                            : MathHelper.Lerp(STRAIGHT_SPEED, TURN_SPEED_MIN, turnFactor);

                        NPC.velocity = new Vector2((float)Math.Cos(newAngle), (float)Math.Sin(newAngle)) * speed;
                    }
                }
            }

            NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2; // Sprite faces up
        }

        public override void OnKill()
        {
            // Spawn multiplier orbs (one per original body segment)
            TitanMultiplierOrb.SpawnOrbs(NPC.GetSource_Death(), NPC.Center, BODY_SEGMENT_COUNT);
        }

        public override bool CheckActive() => false;

        // Spawns a complete worm (head + body segments + tail) at the given position.
        public static void SpawnWorm(IEntitySource source, Vector2 position)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int headIdx = NPC.NewNPC(source, (int)position.X, (int)position.Y, ModContent.NPCType<TitanWormHead>());
            if (headIdx < 0 || headIdx >= Main.maxNPCs) return;

            int previousIdx = headIdx;
            Vector2 prevCenter = position;
            float prevHalfH = HALF_HEIGHT;

            // Spawn body segments in a line behind the head
            for (int i = 0; i < BODY_SEGMENT_COUNT; i++)
            {
                float spacing = GetSpacing(prevHalfH, TitanWormBody.HALF_HEIGHT);
                Vector2 segPos = prevCenter - new Vector2(spacing, 0);
                int bodyIdx = NPC.NewNPC(source, (int)segPos.X, (int)segPos.Y, ModContent.NPCType<TitanWormBody>());
                if (bodyIdx < 0 || bodyIdx >= Main.maxNPCs) continue;

                Main.npc[bodyIdx].ai[0] = previousIdx; // Parent segment
                Main.npc[bodyIdx].ai[1] = headIdx;      // Head reference

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, bodyIdx);

                prevCenter = segPos;
                previousIdx = bodyIdx;
                prevHalfH = TitanWormBody.HALF_HEIGHT;
            }

            // Spawn tail
            float tailSpacing = GetSpacing(prevHalfH, TitanWormTail.HALF_HEIGHT);
            Vector2 tailPos = prevCenter - new Vector2(tailSpacing, 0);
            int tailIdx = NPC.NewNPC(source, (int)tailPos.X, (int)tailPos.Y, ModContent.NPCType<TitanWormTail>());
            if (tailIdx >= 0 && tailIdx < Main.maxNPCs)
            {
                Main.npc[tailIdx].ai[0] = previousIdx;
                Main.npc[tailIdx].ai[1] = headIdx;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, tailIdx);
            }

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, headIdx);
        }
    }
}
