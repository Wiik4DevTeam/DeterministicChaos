using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class KnightEye : ModProjectile
    {
        private const float TargetRange = 900f;
        private const float PupilRadiusX = 1f;
        private const float PupilRadiusY = 1f;

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 22;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2;
        }

        public override bool? CanDamage() => false;

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead || !player.GetModPlayer<Items.RoaringLensPlayer>().RoaringLensActive)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;

            NPC target = FindNearestTarget(player.Center, TargetRange);

            Projectile sphere = FindOwnedSphere(Projectile.owner);
            Vector2 sphereCenter = sphere != null
                ? sphere.Center
                : player.Center + new Vector2(0f, -player.height / 2f - 10f);

            Vector2 lookDir = Vector2.Zero;
            if (target != null)
                lookDir = (target.Center - sphereCenter).SafeNormalize(Vector2.Zero);

            // Eye acts as the pupil: small offset inside the white sphere.
            Vector2 desiredOffset = new Vector2(lookDir.X * PupilRadiusX, lookDir.Y * PupilRadiusY);
            Vector2 desiredPos = sphereCenter + desiredOffset;
            Projectile.Center = desiredPos;
            Projectile.velocity = Vector2.Zero;

            // Hard clamp in ellipse space to guarantee the pupil stays inside bounds.
            Vector2 offset = Projectile.Center - sphereCenter;
            float nx = offset.X / PupilRadiusX;
            float ny = offset.Y / PupilRadiusY;
            float normSq = nx * nx + ny * ny;
            if (normSq > 1f)
            {
                float invLen = 1f / (float)System.Math.Sqrt(normSq);
                offset = new Vector2(nx * invLen * PupilRadiusX, ny * invLen * PupilRadiusY);
                Projectile.Center = sphereCenter + offset;
            }

            // Pupil should remain visually static (no sprite rotation).
            Projectile.rotation = 0f;

            Projectile.localAI[1] = target?.whoAmI ?? -1f;

            Lighting.AddLight(Projectile.Center, 0.46f, 0.54f, 0.75f);

            if (target != null)
            {
                for (int i = 1; i <= 12; i++)
                {
                    Vector2 p = Vector2.Lerp(Projectile.Center, target.Center, i / 12f);
                    Lighting.AddLight(p, 0.03f, 0.04f, 0.06f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            // Soft pupil glow.
            Main.EntitySpriteDraw(
                tex,
                Projectile.Center - Main.screenPosition,
                null,
                new Color(120, 190, 255, 0) * 0.28f,
                Projectile.rotation,
                origin,
                Projectile.scale * 1.55f,
                SpriteEffects.None,
                0f);

            Main.EntitySpriteDraw(
                tex,
                Projectile.Center - Main.screenPosition,
                null,
                Color.White,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0f);

            int targetIndex = (int)Projectile.localAI[1];

            return false;
        }

        private static NPC FindNearestTarget(Vector2 center, float maxDistance)
        {
            NPC nearest = null;
            float maxDistSq = maxDistance * maxDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy())
                    continue;

                float distSq = Vector2.DistanceSquared(center, npc.Center);
                if (distSq < maxDistSq)
                {
                    maxDistSq = distSq;
                    nearest = npc;
                }
            }

            return nearest;
        }

        private static Projectile FindOwnedSphere(int owner)
        {
            int sphereType = ModContent.ProjectileType<KnightSphere>();

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != owner || p.type != sphereType)
                    continue;

                return p;
            }

            return null;
        }
    }
}
