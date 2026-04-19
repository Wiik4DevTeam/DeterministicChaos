using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    // Titan Worm body segment, independently killable.
    // On death, reconnects the child segment to this segment's parent to shorten the worm.
    public class TitanWormBody : ModNPC
    {
        public const float HALF_HEIGHT = 24f; // 48 / 2

        private int ParentIndex => (int)NPC.ai[0];
        private int HeadIndex => (int)NPC.ai[1];

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NoMultiplayerSmoothingByType[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 36;
            NPC.height = 28;
            NPC.damage = 55;
            NPC.defense = 10;
            NPC.lifeMax = 40;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.npcSlots = 0f;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            if (Main.masterMode)
                NPC.lifeMax = (int)(NPC.lifeMax * 2f);
            else if (Main.expertMode)
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);

            NPC.lifeMax = (int)(NPC.lifeMax * balance);
            NPC.damage = 55;
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
            float parentHalfH = GetParentHalfHeight(parent);
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
                // Overlapping, place behind parent based on its facing
                float angle = parent.rotation - MathHelper.PiOver2 + MathHelper.Pi;
                NPC.Center = parent.Center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * spacing;
                NPC.rotation = parent.rotation;
            }

            NPC.velocity = Vector2.Zero;
        }

        public override void OnKill()
        {
            // Reconnect: find the child segment (its ai[0] == this NPC's whoAmI)
            // and point it to this segment's parent instead
            int bodyType = ModContent.NPCType<TitanWormBody>();
            int tailType = ModContent.NPCType<TitanWormTail>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && (n.type == bodyType || n.type == tailType) && (int)n.ai[0] == NPC.whoAmI)
                {
                    n.ai[0] = NPC.ai[0]; // Child now follows this segment's parent
                    n.netUpdate = true;
                    break; // Only one child per segment
                }
            }
        }

        public override bool CheckActive() => false;

        private float GetParentHalfHeight(NPC parent)
        {
            if (parent.type == ModContent.NPCType<TitanWormHead>()) return TitanWormHead.HALF_HEIGHT;
            if (parent.type == ModContent.NPCType<TitanWormBody>()) return HALF_HEIGHT;
            return HALF_HEIGHT;
        }
    }
}
