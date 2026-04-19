using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class BalletShoesProjectile : ModProjectile
    {
        // ai[0] = parry stacks when launched
        private int ParryStacks => (int)Projectile.ai[0];

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 20;
            Projectile.DamageType = ModContent.GetInstance<SummonerMeleeDamageClass>();
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            Projectile.netImportant = true;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            var shoesPlayer = owner.GetModPlayer<BalletShoesPlayer>();

            if (shoesPlayer.HasParriedThisKick)
            {
                Projectile.Kill();
                return;
            }

            // Center hitbox on the player
            Projectile.Center = owner.Center;
            Projectile.velocity = owner.velocity;

            if (owner.velocity.Length() > 0.5f)
                Projectile.rotation = owner.velocity.ToRotation();

            bool inParryWindow = shoesPlayer.IsCurrentlyInParryWindow;
            Vector2 dashDir = owner.velocity.SafeNormalize(Vector2.Zero);
            Vector2 perp = new Vector2(-dashDir.Y, dashDir.X);

            // Flame aura dust, biased forward in movement direction
            int dustCount = 3 + ParryStacks;
            int dustType = inParryWindow ? DustID.GoldFlame : DustID.BlueFairy;

            for (int i = 0; i < dustCount; i++)
            {
                float forwardBias = Main.rand.NextFloat(0f, 18f);
                float sideBias = Main.rand.NextFloat(-18f, 18f);
                Vector2 offset = dashDir * forwardBias + perp * sideBias;
                Vector2 spawnPos = owner.Center + offset + Main.rand.NextVector2Circular(6f, 6f);

                Vector2 dustVel = -dashDir * Main.rand.NextFloat(2f, 5f) + Main.rand.NextVector2Circular(1.5f, 1.5f);

                Dust flame = Dust.NewDustDirect(spawnPos, 0, 0, dustType, dustVel.X, dustVel.Y);
                flame.noGravity = true;
                flame.scale = 1.3f + Main.rand.NextFloat(0.5f) + ParryStacks * 0.15f;
                flame.fadeIn = 1.5f;
                flame.alpha = 120;
            }

            // Golden glow during parry window
            if (inParryWindow && Main.rand.NextBool(2))
            {
                Vector2 orbPos = owner.Center + Main.rand.NextVector2Circular(22f, 22f);
                Dust gold = Dust.NewDustDirect(orbPos, 0, 0, DustID.GoldFlame, 0f, 0f);
                gold.noGravity = true;
                gold.scale = 1.2f + Main.rand.NextFloat(0.3f);
                gold.velocity = Main.rand.NextVector2Circular(2f, 2f);
                Lighting.AddLight(owner.Center, new Vector3(1f, 0.9f, 0.5f));
            }

            // Dark blue integrity wisps orbiting the player
            if (Main.rand.NextBool(2))
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = 14f + Main.rand.NextFloat(10f);
                Vector2 orbPos = owner.Center + new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle)) * radius;
                Dust wisp = Dust.NewDustDirect(orbPos, 0, 0, DustID.BlueTorch, -dashDir.X * 2f, -dashDir.Y * 2f);
                wisp.noGravity = true;
                wisp.scale = 1.4f + ParryStacks * 0.3f;
            }

            // Stack-colored sparks
            if (ParryStacks > 0 && Main.rand.NextBool(2))
            {
                Color stackColor = GetStackColor();
                Vector2 sparkPos = owner.Center + Main.rand.NextVector2Circular(20f, 20f);
                Dust dust = Dust.NewDustDirect(sparkPos, 0, 0, DustID.WhiteTorch);
                dust.color = stackColor;
                dust.noGravity = true;
                dust.scale = 0.8f + ParryStacks * 0.2f;
            }

            Lighting.AddLight(owner.Center, new Vector3(0.3f, 0.4f, 0.9f) * (1f + ParryStacks * 0.2f));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Player owner = Main.player[Projectile.owner];
            var shoesPlayer = owner.GetModPlayer<BalletShoesPlayer>();

            shoesPlayer.OnKickHitEnemy(target);
            owner.MinionAttackTargetNPC = target.whoAmI;

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = 0.2f }, target.Center);

            for (int i = 0; i < 8 + (ParryStacks * 4); i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(6f, 6f);
                Dust dust = Dust.NewDustDirect(target.Center, 0, 0, DustID.BlueFairy, dustVel.X, dustVel.Y);
                dust.noGravity = true;
                dust.scale = 1.3f + (ParryStacks * 0.2f);
            }
            for (int i = 0; i < 6; i++)
            {
                Vector2 sparkVel = Main.rand.NextVector2Circular(4f, 4f);
                Dust spark = Dust.NewDustDirect(target.Center, 0, 0, DustID.BlueTorch, sparkVel.X, sparkVel.Y);
                spark.noGravity = true;
                spark.scale = 1.2f;
            }

            Projectile.Kill();
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.FinalDamage += ParryStacks * 0.15f;
            if (ParryStacks >= 2)
                modifiers.CritDamage += 0.25f;
        }

        private Color GetStackColor()
        {
            Player owner = Main.player[Projectile.owner];
            var shoesPlayer = owner.GetModPlayer<BalletShoesPlayer>();

            if (shoesPlayer.IsCurrentlyInParryWindow)
                return new Color(255, 240, 180);

            return ParryStacks switch
            {
                1 => new Color(80, 130, 255),
                2 => new Color(50, 100, 230),
                3 => new Color(30, 80, 210),
                _ => new Color(100, 150, 255)
            };
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player owner = Main.player[Projectile.owner];
            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = texture.Size() / 2f;
            float movementRotation = Projectile.rotation;

            var shoesPlayer = owner.GetModPlayer<BalletShoesPlayer>();
            bool inParryWindow = shoesPlayer.IsCurrentlyInParryWindow;

            float pulse = 0.9f + 0.1f * (float)System.Math.Sin(Main.GameUpdateCount * 0.2f);
            float baseScale = 1.8f * pulse;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // Afterimages trailing behind the player
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = (float)i / Projectile.oldPos.Length;
                float alpha = (1f - progress) * 0.5f;
                float afterScale = baseScale * (1f - progress * 0.3f);
                Color afterColor = inParryWindow
                    ? new Color(255, 210, 80) * alpha
                    : new Color(30, 120, 255) * alpha;

                Vector2 afterPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float afterRot = Projectile.oldRot[i];

                Main.spriteBatch.Draw(texture, afterPos, null, afterColor, afterRot,
                    drawOrigin, afterScale, SpriteEffects.None, 0f);
            }

            // Main sprite on the player, facing movement direction
            Vector2 drawPos = owner.Center - Main.screenPosition;
            Color mainColor = inParryWindow
                ? new Color(255, 230, 120) * 0.7f
                : new Color(60, 150, 255) * 0.7f;
            Main.spriteBatch.Draw(texture, drawPos, null, mainColor, movementRotation,
                drawOrigin, baseScale, SpriteEffects.None, 0f);

            // Brighter core
            Color coreColor = inParryWindow
                ? new Color(255, 245, 200) * 0.45f
                : new Color(160, 210, 255) * 0.45f;
            Main.spriteBatch.Draw(texture, drawPos, null, coreColor, movementRotation,
                drawOrigin, baseScale * 0.7f, SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            Player owner = Main.player[Projectile.owner];
            for (int i = 0; i < 8; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(3f, 3f);
                Dust dust = Dust.NewDustDirect(owner.Center, 0, 0, DustID.BlueTorch, dustVel.X, dustVel.Y);
                dust.noGravity = true;
                dust.scale = 1f;
            }
            for (int i = 0; i < 6; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(3f, 3f);
                Dust dust = Dust.NewDustDirect(owner.Center, 0, 0, DustID.BlueTorch, dustVel.X, dustVel.Y);
                dust.noGravity = true;
                dust.scale = 1f;
            }
        }
    }
}
