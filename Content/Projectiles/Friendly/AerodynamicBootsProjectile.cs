using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;
using DeterministicChaos.Content.Items.Accessories;
using DeterministicChaos.Content.Items.BossBags;
using DeterministicChaos.Content.Items.BossSummons;
using DeterministicChaos.Content.Items.Consumables;
using DeterministicChaos.Content.Items.DamageClasses;
using DeterministicChaos.Content.Items.Globals;
using DeterministicChaos.Content.Items.Materials;
using DeterministicChaos.Content.Items.Placeable;
using DeterministicChaos.Content.Items.Rarities;
using DeterministicChaos.Content.Items.Weapons;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class AerodynamicBootsProjectile : ModProjectile
    {
        // ai[0] = grace stacks when launched
        private int GraceStacks => (int)Projectile.ai[0];

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
            Projectile.timeLeft = 6;
            Projectile.DamageType = ModContent.GetInstance<SummonerMeleeDamageClass>();
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.netImportant = true;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            var bootsPlayer = owner.GetModPlayer<AerodynamicBootsPlayer>();

            if (bootsPlayer.HasParriedThisDash)
            {
                Projectile.Kill();
                return;
            }

            // Center hitbox on the player
            Projectile.Center = owner.Center;
            Projectile.velocity = owner.velocity;

            if (owner.velocity.Length() > 0.5f)
                Projectile.rotation = owner.velocity.ToRotation();

            Vector2 dashDir = owner.velocity.SafeNormalize(Vector2.Zero);
            Vector2 perp = new Vector2(-dashDir.Y, dashDir.X);

            // Flame aura dust, biased forward in movement direction
            for (int i = 0; i < 4 + GraceStacks; i++)
            {
                // Spawn around the player, biased toward movement direction
                float forwardBias = Main.rand.NextFloat(0f, 20f);
                float sideBias = Main.rand.NextFloat(-20f, 20f);
                Vector2 offset = dashDir * forwardBias + perp * sideBias;
                Vector2 spawnPos = owner.Center + offset + Main.rand.NextVector2Circular(8f, 8f);

                // Dust drifts opposite to movement
                Vector2 dustVel = -dashDir * Main.rand.NextFloat(2f, 6f) + Main.rand.NextVector2Circular(1.5f, 1.5f);

                Dust flame = Dust.NewDustDirect(spawnPos, 0, 0, DustID.BlueFairy, dustVel.X, dustVel.Y);
                flame.noGravity = true;
                flame.scale = 1.4f + Main.rand.NextFloat(0.6f) + GraceStacks * 0.1f;
                flame.fadeIn = 1.6f;
                flame.alpha = 120;
            }

            // Dark blue integrity wisps orbiting the player
            if (Main.rand.NextBool(2))
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = 16f + Main.rand.NextFloat(10f);
                Vector2 orbPos = owner.Center + new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle)) * radius;
                Dust wisp = Dust.NewDustDirect(orbPos, 0, 0, DustID.BlueTorch, -dashDir.X * 2f, -dashDir.Y * 2f);
                wisp.noGravity = true;
                wisp.scale = 1.5f + GraceStacks * 0.2f;
            }

            // Speed lines
            if (Main.myPlayer == Projectile.owner && Main.rand.NextBool(2))
            {
                float sideOffset = Main.rand.NextFloat(-40f, 40f);
                Vector2 spawnPos = owner.Center + perp * sideOffset - dashDir * Main.rand.NextFloat(10f, 30f);
                Vector2 lineVel = -dashDir * Main.rand.NextFloat(4f, 8f) + perp * Main.rand.NextFloat(-1f, 1f);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromAI(),
                    spawnPos, lineVel,
                    ModContent.ProjectileType<SpeedLine>(),
                    0, 0f, Projectile.owner,
                    Main.rand.NextFloat(25f, 50f), Main.rand.NextFloat(2f, 4f));
            }

            // Stack-colored sparks
            if (GraceStacks > 0 && Main.rand.NextBool(2))
            {
                Color stackColor = GetStackColor();
                Vector2 sparkPos = owner.Center + Main.rand.NextVector2Circular(20f, 20f);
                Dust dust = Dust.NewDustDirect(sparkPos, 0, 0, DustID.WhiteTorch);
                dust.color = stackColor;
                dust.noGravity = true;
                dust.scale = 0.8f + GraceStacks * 0.15f;
            }

            Lighting.AddLight(owner.Center, new Vector3(0.3f, 0.6f, 0.9f) * (1f + GraceStacks * 0.15f));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Player owner = Main.player[Projectile.owner];
            var bootsPlayer = owner.GetModPlayer<AerodynamicBootsPlayer>();

            bootsPlayer.OnDashHitEnemy(target);
            owner.MinionAttackTargetNPC = target.whoAmI;

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = 0.3f }, target.Center);

            for (int i = 0; i < 10 + (GraceStacks * 3); i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(6f, 6f);
                Dust dust = Dust.NewDustDirect(target.Center, 0, 0, DustID.BlueFairy, dustVel.X, dustVel.Y);
                dust.noGravity = true;
                dust.scale = 1.3f + (GraceStacks * 0.15f);
            }
            for (int i = 0; i < 8; i++)
            {
                Vector2 sparkVel = Main.rand.NextVector2Circular(4f, 4f);
                Dust spark = Dust.NewDustDirect(target.Center, 0, 0, DustID.IceTorch, sparkVel.X, sparkVel.Y);
                spark.noGravity = true;
                spark.scale = 1.2f;
            }

            Projectile.Kill();
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.FinalDamage += GraceStacks * 0.12f;
            if (GraceStacks >= 3)
                modifiers.CritDamage += 0.2f;
        }

        private Color GetStackColor()
        {
            return GraceStacks switch
            {
                1 => new Color(120, 190, 255),
                2 => new Color(100, 170, 255),
                3 => new Color(80, 150, 255),
                4 => new Color(60, 130, 255),
                5 => new Color(40, 110, 255),
                _ => new Color(140, 210, 255)
            };
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player owner = Main.player[Projectile.owner];
            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = texture.Size() / 2f;
            float movementRotation = Projectile.rotation;

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
                Color afterColor = new Color(30, 120, 255) * alpha;

                Vector2 afterPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float afterRot = Projectile.oldRot[i];

                Main.spriteBatch.Draw(texture, afterPos, null, afterColor, afterRot,
                    drawOrigin, afterScale, SpriteEffects.None, 0f);
            }

            // Main sprite on the player, facing movement direction
            Vector2 drawPos = owner.Center - Main.screenPosition;
            Color mainColor = new Color(60, 150, 255) * 0.7f;
            Main.spriteBatch.Draw(texture, drawPos, null, mainColor, movementRotation,
                drawOrigin, baseScale, SpriteEffects.None, 0f);

            // Brighter core
            Color coreColor = new Color(160, 210, 255) * 0.45f;
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
                Dust dust = Dust.NewDustDirect(owner.Center, 0, 0, DustID.IceTorch, dustVel.X, dustVel.Y);
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
