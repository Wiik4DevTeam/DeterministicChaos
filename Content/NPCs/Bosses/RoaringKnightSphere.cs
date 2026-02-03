using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Systems;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    public class RoaringKnightSphere : ModNPC
    {
        private const int FrameW = 20;
        private const int FrameH = 20;
        private const int AnimFrameCount = 5; // 5 frames in column 0
        
        // Pending damage to transfer to parent (accumulated and applied in AI)
        private int pendingDamage = 0;
        private bool pendingCrit = false;
        
        private int animTick;
        private int animRow;
        private const int animTicksPerFrame = 6;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
        }

        public override void SetDefaults()
        {
            NPC.width = 34;
            NPC.height = 34;
            NPC.scale = 4.8f;

            NPC.damage = 0;
            NPC.defense = -25;
            NPC.lifeMax = 999999;

            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.dontCountMe = true;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;

            NPC.ShowNameOnHover = false;
            NPC.HitSound = SoundID.NPCHit53;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        // Gets the parent Roaring Knight NPC
        private NPC Parent
        {
            get
            {
                int idx = (int)NPC.ai[0];
                if (idx < 0 || idx >= Main.maxNPCs)
                    return null;

                return Main.npc[idx];
            }
        }

        // Sphere follows parent knight and handles visibility
        public override void AI()
        {
            NPC parent = Parent;

            if (parent == null || !parent.active || parent.type != ModContent.NPCType<RoaringKnight>())
            {
                NPC.active = false;
                return;
            }

            bool inMajorPhase = parent.ai[0] == 3f;

            if (!inMajorPhase)
            {
                Vector2 coreOffset = new Vector2(0f, -10f);
                NPC.Center = parent.Center + coreOffset;
                NPC.velocity = Vector2.Zero;
            }

            NPC.timeLeft = 60;

            if (NPC.alpha <= 0)
                Lighting.AddLight(NPC.Center, 0.9f, 0.75f, 0.35f);
                
            // Apply pending damage to parent (from client sync)
            if (pendingDamage > 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                if (parent != null && parent.active)
                {
                    parent.life -= pendingDamage;
                    if (parent.life <= 0)
                    {
                        parent.life = 0;
                        // Properly kill the parent NPC
                        parent.HitEffect(0, pendingDamage);
                        parent.NPCLoot();
                        parent.active = false;
                        
                        // Also kill the sphere
                        NPC.active = false;
                        
                        // Disable the background
                        RoaringKnightBackgroundSystem.ShowBackground = false;
                    }
                    parent.netUpdate = true;
                }
                pendingDamage = 0;
            }
        }

        // Capture damage from projectiles
        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            TransferDamageToParent(damageDone, hit.Crit);
        }
        
        // Capture damage from melee items
        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
        {
            TransferDamageToParent(damageDone, hit.Crit);
        }
        
        private void TransferDamageToParent(int damage, bool crit)
        {
            NPC parent = Parent;
            if (parent == null || !parent.active)
                return;
            
            // Show combat text on sphere position
            if (Main.netMode != NetmodeID.Server)
            {
                Color c = crit ? Color.Orange : Color.LightGoldenrodYellow;
                CombatText.NewText(NPC.Hitbox, c, damage, crit);
            }
            
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // Client: Send packet to server to apply damage
                ModPacket packet = Mod.GetPacket();
                packet.Write((byte)3); // Sphere damage packet type
                packet.Write(NPC.whoAmI);
                packet.Write(damage);
                packet.Write(crit);
                packet.Send();
            }
            else
            {
                // Server or singleplayer: Apply damage directly
                parent.life -= damage;
                if (parent.life <= 0)
                {
                    parent.life = 0;
                    // Properly kill the parent NPC
                    parent.HitEffect(0, damage);
                    parent.NPCLoot();
                    parent.active = false;
                    
                    // Also kill the sphere
                    NPC.active = false;
                    
                    // Disable the background
                    RoaringKnightBackgroundSystem.ShowBackground = false;
                }
                parent.netUpdate = true;
            }
        }
        
        // Handle network sync for damage transfer
        public void ReceiveDamageSync(int damage, bool crit)
        {
            pendingDamage += damage;
            pendingCrit = crit;
        }

        // Keep sphere alive, it can't actually die
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
        {
            // Don't modify damage here, just let it hit normally
            // The OnHitBy hooks will handle transferring it
        }

        public override bool CheckDead()
        {
            NPC.life = 1;
            return false;
        }

        // Handles sprite animation
        public override void FindFrame(int frameHeight)
        {
            NPC parent = Parent;
            if (parent == null)
                return;

            bool inMajorPhase = (int)parent.ai[0] == 3;
            int column = inMajorPhase ? 1 : 0;

            if (inMajorPhase)
            {
                SetFrame(column, 0);
            }
            else
            {
                animTick++;
                if (animTick >= animTicksPerFrame)
                {
                    animTick = 0;
                    animRow++;
                    if (animRow >= AnimFrameCount)
                        animRow = 0;
                }
                SetFrame(column, animRow);
            }
        }

        private void SetFrame(int col, int row)
        {
            NPC.frame = new Rectangle(col * FrameW, row * FrameH, FrameW, FrameH);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            bool visible = NPC.alpha <= 0;
            if (!visible)
                return false;

            Texture2D tex = TextureAssets.Npc[Type].Value;

            Vector2 origin = new Vector2(FrameW * 0.5f, FrameH * 0.5f);
            Vector2 basePos = NPC.Center - screenPos;
            basePos.Y += NPC.gfxOffY;

            spriteBatch.Draw(
                tex,
                basePos,
                NPC.frame,
                Color.White,
                NPC.rotation,
                origin,
                NPC.scale,
                SpriteEffects.None,
                0f
            );

            return false;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
        }
    }
}
