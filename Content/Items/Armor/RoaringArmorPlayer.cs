using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items.Armor
{
    public class RoaringArmorPlayer : ModPlayer
    {
        // Set bonus flags
        public bool roaringMeleeSet;
        public bool roaringRangerSet;
        public bool roaringMageSet;
        public bool roaringSummonerSet;
        public bool roaringRogueSet;
        
        // Melee crit damage bonus from helmet
        public float meleeCritDamageBonus;
        
        // Melee set bonus, sword size increase
        public float swordScaleBonus;
        
        // Ranger set bonus, fire rate buff
        public int ricochetFireRateTimer;
        public const int RicochetFireRateDuration = 60;
        public const float RicochetFireRateBonus = 0.25f;
        
        // Rogue set bonus, stealth strike fire rate
        public int rogueFireRateTimer;
        public const int RogueFireRateDuration = 300; // 5 seconds at 60 fps
        public const float RogueFireRateMaxBonus = 1.0f; // 100% fire rate at start (2x speed), decays to 0
        public bool isStealthStrike; // True when next attack should be a stealth strike
        
        // Track stealth to detect when it gets consumed
        private float previousStealth = 0f;
        private bool wasStealthFull = false;

        public override void ResetEffects()
        {
            roaringMeleeSet = false;
            roaringRangerSet = false;
            roaringMageSet = false;
            roaringSummonerSet = false;
            roaringRogueSet = false;
            meleeCritDamageBonus = 0f;
            swordScaleBonus = 0f;
        }
        
        public override void PostUpdateEquips()
        {
            // Apply fire rate bonus when ricochet timer is active
            if (ricochetFireRateTimer > 0)
            {
                ricochetFireRateTimer--;
                Player.GetAttackSpeed(DamageClass.Ranged) += RicochetFireRateBonus;
            }
            
            // Rogue stealth strike fire rate bonus (rapidly decaying from 2x to 1x)
            if (rogueFireRateTimer > 0)
            {
                rogueFireRateTimer--;
                
                // Calculate decaying bonus based on remaining time
                float decayProgress = rogueFireRateTimer / (float)RogueFireRateDuration;
                float currentBonus = RogueFireRateMaxBonus * decayProgress;
                
                // Apply to throwing/rogue damage class
                Player.GetAttackSpeed(DamageClass.Throwing) += currentBonus;
                
                // Visual effect while buff is active
                if (rogueFireRateTimer % 10 == 0)
                {
                    Lighting.AddLight(Player.Center, 0.4f * decayProgress, 0.4f * decayProgress, 0.5f * decayProgress);
                }
            }
            
            // Rogue stealth system, detect when stealth gets consumed
            if (roaringRogueSet)
            {
                CheckAndTriggerStealthStrike();
            }
            else
            {
                isStealthStrike = false;
                previousStealth = 0f;
                wasStealthFull = false;
            }
        }
        
        // Called by RoaringRangerGlobalProjectile when a ricochet occurs
        public void OnRicochet()
        {
            ricochetFireRateTimer = RicochetFireRateDuration;
        }
        
        public override void ModifyShootStats(Item item, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            // Ranger set: 30% increased bullet and arrow speed
            if (roaringRangerSet && item.CountsAsClass(DamageClass.Ranged))
            {
                if (item.useAmmo == AmmoID.Bullet || item.useAmmo == AmmoID.Arrow)
                {
                    velocity *= 1.30f;
                }
            }
            
            // Rogue set: Apply velocity bonus to thrown items
            if (roaringRogueSet)
            {
                if (IsRogueDamageClass(item))
                {
                    velocity *= 1.15f;
                    damage = (int)(damage * 1.25f);
                }
            }
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Melee set: Critical strikes restore 1% max health
            if (roaringMeleeSet && proj.CountsAsClass(DamageClass.Melee) && hit.Crit)
            {
                int healAmount = (int)(Player.statLifeMax2 * 0.01f);
                Player.Heal(healAmount);
            }

            // Mage set: Gain mana when dealing less than 10 damage
            if (roaringMageSet && proj.CountsAsClass(DamageClass.Magic) && damageDone < 10)
            {
                int manaGain = 25;
                Player.statMana += manaGain;
                if (Player.statMana > Player.statManaMax2)
                    Player.statMana = Player.statManaMax2;
                Player.ManaEffect(manaGain);
            }
        }
        
        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            // Apply melee crit damage bonus from helmet
            if (meleeCritDamageBonus > 0f && proj.CountsAsClass(DamageClass.Melee))
            {
                modifiers.CritDamage += meleeCritDamageBonus;
            }
            
            if (roaringMeleeSet && proj.CountsAsClass(DamageClass.Melee))
            {
                modifiers.CritDamage += 1f;
            }
        }
        
        public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            // Apply melee crit damage bonus from helmet
            if (meleeCritDamageBonus > 0f && item.CountsAsClass(DamageClass.Melee))
            {
                modifiers.CritDamage += meleeCritDamageBonus;
            }
            
            if (roaringMeleeSet && item.CountsAsClass(DamageClass.Melee))
            {
                modifiers.CritDamage += 1f;
            }
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Melee set: Critical strikes restore 1% max health
            if (roaringMeleeSet && item.CountsAsClass(DamageClass.Melee) && hit.Crit)
            {
                int healAmount = (int)(Player.statLifeMax2 * 0.01f);
                Player.Heal(healAmount);
            }

            // Mage set: Gain mana when dealing less than 10 damage
            if (roaringMageSet && item.CountsAsClass(DamageClass.Magic) && damageDone < 10)
            {
                int manaGain = 25;
                Player.statMana += manaGain;
                if (Player.statMana > Player.statManaMax2)
                    Player.statMana = Player.statManaMax2;
                Player.ManaEffect(manaGain);
            }
        }

        private void SpawnShadowOrb(Vector2 position)
        {
            // Spawn a homing shadow orb projectile
            if (Main.myPlayer == Player.whoAmI)
            {
                // Use shadow orb projectile from vanilla or spawn a simple damaging effect
                int damage = (int)(Player.GetDamage(DamageClass.Magic).ApplyTo(50));
                Projectile.NewProjectile(
                    Player.GetSource_FromThis(),
                    position + Main.rand.NextVector2Circular(20, 20),
                    Main.rand.NextVector2CircularEdge(6, 6),
                    ProjectileID.SpectreWrath,
                    damage,
                    2f,
                    Player.whoAmI
                );
            }
        }
        
        private void SpawnSeekingKnife(NPC target, int baseDamage)
        {
            if (Main.myPlayer != Player.whoAmI)
                return;
            
            // Spawn 1-2 seeking knives at the target
            int knifeCount = Main.rand.Next(1, 3);
            int knifeDamage = (int)(baseDamage * 0.5f); // 50% of the hit damage
            
            for (int i = 0; i < knifeCount; i++)
            {
                int p = Projectile.NewProjectile(
                    Player.GetSource_FromThis(),
                    target.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<Projectiles.Friendly.RogueSeekingKnife>(),
                    knifeDamage,
                    2f,
                    Player.whoAmI,
                    target.whoAmI // ai[0] = target NPC index
                );
                
                if (p >= 0 && p < Main.maxProjectiles)
                    Main.projectile[p].netUpdate = true;
            }
        }

        private bool IsRogueDamageClass(Item item)
        {
            // Check if the item uses Calamity's Rogue damage class or Throwing
            try
            {
                string damageClassName = item.DamageType.GetType().Name;
                return damageClassName.Contains("Rogue") || damageClassName.Contains("Throwing") || item.DamageType == DamageClass.Throwing;
            }
            catch
            {
                return false;
            }
        }
        
        private bool IsRogueDamageClassProj(Projectile proj)
        {
            // Check if projectile uses Rogue or Throwing damage
            try
            {
                string damageClassName = proj.DamageType.GetType().Name;
                return damageClassName.Contains("Rogue") || damageClassName.Contains("Throwing") || proj.DamageType == DamageClass.Throwing;
            }
            catch
            {
                return false;
            }
        }
        
        // Called by RoaringRogueGlobalProjectile when a stealth strike is used
        public void OnStealthStrike()
        {
            rogueFireRateTimer = RogueFireRateDuration;
        }
        
        private void CheckAndTriggerStealthStrike()
        {
            // Check Calamity stealth and detect when it gets consumed
            try
            {
                foreach (var modPlayer in Player.ModPlayers)
                {
                    if (modPlayer.GetType().Name == "CalamityPlayer")
                    {
                        var type = modPlayer.GetType();
                        
                        var rogueStealthField = type.GetField("rogueStealth");
                        var rogueStealthMaxField = type.GetField("rogueStealthMax");
                        
                        if (rogueStealthField != null && rogueStealthMaxField != null)
                        {
                            float currentStealth = (float)rogueStealthField.GetValue(modPlayer);
                            float maxStealth = (float)rogueStealthMaxField.GetValue(modPlayer);
                            
                            if (maxStealth > 0)
                            {
                                // Check if stealth is full (95% threshold for floating point)
                                bool isStealthFull = currentStealth >= maxStealth * 0.95f;
                                
                                // Detect when stealth drops from full to less than half
                                // This means a stealth strike was just used
                                if (wasStealthFull && currentStealth < maxStealth * 0.5f)
                                {
                                    // Stealth was consumed, trigger fire rate buff
                                    rogueFireRateTimer = RogueFireRateDuration;
                                    
                                    // Play sound effect
                                    if (Main.netMode != Terraria.ID.NetmodeID.Server)
                                    {
                                        Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item71 with { Volume = 0.6f, Pitch = 0.3f }, Player.Center);
                                    }
                                }
                                
                                // Update tracking
                                wasStealthFull = isStealthFull;
                                previousStealth = currentStealth;
                                isStealthStrike = isStealthFull;
                            }
                        }
                        
                        break;
                    }
                }
            }
            catch
            {
                isStealthStrike = false;
            }
        }
        
        public void AddCalamityStealthPublic(float amount)
        {
            // Add stealth to Calamity's stealth meter
            try
            {
                foreach (var modPlayer in Player.ModPlayers)
                {
                    if (modPlayer.GetType().Name == "CalamityPlayer")
                    {
                        var type = modPlayer.GetType();
                        
                        // Get current stealth and max stealth
                        var rogueStealthField = type.GetField("rogueStealth");
                        var rogueStealthMaxField = type.GetField("rogueStealthMax");
                        
                        if (rogueStealthField != null && rogueStealthMaxField != null)
                        {
                            float currentStealth = (float)rogueStealthField.GetValue(modPlayer);
                            float maxStealth = (float)rogueStealthMaxField.GetValue(modPlayer);
                            
                            // Add stealth, capped at max
                            float newStealth = Math.Min(currentStealth + amount, maxStealth);
                            rogueStealthField.SetValue(modPlayer, newStealth);
                        }
                        
                        break;
                    }
                }
            }
            catch
            {
                // Calamity not installed or field not found
            }
        }
    }
}
