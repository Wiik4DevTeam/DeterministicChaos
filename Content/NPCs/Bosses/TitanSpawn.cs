using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using DeterministicChaos.Content.Projectiles.Friendly;
using System;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    // TitanSpawn, a fragile enemy that spawns above the Titan during Parkour phase
    // when a player is within 15 tiles. Drifts slowly toward the nearest player.
    // On death, releases a TitanMultiplierOrb to increase the damage multiplier.
    // Persists through EnemyClear but stops spawning once EnemyClear begins.
    // Despawned at the end of the EnemyClear phase.
    public class TitanSpawn : ModNPC
    {
        private const float MOVE_SPEED = 1.5f;      // Max speed toward player
        private const float ACCEL = 0.04f;           // Acceleration toward player
        private const float BOB_AMPLITUDE = 0.3f;    // Vertical bobbing pixels per tick
        private const float BOB_RATE = 0.04f;        // Bobbing frequency

        private float bobOffset = 0f;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
        }

        public override void SetDefaults()
        {
            NPC.width = 24;
            NPC.height = 24;
            NPC.damage = 30;
            NPC.defense = 2;
            NPC.lifeMax = 600;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0.6f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.npcSlots = 0f;
            NPC.value = 0f;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.lifeMax = (int)(NPC.lifeMax * balance);
        }

        public override void AI()
        {
            // Gentle bobbing
            bobOffset += BOB_RATE;
            NPC.position.Y += (float)Math.Sin(bobOffset) * BOB_AMPLITUDE;

            // Orient toward nearest player
            Player target = Main.player[NPC.target];
            if (!target.active || target.dead)
                NPC.TargetClosest(true);
            target = Main.player[NPC.target];

            if (target.active && !target.dead)
            {
                Vector2 dir = target.Center - NPC.Center;
                if (dir.LengthSquared() > 0)
                {
                    dir.Normalize();
                    NPC.velocity = Vector2.Lerp(NPC.velocity, dir * MOVE_SPEED, ACCEL);
                }

                // Face movement direction
                NPC.spriteDirection = NPC.velocity.X > 0 ? 1 : -1;
            }

            // Slow spin
            NPC.rotation += NPC.velocity.X * 0.02f;

            // Faint light
            Lighting.AddLight(NPC.Center, 0.3f, 0.25f, 0.1f);
        }

        public override void OnKill()
        {
            // Drop a multiplier orb on death
            TitanMultiplierOrb.SpawnOrbs(NPC.GetSource_Death(), NPC.Center, 1);
        }

        public override bool CheckActive() => false; // Managed by TitanBody despawn

        // Spawns a TitanSpawn above the Titan's position.
        public static void SpawnAboveTitan(IEntitySource source, Vector2 titanCenter)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // Spawn 8-14 tiles above the titan with slight horizontal spread
            float offsetX = Main.rand.NextFloat(-80f, 80f);
            float offsetY = -Main.rand.NextFloat(8f * 16f, 14f * 16f);
            Vector2 spawnPos = titanCenter + new Vector2(offsetX, offsetY);

            int idx = NPC.NewNPC(source, (int)spawnPos.X, (int)spawnPos.Y,
                ModContent.NPCType<TitanSpawn>());

            if (idx >= 0 && idx < Main.maxNPCs)
            {
                Main.npc[idx].velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), 0.5f);
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
            }
        }
    }
}
