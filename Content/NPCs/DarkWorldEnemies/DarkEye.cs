using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;
using System;
namespace DeterministicChaos.Content.NPCs.DarkWorldEnemies
{
    public class DarkEye : ModNPC
    {

        public int ShootTimer
        {
            get{ return (int)NPC.ai[0];}
            set{ NPC.ai[0] = (float)value;}
        }
        public int ShootIntervalTimer 
        {
            get{ return (int)NPC.ai[1];}
            set{ NPC.ai[1] = (float)value;}
        }

        public float maxSpeed = 7f;
        public float minSpeed = 1f;
        public float acceleration = 0.1f;

        public int laserDamage = 30;

        public int shotTime = 300;
        public int betweenShotTime = 30;
        public int firedShots = 0;
        public int maxFiredShots = 5;
        public bool isFiring = false;
        public Player Target
        {
            get
            {
                if (NPC.target < 0 || NPC.target == 255 || Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                {
                    NPC.TargetClosest();
                }
                return Main.player[NPC.target];
            }
        }
        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        }

        public override void Load()
        {
            On_NPC.Collision_DecideFallThroughPlatforms += DecideFallThroughPlatforms;
        }

        private bool DecideFallThroughPlatforms(On_NPC.orig_Collision_DecideFallThroughPlatforms orig, NPC self)
        {
            bool result = orig.Invoke(self);
            if(self.type == ModContent.NPCType<DarkEye>())
            {
                return true;
            }
            return result;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            // Sets the description of this NPC that is listed in the bestiary
            bestiaryEntry.UIInfoProvider = new CommonEnemyUICollectionInfoProvider(ContentSamples.NpcBestiaryCreditIdsByNpcNetIds[NPC.type] , false);
            bestiaryEntry.Info.AddRange(new List<IBestiaryInfoElement> {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface, //Remove this when you set up Dark world bestiary
				new FlavorTextBestiaryInfoElement("As if normal demon eyes weren't bad enough. These grotesque flying monsters barrage hapless lightners with sets of lasers.")//Replace this with a proper key.
            });
        }

        public override void SetDefaults()
        {
            base.SetDefaults();
            NPC.width = 30;
            NPC.height = 30;
            NPC.lifeMax = 550;
            NPC.damage = 35;
            NPC.defense = 10;
            NPC.value = 1500f;
            NPC.aiStyle = -1;

            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.noGravity = true;

            Banner = Type;
            BannerItem = ModContent.ItemType<DarkEyeBanner>();
            //Set SpawnModBiomes to get the right biome in the bestiary
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.lifeMax = (int)(NPC.lifeMax * balance);
        }

        public override void AI()
        {
            base.AI();
            NPC.localAI[0]++;//Timer for wing anims;
            if((Target.Center - NPC.Center).Length() > 300f)
            {
                NPC.TargetClosest();
            }
            if (!NPC.HasValidTarget)
            {
                return;
            }
            Vector2 targetPos = new Vector2(Target.Center.X, Target.Center.Y - 90f);
            Vector2 posdir = targetPos - NPC.Center;
            float dist = posdir.Length();
            posdir = posdir.SafeNormalize(Vector2.Zero);

            Vector2 playerDir = Target.Center - NPC.Center;
            float playerDist = playerDir.Length();
            playerDir = playerDir.SafeNormalize(Vector2.Zero);
            if(dist > 40f)
            {
                NPC.velocity += posdir * acceleration;
            }
            Vector2 velDir = NPC.velocity.SafeNormalize(Vector2.Zero);
            if(NPC.velocity.Length() > maxSpeed)
            {
                NPC.velocity = velDir * maxSpeed;
            }
            else if(NPC.velocity.Length() < minSpeed)
            {
                NPC.velocity = velDir * minSpeed;
            }

            Point? point = GetCollidingPoint();

            if(point != null)
            {
                Vector2 pulsePos = ((Point)point).ToWorldCoordinates();
                NPC.velocity += (NPC.Center - pulsePos).SafeNormalize(Vector2.Zero) * 1f;
            }

            if(ShootTimer < shotTime && !isFiring)
            {
                ShootTimer++;
            }
            else if (Collision.CanHitLine(NPC.Center, 0, 0, Target.Center, 0, 0))
            {
                ShootTimer = 0;
                isFiring = true;
            }

            if(ShootIntervalTimer < betweenShotTime && isFiring)
            {
                ShootIntervalTimer++;
            }
            else if(isFiring)
            {
                ShootIntervalTimer = 0;
                if(firedShots >= maxFiredShots)
                {
                    isFiring = false;
                    firedShots = 0;
                }
                else
                {
                    if(Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + playerDir * 10f, playerDir * 15f, ModContent.ProjectileType<DarkEyeLaser>(), laserDamage, 0.5f);
                    }
                    firedShots++;
                    SoundEngine.PlaySound(SoundID.Item33, NPC.Center);
                    if (!Main.dedServ)
                    {
                        for(int i = 0; i < 10; i++)
                        {
                            Dust.NewDust(NPC.Center, 1,1, ModContent.DustType<DarkEyeLaserDust>(), playerDir.X * Main.rand.NextFloat(2f, 10f), playerDir.Y * Main.rand.NextFloat(2f, 10f));
                        }
                    }
                }
            }
            float ticker = NPC.velocity.X;
            ticker = (Math.Clamp(ticker, -maxSpeed, maxSpeed) + maxSpeed) / (maxSpeed*2f);
            float rot = MathHelper.Lerp(-(float)Math.PI/4f, (float)Math.PI/4f, ticker);
            NPC.rotation = rot;
            NPC.direction = playerDir.X >= 0 ? 1 : -1;
        }

