using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    // The eye centered on a TitanBlob. Does NOT rotate, always faces upright.
    public class TitanEye : ModNPC
    {
        // ai[0] = parent TitanBlob whoAmI

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NoMultiplayerSmoothingByType[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 30;
            NPC.height = 30;
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
            int parentIdx = (int)NPC.ai[0];

            // Despawn if parent blob is gone
            if (parentIdx < 0 || parentIdx >= Main.maxNPCs
                || !Main.npc[parentIdx].active
                || Main.npc[parentIdx].type != ModContent.NPCType<TitanBlob>())
            {
                NPC.active = false;
                return;
            }

            // Stick to the blob's center, offset upward toward the face
            NPC.Center = Main.npc[parentIdx].Center + new Vector2(0f, -8f);
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
