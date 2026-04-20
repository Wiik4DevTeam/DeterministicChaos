using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
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
    // Spade bullet fired by the AceOfSpades gun. Pierces up to 3 enemies.
    // On each hit, splits into 3 half-size, half-damage projectiles in a Y shape, then despawns.
    // ai[0] = split generation (0 = original, max 3)
    public class AceOfSpadesBullet : ModProjectile
    {
        private int _age;
        private const int MaxSplits = 3;
        private const int HitImmunityTicks = 12; // ~0.2 seconds before children can hit same target
        private const int WidthRampTicks = 12; // 0.2s to reach full width

        private ref float SplitGen => ref Projectile.ai[0];

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 4; // hits 3 enemies then dies on the 4th
            Projectile.timeLeft = 300;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override void AI()
        {
            _age++;

            // Smallest generation can't pierce, set penetrate to 1 on first tick
            if (_age == 1 && (int)SplitGen >= MaxSplits)
                Projectile.penetrate = 1;

            // Muzzle flash on first tick
            if (_age == 1)
                SpawnMuzzleFlash();

            if (Projectile.velocity.LengthSquared() > 0.1f)
                Projectile.rotation = Projectile.velocity.ToRotation();
        }

        private void SpawnMuzzleFlash()
        {
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 flashPos = Projectile.Center - dir * 12f;

            // Bright core flash
            for (int i = 0; i < 6; i++)
            {
                Vector2 dustVel = dir.RotatedByRandom(MathHelper.PiOver4) * Main.rand.NextFloat(2f, 5f);
                Dust d = Dust.NewDustDirect(flashPos, 0, 0, DustID.BlueTorch, dustVel.X, dustVel.Y, 100, default, 1.4f);
                d.noGravity = true;
                d.fadeIn = 1.1f;
            }

            // Hot white sparks
            for (int i = 0; i < 4; i++)
            {
                Vector2 sparkVel = dir.RotatedByRandom(MathHelper.Pi / 6f) * Main.rand.NextFloat(3f, 7f);
                Dust s = Dust.NewDustDirect(flashPos, 0, 0, DustID.WhiteTorch, sparkVel.X, sparkVel.Y, 0, default, 0.8f);
                s.noGravity = true;
            }
        }

        public override bool? CanDamage()
        {
            // Children have a brief immunity window so they don't instantly hit the same target
            if ((int)SplitGen > 0 && _age < HitImmunityTicks)
                return false;
            return null;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.myPlayer != Projectile.owner)
                return;

            int gen = (int)SplitGen;
            if (gen >= MaxSplits)
                return;

            int nextGen = gen + 1;
            int splitDamage = (int)(Projectile.damage * 0.667f);
            float nextScale = Projectile.scale * 0.667f;
            int type = ModContent.ProjectileType<AceOfSpadesBullet>();
            Vector2 vel = Projectile.velocity;
            float speed = vel.Length();
            Vector2 dir = vel.SafeNormalize(Vector2.UnitX);

            // Y shape: one backward, two diagonal forward
            Vector2 backVel = -dir * speed;
            Vector2 diagLeft = dir.RotatedBy(-MathHelper.PiOver4) * speed;
            Vector2 diagRight = dir.RotatedBy(MathHelper.PiOver4) * speed;

            // Backward (stem of Y)
            int b = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, backVel,
                type, splitDamage, Projectile.knockBack * 0.5f, Projectile.owner, nextGen);
            if (b >= 0 && b < Main.maxProjectiles)
                Main.projectile[b].scale = nextScale;

            // Diagonal left (branch of Y)
            int dl = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, diagLeft,
                type, splitDamage, Projectile.knockBack * 0.5f, Projectile.owner, nextGen);
            if (dl >= 0 && dl < Main.maxProjectiles)
                Main.projectile[dl].scale = nextScale;

            // Diagonal right (branch of Y)
            int dr = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, diagRight,
                type, splitDamage, Projectile.knockBack * 0.5f, Projectile.owner, nextGen);
            if (dr >= 0 && dr < Main.maxProjectiles)
                Main.projectile[dr].scale = nextScale;

            // Parent despawns after splitting
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 pos = Projectile.Center - Main.screenPosition;

            const int TweenFrames = 6;
            float spawnT = MathHelper.Clamp(_age / (float)TweenFrames, 0f, 1f);
            float despawnT = MathHelper.Clamp(Projectile.timeLeft / (float)TweenFrames, 0f, 1f);
            float tweenScale = Math.Min(spawnT, despawnT);

            // Width ramp: starts thin, reaches full width over 0.2s
            float widthT = MathHelper.Clamp(_age / (float)WidthRampTicks, 0f, 1f);
            float widthFactor = MathHelper.Lerp(0.2f, 1f, widthT);

            float baseScale = Projectile.scale * tweenScale;
            // Scale as a 2D vector: full length, ramping width
            Vector2 drawScale = new Vector2(baseScale, baseScale * widthFactor);

            Color tint = Color.Lerp(Color.White, new Color(40, 40, 200), 0.7f);
            Color glowColor = new Color(40, 40, 200) * (0.5f * tweenScale);
            Main.EntitySpriteDraw(tex, pos, null, glowColor, Projectile.rotation, origin, drawScale * 1.3f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, tint * tweenScale, Projectile.rotation, origin, drawScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
