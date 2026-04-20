using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Imbued;
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
    public class RoaringMiniStar : ModProjectile
    {
        public override string Texture => "DeterministicChaos/Content/Projectiles/Enemy/Projectile_Star";
        
        private float initialScale = 0.5f;
        private float shrinkRate = 0.008f;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;

            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;

            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;

            Projectile.scale = initialScale;
            
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI()
        {
            ImbuedStaticVariant variant = (ImbuedStaticVariant)(int)Projectile.ai[0];
            bool longLived = variant == ImbuedStaticVariant.Patience
                || variant == ImbuedStaticVariant.Perseverance
                || variant == ImbuedStaticVariant.Bravery;

            // Long-lived variants shrink more slowly so they actually live longer
            float effectiveShrink = longLived ? 0.0025f : shrinkRate;
            Projectile.scale -= effectiveShrink;

            // Kill when too small
            if (Projectile.scale <= 0.1f)
            {
                Projectile.Kill();
                return;
            }

            // Track age via localAI[0]
            Projectile.localAI[0]++;
            int age = (int)Projectile.localAI[0];

            Player owner = Main.player[Projectile.owner];

            if (variant == ImbuedStaticVariant.Patience && age >= 60 && owner != null && owner.active)
            {
                // After a few seconds, slowly track the nearest enemy
                NPC tgt = FindNearestEnemy(Projectile.Center, 800f);
                if (tgt != null)
                {
                    Vector2 dir = (tgt.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    float speed = System.Math.Max(Projectile.velocity.Length(), 4f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * speed, 0.05f);
                }
                else
                {
                    Projectile.velocity *= 0.99f;
                }
            }
            else if (variant == ImbuedStaticVariant.Bravery && owner != null && owner.active)
            {
                // Always slowly move toward the player after spawning
                Vector2 dir = (owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                float speed = System.Math.Max(Projectile.velocity.Length(), 3.5f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * speed, 0.04f);
            }
            else
            {
                // Slight deceleration
                Projectile.velocity *= 0.98f;
            }

            // Add some light
            Lighting.AddLight(Projectile.Center, 0.4f, 0.35f, 0.15f);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            ImbuedStaticVariant variant = (ImbuedStaticVariant)(int)Projectile.ai[0];
            // Integrity: stars do +20% damage
            if (variant == ImbuedStaticVariant.Integrity)
                modifiers.SourceDamage *= 1.2f;
        }

        private static NPC FindNearestEnemy(Vector2 center, float range)
        {
            float bestSq = range * range;
            NPC best = null;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (!n.active || n.friendly || n.dontTakeDamage || n.lifeMax <= 5 || n.CountsAsACritter)
                    continue;
                float d = Vector2.DistanceSquared(n.Center, center);
                if (d < bestSq)
                {
                    bestSq = d;
                    best = n;
                }
            }
            return best;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            // Golden/orange color tint (multiplied by trait color when imbued)
            Color baseTint = new Color(255, 200, 100);
            int variantInt = (int)Projectile.ai[0];
            Color traitTint = ImbuedTraitColor.FromNoneFirst(variantInt);
            Color tint = ImbuedTraitColor.Multiply(baseTint, traitTint);

            // Draw trail
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                float t = i / (float)Projectile.oldPos.Length;
                float a = (1f - t) * 0.4f;
                float trailScale = Projectile.scale * (1f - t * 0.5f);

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;

                Main.spriteBatch.Draw(
                    tex,
                    pos,
                    null,
                    tint * a,
                    Projectile.rotation,
                    origin,
                    trailScale,
                    SpriteEffects.None,
                    0f
                );
            }

            // Draw main star
            Main.spriteBatch.Draw(
                tex,
                Projectile.Center - Main.screenPosition,
                null,
                tint,
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
