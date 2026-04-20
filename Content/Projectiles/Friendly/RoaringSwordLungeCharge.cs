using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;
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
using DeterministicChaos.Content.Items.Armor;
using DeterministicChaos.Content.Items.Imbued;

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

                // Reset Perseverance mana bonus at the start of each charge
                var swordPlayer = player.GetModPlayer<RoaringSwordPlayer>();
                swordPlayer.perseveranceManaBonus = 0f;
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

                // Perseverance: Drain mana after full charge to increase damage
                var persPlayer = player.GetModPlayer<RoaringSwordPlayer>();
                if (persPlayer.imbuedWillbreakerVariant == (int)ImbuedWillbreakerVariant.Perseverance
                    && ChargeTimer > MaxChargeTime)
                {
                    if (player.statMana >= 5)
                    {
                        player.statMana -= 5;
                        player.manaRegenDelay = 10;
                        persPlayer.perseveranceManaBonus += 0.02f;

                        // Purple mana drain dust
                        if (Main.rand.NextBool(2))
                        {
                            Vector2 dPos = player.Center + Main.rand.NextVector2Circular(20f, 20f);
                            Dust d = Dust.NewDustPerfect(dPos, DustID.PurpleTorch, -toMouse * 1.5f, 0, default, 1f);
                            d.noGravity = true;
                        }
                    }
                    else
                    {
                        // Out of mana — fire the lunge immediately at the current charge percent and end the charge.
                        Released = 1;

                        float chargePercentOOM = MathHelper.Clamp(ChargeTimer / MaxChargeTime, MinChargePercent, 1f);

                        if (Projectile.owner == Main.myPlayer)
                        {
                            int damageOOM = (int)(Projectile.damage * (0.3f + chargePercentOOM * 0.3f));

                            Projectile.NewProjectile(
                                Projectile.GetSource_FromThis(),
                                player.Center,
                                toMouse,
                                ModContent.ProjectileType<RoaringSwordLunge>(),
                                damageOOM,
                                Projectile.knockBack * (0.5f + chargePercentOOM * 0.5f),
                                Projectile.owner,
                                0f,
                                chargePercentOOM
                            );
                        }

                        Projectile.Kill();
                        return;
                    }
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

            // Imbued Willbreaker trait tint
            Color traitTint = Color.White;
            var sp = player.GetModPlayer<RoaringSwordPlayer>();
            if (sp.isHoldingWillbreaker)
                traitTint = ImbuedTraitColor.FromZeroDetermination(sp.imbuedWillbreakerVariant);
            
            Vector2 origin = new Vector2(texture.Width * 0.5f, texture.Height);
            
            float chargePercent = MathHelper.Clamp(ChargeTimer / MaxChargeTime, 0f, 1f);
            float pulseScale = 1f + (float)System.Math.Sin(ChargeTimer * 0.2f) * 0.05f * chargePercent;
            float drawScale = baseScale * pulseScale;
            
            float drawRotation = Projectile.rotation + MathHelper.PiOver2;
            SpriteEffects effects = player.direction == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 handOffset = new Vector2(0f, -15f);
            
            Color glowColor = ImbuedTraitColor.Multiply(Color.Lerp(Color.Purple * 0.3f, Color.White * 0.8f, chargePercent), traitTint);
            
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
                traitTint,
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
