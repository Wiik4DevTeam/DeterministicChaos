using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class SteadfastV1DashProjectile : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 44;
            Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10; // Matches DASH_TOTAL
            Projectile.DamageType = ModContent.GetInstance<SummonerMeleeDamageClass>();
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // Hit each NPC once
            Projectile.netImportant = true;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            var sfPlayer = owner.GetModPlayer<SteadfastV1Player>();

            // Center hitbox on the player
            Projectile.Center = owner.Center;
            Projectile.velocity = owner.velocity;

            if (owner.velocity.Length() > 0.5f)
                Projectile.rotation = owner.velocity.ToRotation();

            float speed = owner.velocity.Length();
            Vector2 dashDir = owner.velocity.SafeNormalize(Vector2.Zero);
            Vector2 perp = new Vector2(-dashDir.Y, dashDir.X);

            // Light trail dust (secondary, sprite is the main visual)
            if (Main.rand.NextBool(2))
            {
                Vector2 offset = perp * Main.rand.NextFloat(-16f, 16f) - dashDir * Main.rand.NextFloat(8f, 20f);
                Dust trail = Dust.NewDustDirect(owner.Center + offset, 0, 0, DustID.BlueTorch,
                    -dashDir.X * 3f, -dashDir.Y * 3f);
                trail.noGravity = true;
                trail.scale = 1.2f + Main.rand.NextFloat(0.4f);
            }

            // Speed lines during burst phase
            if (Main.myPlayer == Projectile.owner && speed > 20f && Main.rand.NextBool(2))
            {
                float sideOffset = Main.rand.NextFloat(-50f, 50f);
                Vector2 spawnPos = owner.Center + perp * sideOffset - dashDir * Main.rand.NextFloat(10f, 40f);
                Vector2 lineVel = -dashDir * Main.rand.NextFloat(5f, 12f) + perp * Main.rand.NextFloat(-1.5f, 1.5f);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromAI(),
                    spawnPos, lineVel,
                    ModContent.ProjectileType<SpeedLine>(),
                    0, 0f, Projectile.owner,
                    Main.rand.NextFloat(30f, 60f), Main.rand.NextFloat(2f, 5f));
            }

            Lighting.AddLight(owner.Center, new Vector3(0.3f, 0.6f, 0.9f));

            // Kill if dash ended
            if (!sfPlayer.IsDashing)
                Projectile.Kill();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Player owner = Main.player[Projectile.owner];
            var sfPlayer = owner.GetModPlayer<SteadfastV1Player>();
            sfPlayer.OnDashHitEnemy(target);

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = 0.3f }, target.Center);

            for (int i = 0; i < 20; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(8f, 8f);
                Dust dust = Dust.NewDustDirect(target.Center, 0, 0, DustID.IceTorch, dustVel.X, dustVel.Y);
                dust.noGravity = true;
                dust.scale = 1.5f + Main.rand.NextFloat(0.3f);
            }
            for (int i = 0; i < 12; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(5f, 5f);
                Dust dust = Dust.NewDustDirect(target.Center, 0, 0, DustID.GoldFlame, dustVel.X, dustVel.Y);
                dust.noGravity = true;
                dust.scale = 1.3f;
            }
            for (int i = 0; i < 8; i++)
            {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 ringVel = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle)) * 4f;
                Dust dust = Dust.NewDustDirect(target.Center, 0, 0, DustID.WhiteTorch, ringVel.X, ringVel.Y);
                dust.noGravity = true;
                dust.scale = 1.2f;
            }

            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player owner = Main.player[Projectile.owner];
            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = texture.Size() / 2f;
            float movementRotation = Projectile.rotation;

            float pulse = 0.9f + 0.12f * (float)System.Math.Sin(Main.GameUpdateCount * 0.2f);
            float baseScale = 2.2f * pulse;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // Afterimages trailing behind the player
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = (float)i / Projectile.oldPos.Length;
                float alpha = (1f - progress) * 0.55f;
                float afterScale = baseScale * (1f - progress * 0.3f);
                Color afterColor = new Color(30, 120, 255) * alpha;

                Vector2 afterPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float afterRot = Projectile.oldRot[i];

                Main.spriteBatch.Draw(texture, afterPos, null, afterColor, afterRot,
                    drawOrigin, afterScale, SpriteEffects.None, 0f);
            }

            // Main sprite on the player, facing movement direction
            Vector2 drawPos = owner.Center - Main.screenPosition;
            Color mainColor = new Color(60, 150, 255) * 0.75f;
            Main.spriteBatch.Draw(texture, drawPos, null, mainColor, movementRotation,
                drawOrigin, baseScale, SpriteEffects.None, 0f);

            // Brighter core
            Color coreColor = new Color(160, 210, 255) * 0.5f;
            Main.spriteBatch.Draw(texture, drawPos, null, coreColor, movementRotation,
                drawOrigin, baseScale * 0.7f, SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}