        public override void FindFrame(int frameHeight)
        {
            base.FindFrame(frameHeight);
            if (NPC.IsABestiaryIconDummy)
            {
                NPC.localAI[0]++;
            }
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            if(NPC.life <= 0 && Main.netMode != NetmodeID.Server)
            {
                for(int i = 1; i < 5; i++)
                {
                   Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, Mod.Find<ModGore>($"DarkEye_gore_{i}").Type, NPC.scale);
                }
            }
            if(Main.netMode != NetmodeID.MultiplayerClient && !isFiring)
            {
                ShootTimer = 0;
                NPC.netUpdate = true;
            }
        }

        public Point? GetCollidingPoint()
        {
            //Vector2 topLeft = NPC.position + new Vector2(-16f, -16f);
            var points = Collision.GetTilesIn(NPC.position, NPC.position + new Vector2(NPC.Size.X, NPC.Size.Y));
            foreach (var point in points)
            {
                
                var tile = Main.tile[point];
                bool flag1 = Main.tileSolid[tile.TileType];
                bool flag2 = Main.tileSolidTop[tile.TileType];
                if (!tile.IsActuated && tile.HasTile)
                {
                    if(flag1 && !flag2)
                    {
                        return point;
                    }
                }
            }
            return null;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            var mainAsset = TextureAssets.Npc[Type];
            /*
                Replace the paths of these textures with the appropriate paths
            */
            var wingAsset = ModContent.Request<Texture2D>("DeterministicChaos/Content/NPCs/DarkWorldEnemies/DarkEyeWing");
            var shineAsset = ModContent.Request<Texture2D>("DeterministicChaos/Content/NPCs/DarkWorldEnemies/DarkEyeLaserSpark");

            float drawTime = (float)(NPC.localAI[0] % 60f)/60f;
            float wingRotation = (MathHelper.Hermite(0, -8.4f, 0, -8.4f, drawTime) * (float)Math.PI/4f) - (float)Math.PI/8f;
            float wingScale = 1f;//MathHelper.Hermite(1f,8.4f,1f,0f, drawTime);

            int animFrame = (int)(drawTime * 5f);
            if(animFrame == 5)
            {
                animFrame = 0;
            }

            Vector2 eyePosition = NPC.Center;

            Rectangle wingRect = new Rectangle(0, animFrame * 80, 58, 80);
            Vector2 origin = new Vector2(0f, 32f);
            Vector2 leftOrigin = new Vector2(58f, 32f);
            spriteBatch.Draw(wingAsset.Value, eyePosition-screenPos, wingRect, drawColor, wingRotation + NPC.rotation, origin, wingScale, SpriteEffects.None, 1f);
            spriteBatch.Draw(wingAsset.Value, eyePosition-screenPos, wingRect, drawColor, -wingRotation + NPC.rotation, leftOrigin, wingScale, SpriteEffects.FlipHorizontally, 1f);

            Rectangle mainRect = new Rectangle(0,0, mainAsset.Width(), mainAsset.Height());
            Vector2 mainOrigin = (mainRect.Size()/2f) + new Vector2(0f, -12f);

            spriteBatch.Draw(mainAsset.Value, NPC.Center-screenPos, mainRect, drawColor, NPC.rotation, mainOrigin, NPC.scale, NPC.direction == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 1f);

            if (isFiring && firedShots > 0)
            {
                float sparkRotation = (float)((NPC.localAI[0] * 0.2f) % Math.PI*2);
                float sparkScale = MathHelper.Hermite(0f,8.4f,0f,0f, ((float)ShootIntervalTimer/(float)betweenShotTime));
                Rectangle sparkRect = new Rectangle(0,0, 22, 22);
                Vector2 sparkOrigin = sparkRect.Size() / 2f;
                spriteBatch.Draw(shineAsset.Value, eyePosition-screenPos, sparkRect, drawColor, sparkRotation, sparkOrigin, sparkScale, SpriteEffects.None, 1f);
            }
            return false;
        }
    }
}