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
    public class RoaringYoyoProjectile : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 12f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 280f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 14f;
            
            // Trail for afterimages
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(Projectile.localAI[1]);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            Projectile.localAI[1] = reader.ReadSingle();
        }

        public override void AI()
        {
            // Kill if too far from player
            if ((Projectile.position - Main.player[Projectile.owner].position).Length() > 3200f)
            {
                Projectile.Kill();
                return;
            }

            // Add light effect
            Lighting.AddLight(Projectile.Center, 0.5f, 0.4f, 0.2f);

            // Spawn stars periodically, faster rate (every 10 ticks instead of 20)
            Projectile.localAI[1]++;
            
            if (Projectile.localAI[1] % 10f == 0f && Main.myPlayer == Projectile.owner)
            {
                // Calculate direction based on timer (rotating pattern)
                int patternIndex = (int)(Projectile.localAI[1] / 10f) % 8;
                
                Vector2 velocity = patternIndex switch
                {
                    0 => new Vector2(0f, -8f),
                    1 => new Vector2(5.6f, -5.6f),
                    2 => new Vector2(8f, 0f),
                    3 => new Vector2(5.6f, 5.6f),
                    4 => new Vector2(0f, 8f),
                    5 => new Vector2(-5.6f, 5.6f),
                    6 => new Vector2(-8f, 0f),
                    7 => new Vector2(-5.6f, -5.6f),
                    _ => new Vector2(0f, -8f)
                };
                
                // Add slight randomness
                Vector2 vel1 = velocity.RotatedByRandom(0.15f) * Main.rand.NextFloat(0.9f, 1.1f);
                Vector2 vel2 = (-velocity).RotatedByRandom(0.15f) * Main.rand.NextFloat(0.9f, 1.1f);

                // Play sound
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.4f, Pitch = 0.5f }, Projectile.Center);
                
                // Spawn mini stars from BOTH ends (opposite directions)
                int starType = ModContent.ProjectileType<RoaringMiniStar>();
                int damage = (int)(Projectile.damage * 0.6f);
                
                // First star
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    vel1,
                    starType,
                    damage,
                    Projectile.knockBack * 0.5f,
                    Projectile.owner
                );
                
                // Second star (opposite direction)
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    vel2,
                    starType,
                    damage,
                    Projectile.knockBack * 0.5f,
                    Projectile.owner
                );
                
                // Reset pattern after full rotation
                if (patternIndex == 7)
                {
                    Projectile.localAI[1] = 0f;
                }
            }
        }
        
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            
            // Fullbright golden color
            Color drawColor = new Color(255, 220, 150);
            
            // Draw afterimage trail
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                float t = i / (float)Projectile.oldPos.Length;
                float alpha = (1f - t) * 0.5f;
                float scale = Projectile.scale * (1f - t * 0.3f);
                
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                
                Main.spriteBatch.Draw(
                    tex,
                    pos,
                    null,
                    drawColor * alpha,
                    Projectile.oldRot[i],
                    origin,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
            
            // Draw main yoyo (fullbright, ignores lighting)
            Main.spriteBatch.Draw(
                tex,
                Projectile.Center - Main.screenPosition,
                null,
                drawColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );
            
            return false;
        }
    }
}
