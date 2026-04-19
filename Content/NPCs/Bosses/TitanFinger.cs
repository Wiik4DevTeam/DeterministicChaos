using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    public class TitanFinger : ModNPC
    {
        public const int FRAME_WIDTH = 166;
        public const int FRAME_HEIGHT = 48;
        public const int FRAME_COUNT = 8;
        private const float ANIM_FPS = 10f;

        // Extra horizontal offset in hand-local space. Positive = outward from hand center.
        public static float FINGER_OFFSET_X = -FRAME_WIDTH * 0.0f;

        // Extra vertical offset in hand-local space. Negative = upward (toward fingertips).
        public static float FINGER_OFFSET_Y = -FRAME_HEIGHT * 0.5f;

        private float animTimer = 0f;
        private int animFrame = 0;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1; // We handle frames manually via source rect
            NPCID.Sets.NoMultiplayerSmoothingByType[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 40;
            NPC.height = 20;
            NPC.damage = 0;
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

        public override void AI()
        {
            int handIdx = (int)NPC.ai[0];
            int fingerSlot = (int)NPC.ai[1];

            // Despawn if parent hand is gone
            if (handIdx < 0 || handIdx >= Main.maxNPCs
                || !Main.npc[handIdx].active
                || Main.npc[handIdx].type != ModContent.NPCType<TitanHand>())
            {
                NPC.active = false;
                return;
            }

            NPC hand = Main.npc[handIdx];
            TitanHand handNpc = hand.ModNPC as TitanHand;
            if (handNpc == null) { NPC.active = false; return; }

            // Hide if this finger has been launched
            if (fingerSlot >= 0 && fingerSlot < TitanHand.FINGER_COUNT && handNpc.FingerHidden[fingerSlot])
            {
                NPC.Center = hand.Center; // Keep near hand for safety
                return;
            }

            bool isLeft = handNpc.IsLeft;
            float handRotation = hand.rotation;

            // Get the anchor offset for this finger (in hand-local sprite space, from hand center)
            Vector2 anchorOffset = TitanHand.FingerOffsets[fingerSlot];

            // Apply the tunable extra offsets (in hand-local space)
            Vector2 extraOffset = new Vector2(FINGER_OFFSET_X, FINGER_OFFSET_Y);
            if (isLeft) extraOffset.X = -extraOffset.X;
            anchorOffset += extraOffset;

            if (isLeft) anchorOffset.X = -anchorOffset.X;

            // Rotate the anchor offset by the hand's rotation to get world position
            Vector2 worldAnchor = hand.Center + anchorOffset.RotatedBy(handRotation);

            // Finger center offset from its anchor (in sprite-local coords, before rotation)
            // The anchor is the bottom-left of the finger; center is at (width/2, -height/2)
            Vector2 centerFromAnchor = isLeft
                ? new Vector2(-FRAME_WIDTH / 2f, -FRAME_HEIGHT / 2f)
                : new Vector2(FRAME_WIDTH / 2f, -FRAME_HEIGHT / 2f);

            // Rotate to get world offset from anchor to finger center
            NPC.Center = worldAnchor + centerFromAnchor.RotatedBy(handRotation);

            NPC.rotation = handRotation;
            NPC.velocity = Vector2.Zero;

            // Animate
            animTimer += ANIM_FPS / 60f;
            if (animTimer >= 1f)
            {
                animTimer -= 1f;
                animFrame = (animFrame + 1) % FRAME_COUNT;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            int handIdx = (int)NPC.ai[0];
            if (handIdx < 0 || handIdx >= Main.maxNPCs || !Main.npc[handIdx].active)
                return false;

            TitanHand handNpc = Main.npc[handIdx].ModNPC as TitanHand;
            if (handNpc == null) return false;

            int fingerSlot = (int)NPC.ai[1];
            if (fingerSlot >= 0 && fingerSlot < TitanHand.FINGER_COUNT && handNpc.FingerHidden[fingerSlot])
                return false;

            Texture2D tex = TextureAssets.Npc[Type].Value;
            Rectangle sourceRect = new Rectangle(animFrame * FRAME_WIDTH, 0, FRAME_WIDTH, FRAME_HEIGHT);
            Vector2 origin = new Vector2(FRAME_WIDTH / 2f, FRAME_HEIGHT / 2f);
            Vector2 drawPos = NPC.Center - screenPos;

            SpriteEffects effects = handNpc.IsLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            spriteBatch.Draw(tex, drawPos, sourceRect, drawColor, NPC.rotation, origin, NPC.scale, effects, 0f);
            return false;
        }

        public override bool CheckActive() => false;

        public override void DrawBehind(int index)
        {
            // Draw fingers above hands and everything else
            Main.instance.DrawCacheNPCsOverPlayers.Add(index);
        }
    }
}
