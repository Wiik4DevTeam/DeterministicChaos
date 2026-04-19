using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    // Warning indicator beam before the TitanLaserBeam fires.
    // Tiles a 20x20 texture vertically from the hand downward into the ground.
    // Fades in then holds, killed externally by TitanHand when indicator time ends.
    // ai[0] = parent TitanHand NPC whoAmI
    // ai[1] = target angle (radians, downward from hand)
    public class TitanLaserIndicator : ModProjectile
    {
        private const int TILE_SIZE = 20;
        private const float MAX_LENGTH = 3200f; // Max beam length in pixels (200 tiles)
        private const float INDICATOR_WIDTH = 3f;   // Matches beam max width

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400000;
        }

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false; // Indicator doesn't deal damage
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300; // Killed externally, this is a safety net
            Projectile.hide = false;
        }

        public override void AI()
        {
            int parentIdx = (int)Projectile.ai[0];

            // Despawn if parent hand is gone
            if (parentIdx < 0 || parentIdx >= Main.maxNPCs
                || !Main.npc[parentIdx].active
                || Main.npc[parentIdx].type != ModContent.NPCType<NPCs.Bosses.TitanHand>())
            {
                Projectile.Kill();
                return;
            }

            // Stick to parent hand center
            NPC hand = Main.npc[parentIdx];
            Projectile.Center = hand.Center;
            Projectile.rotation = Projectile.ai[1];

            // Gentle pulsing alpha
            float fade = 0.6f + 0.4f * (float)Math.Sin(Projectile.localAI[0] * 0.15f);
            Projectile.Opacity = fade;

            // Play charge sound on first tick (runs on all clients)
            if (Projectile.localAI[0] == 0f && Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/TitanLaserCharge")
                {
                    Volume = 0.3f, MaxInstances = 2
                });
            }

            Projectile.localAI[0]++;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = TextureAssets.Projectile[Type].Value;

            Vector2 origin = new Vector2(tex.Width / 2f, 0f); // Top-center origin
            float angle = Projectile.rotation;
            Vector2 direction = angle.ToRotationVector2();
            Vector2 drawStart = Projectile.Center - Main.screenPosition;

            // Calculate beam length, raycast tiles to find ground
            float beamLength = CalculateBeamLength(Projectile.Center, direction);

            int tileCount = (int)(beamLength / TILE_SIZE) + 1;
            Color beamColor = new Color(255, 80, 80, 120) * Projectile.Opacity; // Red-ish warning
            float width = INDICATOR_WIDTH * (Projectile.ai[2] == 1f ? 2f : 1f);

            for (int i = 0; i < tileCount; i++)
            {
                Vector2 tilePos = drawStart + direction * (i * TILE_SIZE);
                sb.Draw(tex, tilePos, null, beamColor, angle - MathHelper.PiOver2,
                    origin, new Vector2(width, 1f), SpriteEffects.None, 0f);
            }

            return false;
        }

        // Returns the beam length, always MAX_LENGTH (passes through all tiles).
        public static float CalculateBeamLength(Vector2 worldOrigin, Vector2 direction)
        {
            return MAX_LENGTH;
        }

        public override bool ShouldUpdatePosition() => false;
    }
}
