using Terraria.ModLoader;
using Terraria;
using Terraria.ID;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using ReLogic.Content;
using System;
using Terraria.GameContent.Bestiary;
using System.Collections.Generic;
using Terraria.Audio;

namespace DeterministicChaos.Content.NPCs.DarkWorldEnemies
{
    public class ArmoredZombie : ModNPC
    {
        public bool armored = true;
        private int animFrameHeight = 48;
        private int animFrameHeightNoHelemt = 56;
        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            Main.npcFrameCount[Type] = 9;

            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers()
            {
                Velocity = 1.5f,
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            // Sets the description of this NPC that is listed in the bestiary
            bestiaryEntry.UIInfoProvider = new CommonEnemyUICollectionInfoProvider(ContentSamples.NpcBestiaryCreditIdsByNpcNetIds[NPC.type] , false);
            bestiaryEntry.Info.AddRange(new List<IBestiaryInfoElement> {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface, //Remove this when you set up Dark world bestiary
				new FlavorTextBestiaryInfoElement("These zombies have found themselves suited up with additional armor. This \"blessing\" may have unforseen consequences...")//Replace this with a proper key.
            });
        }
        
        public override void SetDefaults()
        {
            base.SetDefaults();
            NPC.aiStyle = NPCAIStyleID.Fighter;
            AIType = NPCID.ArmoredSkeleton;
            NPC.width = 30;
            NPC.height = 45;
            NPC.damage = 40;
            NPC.lifeMax = 500;
            NPC.defense = 30;
            NPC.knockBackResist = 0.25f;
            NPC.value = 1500f;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath2;

            Banner = Type;
            BannerItem = ModContent.ItemType<ArmoredZombieBanner>();
            //Set SpawnModBiomes to get the right biome in the bestiary
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.lifeMax = (int)(NPC.lifeMax * balance);
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            base.SendExtraAI(writer);
            writer.Write(armored);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            base.ReceiveExtraAI(reader);
            armored = reader.ReadBoolean();
        }

        public override void AI()
        {
            base.AI();
            if (armored)
            {
                if(MathF.Abs(NPC.velocity.X) > 1.5f)
                {
                    if(NPC.velocity.X > 0 && NPC.direction == 1)
                    {
                        NPC.velocity.X = 1.5f;
                    }
                    else if(NPC.velocity.X < 0 && NPC.direction == -1)
                    {
                        NPC.velocity.X = -1.5f;
                    }
                }
            }
            else
            {
                if(MathF.Abs(NPC.velocity.X) < 2f)
                {
                    if(NPC.velocity.X > 0 && NPC.direction == 1)
                    {
                        NPC.velocity.X += NPC.velocity.X * 0.3f;
                    }
                    else if(NPC.velocity.X < 0 && NPC.direction == -1)
                    {
                        NPC.velocity.X += NPC.velocity.X * 0.3f;
                    }
                }
            }
        }

        public override bool CheckDead()
        {
            if (armored)
            {
                NPC.lifeMax = (int)(800 * (Main.expertMode ? 1.5f : 1f) * (Main.masterMode ? 1.33f : 1f));
                NPC.life = NPC.lifeMax;
                NPC.defense = 0;
                NPC.HitSound = SoundID.NPCHit1;
                NPC.knockBackResist = 0.8f;

                armored = false;
                if(Main.netMode != NetmodeID.MultiplayerClient)
                {
                    NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.position.X, (int)NPC.position.Y, ModContent.NPCType<HauntedHelmet>(), Target: NPC.target);
                }
                if (!Main.dedServ)
                {
                    for(int i = 0; i < 16; i++)
                    {
                        Dust.NewDust(NPC.Center, 0, 0, DustID.Cloud);
                    }
                }
                SoundEngine.PlaySound(SoundID.NPCHit42, NPC.Center);
                return false;
            }
            return true;
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            base.HitEffect(hit);
            if(NPC.life <= 0 && !armored)
            {
                if (!Main.dedServ)
                {
                    for(int i = 1; i < 5; i++)
                    {
                        Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, Mod.Find<ModGore>($"ArmoredZombie_gore_{i}").Type, NPC.scale);
                    }
                }
            }
        }

        public override void FindFrame(int frameHeight)
        {
            float increment = 0.25f * Math.Abs(NPC.velocity.X);
            if(!(NPC.velocity.Y >= -0.01f && NPC.velocity.Y <= 0.01f && (NPC.velocity.X >= 0.01f || NPC.velocity.X <= -0.01f)))
            {
                increment = 0f;
            }
            NPC.frameCounter = (NPC.frameCounter + increment) % Main.npcFrameCount[Type];
            NPC.frame.Y = armored ? (int)NPC.frameCounter * animFrameHeight : (int)NPC.frameCounter * animFrameHeightNoHelemt;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Asset<Texture2D> main = Terraria.GameContent.TextureAssets.Npc[Type];
            /*
                Replace the paths of this texture with the appropriate path
            */
            Asset<Texture2D> noHelmet = ModContent.Request<Texture2D>("DeterministicChaos/Content/NPCs/DarkWorldEnemies/ArmoredZombieUnhelmed");

            var texture = main.Value;
            if (!armored)
            {
                texture = noHelmet.Value;
            }

            var rect = new Rectangle(0, NPC.frame.Y, 34, armored ? animFrameHeight : animFrameHeightNoHelemt);

            if (NPC.IsABestiaryIconDummy)
            {
                rect = new Rectangle(0, NPC.frame.Y + 1, 34, armored ? animFrameHeight : animFrameHeightNoHelemt); // fuck
            }

            spriteBatch.Draw(texture, NPC.position + new Vector2(0f, armored ? 0f : -8f) - screenPos, rect, drawColor, NPC.rotation, new Vector2(0,0), NPC.scale, NPC.direction == 1  ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 1f);
            return false;
        }
    }
}