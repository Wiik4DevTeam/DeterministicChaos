using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Armor;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class RoaringSwordLungeCharge : ModProjectile
    {
        private const int MaxChargeTime = 60;
        private const float MinChargePercent = 0.2f;
        
        private ref float ChargeTimer => ref Projectile.ai[0];
        private ref float Released => ref Projectile.ai[1];
        
        private float baseScale = 1f;
        private bool initialized = false;
        private bool playedChargeSound = false;
        private bool playedFullChargeSound = false;

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(baseScale);
            writer.Write(initialized);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            baseScale = reader.ReadSingle();
            initialized = reader.ReadBoolean();
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            if (!initialized)
            {
                initialized = true;
                float armorBonus = player.GetModPlayer<RoaringArmorPlayer>().swordScaleBonus;
                baseScale = 1f + armorBonus;
            }

            Vector2 toMouse = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            player.direction = toMouse.X >= 0 ? 1 : -1;

            bool holdingRightClick = Main.mouseRight && Projectile.owner == Main.myPlayer;
            
            if (Released == 0 && holdingRightClick)
            {
                ChargeTimer++;
                
                if (ChargeTimer >= MaxChargeTime && !playedFullChargeSound)
                {
                    playedFullChargeSound = true;
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = 0.5f }, player.Center);
                }
                else if (ChargeTimer > 10 && !playedChargeSound)
                {
                    playedChargeSound = true;
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = -0.3f }, player.Center);
                }
                
                float chargePercent = MathHelper.Clamp(ChargeTimer / MaxChargeTime, 0f, 1f);
                
                if (Main.rand.NextBool(3))
                {
                    Vector2 dustPos = player.Center + toMouse * 40f + Main.rand.NextVector2Circular(15f, 15f);
                    Dust dust = Dust.NewDustPerfect(dustPos, DustID.WhiteTorch, -toMouse * 2f, 0, Color.White, 0.8f + chargePercent * 0.7f);
                    dust.noGravity = true;
                }
                
                if (chargePercent >= 1f && Main.rand.NextBool(2))
                {
                    Vector2 dustPos = player.Center + Main.rand.NextVector2Circular(30f, 30f);
                    Dust dust = Dust.NewDustPerfect(dustPos, DustID.Shadowflame, Vector2.Zero, 0, default, 1.2f);
                    dust.noGravity = true;
                }
                
                float aimAngle = toMouse.ToRotation();
                Projectile.Center = player.Center;
                Projectile.rotation = aimAngle;
                
                player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, aimAngle - MathHelper.PiOver2);
                player.heldProj = Projectile.whoAmI;
                
                player.itemTime = 2;
                player.itemAnimation = 2;
            }
            else if (Released == 0)
            {
                Released = 1;
                
                float chargePercent = MathHelper.Clamp(ChargeTimer / MaxChargeTime, MinChargePercent, 1f);
                
                if (Projectile.owner == Main.myPlayer)
                {
                    // Damage scales from 30% at minimum charge to 60% at full charge (reduced by 40%)
                    int damage = (int)(Projectile.damage * (0.3f + chargePercent * 0.3f));
                    
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        player.Center,
                        toMouse,
                        ModContent.ProjectileType<RoaringSwordLunge>(),
                        damage,
                        Projectile.knockBack * (0.5f + chargePercent * 0.5f),
                        Projectile.owner,
                        0f,
                        chargePercent
                    );
                }
                
                Projectile.Kill();
            }
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
            
            float chargePercent = MathHelper.Clamp(ChargeTimer / MaxChargeTime, 0f, 1f);
            float pulseScale = 1f + (float)System.Math.Sin(ChargeTimer * 0.2f) * 0.05f * chargePercent;
            float drawScale = baseScale * pulseScale;
            
            float drawRotation = Projectile.rotation + MathHelper.PiOver2;
            SpriteEffects effects = player.direction == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 handOffset = new Vector2(0f, -15f);
            
            Color glowColor = Color.Lerp(Color.Purple * 0.3f, Color.White * 0.8f, chargePercent);
            
            for (int i = 0; i < 4; i++)
            {
                Vector2 offset = new Vector2(2f + chargePercent * 2f, 0f).RotatedBy(i * MathHelper.PiOver2 + ChargeTimer * 0.1f);
                Main.EntitySpriteDraw(
                    texture,
                    player.Center + handOffset + offset - Main.screenPosition,
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
                player.Center + handOffset - Main.screenPosition,
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
