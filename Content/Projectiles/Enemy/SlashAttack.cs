using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    public class SlashAttack : ModProjectile
    {
        private const int Frames = 4;
        private const int TotalLife = 20;
        private const int TicksPerFrame = 5;

        private const float WidthScale = 3000f / 300f;
        private const float HeightScale = 3.0f;
        
        private bool hasPlayedSound = false;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = Frames;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults()
        {
            // Use a large circular hitbox for initial detection, then refine in Colliding
            Projectile.width = 200;
            Projectile.height = 200;

            Projectile.hostile = true;
            Projectile.friendly = false;

            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;

            Projectile.hide = false;
        }

        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.Write(Projectile.localAI[0]);
            writer.Write(Projectile.localAI[1]);
            writer.Write(Projectile.rotation);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            Projectile.localAI[0] = reader.ReadSingle();
            Projectile.localAI[1] = reader.ReadSingle();
            Projectile.rotation = reader.ReadSingle();
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            // Lock the spawn point so multiplayer timing/rounding doesn't drift while scaling.
            Projectile.localAI[0] = Projectile.Center.X;
            Projectile.localAI[1] = Projectile.Center.Y;

            // Only calculate rotation on server/singleplayer
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                float passedAngle = Projectile.ai[0];
                Projectile.rotation = passedAngle - MathHelper.PiOver2;

                if (Main.rand.NextBool())
                    Projectile.rotation += MathHelper.Pi;
                    
                Projectile.netUpdate = true;
            }
        }

        public override void AI()
        {
            // Play sound on first tick (after spawn sync)
            if (!hasPlayedSound && Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnifeLaunch")
                {
                    Volume = 0.7f
                }, Projectile.Center);
                hasPlayedSound = true;
            }
            
            Projectile.velocity = Vector2.Zero;
            Projectile.Center = new Vector2(Projectile.localAI[0], Projectile.localAI[1]);

            Projectile.frameCounter++;
            if (Projectile.frameCounter >= TicksPerFrame)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= Frames)
                    Projectile.frame = Frames - 1;
            }

            // Emissive
            Lighting.AddLight(Projectile.Center, 1.0f, 0.2f, 0.2f);

            if (Projectile.timeLeft <= 2)
                Projectile.Kill();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            // Compute the rotated line segment representing the slash
            float lifeT = (TotalLife - Projectile.timeLeft) / (float)TotalLife;
            float curHeightScale = MathHelper.Lerp(HeightScale, 0f, lifeT);
            
            if (curHeightScale < 0.1f)
                return false; // slash has shrunk too much

            // The slash is a long thin rectangle
            float actualWidth = 300f * WidthScale;  // 3000px
            float actualHeight = 10f * curHeightScale;

            Vector2 center = new Vector2(Projectile.localAI[0], Projectile.localAI[1]);
            
            // Get the direction vector from rotation
            Vector2 direction = new Vector2(1f, 0f).RotatedBy(Projectile.rotation);
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);

            // Convert target rectangle to center + half-extents
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            Vector2 targetHalfSize = new Vector2(targetHitbox.Width * 0.5f, targetHitbox.Height * 0.5f);

            // Vector from slash center to target center
            Vector2 toTarget = targetCenter - center;

            // Project onto slash's local axes
            float alongSlash = Vector2.Dot(toTarget, direction);
            float perpToSlash = System.Math.Abs(Vector2.Dot(toTarget, perpendicular));

            // Check if target overlaps the slash's extents
            float halfWidth = actualWidth * 0.5f;
            float halfHeight = actualHeight * 0.5f;

            // Add target's half-extents (treat target as circle with radius = max dimension)
            float targetRadius = System.Math.Max(targetHalfSize.X, targetHalfSize.Y);

            // Check collision using separating axis theorem (simplified for AABB vs rotated rect)
            bool withinWidth = System.Math.Abs(alongSlash) <= (halfWidth + targetRadius);
            bool withinHeight = perpToSlash <= (halfHeight + targetRadius);

            return withinWidth && withinHeight;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            if (tex == null)
                return false;

            int frameWidth = tex.Width;
            int frameHeight = tex.Height / Frames;

            Rectangle src = new Rectangle(0, Projectile.frame * frameHeight, frameWidth, frameHeight);
            Vector2 origin = new Vector2(frameWidth * 0.5f, frameHeight * 0.5f);

            Vector2 pos = new Vector2(Projectile.localAI[0], Projectile.localAI[1]) - Main.screenPosition;

            float lifeT = (TotalLife - Projectile.timeLeft) / (float)TotalLife;
            float curHeightScale = MathHelper.Lerp(HeightScale, 0f, lifeT);
            Vector2 drawScale = new Vector2(WidthScale * Projectile.scale, System.Math.Max(0.001f, curHeightScale * Projectile.scale));

            // Decode flip flags: 1 = flipH, 2 = flipV
            int flipFlags = (Projectile.ai.Length > 1) ? (int)Projectile.ai[1] : 0;
            SpriteEffects fx = SpriteEffects.None;
            if ((flipFlags & 1) != 0) fx |= SpriteEffects.FlipHorizontally;
            if ((flipFlags & 2) != 0) fx |= SpriteEffects.FlipVertically;

            // Color mode: 0 = red (slash), 1 = white (split screen), default red.
            int colorMode = (Projectile.ai.Length > 2) ? (int)Projectile.ai[2] : 0;
            Color baseColor = (colorMode == 1) ? Color.White : Color.Red;
            Color drawColor = baseColor * 0.95f;

            Main.EntitySpriteDraw(tex, pos, src, drawColor, Projectile.rotation, origin, drawScale, fx, 0);
            return false;
        }
    }
}
