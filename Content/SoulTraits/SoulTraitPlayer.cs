using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Achievements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using DeterministicChaos.Content.SoulTraits.Buffs;

namespace DeterministicChaos.Content.SoulTraits
{
    public class SoulTraitPlayer : ModPlayer
    {
        public SoulTraitType CurrentTrait = SoulTraitType.None;
        public bool TraitLocked = false;
        public bool SoulVisible = true;

        // Investment points from different sources
        public int ArmorInvestment = 0;
        public int WeaponInvestment = 0;
        public int PotionInvestment = 0;
        public int TotalInvestment => Math.Min(ArmorInvestment + WeaponInvestment + PotionInvestment, 20);

        // Mark stacks for each trait
        public int JusticeHitCounter = 0;
        public bool JusticeMarkActive = false;

        public int KindnessMarkTimer = 0;
        public const int KindnessMarkDuration = 600;

        public bool BraveryMarkActive = false;
        public int BraveryMarkTimer = 0;

        public int PatienceMarkStacks = 0;
        public int PatienceNoDamageTimer = 0;
        public const int PatienceMarkInterval = 1800;

        public int IntegrityMarkStacks = 0;

        public bool PerseveranceMarkActive = false;

        public bool DeterminationMarkActive = false;
        public Vector2 DeterminationSavedPosition;
        public int DeterminationCooldown = 0;
        public const int DeterminationCooldownDuration = 7200;

        // Buff timers
        public int KindnessDefenseTimer = 0;
        public const int KindnessDefenseDuration = 180;

        public int BraveryDamageTimer = 0;
        public const int BraveryDamageDuration = 360;

        public int PerseveranceDamageStacks = 0;
        public int PerseveranceDamageTimer = 0;
        public const int PerseveranceDamageDuration = 300;

        public int CombatTimer = 0;
        public const int CombatDuration = 300;

        public override void Initialize()
        {
            CurrentTrait = SoulTraitType.None;
            TraitLocked = false;
            ResetAllMarks();
        }

        public void ResetAllMarks()
        {
            JusticeHitCounter = 0;
            JusticeMarkActive = false;
            KindnessMarkTimer = 0;
            BraveryMarkActive = false;
            BraveryMarkTimer = 0;
            PatienceMarkStacks = 0;
            PatienceNoDamageTimer = 0;
            IntegrityMarkStacks = 0;
            PerseveranceMarkActive = false;
            DeterminationMarkActive = false;
            DeterminationCooldown = 0;
            KindnessDefenseTimer = 0;
            BraveryDamageTimer = 0;
            PerseveranceDamageStacks = 0;
            PerseveranceDamageTimer = 0;
            CombatTimer = 0;
        }

        public override void SaveData(TagCompound tag)
        {
            tag["SoulTrait"] = (int)CurrentTrait;
            tag["TraitLocked"] = TraitLocked;
            tag["SoulVisible"] = SoulVisible;
            tag["PatienceMarkStacks"] = PatienceMarkStacks;
            tag["DeterminationCooldown"] = DeterminationCooldown;
            if (DeterminationMarkActive)
            {
                tag["DeterminationMarkActive"] = true;
                tag["DeterminationPosX"] = DeterminationSavedPosition.X;
                tag["DeterminationPosY"] = DeterminationSavedPosition.Y;
            }
        }

        public override void LoadData(TagCompound tag)
        {
            CurrentTrait = (SoulTraitType)tag.GetInt("SoulTrait");
            TraitLocked = tag.GetBool("TraitLocked");
            SoulVisible = tag.GetBool("SoulVisible");
            PatienceMarkStacks = tag.GetInt("PatienceMarkStacks");
            DeterminationCooldown = tag.GetInt("DeterminationCooldown");
            DeterminationMarkActive = tag.GetBool("DeterminationMarkActive");
            if (DeterminationMarkActive)
            {
                DeterminationSavedPosition = new Vector2(tag.GetFloat("DeterminationPosX"), tag.GetFloat("DeterminationPosY"));
            }
        }

