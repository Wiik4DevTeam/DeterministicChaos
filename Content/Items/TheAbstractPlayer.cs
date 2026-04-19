using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using DeterministicChaos.Content.Projectiles.Friendly;
using DeterministicChaos.Content.SoulTraits;

namespace DeterministicChaos.Content.Items
{
    public class TheAbstractPlayer : ModPlayer
    {
        public string StoredText = "";

        private int currentFiringIndex = 0;
        private bool isFiring = false;
        private bool stealthFiringActive = false;

        public bool IsAutoFiring => autoFireActive;
        private bool autoFireActive = false;
        private int autoFireTimer = 0;
        private const int LETTER_FIRE_DELAY = 6;
        private const int LETTER_FIRE_DELAY_FAST = 2;
        private const int SPACE_DELAY = 15;
        private const int SPACE_DELAY_FAST = 6;
        private const float STEALTH_COST_PER_LETTER = 150f;

        private int firingCooldown = 0;
        private const int FIRING_COOLDOWN_DURATION = 300;
        public bool IsOnCooldown => firingCooldown > 0;
        public float CooldownProgress => (float)firingCooldown / FIRING_COOLDOWN_DURATION;

        private IEntitySource autoFireSource;
        private Vector2 autoFireVelocity;
        private int autoFireProjectileType;
        private int autoFireDamage;
        private float autoFireKnockback;

        private bool burstPending = false;
        private List<BurstLetterInfo> burstLetters = new List<BurstLetterInfo>();

        private struct BurstLetterInfo
        {
            public int letterIndex;
            public LetterEffects effects;
        }

        public override void ResetEffects()
        {
        }

        private int GetManaCostPerLetter()
        {
            return stealthFiringActive ? TheAbstract.MANA_PER_LETTER / 2 : TheAbstract.MANA_PER_LETTER;
        }

