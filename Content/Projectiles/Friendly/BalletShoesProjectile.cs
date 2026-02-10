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

        // Afterimage positions
        private const int AFTERIMAGE_COUNT = 8;
        private Vector2[] oldPositions = new Vector2[AFTERIMAGE_COUNT];
        private float[] oldRotations = new float[AFTERIMAGE_COUNT];

        public override void SetStaticDefaults()
        {
            // Enable trail for afterimages
            ProjectileID.Sets.TrailCacheLength[Type] = AFTERIMAGE_COUNT;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1; // Infinite pierce
            Projectile.timeLeft = 20; // Matches kick duration
            Projectile.DamageType = ModContent.GetInstance<SummonerMeleeDamageClass>();
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;

            // Good multiplayer sync
            Projectile.netImportant = true;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            var shoesPlayer = owner.GetModPlayer<BalletShoesPlayer>();

            // If player parried, kill the projectile immediately
            if (shoesPlayer.HasParriedThisKick)
            {
                Projectile.Kill();
                return;
            }

            // Follow the player
            Projectile.Center = owner.Center + owner.velocity.SafeNormalize(Vector2.Zero) * 20f;
            Projectile.velocity = owner.velocity;

            // Rotate based on movement direction
            if (owner.velocity.Length() > 0.5f)
            {
                Projectile.rotation = owner.velocity.ToRotation();
            }

            // Check parry window for special effects
            bool inParryWindow = shoesPlayer.IsCurrentlyInParryWindow;

            // Golden particles during parry window
            if (inParryWindow)
            {
                // Bright golden dust to show parry opportunity
                Dust gold = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    0, 0,
                    DustID.GoldFlame,
                    0f, 0f
                );
                gold.noGravity = true;
                gold.scale = 1.2f + Main.rand.NextFloat(0.3f);
                gold.velocity = Main.rand.NextVector2Circular(2f, 2f);

                // Extra bright light during parry window
                Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.5f));
            }

            // Blue sparkles
            if (Main.rand.NextBool(2))
            {
                Dust sparkle = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(15f, 15f),
                    0, 0,
                    inParryWindow ? DustID.GoldFlame : DustID.BlueTorch,
                    -owner.velocity.X * 0.2f,
                    -owner.velocity.Y * 0.2f
                );
                sparkle.noGravity = true;
                sparkle.scale = 0.8f + Main.rand.NextFloat(0.4f);
                sparkle.fadeIn = 1.2f;
            }

            // Extra blue sparkles trailing behind
            if (Main.rand.NextBool(3))
            {
                Vector2 trailPos = Projectile.Center - owner.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(10f, 30f);
                Dust trail = Dust.NewDustDirect(
                    trailPos,
                    0, 0,
                    inParryWindow ? DustID.GoldFlame : DustID.BlueFairy,
                    0f, 0f
                );
                trail.noGravity = true;
                trail.scale = 0.6f + Main.rand.NextFloat(0.3f);
                trail.velocity = Main.rand.NextVector2Circular(1f, 1f);
            }

            // Purple dust for Integrity theme
            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(
                    owner.position,
                    owner.width,
                    owner.height,
                    DustID.PurpleTorch,
                    -owner.velocity.X * 0.3f,
                    -owner.velocity.Y * 0.3f
                );
                dust.noGravity = true;
                dust.scale = 1.2f + (ParryStacks * 0.3f);
            }

            // Extra visual for stacks
            if (ParryStacks > 0 && Main.rand.NextBool(3))
            {
                Color stackColor = GetStackColor();
                Dust dust = Dust.NewDustDirect(
                    owner.Center + Main.rand.NextVector2Circular(20f, 20f),
                    0, 0,
                    DustID.WhiteTorch
                );
                dust.color = stackColor;
                dust.noGravity = true;
                dust.scale = 0.8f + (ParryStacks * 0.2f);
            }

            // Bright light - blue/purple
            Lighting.AddLight(Projectile.Center, new Vector3(0.3f, 0.4f, 0.9f) * (1f + ParryStacks * 0.2f));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Player owner = Main.player[Projectile.owner];
            var shoesPlayer = owner.GetModPlayer<BalletShoesPlayer>();

            // Notify player of hit - grants a stack, gives immunity, and knockback
            shoesPlayer.OnKickHitEnemy(target);

            // Focus summons on this target (like whip targeting)
            owner.MinionAttackTargetNPC = target.whoAmI;

            // Hit sound
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = 0.2f }, target.Center);

            // Impact visuals - purple and blue burst
            for (int i = 0; i < 8 + (ParryStacks * 4); i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(6f, 6f);
                Dust dust = Dust.NewDustDirect(target.Center, 0, 0, DustID.PurpleTorch, dustVel.X, dustVel.Y);
                dust.noGravity = true;
                dust.scale = 1.3f + (ParryStacks * 0.2f);
            }

            // Blue sparkle burst on hit
            for (int i = 0; i < 6; i++)
            {
                Vector2 sparkVel = Main.rand.NextVector2Circular(4f, 4f);
                Dust spark = Dust.NewDustDirect(target.Center, 0, 0, DustID.BlueTorch, sparkVel.X, sparkVel.Y);
                spark.noGravity = true;
                spark.scale = 1.2f;
            }

            // End the projectile after hitting
            Projectile.Kill();
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Bonus damage with stacks
            modifiers.FinalDamage += ParryStacks * 0.15f; // +15% damage per stack

            // Bonus crit damage with stacks
            if (ParryStacks >= 2)
            {
                modifiers.CritDamage += 0.25f; // 25% extra crit damage at 2+ stacks
            }
        }

        private Color GetStackColor()
        {
            // Check if in parry window - make it glow bright gold/white
            Player owner = Main.player[Projectile.owner];
            var shoesPlayer = owner.GetModPlayer<BalletShoesPlayer>();
            
            if (shoesPlayer.IsCurrentlyInParryWindow)
            {
                // Bright gold/white glow during parry window
                return new Color(255, 240, 180); // Bright golden
            }

            return ParryStacks switch
            {
                1 => new Color(180, 150, 255),  // Light purple
                2 => new Color(220, 180, 255),  // Brighter purple
                3 => new Color(255, 200, 255),  // Brightest pink-purple
                _ => new Color(100, 150, 255)   // Blue-ish default
            };
        }

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 drawOrigin = texture.Size() / 2f;

            // Draw afterimages
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                Vector2 afterimagePos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float afterimageRot = Projectile.oldRot[i];
                
                // Fade out based on index
                float progress = (float)i / Projectile.oldPos.Length;
                float alpha = (1f - progress) * 0.5f;
                float scale = (1f - progress * 0.3f) * (1f + ParryStacks * 0.1f);
                
                // Blue-purple gradient for afterimages
                Color afterimageColor = Color.Lerp(new Color(100, 150, 255), new Color(180, 130, 255), progress) * alpha;

                spriteBatch.Draw(
                    texture,
                    afterimagePos,
                    null,
                    afterimageColor,
                    afterimageRot,
                    drawOrigin,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }

            // Draw main projectile emissively (ignore lighting, full brightness)
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color emissiveColor = GetStackColor(); // Full color, not affected by lighting
            float mainScale = 1f + ParryStacks * 0.15f;

            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                emissiveColor,
                Projectile.rotation,
                drawOrigin,
                mainScale,
                SpriteEffects.None,
                0f
            );

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            // Final dust burst when kick ends - blue and purple
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
                Dust dust = Dust.NewDustDirect(owner.Center, 0, 0, DustID.PurpleTorch, dustVel.X, dustVel.Y);
                dust.noGravity = true;
                dust.scale = 1f;
            }
        }
    }
}
