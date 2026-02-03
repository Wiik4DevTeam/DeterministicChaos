using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    public class SliceIndicator : ModProjectile
    {
        private const int TelegraphLife = 60 + 15;
        private const int PostLife = 30;
        private const int TotalLife = TelegraphLife + PostLife;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.timeLeft = TotalLife;

            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;

            Projectile.hide = false;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            Projectile.localAI[0] = Projectile.Center.X;
            Projectile.localAI[1] = Projectile.Center.Y;
            Projectile.timeLeft = TotalLife;
            Projectile.rotation = Projectile.ai[1];
            
            // Play indicator sound (only if not spawned by LatticeKnife)
            if (Main.netMode != NetmodeID.Server && !(Projectile.ai[0] < 0 && Projectile.ai[2] > 1f))
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightIndicator")
                {
                    Volume = 0.7f
                }, Projectile.Center);
            }
            
            Projectile.netUpdate = true;
        }

        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.Write(Projectile.localAI[0]);
            writer.Write(Projectile.localAI[1]);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            Projectile.localAI[0] = reader.ReadSingle();
            Projectile.localAI[1] = reader.ReadSingle();
        }

        public override void AI()
        {
            Projectile.Center = new Vector2(Projectile.localAI[0], Projectile.localAI[1]);

            float age = TotalLife - Projectile.timeLeft;
            float t = age / TotalLife;

            float baseAngle = Projectile.ai[1];
            float rotationDirection = Projectile.ai[2];

            // If spawned by LatticeKnife (ai[0] < 0 AND ai[2] > 1), don't rotate
            if (Projectile.ai[0] < 0 && Projectile.ai[2] > 1f)
            {
                // Keep the indicator pointing in the original direction (no rotation)
                Projectile.rotation = baseAngle + MathHelper.PiOver2;
            }
            else
            {
                // Rotate exactly 90 degrees over lifetime (normal behavior for other attacks)
                float targetDelta = MathHelper.PiOver2 * rotationDirection;
                float easedT = 1f - (1f - t) * (1f - t) * (1f - t);
                float delta = targetDelta * easedT;
                Projectile.rotation = baseAngle + delta + MathHelper.PiOver2;
            }

            float growT = MathHelper.Clamp(age / TelegraphLife, 0f, 1f);
            float growEase = 1f - (1f - growT) * (1f - growT);
            Projectile.scale = MathHelper.Lerp(0.10f, 2.50f, growEase);

            if (Projectile.timeLeft == PostLife + 2)
            {
                // Play sound and screen shake on all clients
                if (Main.netMode != NetmodeID.Server)
                {
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightBoxBreak")
                    {
                        Volume = 0.7f
                    }, Projectile.Center);
                    
                    // Add screen shake
                    Main.instance.CameraModifiers.Add(new Terraria.Graphics.CameraModifiers.PunchCameraModifier(
                        Projectile.Center, 
                        new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f)).SafeNormalize(Vector2.UnitX), 
                        12f, // strength
                        8f, // vibration speed
                        20, // frames
                        2000f // max distance for effect
                    ));
                }
                
                // Spawn attacks server-side
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    SpawnSlashAndTeeth();
            }

            if (Projectile.timeLeft <= 2)
                Projectile.Kill();
        }

        private void SpawnSlashAndTeeth()
        {
            // If damage is negative, don't spawn any attacks (used by LatticeKnife)
            if (Projectile.ai[0] < 0)
                return;

            int slashType = ModContent.ProjectileType<Enemy.SlashAttack>();
            int toothType = ModContent.ProjectileType<Enemy.ToothProjectile>();

            int damage = (int)Projectile.ai[0];
            float kb = 0f;
            int owner = Main.myPlayer;

            Vector2 pos = Projectile.Center;

            float baseAngle = Projectile.ai[1];
            float rotationDirection = Projectile.ai[2];

            // Compute final rotation same as SlashIndicator
            float finalDelta = MathHelper.PiOver2 * rotationDirection;
            
            // World angle (for spawning teeth along the line)
            float worldAngle = baseAngle + finalDelta;
            
            // Sprite-corrected rotation (for SlashAttack rendering)
            float finalRotation = worldAngle + MathHelper.PiOver2 + MathHelper.Pi;

            float vFlip = Main.rand.NextBool() ? 1f : 0f;

            // ai[0] = finalRotation, ai[1] = flipFlags, ai[2] = colorMode (1 = white for split screen)
            int s = Projectile.NewProjectile(
                Projectile.GetSource_FromAI(),
                pos.X,
                pos.Y,
                0f,
                0f,
                slashType,
                damage,
                0f,
                Main.myPlayer,
                finalRotation,
                vFlip > 0.5f ? 2 : 0,
                1f  // white color mode
            );

            if (s >= 0 && s < Main.maxProjectiles)
                Main.projectile[s].netUpdate = true;

            // Use the WORLD angle to spawn teeth along the slash line
            Vector2 along = new Vector2(1f, 0f).RotatedBy(worldAngle);
            float lineLength = 2400f; // total line length

            int teeth = 20;
            float speed = 12f;

            // Find Roaring Knight to check health
            NPC knight = null;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<NPCs.Bosses.RoaringKnight>())
                {
                    knight = Main.npc[i];
                    break;
                }
            }
            
            // Below half health: spawn fewer teeth
            if (knight != null && knight.life < knight.lifeMax * 0.5f)
                teeth = 8;
                
            for (int i = 0; i < teeth; i++)
            {
                // Distribute teeth along the line from -halfLen to +halfLen
                float t = Main.rand.NextFloat(0f, 1f);
                float offset = MathHelper.Lerp(-lineLength * 0.5f, lineLength * 0.5f, t);
                Vector2 spawnPos = pos + along * offset;

                // Add perpendicular variance
                Vector2 perp = new Vector2(-along.Y, along.X);
                spawnPos += perp * Main.rand.NextFloat(-40f, 40f);

                // Teeth shoot perpendicular to the slash line (inward)
                float angleVariance = Main.rand.NextFloat(-0.35f, 0.35f);
                Vector2 toothDir = perp.RotatedBy(angleVariance);
                
                // Randomize which side they shoot from
                if (Main.rand.NextBool())
                    toothDir = -toothDir;

                int p = Projectile.NewProjectile(
                    Projectile.GetSource_FromAI(), 
                    spawnPos, 
                    toothDir * speed, 
                    toothType, 
                    damage / 2, 
                    0f, 
                    owner
                );

                if (p >= 0 && p < Main.maxProjectiles) 
                    Main.projectile[p].netUpdate = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            if (tex == null) return false;

            float age = TotalLife - Projectile.timeLeft;
            bool inPost = Projectile.timeLeft <= PostLife;

            // Emissive, ignore lightColor parameter, use full brightness
            Color c = inPost ? Color.White : Color.Red;
            c *= 0.95f;

            float shrinkT = 0f;
            if (inPost)
                shrinkT = 1f - (Projectile.timeLeft / (float)PostLife);

            float xScale = Projectile.scale * (1f - shrinkT);
            float yScale = Projectile.scale;
            
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Bounds.Size() * 0.5f;
            
            // If spawned by LatticeKnife (ai[0] < 0 AND ai[2] > 1), use ai[2] as scale modifier
            if (Projectile.ai[0] < 0 && Projectile.ai[2] > 1f)
            {
                float scaleModifier = Projectile.ai[2];
                xScale *= 0.4f; // Make it skinnier (40% width)
                yScale *= scaleModifier; // Make it taller (2.5x height)
            }

            Rectangle src = tex.Bounds;

            // Match SlashIndicator's scale orientation (wider horizontally)
            Main.EntitySpriteDraw(tex, pos, src, c, Projectile.rotation, origin, new Vector2(xScale * 0.45f, yScale), SpriteEffects.None, 0);
            return false;
        }
    }
}