        private bool IsInRogueStealth()
        {
            try
            {
                if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
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
                                float stealth = (float)rogueStealthField.GetValue(modPlayer);
                                float maxStealth = (float)rogueStealthMaxField.GetValue(modPlayer);
                                return maxStealth > 0 && stealth >= maxStealth * 0.5f;
                            }
                            break;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private void ConsumeRogueStealth()
        {
            try
            {
                if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
                {
                    foreach (var modPlayer in Player.ModPlayers)
                    {
                        if (modPlayer.GetType().Name == "CalamityPlayer")
                        {
                            var type = modPlayer.GetType();
                            var rogueStealthField = type.GetField("rogueStealth");

                            if (rogueStealthField != null)
                            {
                                float stealth = (float)rogueStealthField.GetValue(modPlayer);
                                float newStealth = stealth - STEALTH_COST_PER_LETTER;
                                if (newStealth < 0) newStealth = 0;
                                rogueStealthField.SetValue(modPlayer, newStealth);
                            }
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        public void StartAutoFireSequence(IEntitySource source, Vector2 velocity, int projectileType, int damage, float knockback)
        {
            string textToFire = GetTextToFire();
            if (string.IsNullOrEmpty(textToFire))
            {
                FireSingleLetter(source, velocity, projectileType, damage, knockback);
                return;
            }

            autoFireActive = true;
            autoFireTimer = 0;
            currentFiringIndex = 0;
            isFiring = true;
            burstPending = false;
            burstLetters.Clear();

            stealthFiringActive = IsInRogueStealth();

            autoFireSource = source;
            autoFireVelocity = velocity;
            autoFireProjectileType = projectileType;
            autoFireDamage = damage;
            autoFireKnockback = knockback;

            FireNextLetter();
        }

        public override void PostUpdate()
        {
            if (firingCooldown > 0)
                firingCooldown--;

            if (autoFireActive && Player.whoAmI == Main.myPlayer)
            {
                Player.itemAnimation = 2;
                Player.itemTime = 2;

                int manaCost = GetManaCostPerLetter();
                if (Player.statMana < manaCost)
                {
                    StopAutoFire();
                    return;
                }

                autoFireTimer++;

                string text = GetTextToFire();
                bool hasFast = IsFastActiveAtPosition(text, currentFiringIndex);
                bool hasBurst = IsBurstActiveAtPosition(text, currentFiringIndex);

                int baseDelay = hasFast ? LETTER_FIRE_DELAY_FAST : LETTER_FIRE_DELAY;

                float attackSpeedMult = Player.GetAttackSpeed(DamageClass.Magic);
                int currentDelay = (int)(baseDelay / attackSpeedMult);
                if (currentDelay < 2) currentDelay = 2;

                if (autoFireTimer >= currentDelay)
                {
                    autoFireTimer = 0;

                    if (hasBurst)
                    {
                        if (!CollectAndFireBurstWord())
                        {
                            StopAutoFire();
                        }
                    }
                    else
                    {
                        if (!FireNextLetter())
                        {
                            StopAutoFire();
                        }
                    }
                }
            }
        }

        private bool CollectAndFireBurstWord()
        {
            string textToFire = GetTextToFire();
            if (string.IsNullOrEmpty(textToFire) || currentFiringIndex >= textToFire.Length)
                return false;

            while (currentFiringIndex < textToFire.Length && textToFire[currentFiringIndex] == ' ')
                currentFiringIndex++;

            if (currentFiringIndex >= textToFire.Length)
                return false;

            burstLetters.Clear();
            while (currentFiringIndex < textToFire.Length && textToFire[currentFiringIndex] != ' ')
            {
                char letter = textToFire[currentFiringIndex];
                int letterIndex = TornNotebook.GetLetterIndex(letter);
                LetterEffects effects = CalculateEffectsForPosition(textToFire, currentFiringIndex);

                if (letterIndex >= 0)
                {
                    burstLetters.Add(new BurstLetterInfo { letterIndex = letterIndex, effects = effects });
                }
                currentFiringIndex++;
            }

            if (burstLetters.Count == 0)
                return currentFiringIndex < textToFire.Length;

            int manaCost = GetManaCostPerLetter();
            int totalCost = manaCost * burstLetters.Count;
            if (Player.statMana < totalCost)
                return false;

            Player.statMana -= totalCost;
            if (Player.statMana < 0) Player.statMana = 0;
            Player.manaRegenDelay = (int)Player.maxRegenDelay;

            Vector2 toMouse = Main.MouseWorld - Player.Center;
            Vector2 direction = toMouse.SafeNormalize(Vector2.UnitX * Player.direction);
            Vector2 handPosition = Player.RotatedRelativePoint(Player.MountedCenter);
            handPosition += direction * 20f;

            float baseAngle = direction.ToRotation();
            int count = burstLetters.Count;

            // If aura is active, spread burst letters evenly around the orbit
            bool hasAura = false;
            if (count > 0)
                hasAura = (burstLetters[0].effects & LetterEffects.Aura) != 0;

            if (hasAura)
            {
                float angleStep = MathHelper.TwoPi / count;
                float startAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < count; i++)
                {
                    var info = burstLetters[i];
                    float orbitAngle = startAngle + angleStep * i;
                    bool hasPartition = (info.effects & LetterEffects.Partition) != 0;
                    int burstDamage = hasPartition ? autoFireDamage / 2 : autoFireDamage;

                    if (hasPartition)
                    {
                        float[] partAngles = { orbitAngle, orbitAngle + MathHelper.TwoPi / 3f / count, orbitAngle - MathHelper.TwoPi / 3f / count };
                        foreach (float a in partAngles)
                            SpawnAuraLetter(info.letterIndex, info.effects, burstDamage, a);
                    }
                    else
                    {
                        SpawnAuraLetter(info.letterIndex, info.effects, autoFireDamage, orbitAngle);
                    }
                }

                SoundEngine.PlaySound(SoundID.Item36 with { Pitch = 0.3f, Volume = 0.8f }, Player.Center);

                bool hasFastBurst = IsFastActiveAtPosition(textToFire, currentFiringIndex);
                int baseSpaceDelayBurst = hasFastBurst ? SPACE_DELAY_FAST : SPACE_DELAY;
                float attackSpeedMultBurst = Player.GetAttackSpeed(DamageClass.Magic);
                int spaceDelayBurst = (int)(baseSpaceDelayBurst * 2 / attackSpeedMultBurst);
                if (spaceDelayBurst < 6) spaceDelayBurst = 6;
                autoFireTimer = -spaceDelayBurst;

                burstLetters.Clear();
                return currentFiringIndex < textToFire.Length;
            }

            float totalSpread = MathHelper.ToRadians(Math.Min(5f * count, 30f));
            float burstStartAngle = baseAngle - totalSpread / 2f;
            float burstAngleStep = count > 1 ? totalSpread / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                var info = burstLetters[i];
                float angle = count > 1 ? burstStartAngle + burstAngleStep * i : baseAngle;

                float speedMult = 1f + Main.rand.NextFloat(-0.1f, 0.1f);
                Vector2 vel = angle.ToRotationVector2() * autoFireVelocity.Length() * speedMult;

                if ((info.effects & LetterEffects.Fast) != 0)
                    vel *= 1.5f;

                FireLetterWithEffects(handPosition, vel, info.letterIndex, info.effects, autoFireDamage);
            }

            SoundEngine.PlaySound(SoundID.Item36 with { Pitch = 0.3f, Volume = 0.8f }, Player.Center);

            bool hasFast = IsFastActiveAtPosition(textToFire, currentFiringIndex);
            int baseSpaceDelay = hasFast ? SPACE_DELAY_FAST : SPACE_DELAY;
            float attackSpeedMult = Player.GetAttackSpeed(DamageClass.Magic);
            int spaceDelay = (int)(baseSpaceDelay * 2 / attackSpeedMult);
            if (spaceDelay < 6) spaceDelay = 6;
            autoFireTimer = -spaceDelay;

            burstLetters.Clear();
            return currentFiringIndex < textToFire.Length;
        }

        private bool FireNextLetter()
        {
            string textToFire = GetTextToFire();
            if (string.IsNullOrEmpty(textToFire) || currentFiringIndex >= textToFire.Length)
                return false;

            if (textToFire[currentFiringIndex] == ' ')
            {
                currentFiringIndex++;
                bool hasFast = IsFastActiveAtPosition(textToFire, currentFiringIndex);
                int baseSpaceDelay = hasFast ? SPACE_DELAY_FAST : SPACE_DELAY;
                float attackSpeedMult = Player.GetAttackSpeed(DamageClass.Magic);
                int spaceDelay = (int)(baseSpaceDelay / attackSpeedMult);
                if (spaceDelay < 3) spaceDelay = 3;
                autoFireTimer = -spaceDelay;
                return currentFiringIndex < textToFire.Length;
            }

            LetterEffects effects = CalculateEffectsForPosition(textToFire, currentFiringIndex);

            // Calculate mana cost multipliers
            int manaCost = GetManaCostPerLetter();
            int costMult = 1;
            if ((effects & LetterEffects.Partition) != 0)
                costMult += 2; // 3 letters
            if ((effects & LetterEffects.Rain) != 0)
                costMult *= 4; // x4 letters

            int actualManaCost = manaCost * costMult;

            if (Player.statMana < actualManaCost)
                return false;

            Player.statMana -= actualManaCost;
            if (Player.statMana < 0) Player.statMana = 0;
            Player.manaRegenDelay = (int)Player.maxRegenDelay;

            char letter = textToFire[currentFiringIndex];
            int letterIndex = TornNotebook.GetLetterIndex(letter);
            currentFiringIndex++;

            if (letterIndex < 0)
                return currentFiringIndex < textToFire.Length;

            Vector2 toMouse = Main.MouseWorld - Player.Center;
            Vector2 direction = toMouse.SafeNormalize(Vector2.UnitX * Player.direction);
            Vector2 handPosition = Player.RotatedRelativePoint(Player.MountedCenter);
            handPosition += direction * 20f;

            Vector2 finalVelocity = direction * autoFireVelocity.Length();
            if ((effects & LetterEffects.Fast) != 0)
                finalVelocity *= 1.5f;

            FireLetterWithEffects(handPosition, finalVelocity, letterIndex, effects, autoFireDamage);

            ConsumeRogueStealth();
            SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.3f, Volume = 0.6f }, Player.Center);

            return currentFiringIndex < textToFire.Length;
        }

        /// <summary>
        /// Core firing method that handles PARTITION, RAIN, and AURA interactions.
        /// </summary>
        private void FireLetterWithEffects(Vector2 position, Vector2 velocity, int letterIndex, LetterEffects effects, int damage)
        {
            bool hasRain = (effects & LetterEffects.Rain) != 0;
            bool hasAura = (effects & LetterEffects.Aura) != 0;
            bool hasPartition = (effects & LetterEffects.Partition) != 0;

            int rainDamage = hasRain ? damage / 2 : damage;
            int partDamage = hasPartition ? rainDamage / 2 : rainDamage;
            int letterCount = hasRain ? 4 : 1;

            if (hasAura)
            {
                // AURA: spawn letters orbiting the player
                if (hasPartition)
                {
                    // AURA + PARTITION: 3 letters per copy in orbit
                    for (int r = 0; r < letterCount; r++)
                    {
                        float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float[] angles = { baseAngle, baseAngle + MathHelper.TwoPi / 3f, baseAngle + MathHelper.TwoPi * 2f / 3f };
                        foreach (float a in angles)
                        {
                            SpawnAuraLetter(letterIndex, effects, partDamage, a);
                        }
                    }
                }
                else
                {
                    for (int r = 0; r < letterCount; r++)
                    {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        SpawnAuraLetter(letterIndex, effects, partDamage, angle);
                    }
                }
            }
            else if (hasRain)
            {
                // RAIN: letters fall from sky at random positions near cursor
                Vector2 targetPos = Main.MouseWorld;
                for (int r = 0; r < letterCount; r++)
                {
                    float xOffset = Main.rand.NextFloat(-200f, 200f);
                    float yOffset = Main.rand.NextFloat(-600f, -400f);
                    Vector2 skyPos = new Vector2(targetPos.X + xOffset, targetPos.Y + yOffset);

                    // Randomized downward direction with slight horizontal variance
                    float angle = MathHelper.PiOver2 + Main.rand.NextFloat(-0.3f, 0.3f);
                    float speed = autoFireVelocity.Length() * (0.8f + Main.rand.NextFloat(0.4f));
                    if ((effects & LetterEffects.Fast) != 0)
                        speed *= 1.5f;
                    Vector2 rainVel = angle.ToRotationVector2() * speed;

                    if (hasPartition)
                    {
                        FirePartitionLetters(skyPos, rainVel, letterIndex, effects, partDamage);
                    }
                    else
                    {
                        Projectile.NewProjectile(
                            autoFireSource, skyPos, rainVel,
                            autoFireProjectileType, rainDamage, autoFireKnockback,
                            Player.whoAmI, letterIndex, (float)(int)effects
                        );
                    }
                }
            }
            else if (hasPartition)
            {
                FirePartitionLetters(position, velocity, letterIndex, effects, partDamage);
            }
            else
            {
                Projectile.NewProjectile(
                    autoFireSource, position, velocity,
                    autoFireProjectileType, damage, autoFireKnockback,
                    Player.whoAmI, letterIndex, (float)(int)effects
                );
            }
        }

        private void SpawnAuraLetter(int letterIndex, LetterEffects effects, int damage, float startAngle)
        {
            // Aura letters use ai[0] for letter index, ai[1] for effects
            // The AbstractLetter projectile will detect the Aura flag and orbit instead of flying
            int proj = Projectile.NewProjectile(
                autoFireSource, Player.Center, Vector2.Zero,
                autoFireProjectileType, damage, autoFireKnockback,
                Player.whoAmI, letterIndex, (float)(int)effects
            );

            if (proj >= 0 && proj < Main.maxProjectiles)
            {
                // Store starting orbit angle in localAI[0]
                Main.projectile[proj].localAI[0] = startAngle;
            }
        }

        private void FirePartitionLetters(Vector2 position, Vector2 velocity, int letterIndex, LetterEffects effects, int damage)
        {
            float baseAngle = velocity.ToRotation();
            float speed = velocity.Length();
            float spread = MathHelper.ToRadians(25f);

            Vector2 perpendicular = velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            float positionOffset = 24f;

            float[] angles = { baseAngle, baseAngle - spread, baseAngle + spread };
            Vector2[] offsets = { Vector2.Zero, -perpendicular * positionOffset, perpendicular * positionOffset };

            for (int i = 0; i < 3; i++)
            {
                Vector2 spawnPos = position + offsets[i];
                Vector2 vel = angles[i].ToRotationVector2() * speed;
                Projectile.NewProjectile(
                    autoFireSource, spawnPos, vel,
                    autoFireProjectileType, damage, autoFireKnockback,
                    Player.whoAmI, letterIndex, (float)(int)effects
                );
            }
        }

        private void FireSingleLetter(IEntitySource source, Vector2 velocity, int projectileType, int damage, float knockback)
        {
            int manaCost = IsInRogueStealth() ? TheAbstract.MANA_PER_LETTER / 2 : TheAbstract.MANA_PER_LETTER;

            if (Player.statMana < manaCost)
                return;

            Player.statMana -= manaCost;
            if (Player.statMana < 0) Player.statMana = 0;
            Player.manaRegenDelay = (int)Player.maxRegenDelay;

            int letterIndex = Main.rand.Next(26);

            Vector2 handPosition = Player.RotatedRelativePoint(Player.MountedCenter);
            handPosition += velocity.SafeNormalize(Vector2.Zero) * 20f;

            Projectile.NewProjectile(
                source, handPosition, velocity,
                projectileType, damage, knockback,
                Player.whoAmI, letterIndex, 0f
            );

            ConsumeRogueStealth();
            SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.3f, Volume = 0.6f }, Player.Center);
        }

