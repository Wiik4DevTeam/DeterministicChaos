using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class KindnessPickup : ModProjectile
    {
        private const int HealAmount = 2;
        private const float BasePickupRange = 80f; // 5 tiles base range
        private const float HeartreachPickupRange = 250f; // ~15 tiles with Heartreach
        private const int MaxLifetime = 300; // 5 seconds

        private int timer = 0;
        private float bobOffset = 0f;
        
        // Use ai[0] as synced "collected" flag for multiplayer safety
        private bool Collected => Projectile.ai[0] >= 1f;

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.scale = 0.7f;
            Projectile.friendly = false; // Doesn't damage enemies
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLifetime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1; // Custom AI
        }

        public override void AI()
        {
            if (Collected)
            {
                Projectile.Kill();
                return;
            }

            timer++;

            // Apply gravity until on ground
            if (Projectile.velocity.Y < 8f)
            {
                Projectile.velocity.Y += 0.2f;
            }

            // Slow down horizontal movement
            Projectile.velocity.X *= 0.95f;

            // Bob animation
            bobOffset = MathF.Sin(timer * 0.1f) * 2f;

            // Emit green light
            Lighting.AddLight(Projectile.Center, 0.1f, 0.4f, 0.1f);

            // Green sparkle dust
            if (Main.rand.NextBool(15))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GreenTorch, 0f, -1f, 100, default, 0.6f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }

            // Check for player pickup
            CheckPlayerPickup();

            // Fade out near end of life
            if (Projectile.timeLeft < 60)
            {
                Projectile.alpha = (int)MathHelper.Lerp(255, 0, Projectile.timeLeft / 60f);
            }
        }

        private void CheckPlayerPickup()
        {
            // Only process pickup logic for the local player
            Player localPlayer = Main.LocalPlayer;
            if (!localPlayer.active || localPlayer.dead)
                return;

            // Check if local player is on the same team as the owner (or IS the owner)
            Player owner = Main.player[Projectile.owner];
            bool canPickup = false;

            if (localPlayer.whoAmI == Projectile.owner)
            {
                // Owner can always pick up their own
                canPickup = true;
            }
            else if (owner != null && owner.active && owner.team != 0 && localPlayer.team == owner.team)
            {
                // Teammate on same team can pick up
                canPickup = true;
            }

            if (!canPickup)
                return;

            // Calculate pickup range (affected by local player's Heartreach)
            float pickupRange = localPlayer.lifeMagnet ? HeartreachPickupRange : BasePickupRange;

            float distance = Vector2.Distance(Projectile.Center, localPlayer.Center);
            
            if (distance < pickupRange)
            {
                // Move toward player if within range but not touching
                if (distance > 16f)
                {
                    Vector2 direction = (localPlayer.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    float speed = MathHelper.Lerp(6f, 14f, 1f - (distance / pickupRange));
                    Projectile.velocity = direction * speed;
                }
                
                // Actually collect when close enough (use hitbox overlap)
                if (Projectile.Hitbox.Intersects(localPlayer.Hitbox) || distance < 32f)
                {
                    CollectPickup(localPlayer);
                }
            }
        }

        private void CollectPickup(Player player)
        {
            if (Collected)
                return;

            // Mark as collected and sync to all clients
            Projectile.ai[0] = 1f;
            Projectile.netUpdate = true;

            // Only heal if player needs healing
            if (player.statLife < player.statLifeMax2)
            {
                player.HealEffect(HealAmount);
                player.statLife += HealAmount;
                if (player.statLife > player.statLifeMax2)
                    player.statLife = player.statLifeMax2;
            }

            // Pickup sound
            SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/UTHeal"), Projectile.Center);

            // Green healing burst
            for (int i = 0; i < 8; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch, vel, 0, default, 1.2f);
                dust.noGravity = true;
            }

            Projectile.Kill();
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            // Bounce slightly on tiles
            if (Projectile.velocity.Y != oldVelocity.Y)
            {
                Projectile.velocity.Y = -oldVelocity.Y * 0.3f;
            }
            if (Projectile.velocity.X != oldVelocity.X)
            {
                Projectile.velocity.X = -oldVelocity.X * 0.3f;
            }
            return false; // Don't kill on tile collision
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            if (tex == null)
                return false;

            Vector2 origin = tex.Size() * 0.5f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition + new Vector2(0f, bobOffset);
            
            // Pulsing glow effect
            float pulse = 1f + MathF.Sin(timer * 0.15f) * 0.1f;
            Color glowColor = Color.LimeGreen * 0.3f * pulse;

            // Draw glow
            Main.EntitySpriteDraw(
                tex,
                drawPos,
                null,
                glowColor,
                0f,
                origin,
                Projectile.scale * 1.3f * pulse,
                SpriteEffects.None,
                0
            );

            // Draw main sprite
            Color drawColor = Color.White;
            if (Projectile.alpha > 0)
            {
                drawColor *= 1f - (Projectile.alpha / 255f);
            }

            Main.EntitySpriteDraw(
                tex,
                drawPos,
                null,
                drawColor,
                0f,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}
