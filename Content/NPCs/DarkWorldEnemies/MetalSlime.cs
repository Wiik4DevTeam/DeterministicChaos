using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.DarkWorldEnemies
{
    public class MetalSlime: ModNPC
    {

        public int Timer
        {
            get => (int)NPC.ai[0];
            set => NPC.ai[0] = value;
        }
        public State state
        {
            get => (State)NPC.ai[1];
            set => NPC.ai[1] = (float)value;
        }

        public int randomHopDirection
        {
            get => (int)NPC.ai[2];
            set => NPC.ai[2] = (float)value;
        }
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

        public float hopStrength = 3.5f;
        public float chargeSpeed = 15f;

        public bool hasBeenHit = false;
        private int animFrame = 0;
        private int frameSizeY = 100;

        public override void Load()
        {
            On_NPC.Collision_DecideFallThroughPlatforms += DecideFallThroughPlatforms;
        }

        private bool DecideFallThroughPlatforms(On_NPC.orig_Collision_DecideFallThroughPlatforms orig, NPC self)
        {
            bool result = orig.Invoke(self);
            if(self.type == ModContent.NPCType<MetalSlime>())
            {
                MetalSlime inst = (MetalSlime)self.ModNPC;
                if(inst.state == State.Dash)
                {
                    return true;
                }
            }
            return result;
        }

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 28;

            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers()
            {
                PortraitPositionYOverride = 24f,
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            base.SetStaticDefaults();
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            // Sets the description of this NPC that is listed in the bestiary
            bestiaryEntry.UIInfoProvider = new CommonEnemyUICollectionInfoProvider(ContentSamples.NpcBestiaryCreditIdsByNpcNetIds[NPC.type] , false);
            bestiaryEntry.Info.AddRange(new List<IBestiaryInfoElement> {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface, //Remove this when you set up Dark world bestiary
				new FlavorTextBestiaryInfoElement("Through the strange properties of the dark world, these overworld slimes gain a metallic sheen, an increase in size, and deadly new abilities.")//Replace this with a proper key.
            });
        }

        public enum State
        {
            Idle,
            IntroHop,
            IntroDash,
            DashCircle,
            DashPullback,
            Dash,
            DashOut,
            SpikeIn,
            Spiked,
            SpikeOut,

        }
        public override void SetDefaults()
        {
            NPC.width = 50;
            NPC.height = 28;
            NPC.lifeMax = 900;
            NPC.damage = 45;
            NPC.defense = 20;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0.25f;
            NPC.value = 2000f;
            NPC.noGravity = true;//uses custom gravity
            Banner = Type;
            BannerItem = ModContent.ItemType<MetalSlimeBanner>();

            //Set SpawnModBiomes to get the right biome in the bestiary
            base.SetStaticDefaults();
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.lifeMax = (int)(NPC.lifeMax * balance);
        }

        public override void AI()
        {
            base.AI();
            Timer++;
            int frameTimeLimit = 5;
            int startFrame = 0;
            int endFrame = 1;
            bool stateAnimLoop = true;
            bool stateSwitchOnAnimEnd = false;
            bool doGravity = true;
            int stateMaxTime = 60;

            Vector2 dir = Target.Center - NPC.Center;
            float dist = dir.Length();
            dir = dir.SafeNormalize(Vector2.Zero);

            switch (state)
            {
                case State.Idle:
                    startFrame = 0;
                    endFrame = 1;
                    stateAnimLoop = true;
                    stateSwitchOnAnimEnd = false;
                    stateMaxTime = 60;

                    if(NPC.velocity.Y == 0)
                    {
                        NPC.velocity.X *= 0.6f;
                    }
                    break;
                case State.IntroHop:
                    frameTimeLimit = 2;
                    startFrame = 0;
                    endFrame = 1;
                    stateAnimLoop = true;
                    stateSwitchOnAnimEnd = false;
                    stateMaxTime = 30;
                    break;
                case State.IntroDash:
                    startFrame = 2;
                    endFrame = 8;
                    stateAnimLoop = false;
                    stateSwitchOnAnimEnd = true;
                    stateMaxTime = 60;
                    doGravity = false;

                    NPC.velocity.Y = MathHelper.Hermite(0,3,1,0, (Timer/(float)stateMaxTime) - 1) * 1f;
                    break;
                case State.DashCircle:
                    startFrame = 9;
                    endFrame = 12;
                    stateAnimLoop = true;
                    stateSwitchOnAnimEnd = false;
                    stateMaxTime = 120;
                    doGravity = false;

                    NPC.velocity = dir * ((dist - 400f) / 60f);
                    //Main.NewText($"Velocity: {NPC.velocity}, {dist}");
                    SmoothRotation(dir);
                    break;
                case State.DashPullback:
                    startFrame = 9;
                    endFrame = 12;
                    stateAnimLoop = true;
                    stateSwitchOnAnimEnd = false;
                    stateMaxTime = 30;
                    NPC.velocity = dir * -8f * (Timer/(float)stateMaxTime);
                    doGravity = false;
                     
                    SmoothRotation(dir);
                    break;
                case State.Dash:
                    startFrame = 9;
                    endFrame = 12;
                    stateAnimLoop = true;
                    stateSwitchOnAnimEnd = false;
                    stateMaxTime = 60;
                    doGravity = false;
                    
                    //Main.NewText($"Charge Velocity: {NPC.velocity}");
                    if (Colliding())
                    {
                        DoStateSwitchingLogic(dir, dist);
                        //Main.NewText("Detected Collision");
                    }
                    if (!Main.dedServ)
                    {
                        Dust.NewDust(NPC.Center, 1, 1, DustID.PlatinumCoin);
                    }
                    break;
                case State.DashOut:
                    NPC.rotation = 0f;
                    startFrame = 13;
                    endFrame = 18;
                    stateAnimLoop = false;
                    stateSwitchOnAnimEnd = true;
                    stateMaxTime = 60;
                    break;
                case State.SpikeIn:
                    startFrame = 19;
                    endFrame = 20;
                    stateAnimLoop = false;
                    stateSwitchOnAnimEnd = true;
                    stateMaxTime = 60;
                    break;
                case State.Spiked:
                    startFrame = 21;
                    endFrame = 21;
                    stateAnimLoop = true;
                    stateSwitchOnAnimEnd = false;
                    stateMaxTime = 180;

                    if(NPC.velocity.Y == 0)
                    {
                        NPC.velocity.X *= 0.2f;
                    }
                    break;
                case State.SpikeOut:
                    startFrame = 21;
                    endFrame = 26;
                    stateAnimLoop = false;
                    stateSwitchOnAnimEnd = true;
                    stateMaxTime = 60;
                    break; 
            }

            if(Timer >= stateMaxTime && !stateSwitchOnAnimEnd)
            {
                DoStateSwitchingLogic(dir, dist);
            }
            NPC.frameCounter++;
            if(NPC.frameCounter >= frameTimeLimit)
            {
                NPC.frameCounter = 0;
                if(animFrame < startFrame)
                {
                    animFrame = startFrame;
                }
                else if(animFrame >= endFrame)
                {
                    animFrame = endFrame;
                }

                animFrame++;
                if(animFrame > endFrame)
                {
                    if(stateAnimLoop)
                    {
                        animFrame = startFrame;
                    }
                    if (stateSwitchOnAnimEnd)
                    {
                        DoStateSwitchingLogic(dir, dist);
                    }
                }
            }

            if(doGravity && NPC.velocity.Y < 10f)
            {
                NPC.velocity.Y += 0.167f;
            }

            if(state == State.DashCircle || state == State.DashPullback || state == State.Dash || state == State.Spiked)
            {
                NPC.knockBackResist = 0f;
            }
            else
            {
                NPC.knockBackResist = 0.25f;
            }
        }

        public override void FindFrame(int frameHeight)
        {
            base.FindFrame(frameHeight);
            if (NPC.IsABestiaryIconDummy)
            {
                NPC.frameCounter += 0.15f;
                int frame = (int)(NPC.frameCounter % 2f);
                if(frame == 2)
                {
                    frame = 1;
                }
                NPC.frame.Y = frame * frameSizeY;
            }
        }

        public void DoStateSwitchingLogic(Vector2 dir, float dist)
        {
            NPC.frameCounter = 0;
            Timer = 0;
            switch (state)
            {
                case State.Idle:
                    if(Main.netMode == NetmodeID.MultiplayerClient)
                    {
                        break;
                    }
                    float abs = Math.Abs(dist);
                    bool doOffense = (Main.rand.Next(0,11) > 6) && Collision.CanHitLine(NPC.Center, 0, 0, Target.Center, 0, 0) && hasBeenHit;
                    if (!doOffense)
                    {
                        bool shouldHop = NPC.velocity.Y <= 0.01f && NPC.velocity.Y >= -0.01f && Main.rand.NextBool();
                        randomHopDirection = Main.rand.NextBool() ? 1 : -1;
                        if (shouldHop)
                        {
                            state = State.IntroHop;
                        }
                    }
                    else if(abs > 150f)
                    {
                        state = State.IntroDash;
                    }
                    else
                    {
                        state = State.SpikeIn;
                    }
                    NPC.netUpdate = true;
                    break;
                case State.IntroHop:
                    int hopDir = hasBeenHit ? Math.Sign(dir.X) : randomHopDirection;
                    if(hopDir != 0)
                    {
                        NPC.velocity = new Vector2(hopStrength * hopDir, -hopStrength);
                    }
                    state = State.Idle;
                    break;
                case State.IntroDash:
                    state = State.DashCircle;
                    break;
                case State.DashCircle:
                    state = State.DashPullback;
                    break;
                case State.DashPullback:
                    Vector2 chargeDir = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    //Main.NewText($"Charge Dir: {chargeDir}");
                    NPC.velocity = chargeDir * chargeSpeed;
                    state = State.Dash;
                    break;
                case State.Dash:
                    NPC.velocity = Vector2.Zero;
                    state = State.DashOut;
                    break;
                case State.DashOut:
                    state = State.Idle;
                    break;
                case State.SpikeIn:
                    state = State.Spiked;
                    break;
                case State.Spiked:
                    state = State.SpikeOut;
                    break;
                case State.SpikeOut:
                    state = State.Idle;
                    break; 
            }
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            base.HitEffect(hit);
            if(NPC.life <= 0 && !Main.dedServ)
            {
                for(int i = 0; i < 30; i++)
                {
                    Dust dust = Dust.NewDustDirect(NPC.Center, NPC.width, NPC.height, DustID.Silver, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f,2f));
                }
            }
            hasBeenHit = true;
        }

        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
        {
            base.OnHitByItem(player, item, hit, damageDone);
            DoSpikeDamage(player, hit);
        }

        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            base.OnHitByProjectile(projectile, hit, damageDone);
            if (ProjectileID.Sets.AllowsContactDamageFromJellyfish[projectile.type] && projectile.owner != 256)
            {
                DoSpikeDamage(Main.player[projectile.owner], hit);
            }
        }

        public void DoSpikeDamage(Player player, NPC.HitInfo hit)
        {
            if(player.whoAmI == Main.myPlayer)
            {
                if(state == State.Spiked)
                {
                    player.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), NPC.damage, -player.direction, dodgeable: false);
                }
            }
        }

        public bool Colliding()
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
                        return true;
                    }
                }
            }
            return false;
        }

        public void SmoothRotation(Vector2 dir)
        {
            float currentRotation = NPC.rotation;
            float goalRotation = dir.ToRotation();
            //Main.NewText($"Rotation: {currentRotation}, {goalRotation}");
            if(Math.Abs(goalRotation - currentRotation) > (Math.PI*1.5f))
            {
                float real = goalRotation - currentRotation;
                if(real > 0)
                {
                    goalRotation -= (float)Math.PI*2f;
                }
                else
                {
                    goalRotation += (float)Math.PI*2f;
                }
            }
            float appliedRotation = MathHelper.Lerp(currentRotation, goalRotation, 0.1f);
            NPC.rotation = appliedRotation;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            float xOffset = 38f;
            float yOffset = 74f;
            Asset<Texture2D> tex = TextureAssets.Npc[Type];
            Vector2 size = tex.Size();
            Rectangle rect = new Rectangle(0,animFrame * frameSizeY,(int)size.X, (int)size.Y/Main.npcFrameCount[Type]);
            if (NPC.IsABestiaryIconDummy)
            {
                rect = new Rectangle(0, NPC.frame.Y,(int)size.X, (int)size.Y/Main.npcFrameCount[Type]);
            }
            spriteBatch.Draw(tex.Value, NPC.Center - screenPos, rect, drawColor, NPC.rotation, new Vector2(xOffset, yOffset), NPC.scale, SpriteEffects.None, 1f);
            return false;
        }
    }
} 