
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.DarkWorldEnemies
{
    public class HauntedHelmet : ModNPC
    {
        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            NPCID.Sets.TrailingMode[Type] = 3;
            NPCID.Sets.TrailCacheLength[Type] = 5;

            NPCID.Sets.NPCBestiaryDrawModifiers value = new NPCID.Sets.NPCBestiaryDrawModifiers()
            {
                Hide = true
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(NPC.type, value);
        }

        public override void Load()
        {
            On_NPC.Collision_DecideFallThroughPlatforms += DecideFallThroughPlatforms;
        }

        private bool DecideFallThroughPlatforms(On_NPC.orig_Collision_DecideFallThroughPlatforms orig, NPC self)
        {
            bool result = orig.Invoke(self);
            if(self.type == ModContent.NPCType<HauntedHelmet>())
            {
                return true;
            }
            return result;
        }

        public override void SetDefaults()
        {
            base.SetDefaults();
            NPC.width = 20;
            NPC.height = 20;
            NPC.damage = 30;
            NPC.defense = 8;
            NPC.lifeMax = 300;
            NPC.value = 0f; 
            NPC.noGravity = true;
            NPC.aiStyle = NPCAIStyleID.CursedSkull;
            AIType = NPCID.CursedSkull;

            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath6;

            Banner = ModContent.NPCType<ArmoredZombie>();//Technically this makes the helmet kills count for banner progress. Instead of fixing this I just made banner progress require 100 kills.
            //Shouldn't show up in bestiary
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.lifeMax = (int)(NPC.lifeMax * balance);
        }

        public override void AI()
        {
            base.AI();
            if (!Main.dedServ)
            {
                Dust.NewDust(NPC.Center, 0, 0, DustID.ShadowbeamStaff);
            }
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            base.HitEffect(hit);
            if(NPC.life <= 0)
            {
                if (!Main.dedServ)
                {
                    for(int i = 1; i < 3; i++)
                    {
                        Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, Mod.Find<ModGore>($"HauntedHelmet_gore_{i}").Type, NPC.scale);
                    }
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Asset<Texture2D> main = Terraria.GameContent.TextureAssets.Npc[Type];
            var rect = new Rectangle(0,0, main.Width(), main.Height());
            Vector2 origin = rect.Size() / 2f;
            for(int i = 0; i < NPCID.Sets.TrailCacheLength[Type]; i++)
            {
                Vector2 oldCenter = NPC.oldPos[i] + origin;
                spriteBatch.Draw(main.Value, oldCenter - screenPos, rect, drawColor * ((NPCID.Sets.TrailCacheLength[Type] - i) / 15f), NPC.oldRot[i], origin, NPC.scale, NPC.direction == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 1f);
            }
            spriteBatch.Draw(main.Value, NPC.Center - screenPos, rect, drawColor, NPC.rotation, origin, NPC.scale, NPC.direction == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 1f);
            return false;
        }
    }
}