using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;
using DeterministicChaos.Content.Items;
using DeterministicChaos.Content.Items.Armor;
using DeterministicChaos.Content.VFX;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class RoaringSwordLunge : ModProjectile
    {
        private const int BaseLungeDuration = 10;
        private const float BaseLungeSpeed = 60f;
        private const int ThrustDuration = 4;
        private const int RetractDuration = 6;
        private const float MaxThrustExtend = 40f;
        private const int MaxAfterimages = 8;
        
        private ref float Timer => ref Projectile.ai[0];
        private ref float ChargePercent => ref Projectile.ai[1];
        
        private Vector2 lungeDirection;
        private Vector2 startPosition;
        private int lungeDuration;
        private float lungeSpeed;
        
        private List<Vector2> afterimagePositions = new List<Vector2>();
        private List<float> afterimageRotations = new List<float>();

        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.WriteVector2(lungeDirection);
            writer.WriteVector2(startPosition);
            writer.Write(lungeDuration);
            writer.Write(lungeSpeed);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            lungeDirection = reader.ReadVector2();
            startPosition = reader.ReadVector2();
            lungeDuration = reader.ReadInt32();
            lungeSpeed = reader.ReadSingle();
        }

        public override void SetDefaults()
        {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            if (Timer == 0)
            {
                if (ChargePercent <= 0)
                    ChargePercent = 0.2f;
                
                lungeDirection = Projectile.velocity.SafeNormalize(Vector2.UnitX * player.direction);
                startPosition = player.Center;
                
                lungeDuration = (int)(BaseLungeDuration + ChargePercent * 10f);
                lungeSpeed = BaseLungeSpeed + ChargePercent * 40f;
                
                float armorBonus = player.GetModPlayer<RoaringArmorPlayer>().swordScaleBonus;
                Projectile.scale = (1f + armorBonus) * (0.9f + ChargePercent * 0.3f);
                
                Projectile.timeLeft = lungeDuration + 10;
                
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/SwordDashSwoon") { Volume = 0.8f, Pitch = -0.2f + ChargePercent * 0.4f }, player.Center);
                
                player.immune = true;
                player.immuneTime = lungeDuration;
                
                // Spawn shockwave VFX
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    float shockwaveAngle = lungeDirection.ToRotation();
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        player.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<Shockwave>(),
                        0,
                        0f,
                        Projectile.owner,
                        shockwaveAngle
                    );
                }
                
                SpawnFriendlySlash(player);
                
                // Sync initial state in multiplayer
                Projectile.netUpdate = true;
            }

            if (Timer < lungeDuration)
            {
                afterimagePositions.Insert(0, player.Center);
                afterimageRotations.Insert(0, Projectile.rotation);
                
                if (afterimagePositions.Count > MaxAfterimages)
                {
                    afterimagePositions.RemoveAt(afterimagePositions.Count - 1);
                    afterimageRotations.RemoveAt(afterimageRotations.Count - 1);
                }
                
                float progress = Timer / lungeDuration;
                float easedSpeed = lungeSpeed * (1f - EaseOutQuad(progress));
                
                player.velocity = lungeDirection * easedSpeed;
                player.direction = lungeDirection.X >= 0 ? 1 : -1;
                
                Projectile.Center = player.Center;
                Projectile.rotation = lungeDirection.ToRotation();
                
                int dustCount = (int)(2 + ChargePercent * 3);
                for (int i = 0; i < dustCount; i++)
                {
                    Vector2 dustPos = player.Center + Main.rand.NextVector2Circular(20f, 20f);
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.Shadowflame, -player.velocity.X * 0.2f, -player.velocity.Y * 0.2f, 100, default, 1.2f + ChargePercent * 0.5f);
                    dust.noGravity = true;
                }
                
                if (ChargePercent >= 0.8f)
                {
                    Dust whiteDust = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 10f), DustID.WhiteTorch, -lungeDirection * 3f, 0, Color.White, 1f);
                    whiteDust.noGravity = true;
                }
                
                player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            }
            
            if (Timer == lungeDuration)
            {
                player.velocity *= 0.2f;
            }
            
            Timer++;
            if (Timer >= lungeDuration + 5)
            {
                // Start cooldown when lunge ends
                player.GetModPlayer<RoaringSwordPlayer>().StartLungeCooldown();
                Projectile.Kill();
            }
        }
        
        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }
        
        private float EaseInOutQuad(float t)
        {
            return t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2f) / 2f;
        }
        
        private void SpawnFriendlySlash(Player player)
        {
            if (Projectile.owner != Main.myPlayer)
                return;
                
            float slashAngle = lungeDirection.ToRotation();
            float lungeDistance = lungeSpeed * lungeDuration * 0.25f;
            Vector2 slashPosition = startPosition + lungeDirection * lungeDistance;
            
            // Damage scales from 50% at minimum charge to 100% at full charge
            int slashDamage = (int)(Projectile.damage * (0.5f + ChargePercent * 0.5f));
            
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                slashPosition,
                Vector2.Zero,
                ModContent.ProjectileType<RoaringSwordSlash>(),
                slashDamage,
                Projectile.knockBack,
                Projectile.owner,
                slashAngle
            );
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Eye debuff is applied by the mark system
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active)
                return false;
                
            Texture2D texture = TextureAssets.Projectile[ModContent.ProjectileType<RoaringSwordSwing>()].Value;
            if (texture == null)
                return false;
                
            Vector2 origin = new Vector2(texture.Width * 0.5f, texture.Height);
            
            float thrustExtend = 0f;
            float drawScale = Projectile.scale;
            
            if (Timer < ThrustDuration)
            {
                float thrustProgress = Timer / ThrustDuration;
                thrustExtend = (MaxThrustExtend + ChargePercent * 20f) * EaseOutQuad(thrustProgress);
            }
            else if (Timer < ThrustDuration + RetractDuration)
            {
                float retractProgress = (Timer - ThrustDuration) / RetractDuration;
                thrustExtend = (MaxThrustExtend + ChargePercent * 20f) * (1f - EaseInOutQuad(retractProgress));
            }
            
            Vector2 thrustOffset = lungeDirection * thrustExtend;
            
            float drawRotation = Projectile.rotation + MathHelper.PiOver2;
            SpriteEffects effects = player.direction == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 handOffset = new Vector2(0f, -15f);
            
            for (int i = afterimagePositions.Count - 1; i >= 0; i--)
            {
                float alpha = (1f - (i / (float)MaxAfterimages)) * (0.4f + ChargePercent * 0.2f);
                float scale = drawScale * (1f - (i * 0.03f));
                float afterimageRotation = afterimageRotations[i] + MathHelper.PiOver2;
                
                Main.EntitySpriteDraw(
                    texture,
                    afterimagePositions[i] + thrustOffset + handOffset - Main.screenPosition,
                    null,
                    Color.White * alpha,
                    afterimageRotation,
                    origin,
                    scale,
                    effects,
                    0
                );
            }
            
            Color glowColor = Color.Lerp(Color.Purple, Color.White, ChargePercent * 0.5f) * (0.4f + ChargePercent * 0.3f);
            for (int i = 0; i < 4; i++)
            {
                Vector2 offset = new Vector2(2f + ChargePercent * 2f, 0f).RotatedBy(i * MathHelper.PiOver2);
                Main.EntitySpriteDraw(
                    texture,
                    player.Center + thrustOffset + handOffset + offset - Main.screenPosition,
                    null,
                    glowColor,
                    drawRotation,
                    origin,
                    drawScale * 1.1f,
                    effects,
                    0
                );
            }
            
            Main.EntitySpriteDraw(
                texture,
                player.Center + thrustOffset + handOffset - Main.screenPosition,
                null,
                Color.White,
                drawRotation,
                origin,
                drawScale,
                effects,
                0
            );
            
            return false;
        }
    }
}
