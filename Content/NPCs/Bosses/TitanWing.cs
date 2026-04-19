using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    // A wing segment that orbits around the TitanBody.
    // ai[0] = parent NPC index (TitanBody)
    // ai[1] = wing slot index (0-5), determines position and rotation
    public class TitanWing : ModNPC
    {
        private const int FRAME_WIDTH = 224;
        private const int FRAME_HEIGHT = 228;
        private const int FRAME_COUNT = 6;
        private const float BASE_ANIM_FPS = 6f;
        private const float FPS_VARIANCE = 1.5f; // +/- random offset per wing

        private float actualFps;
        private float animTimer = 0f;
        public int AnimFrame { get; private set; } = 0;

        // Wing placement config per slot:
        // Slots 0-2 = left side (no mirror), Slots 3-5 = right side (mirrored)
        // Slots 1 & 4 = main wings (straight out, centered)
        // Slots 0, 2, 3, 5 = extra wings (rotated, positioned lower on the body)
        private static readonly float[] WingAngles = new float[]
        {
            // Left side
            -MathHelper.ToRadians(15f),    // Slot 0: slight upward angle
            0f,                             // Slot 1: straight out (main wing)
            MathHelper.ToRadians(15f),      // Slot 2: slight downward angle

            // Right side (mirrored)
            -MathHelper.ToRadians(15f),    // Slot 3: slight upward angle
            0f,                             // Slot 4: straight out (main wing)
            MathHelper.ToRadians(15f),      // Slot 5: slight downward angle
        };

        // Y offset per slot, extra wings are placed lower on the body
        private static readonly float[] WingYOffsets = new float[]
        {
            60f,    // Slot 0: lower
            0f,     // Slot 1: centered (main wing)
            120f,   // Slot 2: even lower
            60f,    // Slot 3: lower
            0f,     // Slot 4: centered (main wing)
            120f,   // Slot 5: even lower
        };

        // Distance from body center for each wing (5 tiles = 80px further out)
        private const float WING_DISTANCE = 140f;

        private int ParentIndex => (int)NPC.ai[0];
        private int WingSlot => (int)NPC.ai[1];
        private bool IsRightSide => WingSlot >= 3;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1; // We handle frames manually in PreDraw
            NPCID.Sets.NoMultiplayerSmoothingByType[Type] = true;

            // Hide name on hover
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = false;

            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.OnFire] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 40;
            NPC.height = 40;
            NPC.damage = 0;
            NPC.defense = 20;
            NPC.lifeMax = 8000;
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

        public override void AI()
        {
            // Initialize random FPS on first tick
            if (actualFps == 0f)
                actualFps = BASE_ANIM_FPS + Main.rand.NextFloat(-FPS_VARIANCE, FPS_VARIANCE);

            // Validate parent
            NPC parent = Main.npc[ParentIndex];
            if (!parent.active || parent.type != ModContent.NPCType<TitanBody>())
            {
                NPC.active = false;
                return;
            }

            // Animate at per-wing randomized FPS
            animTimer += 1f / 60f;
            float frameDuration = 1f / actualFps;
            if (animTimer >= frameDuration)
            {
                animTimer -= frameDuration;
                AnimFrame = (AnimFrame + 1) % FRAME_COUNT;
            }

            // Position relative to parent body
            float angle = WingAngles[WingSlot];
            float yOffset = WingYOffsets[WingSlot];

            // Left side wings extend to the left (PI), right side to the right (0)
            float baseAngle = IsRightSide ? 0f : MathHelper.Pi;
            float finalAngle = baseAngle + angle;

            Vector2 offset = new Vector2(
                (float)Math.Cos(finalAngle) * WING_DISTANCE,
                (float)Math.Sin(finalAngle) * WING_DISTANCE + yOffset
            );

            NPC.Center = parent.Center + offset;
            NPC.velocity = Vector2.Zero;

            // Store the visual rotation for drawing
            NPC.rotation = angle;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            // Skip normal NPC layer draw, wings are drawn behind tiles by TitanSpawnCutscene
            return false;
        }

        public override bool CheckActive()
        {
            // Don't despawn naturally, tied to parent
            return false;
        }
    }
}
