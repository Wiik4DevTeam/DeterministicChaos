using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    // The pupil inside a TitanEye. Offsets up to 1 pixel toward the nearest player
    // to create a "looking at you" effect.
    public class TitanPupil : ModNPC
    {
        // ai[0] = parent TitanEye whoAmI
        private const float MAX_OFFSET = 5f;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NoMultiplayerSmoothingByType[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 10;
            NPC.height = 10;
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
            int eyeIdx = (int)NPC.ai[0];

            // Despawn if parent eye is gone
            if (eyeIdx < 0 || eyeIdx >= Main.maxNPCs
                || !Main.npc[eyeIdx].active
                || Main.npc[eyeIdx].type != ModContent.NPCType<TitanEye>())
            {
                NPC.active = false;
                return;
            }

            Vector2 eyeCenter = Main.npc[eyeIdx].Center;

            // Offset up to 1 pixel toward the nearest player
            NPC.TargetClosest(false);
            Vector2 offset = Vector2.Zero;

            if (NPC.target >= 0 && NPC.target < Main.maxPlayers)
            {
                Player target = Main.player[NPC.target];
                if (target.active && !target.dead)
                {
                    Vector2 toPlayer = target.Center - eyeCenter;
                    if (toPlayer.LengthSquared() > 0)
                    {
                        toPlayer.Normalize();
                        offset = toPlayer * MAX_OFFSET;
                    }
                }
            }

            NPC.Center = eyeCenter + offset;
            NPC.rotation = 0f;
            NPC.velocity = Vector2.Zero;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            // Drawn by TitanBlob.PostDraw to ensure correct layering
            return false;
        }

        public override bool CheckActive() => false;
    }
}
