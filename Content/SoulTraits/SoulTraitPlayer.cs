using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.Achievements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
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
using DeterministicChaos.Content.Items.Imbued;
using DeterministicChaos.Content.Items.Prefixes;
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
        public int BonusInvestment = 0;
        public int TotalInvestment => Math.Min(ArmorInvestment + WeaponInvestment + PotionInvestment + BonusInvestment, 20);

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

        public bool IntegrityMarkActive = false;
        public int IntegrityMarkHitsRemaining = 0;

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

        public int IntegrityDamageStacks = 0;
        public int IntegrityDamageTimer = 0;
        public const int IntegrityDamageDuration = 300;

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
            IntegrityMarkActive = false;
            IntegrityMarkHitsRemaining = 0;
            PerseveranceMarkActive = false;
            DeterminationMarkActive = false;
            DeterminationCooldown = 0;
            KindnessDefenseTimer = 0;
            BraveryDamageTimer = 0;
            IntegrityDamageStacks = 0;
            IntegrityDamageTimer = 0;
            CombatTimer = 0;
        }

        public override void SaveData(TagCompound tag)
        {
            tag["SoulTrait"] = (int)CurrentTrait;
            tag["TraitLocked"] = TraitLocked;
            tag["SoulVisible"] = SoulVisible;
            tag["BonusInvestment"] = BonusInvestment;
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
            BonusInvestment = tag.GetInt("BonusInvestment");
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
            // Lock trait when entering hardmode, unlock when in pre-hardmode
            if (Main.hardMode && !TraitLocked && CurrentTrait != SoulTraitType.None)
            {
                TraitLocked = true;
            }
            else if (!Main.hardMode && TraitLocked)
            {
                // Unlock trait if world is not in hardmode (e.g., joined a new pre-hardmode world)
                TraitLocked = false;
            }
        }

        public override void PostUpdateEquips()
        {
            if (CurrentTrait == SoulTraitType.None)
                return;

            // Apply weapon investment early, HoldItem runs after PostUpdateEquips,
            // so we need to grab it here for trait effects like extra jumps to work.
            // Only grant weapon investment if the weapon's required trait matches the player's trait.
            SoulTraitType requiredTrait = SoulTraitGlobalItem.GetWeaponTraitRequirement(Player.HeldItem.type);
            if (requiredTrait == SoulTraitType.None || requiredTrait == CurrentTrait)
            {
                WeaponInvestment = SoulTraitGlobalItem.GetWeaponInvestment(Player.HeldItem.type);
            }

            int investment = TotalInvestment;

            // Apply ALL trait effects, but only give investment bonuses to the matching soul
            ApplyAllTraitEffects(investment);
        }

        public override void PostUpdate()
        {
            UpdateTimers();
            UpdateBuffIcons();
        }

        public override void NaturalLifeRegen(ref float regen)
        {
            // Patience 5 Investment: Double life regen while not moving
            if (CurrentTrait == SoulTraitType.Patience && TotalInvestment >= 5)
            {
                if (Player.velocity.Length() < 0.1f)
                {
                    regen *= 2f;
                }
            }
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

            // Integrity Mark
            if (IntegrityMarkActive)
                Player.AddBuff(ModContent.BuffType<IntegrityMarkBuff>(), 2);

            // Perseverance Mark
            if (PerseveranceMarkActive)
                Player.AddBuff(ModContent.BuffType<PerseveranceMarkBuff>(), 2);

            // Integrity Damage (stacks)
            if (IntegrityDamageStacks > 0 && IntegrityDamageTimer > 0)
                Player.AddBuff(ModContent.BuffType<IntegrityDamageBuff>(), IntegrityDamageTimer);

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

            // Integrity damage timer
            if (IntegrityDamageTimer > 0)
            {
                IntegrityDamageTimer--;
                if (IntegrityDamageTimer <= 0)
                    IntegrityDamageStacks = 0;
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
                // Patience Emblem reduces interval by 10 seconds (600 ticks)
                int interval = Player.GetModPlayer<ImbuedEmblemPlayer>().hasPatienceEmblem
                    ? PatienceMarkInterval - 600
                    : PatienceMarkInterval;
                if (PatienceNoDamageTimer >= interval)
                {
                    PatienceMarkStacks++;
                    PatienceNoDamageTimer = 0;
                }
            }

            // Perseverance mark activation at full health (or any health with emblem)
            if (CurrentTrait == SoulTraitType.Perseverance && TotalInvestment >= 20)
            {
                bool hasEmblem = Player.GetModPlayer<ImbuedEmblemPlayer>().hasPerseveranceEmblem;
                bool healthCheck = hasEmblem || Player.statLife >= Player.statLifeMax2;

                if (healthCheck && Player.statMana > 0 && !PerseveranceMarkActive)
                {
                    PerseveranceMarkActive = true;
                }
                // Deactivate mark if health/mana conditions no longer met
                else if (PerseveranceMarkActive && (!healthCheck || Player.statMana <= 0))
                {
                    PerseveranceMarkActive = false;
                }
            }

            // Determination mark activation at 50% health
            if (CurrentTrait == SoulTraitType.Determination && TotalInvestment >= 20 && DeterminationCooldown <= 0)
            {
                if (Player.statLife <= Player.statLifeMax2 / 2 && !DeterminationMarkActive)
                {
                    DeterminationMarkActive = true;
                    DeterminationSavedPosition = Player.Center;

                    // Play save sound when the star appears
                    SoundEngine.PlaySound(new SoundStyle($"{nameof(DeterministicChaos)}/Assets/Sounds/PlayerSave"), DeterminationSavedPosition);
                }
            }

            // Deactivate all self-targeted marks if investment drops below 20
            if (TotalInvestment < 20)
            {
                JusticeMarkActive = false;
                BraveryMarkActive = false;
                BraveryMarkTimer = 0;
                PatienceMarkStacks = 0;
                PatienceNoDamageTimer = 0;
                IntegrityMarkActive = false;
                PerseveranceMarkActive = false;
                DeterminationMarkActive = false;
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

                // Justice Emblem: Extra double jump
                if (Player.GetModPlayer<ImbuedEmblemPlayer>().hasJusticeEmblem)
                {
                    Player.GetJumpState<JusticeExtraJump3>().Enable();
                }
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
                Player.GetDamage(ModContent.GetInstance<RangedSummonDamageClass>()) += 0.10f;
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
            int baseHeal = 2;
            // Kindness Emblem: +25% healing effectiveness
            bool hasEmblem = Player.GetModPlayer<ImbuedEmblemPlayer>().hasKindnessEmblem;
            int healAmount = hasEmblem ? (int)(baseHeal * 1.25f) : baseHeal;
            if (healAmount < baseHeal) healAmount = baseHeal; // Ensure at least base
            
            // Heal nearby players
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player other = Main.player[i];
                if (other.active && !other.dead && other.whoAmI != Player.whoAmI)
                {
                    if (Vector2.Distance(Player.Center, other.Center) <= range)
                    {
                        RoaringGunPlayer.NotifyAllyHealed(Player.whoAmI);
                        if (other.statLife < other.statLifeMax2)
                        {
                            int heal = Player.GetModPlayer<PrefixEffectPlayer>().ScaleHeal(healAmount);
                            other.statLife = Math.Min(other.statLife + heal, other.statLifeMax2);
                            other.HealEffect(heal);
                        }
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
                        npc.life = Math.Min(npc.life + healAmount, npc.lifeMax);
                        npc.HealEffect(healAmount);
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
                    float proximityFactor = 1f - closestDist / maxRange;
                    float speedBonus = 0.20f * proximityFactor;
                    Player.GetAttackSpeed(DamageClass.Generic) += speedBonus;

                    // Bravery Emblem: +20% weapon damage that scales with the attack speed bonus
                    if (Player.GetModPlayer<ImbuedEmblemPlayer>().hasBraveryEmblem)
                    {
                        Player.GetDamage(DamageClass.Generic) += 0.20f * proximityFactor;
                    }
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

            // 5 Investment: Double life regen while not moving (handled in NaturalLifeRegen hook)
            // This space intentionally left for reference

            // 12 Investment: Stealth mode via Calamity's rogue stealth system
            if (investment >= 12)
            {
                ApplyCalamityStealth();
            }
        }
        
        private void ApplyCalamityStealth()
        {
            // Use Calamity's rogue stealth system via reflection
            if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity))
                return;

            foreach (var modPlayer in Player.ModPlayers)
            {
                if (modPlayer.GetType().Name != "CalamityPlayer")
                    continue;

                var type = modPlayer.GetType();

                // Enable rogue armor flag, required for the stealth meter to appear
                var wearingRogueArmor = type.GetField("wearingRogueArmor");
                wearingRogueArmor?.SetValue(modPlayer, true);

                // Set stealth capacity
                var rogueStealthMaxField = type.GetField("rogueStealthMax");
                if (rogueStealthMaxField != null)
                {
                    float currentMax = (float)rogueStealthMaxField.GetValue(modPlayer);
                    if (currentMax <= 0f)
                        rogueStealthMaxField.SetValue(modPlayer, 1.0f);
                    else
                        rogueStealthMaxField.SetValue(modPlayer, currentMax + 0.3f);
                }

                // Set stealth generation rates so stealth actually regenerates
                var stealthGenStandstill = type.GetField("stealthGenStandstill");
                var stealthGenMoving = type.GetField("stealthGenMoving");

                float standstillRate = 0.5f;
                float movingRate = 0.25f;

                // Patience Emblem: +15% stealth generation
                if (Player.GetModPlayer<ImbuedEmblemPlayer>().hasPatienceEmblem)
                {
                    standstillRate *= 1.15f;
                    movingRate *= 1.15f;
                }

                stealthGenStandstill?.SetValue(modPlayer, standstillRate);
                stealthGenMoving?.SetValue(modPlayer, movingRate);

                break;
            }
        }

        private void ApplyIntegrityEffects(int investment)
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
            if (investment >= 5 && IntegrityDamageStacks > 0)
            {
                Player.GetDamage(DamageClass.Generic) += 0.05f * IntegrityDamageStacks;
            }
        }

        private void ApplyPerseveranceEffects(int investment)
        {
            // 3 Investment: Health/mana pickups grant the other's effect (handled in SoulTraitGlobalItem.OnPickup)

            // 5 Investment: +1% damage per buff/debuff on player
            if (investment >= 5)
            {
                int buffCount = 0;
                for (int i = 0; i < Player.MaxBuffs; i++)
                {
                    if (Player.buffType[i] > 0 && Player.buffTime[i] > 0)
                        buffCount++;
                }
                Player.GetDamage(DamageClass.Generic) += 0.01f * buffCount;
            }

            // 12 Investment: Up to +50 max health from mana ratio, +50 max mana from health ratio
            if (investment >= 12)
            {
                float manaRatio = Player.statManaMax2 > 0 ? (float)Player.statMana / Player.statManaMax2 : 0f;
                float healthRatio = Player.statLifeMax2 > 0 ? (float)Player.statLife / Player.statLifeMax2 : 0f;

                Player.statLifeMax2 += (int)(50 * manaRatio);
                Player.statManaMax2 += (int)(50 * healthRatio);
            }

            // 20 Investment: At full health, gain Perseverance Mark (handled in UpdateTimers)
            // Next hit deals 50% less damage + depletes mana (handled in ModifyHurt)
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
            // Spawn soul shatter effect, the soul breaks into shards of glass
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
                // Determination Emblem: Reduce cooldown by 30 seconds (1800 ticks)
                int cooldown = Player.GetModPlayer<ImbuedEmblemPlayer>().hasDeterminationEmblem
                    ? DeterminationCooldownDuration - 1800
                    : DeterminationCooldownDuration;
                DeterminationCooldown = cooldown;
                Player.statLife = Player.statLifeMax2 / 2;
                Player.Center = DeterminationSavedPosition;
                Player.immune = true;
                Player.immuneTime = 120;

                // Yellow flash burst at the save location
                for (int i = 0; i < 30; i++)
                {
                    Dust dust = Dust.NewDustDirect(DeterminationSavedPosition - new Vector2(16, 16), 32, 32, DustID.GoldFlame);
                    dust.velocity = Main.rand.NextVector2Circular(6f, 6f);
                    dust.noGravity = true;
                    dust.scale = Main.rand.NextFloat(1.5f, 2.5f);
                }

                // Play the PlayerLoad sound
                SoundEngine.PlaySound(new SoundStyle($"{nameof(DeterministicChaos)}/Assets/Sounds/PlayerLoad"), DeterminationSavedPosition);

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
            // Kindness Emblem: +10 seconds (600 ticks) to mark duration
            int markDuration = Player.GetModPlayer<ImbuedEmblemPlayer>().hasKindnessEmblem
                ? KindnessMarkDuration + 600
                : KindnessMarkDuration;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player other = Main.player[i];
                if (other.active && !other.dead)
                {
                    if (other.whoAmI == Player.whoAmI || Vector2.Distance(Player.Center, other.Center) <= range)
                    {
                        var otherTraitPlayer = other.GetModPlayer<SoulTraitPlayer>();
                        otherTraitPlayer.KindnessMarkTimer = Player.GetModPlayer<PrefixEffectPlayer>().ScaleBuffDuration(markDuration, 0);
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
                KindnessDefenseTimer = Player.GetModPlayer<PrefixEffectPlayer>().ScaleBuffDuration(KindnessDefenseDuration, 0);
                TriggerKindnessDefenseForNearbyAllies();
            }

            // Bravery 12 Investment: Trigger damage boost (matching soul only)
            if (CurrentTrait == SoulTraitType.Bravery && TotalInvestment >= 12)
            {
                BraveryDamageTimer = Player.GetModPlayer<PrefixEffectPlayer>().ScaleBuffDuration(BraveryDamageDuration, 0);
            }

            // Integrity 5 Investment: Stack damage boost (matching soul only)
            if (CurrentTrait == SoulTraitType.Integrity && TotalInvestment >= 5)
            {
                IntegrityDamageStacks = Math.Min(IntegrityDamageStacks + 1, 5);
                IntegrityDamageTimer = Player.GetModPlayer<PrefixEffectPlayer>().ScaleBuffDuration(IntegrityDamageDuration, 0);
            }

            // Integrity 20 Investment: Gain mark (matching soul only)
            if (CurrentTrait == SoulTraitType.Integrity && TotalInvestment >= 20)
            {
                IntegrityMarkActive = true;
                // Integrity Emblem: Mark lasts 3 hits instead of 1
                IntegrityMarkHitsRemaining = Player.GetModPlayer<ImbuedEmblemPlayer>().hasIntegrityEmblem ? 3 : 1;
            }

            // Perseverance 20 Investment: Mark consumed on hurt (handled in ModifyHurt)
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

            // Perseverance 20 Investment: Consume mark to reduce damage by 50% and deplete mana
            if (CurrentTrait == SoulTraitType.Perseverance && TotalInvestment >= 20 && PerseveranceMarkActive)
            {
                PerseveranceMarkActive = false;
                modifiers.FinalDamage *= 0.5f;
                Player.statMana = 0;
                Player.manaRegenDelay = 300; // 5 second mana regen delay
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

                // Justice Emblem: Guaranteed Hypercrit (3x damage instead of 2x)
                var emblemPlayer = Player.GetModPlayer<ImbuedEmblemPlayer>();
                if (emblemPlayer.hasJusticeEmblem)
                {
                    modifiers.FinalDamage *= 1.5f;
                    emblemPlayer.justiceMarkHypercritPending = true;
                }
                
                // Sync mark state in multiplayer
                if (Main.netMode != NetmodeID.SinglePlayer)
                {
                    SoulTraitNetworkHandler.SendMarkSync(Player.whoAmI);
                }
            }

            // Integrity 20 Investment: Add defense as flat damage on next hit (matching soul only)
            if (CurrentTrait == SoulTraitType.Integrity && TotalInvestment >= 20 && IntegrityMarkActive)
            {
                modifiers.FlatBonusDamage += Player.statDefense;
                IntegrityMarkHitsRemaining--;
                if (IntegrityMarkHitsRemaining <= 0)
                {
                    IntegrityMarkActive = false;
                    IntegrityMarkHitsRemaining = 0;
                }
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

            // Justice Emblem: Hypercrit VFX from Justice Mark
            var emblemPlayer = Player.GetModPlayer<ImbuedEmblemPlayer>();
            if (emblemPlayer.justiceMarkHypercritPending)
            {
                emblemPlayer.justiceMarkHypercritPending = false;

                // Bright yellow burst
                for (int i = 0; i < 25; i++)
                {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
                    Dust dust = Dust.NewDustPerfect(target.Center, DustID.YellowTorch, vel, 0, default, 2f);
                    dust.noGravity = true;
                }

                // Show bright yellow combat text
                CombatText.NewText(target.Hitbox, new Color(255, 255, 50), damageDone, dramatic: true);

                // Play hypercrit sound
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/Hypercrit") { Volume = 0.6f }, target.Center);

                // Sheriff Hat synergy
                var hatPlayer = Player.GetModPlayer<Armor.CowboyHatPlayer>();
                if (hatPlayer.hasSheriffHat)
                {
                    hatPlayer.hypercritAttackSpeedTimer = 36;
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
                            otherTraitPlayer.KindnessDefenseTimer = Player.GetModPlayer<PrefixEffectPlayer>().ScaleBuffDuration(KindnessDefenseDuration, 0);
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
