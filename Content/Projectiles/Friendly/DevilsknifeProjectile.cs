using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Systems.Cards;
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
    // Devilsknife scythe projectile. Boomerang-style with on-hit suit spawns.
    // ai[0] = mode: 0 = normal boomerang, 1 = stealth strike (arcing)
    // ai[1] = arc direction: +1 = arc up, -1 = arc down (only used in stealth mode)
    public class DevilsknifeProjectile : ModProjectile
    {
        private const float SpinSpeed = 0.35f;
        private const float ReturnAccel = 1.2f;
        private const float MaxReturnSpeed = 18f;
        private const int OutboundTicks = 45; // How long before it starts returning
        private const float ArcStrength = 0.18f; // Lateral force for stealth arcing
        private const int AfterimageLength = 7;

        private ref float Mode => ref Projectile.ai[0];
        private ref float ArcDir => ref Projectile.ai[1];

        private int timer;
        private bool returning;
        private int returnTimer;
        private int suitHitsRemaining;
        private bool initialized;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = AfterimageLength;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.scale = 2.1f;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];

            if (!initialized)
            {
                initialized = true;
                suitHitsRemaining = (int)Mode == 1 ? 4 : 1;
            }

            timer++;

            // Spin
            Projectile.rotation += SpinSpeed * (Projectile.velocity.X >= 0 ? 1f : -1f);

            bool isStealth = (int)Mode == 1;

            if (isStealth)
            {
                // Stealth strike: arc trajectory
                // Apply lateral force perpendicular to velocity
                if (!returning)
                {
                    Vector2 perpendicular = new Vector2(-Projectile.velocity.Y, Projectile.velocity.X).SafeNormalize(Vector2.Zero);
                    Projectile.velocity += perpendicular * ArcDir * ArcStrength;

                    // Slow down gradually
                    Projectile.velocity *= 0.99f;

                    if (timer >= OutboundTicks + 15)
                        returning = true;
                }
            }
            else
            {
                // Normal boomerang: fly out then return
                if (!returning)
                {
                    Projectile.velocity *= 0.985f;

                    if (timer >= OutboundTicks)
                        returning = true;
                }
            }

            if (returning)
            {
                returnTimer++;

                // Return to player with ramping acceleration
                Vector2 toPlayer = owner.Center - Projectile.Center;
                float dist = toPlayer.Length();

                if (dist < 30f)
                {
                    Projectile.Kill();
                    return;
                }

                // Acceleration ramps up over time so it always catches the player
                float accel = ReturnAccel + returnTimer * 0.04f;
                float maxSpeed = MaxReturnSpeed + returnTimer * 0.3f;

                Vector2 returnDir = toPlayer.SafeNormalize(Vector2.Zero);
                Projectile.velocity = (Projectile.velocity + returnDir * accel);

                if (Projectile.velocity.Length() > maxSpeed)
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }

            // Kill if owner is dead
            if (!owner.active || owner.dead)
                Projectile.Kill();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (suitHitsRemaining <= 0)
                return;

            suitHitsRemaining--;
            SpawnRandomSuitProjectile(target);
        }

        private void SpawnRandomSuitProjectile(NPC target)
        {
            if (Main.myPlayer != Projectile.owner)
                return;

            int suitType = Main.rand.Next(4);
            int baseDamage = Projectile.damage;
            float kb = 2f;
            Vector2 spawnPos = target.Center;
            var source = Projectile.GetSource_OnHit(target, "DevilsknifeSuit");

            CardSuitAttackHelper.SpawnSuitAttack(source, Projectile.owner, Projectile.Center, spawnPos, (CardSuit)suitType, baseDamage, kb, spawnAtTarget: true);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 pos = Projectile.Center - Main.screenPosition;

            // Spawn/despawn tween
            const int TweenFrames = 8;
            float spawnT = MathHelper.Clamp(timer / (float)TweenFrames, 0f, 1f);
            float despawnT = MathHelper.Clamp(Projectile.timeLeft / (float)TweenFrames, 0f, 1f);
            float tweenScale = Math.Min(spawnT, despawnT);
            float drawScale = Projectile.scale * tweenScale;

            // Draw motion afterimages behind the scythe
            for (int i = 1; i < Projectile.oldPos.Length; i++)
            {
                Vector2 oldPos = Projectile.oldPos[i];
                if (oldPos == Vector2.Zero)
                    continue;

                float progress = i / (float)Projectile.oldPos.Length;
                float alpha = (1f - progress) * 0.45f * tweenScale;
                Color afterimageColor = new Color(140, 40, 200) * alpha;
                Vector2 drawPos = oldPos + Projectile.Size * 0.5f - Main.screenPosition;
                float oldRot = Projectile.oldRot[i];
                Main.EntitySpriteDraw(tex, drawPos, null, afterimageColor, oldRot, origin, drawScale * (1f - progress * 0.1f), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, pos, null, Color.White, Projectile.rotation, origin, drawScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
