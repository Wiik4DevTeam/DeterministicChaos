using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class KingOfHeartsProjectile : ModProjectile
    {
        private const int InitialHearts = 2;
        private const int MaxHearts = 10;
        private const int SpawnInterval = 30; // 0.5 seconds
        private const int ManaCostPerHeart = 25;
        private const float ReleaseSpeed = 8f;
        private const float BaseRotationSpeed = 0.06f;
        private const float RotationSpeedPerHeart = 0.012f;
        private const int ReleaseDuration = 240; // 4 seconds
        private const float HoldDistanceBack = 10f;
        private const float HoldHeightOffset = -10f;

        // ai[0] = state (0=channeling, 1=released), ai[1] = heart count
        // localAI[0] = global rotation angle, localAI[1] = spawn timer
        private ref float State => ref Projectile.ai[0];
        private ref float HeartCount => ref Projectile.ai[1];

        private bool initialHeartsSpawned;

        private Vector2 GetHoldPosition(Player player)
        {
            Vector2 aimDir = (Main.MouseWorld - player.MountedCenter).SafeNormalize(Vector2.UnitX * player.direction);
            return player.MountedCenter - aimDir * HoldDistanceBack + new Vector2(0f, HoldHeightOffset);
        }

        public override void SetDefaults()
        {
            Projectile.width = 1;
            Projectile.height = 1;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 999999;
            Projectile.netImportant = true;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            float speed = BaseRotationSpeed + RotationSpeedPerHeart * HeartCount;
            Projectile.localAI[0] += speed;

            if (State == 0)
                ChannelingAI(player);
        }

        private void ChannelingAI(Player player)
        {
            Projectile.Center = GetHoldPosition(player);
            player.heldProj = Projectile.whoAmI;
            player.itemTime = 2;
            player.itemAnimation = 2;

            // Face cursor
            Projectile.spriteDirection = Main.MouseWorld.X > player.Center.X ? 1 : -1;
            player.ChangeDir(Projectile.spriteDirection);

            // Spawn initial hearts on first tick
            if (!initialHeartsSpawned)
            {
                initialHeartsSpawned = true;
                if (Main.myPlayer == Projectile.owner)
                {
                    for (int i = 0; i < InitialHearts; i++)
                        SpawnHeart();
                }
            }

            bool shouldRelease = !player.channel;

            // Spawn additional hearts on timer
            if ((int)HeartCount >= InitialHearts && (int)HeartCount < MaxHearts)
            {
                Projectile.localAI[1]++;
                if (Projectile.localAI[1] >= SpawnInterval)
                {
                    if (Main.myPlayer == Projectile.owner)
                    {
                        if (player.CheckMana(ManaCostPerHeart, true))
                        {
                            player.manaRegenDelay = (int)player.maxRegenDelay;
                            SpawnHeart();
                            Projectile.localAI[1] = 0;
                        }
                        else
                        {
                            shouldRelease = true;
                        }
                    }
                }
            }

            // Auto-release at max hearts
            if ((int)HeartCount >= MaxHearts)
                shouldRelease = true;

            if (shouldRelease && (int)HeartCount >= InitialHearts)
                Release(player);
        }

        private void SpawnHeart()
        {
            int index = (int)HeartCount;
            int id = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                Vector2.Zero,
                ModContent.ProjectileType<KingOfHeartsHeart>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner,
                index,
                Projectile.whoAmI
            );
            HeartCount++;
            Projectile.netUpdate = true;
        }

        private void Release(Player player)
        {
            State = 1;
            Vector2 dir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Projectile.velocity = dir * ReleaseSpeed;
            Projectile.timeLeft = ReleaseDuration;
            Projectile.netUpdate = true;

            // Set all hearts to despawn after 4 seconds too
            int heartType = ModContent.ProjectileType<KingOfHeartsHeart>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner && p.type == heartType && (int)p.ai[1] == Projectile.whoAmI)
                {
                    p.timeLeft = ReleaseDuration;
                }
            }
        }

        public override void OnKill(int timeLeft)
        {
            int heartType = ModContent.ProjectileType<KingOfHeartsHeart>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner && p.type == heartType && (int)p.ai[1] == Projectile.whoAmI)
                {
                    p.Kill();
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Only draw while channeling
            if (State != 0)
                return false;

            Player player = Main.player[Projectile.owner];
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0f, tex.Height / 2f);
            Vector2 holdPos = GetHoldPosition(player);

            float rotation = (Main.MouseWorld - holdPos).ToRotation();
            SpriteEffects flip = Projectile.spriteDirection < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Color glowColor = new Color(220, 60, 70, 0) * 0.45f;
            Main.EntitySpriteDraw(tex, holdPos - Main.screenPosition, null, glowColor, rotation, origin, 1.08f, flip, 0);
            Main.EntitySpriteDraw(tex, holdPos - Main.screenPosition, null, lightColor, rotation, origin, 1f, flip, 0);
            return false;
        }
    }
}