        private void StopAutoFire()
        {
            autoFireActive = false;
            autoFireTimer = 0;
            burstPending = false;
            burstLetters.Clear();
            firingCooldown = FIRING_COOLDOWN_DURATION;
            ResetFiringState();
        }

        public bool HasLettersToFire()
        {
            string textToFire = GetTextToFire();
            if (string.IsNullOrEmpty(textToFire))
                return true;
            if (!isFiring)
                return true;
            return currentFiringIndex < textToFire.Length;
        }

        private string GetTextToFire()
        {
            if (string.IsNullOrEmpty(StoredText))
                return null;
            return StoredText.ToUpper();
        }

        private bool IsFastActiveAtPosition(string text, int position)
        {
            if (string.IsNullOrEmpty(text)) return false;
            int fastIndex = text.ToUpper().IndexOf("FAST");
            return fastIndex >= 0 && position >= fastIndex + 4;
        }

        private bool IsBurstActiveAtPosition(string text, int position)
        {
            if (string.IsNullOrEmpty(text)) return false;
            int burstIndex = text.ToUpper().IndexOf("BURST");
            return burstIndex >= 0 && position >= burstIndex + 5;
        }

        private LetterEffects CalculateEffectsForPosition(string text, int position)
        {
            LetterEffects effects = LetterEffects.None;
            string upperText = text.ToUpper();

            var subsequentWords = new Dictionary<string, LetterEffects>
            {
                { "SEEK", LetterEffects.Seek },
                { "FAST", LetterEffects.Fast },
                { "BIG", LetterEffects.Big },
                { "PIERCE", LetterEffects.Pierce },
                { "SPLIT", LetterEffects.Split },
                { "PARTITION", LetterEffects.Partition },
                { "BURROW", LetterEffects.Burrow },
                { "HEAL", LetterEffects.Heal },
                { "AURA", LetterEffects.Aura },
                { "RAIN", LetterEffects.Rain },
            };

            var exactWords = new Dictionary<string, LetterEffects>
            {
                { "BOOM", LetterEffects.Boom },
                { "CRIT", LetterEffects.Crit },
            };

            foreach (var kv in subsequentWords)
            {
                int wordIndex = upperText.IndexOf(kv.Key);
                if (wordIndex >= 0 && position >= wordIndex + kv.Key.Length)
                {
                    effects |= kv.Value;
                }
            }

            foreach (var kv in exactWords)
            {
                int wordIndex = upperText.IndexOf(kv.Key);
                if (wordIndex >= 0)
                {
                    if (position >= wordIndex && position < wordIndex + kv.Key.Length)
                    {
                        effects |= kv.Value;
                    }
                }
            }

            return effects;
        }

