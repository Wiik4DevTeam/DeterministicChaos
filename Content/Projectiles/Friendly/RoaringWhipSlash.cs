using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    // Miniature friendly version of the Roaring Knight screen split attack
    public class RoaringWhipSlash : ModProjectile
    {
        public override string Texture => "DeterministicChaos/Content/Projectiles/Enemy/SliceIndicator";
        
        private const int TelegraphLife = 20; // Faster than boss version
        private const int PostLife = 10;
        private const int TotalLife = TelegraphLife + PostLife;
        
        private bool slashSpawned = false;
        
        // ai[0] = damage for slash
        // ai[1] = starting angle (line faces this direction toward player/clone)
        // ai[2] = rotation direction (1 or -1)
        
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;
        }
        
        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.timeLeft = TotalLife;
            
            Projectile.friendly = false; // Indicator does not damage
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            
            Projectile.hide = false;
        }
        
        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            // Store spawn position
            Projectile.localAI[0] = Projectile.Center.X;
            Projectile.localAI[1] = Projectile.Center.Y;
            Projectile.timeLeft = TotalLife;
            Projectile.rotation = Projectile.ai[1];
            
            // Play indicator sound
            if (Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightIndicator")
                {
                    Volume = 0.4f,
                    Pitch = 0.5f // Higher pitch for smaller version
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
            // Keep position locked
            Projectile.Center = new Vector2(Projectile.localAI[0], Projectile.localAI[1]);
            
            float age = TotalLife - Projectile.timeLeft;
            float t = age / TotalLife;
            
            float baseAngle = Projectile.ai[1];
            float rotationDirection = Projectile.ai[2];
            
            // Rotate 90 degrees over lifetime
            float targetDelta = MathHelper.PiOver2 * rotationDirection;
            float easedT = 1f - (1f - t) * (1f - t) * (1f - t);
            float delta = targetDelta * easedT;
            Projectile.rotation = baseAngle + delta + MathHelper.PiOver2;
            
            // Scale up over time (smaller than boss version)
            float growT = MathHelper.Clamp(age / TelegraphLife, 0f, 1f);
            float growEase = 1f - (1f - growT) * (1f - growT);
            Projectile.scale = MathHelper.Lerp(0.05f, 0.8f, growEase);
            
            // Spawn slash attack when telegraph ends
            if (Projectile.timeLeft == PostLife + 2 && !slashSpawned)
            {
                slashSpawned = true;
                
                // Play sound and minor screen shake
                if (Main.netMode != NetmodeID.Server)
                {
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightBoxBreak")
                    {
                        Volume = 0.4f,
                        Pitch = 0.3f
                    }, Projectile.Center);
                    
                    // Smaller screen shake
                    Main.instance.CameraModifiers.Add(new Terraria.Graphics.CameraModifiers.PunchCameraModifier(
                        Projectile.Center,
                        new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f)).SafeNormalize(Vector2.UnitX),
                        4f,
                        6f,
                        10,
                        800f
                    ));
                }
                
                // Spawn the actual damaging slash
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    SpawnSlash();
            }
            
            if (Projectile.timeLeft <= 2)
                Projectile.Kill();
        }
        
        private void SpawnSlash()
        {
            int damage = (int)Projectile.ai[0];
            Vector2 pos = Projectile.Center;
            
            float baseAngle = Projectile.ai[1];
            float rotationDirection = Projectile.ai[2];
            
            // Calculate final rotation
            float finalDelta = MathHelper.PiOver2 * rotationDirection;
            float worldAngle = baseAngle + finalDelta;
            float finalRotation = worldAngle + MathHelper.PiOver2 + MathHelper.Pi;
            
            float vFlip = Main.rand.NextBool() ? 1f : 0f;
            
            // Spawn the friendly slash attack
            int s = Projectile.NewProjectile(
                Projectile.GetSource_FromAI(),
                pos.X,
                pos.Y,
                0f,
                0f,
                ModContent.ProjectileType<RoaringWhipSlashAttack>(),
                damage,
                0f,
                Projectile.owner,
                finalRotation,
                vFlip > 0.5f ? 2 : 0
            );
            
            if (s >= 0 && s < Main.maxProjectiles)
                Main.projectile[s].netUpdate = true;
        }
        
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[ModContent.ProjectileType<Enemy.SliceIndicator>()].Value;
            if (tex == null) return false;
            
            float age = TotalLife - Projectile.timeLeft;
            bool inPost = Projectile.timeLeft <= PostLife;
            
            // Emissive white/red color
            Color c = inPost ? Color.White : Color.Red;
            c *= 0.95f;
            
            float shrinkT = 0f;
            if (inPost)
                shrinkT = 1f - (Projectile.timeLeft / (float)PostLife);
            
            float xScale = Projectile.scale * (1f - shrinkT);
            float yScale = Projectile.scale;
            
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Bounds.Size() * 0.5f;
            
            Rectangle src = tex.Bounds;
            
            // Draw smaller than boss version
            Main.EntitySpriteDraw(tex, pos, src, c, Projectile.rotation, origin, new Vector2(xScale * 0.45f, yScale), SpriteEffects.None, 0);
            return false;
        }
    }
}