        public override void ResetEffects()
        {
            ArmorInvestment = 0;
            WeaponInvestment = 0;
            PotionInvestment = 0;
        }

        public override void PreUpdate()
        {
            // Check if hardmode started and lock trait
            if (Main.hardMode && !TraitLocked && CurrentTrait != SoulTraitType.None)
            {
                TraitLocked = true;
            }
        }

        public override void PostUpdateEquips()
        {
            if (CurrentTrait == SoulTraitType.None)
                return;

            int investment = TotalInvestment;

            // Apply ALL trait effects, but only give investment bonuses to the matching soul
            ApplyAllTraitEffects(investment);
        }

        public override void PostUpdate()
        {
            UpdateTimers();
            UpdateBuffIcons();
        }

        private void UpdateBuffIcons()
        {
            // Justice Mark
            if (JusticeMarkActive)
                Player.AddBuff(ModContent.BuffType<JusticeMarkBuff>(), 2);

            // Kindness Defense
            if (KindnessDefenseTimer > 0)
                Player.AddBuff(ModContent.BuffType<KindnessDefenseBuff>(), KindnessDefenseTimer);

            // Kindness Mark (death buff)
            if (KindnessMarkTimer > 0)
                Player.AddBuff(ModContent.BuffType<KindnessMarkBuff>(), KindnessMarkTimer);

            // Bravery Mark
            if (BraveryMarkActive)
                Player.AddBuff(ModContent.BuffType<BraveryMarkBuff>(), 2);

            // Bravery Damage
            if (BraveryDamageTimer > 0)
                Player.AddBuff(ModContent.BuffType<BraveryDamageBuff>(), BraveryDamageTimer);

            // Patience Mark (stacks)
            if (PatienceMarkStacks > 0)
                Player.AddBuff(ModContent.BuffType<PatienceMarkBuff>(), 2);

            // Integrity Mark (stacks)
            if (IntegrityMarkStacks > 0)
                Player.AddBuff(ModContent.BuffType<IntegrityMarkBuff>(), 2);

            // Perseverance Mark
            if (PerseveranceMarkActive)
                Player.AddBuff(ModContent.BuffType<PerseveranceMarkBuff>(), 2);

            // Perseverance Damage (stacks)
            if (PerseveranceDamageStacks > 0 && PerseveranceDamageTimer > 0)
                Player.AddBuff(ModContent.BuffType<PerseveranceDamageBuff>(), PerseveranceDamageTimer);

            // Determination Mark
            if (DeterminationMarkActive)
                Player.AddBuff(ModContent.BuffType<DeterminationMarkBuff>(), 2);

            // Determination Cooldown
            if (DeterminationCooldown > 0)
                Player.AddBuff(ModContent.BuffType<DeterminationCooldownBuff>(), DeterminationCooldown);
        }

        private void UpdateTimers()
        {
            // Combat timer
            if (CombatTimer > 0)
                CombatTimer--;

            // Kindness defense timer
            if (KindnessDefenseTimer > 0)
                KindnessDefenseTimer--;

            // Bravery damage timer
            if (BraveryDamageTimer > 0)
                BraveryDamageTimer--;

            // Bravery mark timer
            if (BraveryMarkTimer > 0)
                BraveryMarkTimer--;
            else
                BraveryMarkActive = false;

            // Perseverance damage timer
            if (PerseveranceDamageTimer > 0)
            {
                PerseveranceDamageTimer--;
                if (PerseveranceDamageTimer <= 0)
                    PerseveranceDamageStacks = 0;
            }

            // Kindness mark timer
            if (KindnessMarkTimer > 0)
                KindnessMarkTimer--;

            // Determination cooldown
            if (DeterminationCooldown > 0)
                DeterminationCooldown--;

            // Patience no damage timer for stacking marks
            if (CurrentTrait == SoulTraitType.Patience && TotalInvestment >= 20)
            {
                PatienceNoDamageTimer++;
                if (PatienceNoDamageTimer >= PatienceMarkInterval)
                {
                    PatienceMarkStacks++;
                    PatienceNoDamageTimer = 0;
                }
            }

            // Determination mark activation at 50% health
            if (CurrentTrait == SoulTraitType.Determination && TotalInvestment >= 20 && DeterminationCooldown <= 0)
            {
                if (Player.statLife <= Player.statLifeMax2 / 2 && !DeterminationMarkActive)
                {
                    DeterminationMarkActive = true;
                    DeterminationSavedPosition = Player.Center;
                }
            }
        }