        public void ResetFiringState()
        {
            isFiring = false;
            currentFiringIndex = 0;
        }

        public void SetStoredText(string text)
        {
            StoredText = text ?? "";
            ResetFiringState();

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                SyncStoredText();
            }
        }

        private void SyncStoredText()
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)TheAbstractNetHandler.MessageType.SyncStoredText);
            packet.Write((byte)Player.whoAmI);
            packet.Write(StoredText ?? "");
            packet.Send();
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)TheAbstractNetHandler.MessageType.SyncStoredText);
            packet.Write((byte)Player.whoAmI);
            packet.Write(StoredText ?? "");
            packet.Send(toWho, fromWho);
        }

        public override void SaveData(TagCompound tag)
        {
            if (!string.IsNullOrEmpty(StoredText))
            {
                tag["TheAbstract_StoredText"] = StoredText;
            }
        }

        public override void LoadData(TagCompound tag)
        {
            if (tag.ContainsKey("TheAbstract_StoredText"))
            {
                StoredText = tag.GetString("TheAbstract_StoredText");
            }
        }
    }

    public static class TheAbstractNetHandler
    {
        public enum MessageType : byte
        {
            SyncStoredText = 52
        }

        public static void HandlePacket(System.IO.BinaryReader reader, int whoAmI)
        {
            byte playerIndex = reader.ReadByte();
            string storedText = reader.ReadString();

            if (playerIndex >= 0 && playerIndex < Main.maxPlayers)
            {
                Player player = Main.player[playerIndex];
                if (player.active)
                {
                    var abstractPlayer = player.GetModPlayer<TheAbstractPlayer>();
                    abstractPlayer.StoredText = storedText;
                    abstractPlayer.ResetFiringState();

                    if (Main.netMode == NetmodeID.Server)
                    {
                        ModPacket packet = ModContent.GetInstance<DeterministicChaos>().GetPacket();
                        packet.Write((byte)MessageType.SyncStoredText);
                        packet.Write(playerIndex);
                        packet.Write(storedText);
                        packet.Send(-1, whoAmI);
                    }
                }
            }
        }
    }
}
