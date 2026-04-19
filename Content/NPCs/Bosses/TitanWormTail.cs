using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    // Titan Worm tail, invulnerable visual cap. Despawns when all body segments are gone.
    public class TitanWormTail : ModNPC
    {
        public const float HALF_HEIGHT = 21f; // 42 / 2

        private int ParentIndex => (int)NPC.ai[0];
        private int HeadIndex => (int)NPC.ai[1];

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NoMultiplayerSmoothingByType[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 28;
            NPC.height = 24;
            NPC.damage = 50;
            NPC.defense = 15;
            NPC.lifeMax = 1000;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.npcSlots = 0f;
            NPC.dontTakeDamage = true;
            NPC.immortal = true;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.damage = 50;
        }

        public override void AI()
        {
            // Validate head, if head is dead, despawn
            NPC head = Main.npc[HeadIndex];
            if (!head.active || head.type != ModContent.NPCType<TitanWormHead>())
            {
                NPC.active = false;
                return;
            }

            // Validate parent, if parent died, wait for OnKill reconnection
            // (don't self-destruct; the server will update ai[0] to skip the dead segment)
            NPC parent = Main.npc[ParentIndex];
            if (!parent.active)
                return;

            // Follow parent at proper distance (with 3px overlap)
            float parentHalfH = parent.type == ModContent.NPCType<TitanWormBody>() ? TitanWormBody.HALF_HEIGHT : TitanWormHead.HALF_HEIGHT;
            float spacing = TitanWormHead.GetSpacing(parentHalfH, HALF_HEIGHT);

            Vector2 toParent = parent.Center - NPC.Center;
            float dist = toParent.Length();
            if (dist > 1f)
            {
                toParent.Normalize();
                NPC.Center = parent.Center - toParent * spacing;
                NPC.rotation = toParent.ToRotation() + MathHelper.PiOver2; // Sprite faces up
            }
            else
            {
                float angle = parent.rotation - MathHelper.PiOver2 + MathHelper.Pi;
                NPC.Center = parent.Center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * spacing;
                NPC.rotation = parent.rotation;
            }

            NPC.velocity = Vector2.Zero;
        }

        public override bool CheckActive() => false;
    }
}