        private void ApplyAllTraitEffects(int investment)
        {
            // Apply all trait effects, only giving investment bonuses to the matching soul type
            ApplyJusticeEffects(CurrentTrait == SoulTraitType.Justice ? investment : 0);
            ApplyKindnessEffects(CurrentTrait == SoulTraitType.Kindness ? investment : 0);
            ApplyBraveryEffects(CurrentTrait == SoulTraitType.Bravery ? investment : 0);
            ApplyPatienceEffects(CurrentTrait == SoulTraitType.Patience ? investment : 0);
            ApplyIntegrityEffects(CurrentTrait == SoulTraitType.Integrity ? investment : 0);
            ApplyPerseveranceEffects(CurrentTrait == SoulTraitType.Perseverance ? investment : 0);
            ApplyDeterminationEffects(CurrentTrait == SoulTraitType.Determination ? investment : 0);
        }

        private void ApplyJusticeEffects(int investment)
        {
            // 3 Investment: Triple jump with yellow effects
            if (investment >= 3)
            {
                Player.GetJumpState<JusticeExtraJump>().Enable();
                Player.GetJumpState<JusticeExtraJump2>().Enable();
            }

            // 5 Investment: +6% crit chance
            if (investment >= 5)
            {
                Player.GetCritChance(DamageClass.Generic) += 6;
            }

            // 12 Investment: +10% projectile damage
            if (investment >= 12)
            {
                Player.GetDamage(DamageClass.Ranged) += 0.10f;
                Player.GetDamage(DamageClass.Magic) += 0.10f;
                Player.GetDamage(ModContent.GetInstance<Items.RangedSummonDamageClass>()) += 0.10f;
            }
        }

        private void ApplyKindnessEffects(int investment)
        {
            // 3 Investment: +2 life regen to self and nearby allies
            if (investment >= 3)
            {
                Player.lifeRegen += 2;
                ApplyNearbyAllyHealing();
            }

            // 5 Investment: Defense boost when damaged
            if (investment >= 5 && KindnessDefenseTimer > 0)
            {
                Player.statDefense += 6;
            }

            // 12 Investment: +20% potion duration handled in OnConsumeItem
        }

        private void ApplyNearbyAllyHealing()
        {
            // Only heal once per second (60 ticks)
            if (Main.GameUpdateCount % 60 != 0)
                return;
                
            float range = 400f;
            
            // Heal nearby players
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player other = Main.player[i];
                if (other.active && !other.dead && other.whoAmI != Player.whoAmI && other.statLife < other.statLifeMax2)
                {
                    if (Vector2.Distance(Player.Center, other.Center) <= range)
                    {
                        other.statLife = Math.Min(other.statLife + 2, other.statLifeMax2);
                        other.HealEffect(2);
                    }
                }
            }
            
