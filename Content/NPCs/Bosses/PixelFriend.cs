using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    public class PixelFriend : ModNPC
    {
        private const int FrameWidth = 32;
        private const int FrameHeight = 32;
        private const int FrameCount = 1;
        
        private int animTick;
        private int animFrame;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = FrameCount;
        }

        public override void SetDefaults()
        {
            NPC.width = 24;
            NPC.height = 24;
            NPC.damage = 21;
            NPC.defense = 0;
            NPC.lifeMax = 2;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0.5f;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.aiStyle = -1;
            NPC.value = 0;
            NPC.scale = 1.5f;
        }

        public override void AI()
        {
            NPC.TargetClosest(true);
            Player target = Main.player[NPC.target];
            
            if (!target.active || target.dead)
            {
                return;
            }
            
            // Simple pathfinding, move towards player
            float speed = 3f;
            float jumpSpeed = 8f;
            
            // Horizontal movement towards player
            if (target.Center.X < NPC.Center.X)
            {
                NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, -speed, 0.1f);
                NPC.spriteDirection = -1;
            }
            else
            {
                NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, speed, 0.1f);
                NPC.spriteDirection = 1;
            }
            
            // Jump if on ground and need to go up or blocked
            bool onGround = NPC.velocity.Y == 0f;
            
            if (onGround)
            {
                // Check if blocked by a tile ahead
                int tileX = (int)(NPC.Center.X / 16f) + NPC.spriteDirection;
                int tileY = (int)(NPC.Bottom.Y / 16f);
                
                bool blocked = Main.tile[tileX, tileY].HasTile && Main.tileSolid[Main.tile[tileX, tileY].TileType];
                bool needsToJumpUp = target.Center.Y < NPC.Center.Y - 48f;
                
                if (blocked || needsToJumpUp)
                {
                    NPC.velocity.Y = -jumpSpeed;
                }
            }
            
            // Apply gravity is handled by noGravity = false
        }

        public override void FindFrame(int frameHeight)
        {
            animTick++;
            if (animTick >= 6)
            {
                animTick = 0;
                animFrame++;
                if (animFrame >= FrameCount)
                    animFrame = 0;
            }
            
            NPC.frame.Y = animFrame * frameHeight;
        }

        public override Color? GetAlpha(Color drawColor)
        {
            return Color.White;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos;
            Rectangle sourceRect = NPC.frame;
            Vector2 origin = sourceRect.Size() / 2f;
            SpriteEffects effects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            
            spriteBatch.Draw(texture, drawPos, sourceRect, Color.White, NPC.rotation, origin, NPC.scale, effects, 0f);
            
            return false;
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            return 0f; // Only spawned by ERAM
        }
        
        public override void OnKill()
        {
            // Drop a heart when killed
            Item.NewItem(NPC.GetSource_Death(), NPC.getRect(), ItemID.Heart);
            Item.NewItem(NPC.GetSource_Death(), NPC.getRect(), ItemID.Heart);
            Item.NewItem(NPC.GetSource_Death(), NPC.getRect(), ItemID.Heart);
            Item.NewItem(NPC.GetSource_Death(), NPC.getRect(), ItemID.Heart);
        }
    }
}
