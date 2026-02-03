using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    // Arrow projectile that pauses, shows an indicator, then dashes forward
    public class RoaringArrowProjectile : ModProjectile
    {
        private const int PauseTime = 30;
        private const float DashSpeed = 32f;

        private Vector2 storedVelocity;
        private bool hasDashed = false;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.arrow = true;
            Projectile.aiStyle = -1;
            Projectile.DamageType = DamageClass.Ranged;
        }
        
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.WriteVector2(storedVelocity);
            writer.Write(hasDashed);
        }
        
        public override void ReceiveExtraAI(BinaryReader reader)
        {
            storedVelocity = reader.ReadVector2();
            hasDashed = reader.ReadBoolean();
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            storedVelocity = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Projectile.netUpdate = true;
        }

        public override void AI()
        {
            Projectile.ai[0]++;

            // Emit light so it is always visible
            Lighting.AddLight(Projectile.Center, 0.6f, 0.4f, 0.7f);

            if (Projectile.ai[0] < PauseTime)
            {
                // Pause phase, stay still and show indicator
                Projectile.velocity = Vector2.Zero;

                // Draw indicator line in front of the arrow
                if (Main.rand.NextBool(2))
                {
                    Vector2 indicatorPos = Projectile.Center + storedVelocity * Main.rand.NextFloat(20f, 200f);
                    Dust dust = Dust.NewDustPerfect(indicatorPos, DustID.Shadowflame, Vector2.Zero, 100, default, 0.6f);
                    dust.noGravity = true;
                    dust.fadeIn = 0.5f;
                }

                // Spawn particles around the arrow
                if (Main.rand.NextBool(3))
                {
                    Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 100, default, 0.8f);
                    dust.noGravity = true;
                    dust.velocity *= 0.2f;
                }
            }
            else if (!hasDashed)
            {
                // Dash forward
                hasDashed = true;
                Projectile.velocity = storedVelocity * DashSpeed;
                Projectile.netUpdate = true;
                
                if (Main.netMode != NetmodeID.Server)
                {
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnifeLaunch") { Volume = 0.7f }, Projectile.Center);
                }

                // Burst of particles on dash
                for (int i = 0; i < 8; i++)
                {
                    Dust dust = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Shadowflame, 0f, 0f, 100, default, 1.2f);
                    dust.velocity = Main.rand.NextVector2Circular(4f, 4f);
                    dust.noGravity = true;
                }
            }
            else
            {
                // After dash, update rotation to match velocity
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            // Draw afterimages when dashing
            if (hasDashed)
            {
                for (int i = 0; i < Projectile.oldPos.Length; i++)
                {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;

                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    float trailOpacity = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                    Color trailColor = Color.White * trailOpacity * 0.5f;

                    Main.EntitySpriteDraw(texture, drawPos, null, trailColor, Projectile.oldRot[i], origin, Projectile.scale, SpriteEffects.None, 0);
                }
            }

            // Draw main projectile fully lit (emissive)
            Vector2 mainPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(texture, mainPos, null, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            // Draw indicator line during pause
            if (!hasDashed && Projectile.ai[0] < PauseTime)
            {
                float progress = Projectile.ai[0] / PauseTime;
                // Six seeeeveeeen
                float lengthMultiplier = 1f + 2f * progress;
                float lineLength = 670f * progress * lengthMultiplier;
                Color lineColor = Color.Lerp(Color.Purple * 0.3f, Color.White * 0.8f, progress);

                Vector2 lineStart = Projectile.Center - Main.screenPosition;
                Vector2 lineEnd = lineStart + storedVelocity * lineLength;

                // Draw line using pixel texture
                Texture2D pixel = TextureAssets.MagicPixel.Value;
                Vector2 lineVector = lineEnd - lineStart;
                float lineRotation = lineVector.ToRotation();
                float lineScale = lineVector.Length();

                Main.EntitySpriteDraw(pixel, lineStart, new Rectangle(0, 0, 1, 1), lineColor, lineRotation, Vector2.Zero, new Vector2(lineScale, 2f), SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            // Impact particles
            for (int i = 0; i < 10; i++)
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 100, default, 1.0f);
                dust.velocity = Main.rand.NextVector2Circular(4f, 4f);
                dust.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
        }
    }
}