            // Heal nearby friendly NPCs (town NPCs)
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.friendly && npc.townNPC && npc.life < npc.lifeMax)
                {
                    if (Vector2.Distance(Player.Center, npc.Center) <= range)
                    {
                        npc.life = Math.Min(npc.life + 2, npc.lifeMax);
                        npc.HealEffect(2);
                    }
                }
            }
        }

        private void ApplyBraveryEffects(int investment)
        {
            // 5 Investment: +20% move speed in combat
            if (investment >= 5 && CombatTimer > 0)
            {
                Player.moveSpeed += 0.20f;
            }

            // 12 Investment: Damage boost after taking damage
            if (investment >= 12 && BraveryDamageTimer > 0)
            {
                Player.GetDamage(DamageClass.Generic) += 0.12f;
            }

            // 20 Investment: Attack speed based on enemy proximity
            if (investment >= 20 && BraveryMarkActive)
            {
                float closestDist = GetClosestEnemyDistance();
                float maxRange = 16 * 30;
                if (closestDist < maxRange)
                {
                    float speedBonus = 0.20f * (1f - closestDist / maxRange);
                    Player.GetAttackSpeed(DamageClass.Generic) += speedBonus;
                }
            }
        }

        private void ApplyPatienceEffects(int investment)
        {
            // 3 Investment: Reduced aggro
            if (investment >= 3)
            {
                Player.aggro -= 400;
            }

            // 5 Investment: Double life regen while not moving
            if (investment >= 5)
            {
                if (Player.velocity.Length() < 0.1f)
                {
                    Player.lifeRegen += Player.lifeRegen;
                }
            }

            // 12 Investment: Stealth mode via Calamity's rogue stealth system
            if (investment >= 12)
            {
                ApplyCalamityStealth();
            }
        }
        
        private void ApplyCalamityStealth()
        {
            // Use Calamity's rogue stealth system via reflection
            // CalamityPlayer is not directly accessible, so we use ModPlayer lookup
            if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
            {
                foreach (var modPlayer in Player.ModPlayers)
                {
                    if (modPlayer.GetType().FullName == "CalamityMod.CalPlayer.CalamityPlayer")
                    {
                        var type = modPlayer.GetType();
                        var rogueStealthMaxField = type.GetField("rogueStealthMax");
                        
                        if (rogueStealthMaxField != null)
                        {
                            float currentMax = (float)rogueStealthMaxField.GetValue(modPlayer);
                            
                            if (currentMax <= 0)
                            {
                                // Grant base stealth capacity
                                rogueStealthMaxField.SetValue(modPlayer, 1.0f);
                            }
                            else
                            {
                                // Add 30% more stealth capacity
                                rogueStealthMaxField.SetValue(modPlayer, currentMax + 0.3f);
                            }
                        }
                        break;
                    }
                }
            }
        }

        private void ApplyIntegrityEffects(int investment)
        {
            bool removeNegatives = investment >= 12;

            // 3 Investment: +15% crit damage, -10% crit chance
            if (investment >= 3)
            {
                Player.GetCritChance(DamageClass.Generic) -= removeNegatives ? 0 : 10;
            }

            // 5 Investment: +15% attack speed, -10% damage
            if (investment >= 5)
            {
                Player.GetAttackSpeed(DamageClass.Generic) += 0.15f;
                if (!removeNegatives)
                {
                    Player.GetDamage(DamageClass.Generic) -= 0.10f;
                }
            }
        }

        private void ApplyPerseveranceEffects(int investment)
        {
            bool removeNegatives = investment >= 12;

            // 3 Investment: +20% defense, -10% damage
            if (investment >= 3)
            {
                Player.statDefense += Player.statDefense / 5;
                if (!removeNegatives)
                {
                    Player.GetDamage(DamageClass.Generic) -= 0.10f;
                }
            }

            // 5 Investment: Damage boost stacks after taking damage
            if (investment >= 5 && PerseveranceDamageStacks > 0)
            {
                Player.GetDamage(DamageClass.Generic) += 0.05f * PerseveranceDamageStacks;
            }
        }

        private void ApplyDeterminationEffects(int investment)
        {
            // 3 Investment: -5 seconds respawn time handled in UpdateDead

            // 5 Investment: +6% global damage
            if (investment >= 5)
            {
                Player.GetDamage(DamageClass.Generic) += 0.06f;
            }

            // 12 Investment: Increased immunity frames
            if (investment >= 12)
            {
                Player.longInvince = true;
            }
        }

        public override void UpdateDead()
        {
            // Determination 3 Investment: Reduced respawn time
            if (CurrentTrait == SoulTraitType.Determination && TotalInvestment >= 3)
            {
                if (Player.respawnTimer > 0)
                {
                    Player.respawnTimer = Math.Max(0, Player.respawnTimer - 5);
                }
            }
        }

        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            // Spawn soul shatter effect — the soul breaks into shards of glass
            if (CurrentTrait != SoulTraitType.None && SoulVisible)
            {
                float bobOffset = (float)System.Math.Sin(Main.GameUpdateCount * 0.05f) * 3f;
                Vector2 soulPosition = Player.Center + new Vector2(0, -40 + bobOffset);
                SoulShatterSystem.SpawnShatter(soulPosition, CurrentTrait);
            }
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource)
        {
            // Determination 20 Investment: Revive mechanic
            if (CurrentTrait == SoulTraitType.Determination && TotalInvestment >= 20 && DeterminationMarkActive)
            {
                DeterminationMarkActive = false;
                DeterminationCooldown = DeterminationCooldownDuration;
                Player.statLife = Player.statLifeMax2 / 2;
                Player.Center = DeterminationSavedPosition;
                Player.immune = true;
                Player.immuneTime = 120;

                return false;
            }

            // Kindness 20 Investment: Apply mark to allies on death
            if (CurrentTrait == SoulTraitType.Kindness && TotalInvestment >= 20)
            {
                ApplyKindnessMarkToAllies();
            }

            return true;
        }

        private void ApplyKindnessMarkToAllies()
        {
            float range = 400f;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player other = Main.player[i];
                if (other.active && !other.dead)
                {
                    if (other.whoAmI == Player.whoAmI || Vector2.Distance(Player.Center, other.Center) <= range)
                    {
                        var otherTraitPlayer = other.GetModPlayer<SoulTraitPlayer>();
                        otherTraitPlayer.KindnessMarkTimer = KindnessMarkDuration;
                    }
                }
            }
        }

        public override void OnHurt(Player.HurtInfo info)
        {
            CombatTimer = CombatDuration;

            // Patience: Reset no damage timer (all souls)
            PatienceNoDamageTimer = 0;

            // Kindness 5 Investment: Trigger defense boost (matching soul only)
            if (CurrentTrait == SoulTraitType.Kindness && TotalInvestment >= 5)
            {
                KindnessDefenseTimer = KindnessDefenseDuration;
                TriggerKindnessDefenseForNearbyAllies();
            }

            // Bravery 12 Investment: Trigger damage boost (matching soul only)
            if (CurrentTrait == SoulTraitType.Bravery && TotalInvestment >= 12)
            {
                BraveryDamageTimer = BraveryDamageDuration;
            }

            // Perseverance 5 Investment: Stack damage boost (matching soul only)
            if (CurrentTrait == SoulTraitType.Perseverance && TotalInvestment >= 5)
            {
                PerseveranceDamageStacks = Math.Min(PerseveranceDamageStacks + 1, 5);
                PerseveranceDamageTimer = PerseveranceDamageDuration;
            }

            // Perseverance 20 Investment: Gain mark (matching soul only)
            if (CurrentTrait == SoulTraitType.Perseverance && TotalInvestment >= 20)
            {
                PerseveranceMarkActive = true;
            }
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            // Bravery 3 Investment: Take 5% increased damage (matching soul only)
            if (CurrentTrait == SoulTraitType.Bravery && TotalInvestment >= 3)
            {
                modifiers.FinalDamage *= 1.05f;
            }

            // Patience 20 Investment: Consume mark to reduce damage by 50% (matching soul only)
            if (CurrentTrait == SoulTraitType.Patience && TotalInvestment >= 20 && PatienceMarkStacks > 0)
            {
                PatienceMarkStacks--;
                modifiers.FinalDamage *= 0.5f;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            CombatTimer = CombatDuration;

            // Bravery 3 Investment: +10% damage to nearby enemies (matching soul only)
            if (CurrentTrait == SoulTraitType.Bravery && TotalInvestment >= 3)
            {
                float dist = Vector2.Distance(Player.Center, target.Center);
                if (dist <= 16 * 10)
                {
                    modifiers.FinalDamage *= 1.10f;
                }
            }

            // Justice 20 Investment: Justice Mark guarantees crit (matching soul only)
            if (CurrentTrait == SoulTraitType.Justice && TotalInvestment >= 20 && JusticeMarkActive)
            {
                modifiers.SetCrit();
                JusticeMarkActive = false;
                
                // Sync mark state in multiplayer
                if (Main.netMode != NetmodeID.SinglePlayer)
                {
                    SoulTraitNetworkHandler.SendMarkSync(Player.whoAmI);
                }
            }

            // Integrity 3 Investment: +15% crit damage (matching soul only)
            if (CurrentTrait == SoulTraitType.Integrity && TotalInvestment >= 3)
            {
                modifiers.CritDamage += 0.15f;
            }

            // Integrity 20 Investment: Integrity Mark increases next crit damage (matching soul only)
            if (CurrentTrait == SoulTraitType.Integrity && TotalInvestment >= 20 && IntegrityMarkStacks > 0)
            {
                modifiers.CritDamage += 0.25f * IntegrityMarkStacks;
            }

            // Perseverance 20 Investment: Add defense to armor penetration (matching soul only)
            if (CurrentTrait == SoulTraitType.Perseverance && TotalInvestment >= 20 && PerseveranceMarkActive)
            {
                modifiers.ArmorPenetration += Player.statDefense;
                PerseveranceMarkActive = false;
            }

            // Kindness 20 Investment: Damage boost from mark (all souls can benefit)
            if (KindnessMarkTimer > 0)
            {
                modifiers.FinalDamage *= 1.25f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Justice 20 Investment: Count hits for mark (matching soul only)
            if (CurrentTrait == SoulTraitType.Justice && TotalInvestment >= 20)
            {
                JusticeHitCounter++;
                if (JusticeHitCounter >= 5)
                {
                    JusticeMarkActive = true;
                    JusticeHitCounter = 0;
                    
                    // Sync mark state in multiplayer
                    if (Main.netMode != NetmodeID.SinglePlayer)
                    {
                        SoulTraitNetworkHandler.SendMarkSync(Player.whoAmI);
                    }
                }
            }

            // Bravery 20 Investment: Activate mark (matching soul only)
            if (CurrentTrait == SoulTraitType.Bravery && TotalInvestment >= 20)
            {
                BraveryMarkActive = true;
                BraveryMarkTimer = 300;
            }

            // Integrity 20 Investment: Build mark stacks (matching soul only)
            if (CurrentTrait == SoulTraitType.Integrity && TotalInvestment >= 20)
            {
                if (hit.Crit)
                {
                    IntegrityMarkStacks = 0;
                }
                else
                {
                    IntegrityMarkStacks = Math.Min(IntegrityMarkStacks + 1, 3);
                }
            }
        }

        private void TriggerKindnessDefenseForNearbyAllies()
        {
            float range = 400f;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player other = Main.player[i];
                if (other.active && !other.dead && other.whoAmI != Player.whoAmI)
                {
                    if (Vector2.Distance(Player.Center, other.Center) <= range)
                    {
                        var otherTraitPlayer = other.GetModPlayer<SoulTraitPlayer>();
                        if (otherTraitPlayer.CurrentTrait == SoulTraitType.Kindness && otherTraitPlayer.TotalInvestment >= 5)
                        {
                            otherTraitPlayer.KindnessDefenseTimer = KindnessDefenseDuration;
                        }
                    }
                }
            }
        }

        private float GetClosestEnemyDistance()
        {
            float closest = float.MaxValue;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && npc.damage > 0)
                {
                    float dist = Vector2.Distance(Player.Center, npc.Center);
                    if (dist < closest)
                        closest = dist;
                }
            }
            return closest;
        }

        public void SetTrait(SoulTraitType trait)
        {
            if (TraitLocked)
                return;

            CurrentTrait = trait;
            ResetAllMarks();
        }

        public void ClearTrait()
        {
            if (TraitLocked)
                return;

            CurrentTrait = SoulTraitType.None;
            ResetAllMarks();
        }

        public void ResetPatienceMarksOnBossSummon()
        {
            if (CurrentTrait == SoulTraitType.Patience)
            {
                PatienceMarkStacks = 0;
                PatienceNoDamageTimer = 0;
            }
        }
    }
}
