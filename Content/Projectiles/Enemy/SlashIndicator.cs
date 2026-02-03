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
    public class SlashIndicator : ModProjectile
    {
        private const int Life = 60;
        private const float OmegaMax = 0.0125f;
        private const float DirectionSign = -1f;
        private const float AngularScale = 0.6f;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.timeLeft = Life;

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

            Projectile.timeLeft = Life;
            
            // Play indicator sound
            if (Main.netMode != NetmodeID.Server)
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
            // Lock to anchor forever (no following the player).
            Projectile.Center = new Vector2(Projectile.localAI[0], Projectile.localAI[1]);

            float age = (Life - Projectile.timeLeft);
            float t = age / Life;

            // ai[0] = damage, ai[1] = baseAngle. We store omega in velocity.X (so we don't need localAI[2+]).
            float baseAngle = (Projectile.ai.Length > 1) ? Projectile.ai[1] : 0f;
            float rotationDirection = Projectile.velocity.X; // +1 or -1

            // Ease-out rotation integral (stable, single-direction slowdown).
            // Rotate exactly 45 degrees over lifetime
            float targetDelta = MathHelper.PiOver2 * rotationDirection; // 90 degrees instead of 45
            float easedT = 1f - (1f - t) * (1f - t) * (1f - t);
            float delta = targetDelta * easedT;
            Projectile.rotation = baseAngle + delta + MathHelper.PiOver2;

            // Spawn slash on the last tick (server only).
            if (Projectile.timeLeft == 1 && Main.netMode != NetmodeID.MultiplayerClient)
                SpawnSlash();
        }

        private void SpawnSlash()
        {
            int type = ModContent.ProjectileType<SlashAttack>();
            int damage = (Projectile.ai.Length > 0) ? (int)Projectile.ai[0] : 60;

            Vector2 pos = Projectile.Center;

            float baseAngle = (Projectile.ai.Length > 1) ? Projectile.ai[1] : 0f;
            float rotationDirection = Projectile.velocity.X;

            // t = 1 => delta = ω0*Life*(1 - 1/3) = ω0*Life*(2/3)
            float finalDelta = MathHelper.PiOver2 * rotationDirection; // 90 degrees instead of 45
            float finalRotation = baseAngle + finalDelta + MathHelper.PiOver2;

            // Align to your SlashAttack's "facing left" art expectation (keep your previous correction).
            finalRotation += MathHelper.Pi;

            // Random vertical flip for variety: encode in ai[1] bit-flag (2 = flipV).
            int flipFlags = Main.rand.NextBool() ? 2 : 0;

            // ai[0] = finalRotation, ai[1] = flipFlags, ai[2] = colorMode (0 = red)
            int p = Projectile.NewProjectile(
                Projectile.GetSource_FromAI(),
                pos,
                Vector2.Zero,
                type,
                damage,
                0f,
                Main.myPlayer,
                finalRotation,
                flipFlags,
                0f
            );

            if (p >= 0 && p < Main.maxProjectiles)
                Main.projectile[p].netUpdate = true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            if (tex == null)
                return false;

            float age = (Life - Projectile.timeLeft);
            float t = age / Life;

            float start = 0.10f;
            float end = 2.50f;
            float growEase = 1f - (1f - t) * (1f - t);
            float s = MathHelper.Lerp(start, end, growEase);

            // Fade to red earlier
            float redStart = 0.20f;
            float redT = MathHelper.Clamp((t - redStart) / (1f - redStart), 0f, 1f);

            // Full emissive look
            Color c = Color.Lerp(Color.White, Color.Red, redT) * 0.95f;

            Vector2 pos = Projectile.Center - Main.screenPosition;

            Rectangle src = tex.Bounds;
            Vector2 origin = src.Size() * 0.5f;

            Vector2 scale = new Vector2(s * 0.45f, s);

            Main.EntitySpriteDraw(tex, pos, src, c, Projectile.rotation, origin, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
