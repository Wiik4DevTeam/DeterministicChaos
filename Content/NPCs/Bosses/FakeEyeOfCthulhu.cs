using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    public class FakeEyeOfCthulhu : ModNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.EyeofCthulhu;
        
        private bool hasTriggeredCutscene = false;
        private int freezeTimer = 0;
        private bool hasPlayedSwoon = false;
        private bool hasSpawnedCutscene = false;
        
        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.EyeofCthulhu];
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
        }

        public override void SetDefaults()
        {
            NPC.width = 100;
            NPC.height = 110;
            NPC.damage = 0;
            NPC.defense = 9999;
            NPC.lifeMax = 2800;
            NPC.life = 2800;
            NPC.HitSound = null;
            NPC.DeathSound = null;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.dontTakeDamage = true;
            NPC.immortal = true;
            NPC.boss = true;
            NPC.npcSlots = 10f;

            NPC.scale = 1.5f;
            
            Music = MusicID.Boss1;
        }
        
        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.Write(freezeTimer);
            writer.Write(hasSpawnedCutscene);
        }
        
        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            freezeTimer = reader.ReadInt32();
            hasSpawnedCutscene = reader.ReadBoolean();
        }
        
        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                Main.NewText("Eye of Cthulhu has awoken!", 175, 75, 255);
            }
            else if (Main.netMode == NetmodeID.Server)
            {
                Terraria.Chat.ChatHelper.BroadcastChatMessage(
                    Terraria.Localization.NetworkText.FromLiteral("Eye of Cthulhu has awoken!"), 
                    new Color(175, 75, 255)
                );
            }
        }

        public override void AI()
        {
            if (hasTriggeredCutscene)
            {
                // set the music fade to 0 every single frame because terraria music fade is broken and stupid
                Main.musicFade[Main.curMusic] = 0f;
                freezeTimer++;
                NPC.velocity = Vector2.Zero;
                
                if (freezeTimer == 1 && !hasPlayedSwoon)
                {
                    hasPlayedSwoon = true;
                    if (Main.netMode != NetmodeID.Server)
                    {
                        SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/Swoon")
                        {
                            Volume = 0.9f
                        }, NPC.Center);
                        
                    }
                }
                
                if (freezeTimer == 260 && !hasSpawnedCutscene)
                {
                    hasSpawnedCutscene = true;
                    NPC.netUpdate = true;
                    
                    // Play break sound on all clients
                    if (Main.netMode != NetmodeID.Server)
                    {
                        SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/Break")
                        {
                            Volume = 0.9f
                        }, NPC.Center);
                    }
                    
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        int cutsceneKnight = NPC.NewNPC(
                            NPC.GetSource_FromAI(),
                            (int)NPC.Center.X,
                            (int)NPC.Center.Y,
                            ModContent.NPCType<RoaringKnightCutscene>()
                        );
                        
                        if (cutsceneKnight >= 0 && cutsceneKnight < Main.maxNPCs)
                        {
                            Main.npc[cutsceneKnight].ai[0] = NPC.whoAmI;
                            Main.npc[cutsceneKnight].netUpdate = true;
                        }

                        NPC.life = 0;
                        NPC.active = false;
                        return;
                    }
                }
                return;
            }

            Player target = Main.player[NPC.target];
            if (!target.active || target.dead)
            {
                NPC.TargetClosest(false);
                target = Main.player[NPC.target];
                
                if (!target.active || target.dead)
                {
                    NPC.velocity.Y -= 0.4f;
                    if (NPC.timeLeft > 10)
                        NPC.timeLeft = 10;
                    return;
                }
            }

            float distanceToPlayer = Vector2.Distance(NPC.Center, target.Center);
            
            if (distanceToPlayer < 100f && !hasTriggeredCutscene)
            {
                hasTriggeredCutscene = true;
                Music = -1;
                NPC.netUpdate = true;
                return;
            }

            Vector2 toPlayer = target.Center - NPC.Center;
            toPlayer.Normalize();
            
            float speed = 4f;
            float inertia = 20f;
            
            NPC.velocity = (NPC.velocity * (inertia - 1f) + toPlayer * speed) / inertia;
            
            if (toPlayer.X > 0)
                NPC.spriteDirection = 1;
            else
                NPC.spriteDirection = -1;
            
            NPC.rotation = NPC.velocity.X * 0.05f;
        }

        public override void FindFrame(int frameHeight)
        {
            if (hasTriggeredCutscene)
                return;
                
            NPC.frameCounter++;
            if (NPC.frameCounter >= 8)
            {
                NPC.frameCounter = 0;
                NPC.frame.Y += frameHeight;
                
                if (NPC.frame.Y >= frameHeight * Main.npcFrameCount[Type])
                    NPC.frame.Y = 0;
            }
        }

        public override bool CheckDead()
        {
            return true;
        }
        
        public override void OnKill()
        {
            if (Main.netMode != NetmodeID.Server)
            {
                Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, 143, NPC.scale);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, 144, NPC.scale);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, 145, NPC.scale);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, 146, NPC.scale);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, 147, NPC.scale);
            }
        }

        public override bool CheckActive()
        {
            return false;
        }
        
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (hasTriggeredCutscene)
            {
                Texture2D texture = TextureAssets.Npc[Type].Value;
                Vector2 drawPos = NPC.Center - screenPos;
                
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                
                for (int i = 0; i < 5; i++)
                {
                    spriteBatch.Draw(
                        texture,
                        drawPos,
                        NPC.frame,
                        Color.White,
                        NPC.rotation,
                        NPC.frame.Size() * 0.5f,
                        NPC.scale,
                        NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally,
                        0f
                    );
                }
                
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                
                return false;
            }
            
            return true;
        }
    }
}
