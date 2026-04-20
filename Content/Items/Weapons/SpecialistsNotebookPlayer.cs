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

namespace DeterministicChaos.Content.Items.Weapons
{
    public class SpecialistsNotebookPlayer : ModPlayer
    {
        // The stored text in the notebook
        public string StoredText = "";

        // Current firing position in the text
        private int currentFiringIndex = 0;

        // Whether we're currently in a firing sequence
        private bool isFiring = false;

        // Stealth mana discount
        private bool stealthFiringActive = false;

        // Auto-firing system
        public bool IsAutoFiring => autoFireActive;
        private bool autoFireActive = false;
        private int autoFireTimer = 0;
        private const int LETTER_FIRE_DELAY = 8;       // Faster base rate (vs TornNotebook's 12)
        private const int LETTER_FIRE_DELAY_FAST = 3;   // Faster with FAST (vs 5)
        private const int SPACE_DELAY = 18;              // Faster space delay (vs 23)
        private const int SPACE_DELAY_FAST = 7;          // Faster with FAST (vs 10)
        private const float STEALTH_COST_PER_LETTER = 150f;

        // Cooldown after finishing a firing sequence
        private int firingCooldown = 0;
        private const int FIRING_COOLDOWN_DURATION = 300;
        public bool IsOnCooldown => firingCooldown > 0;
        public float CooldownProgress => (float)firingCooldown / FIRING_COOLDOWN_DURATION;

        // Stored firing parameters
        private IEntitySource autoFireSource;
        private Vector2 autoFireVelocity;
        private int autoFireProjectileType;
        private int autoFireDamage;
        private float autoFireKnockback;

        // BURST state, when active, we accumulate a word's letters and fire them all at once
        private bool burstPending = false;
        private List<BurstLetterInfo> burstLetters = new List<BurstLetterInfo>();

        private struct BurstLetterInfo
        {
            public int letterIndex;
            public LetterEffects effects;
        }

        public override void ResetEffects()
        {
            // Stealth status is checked when starting a firing sequence
        }

        private int GetManaCostPerLetter()
        {
            return stealthFiringActive ? SpecialistsNotebook.MANA_PER_LETTER / 2 : SpecialistsNotebook.MANA_PER_LETTER;
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

            // Fire first letter normally, BURST only activates after its keyword is fully shot
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
                // BURST only activates after its keyword has been fully shot
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
                        // In BURST mode, collect and fire entire words
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

        // Collects all non-space letters of the next word and fires them all at once in a shotgun spread.
        // Then skips to the next word, with double the normal word delay.
        private bool CollectAndFireBurstWord()
        {
            string textToFire = GetTextToFire();
            if (string.IsNullOrEmpty(textToFire) || currentFiringIndex >= textToFire.Length)
                return false;

            // Skip leading spaces
            while (currentFiringIndex < textToFire.Length && textToFire[currentFiringIndex] == ' ')
                currentFiringIndex++;

            if (currentFiringIndex >= textToFire.Length)
                return false;

            // Collect all letters until next space or end
            burstLetters.Clear();
            int wordStart = currentFiringIndex;
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

            // Check if we have enough mana for all letters in this word
            int manaCost = GetManaCostPerLetter();
            int totalCost = manaCost * burstLetters.Count;
            if (Player.statMana < totalCost)
                return false;

            // Consume mana for the whole word
            Player.statMana -= totalCost;
            if (Player.statMana < 0) Player.statMana = 0;
            Player.manaRegenDelay = (int)Player.maxRegenDelay;

            // Fire all letters in a shotgun spread
            Vector2 toMouse = Main.MouseWorld - Player.Center;
            Vector2 direction = toMouse.SafeNormalize(Vector2.UnitX * Player.direction);
            Vector2 handPosition = Player.RotatedRelativePoint(Player.MountedCenter);
            handPosition += direction * 20f;

            float baseAngle = direction.ToRotation();
            int count = burstLetters.Count;

            // Spread: wider with more letters, up to ~30 degrees total
            float totalSpread = MathHelper.ToRadians(Math.Min(5f * count, 30f));
            float startAngle = baseAngle - totalSpread / 2f;
            float angleStep = count > 1 ? totalSpread / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                var info = burstLetters[i];
                float angle = count > 1 ? startAngle + angleStep * i : baseAngle;

                // Slight speed variation for nice spread
                float speedMult = 1f + Main.rand.NextFloat(-0.1f, 0.1f);
                Vector2 vel = angle.ToRotationVector2() * autoFireVelocity.Length() * speedMult;

                if ((info.effects & LetterEffects.Fast) != 0)
                    vel *= 1.5f;

                // PARTITION: fire 3 letters with 1/2 damage each
                if ((info.effects & LetterEffects.Partition) != 0)
                {
                    FirePartitionLetters(handPosition, vel, info.letterIndex, info.effects, autoFireDamage / 2);
                }
                else
                {
                    Projectile.NewProjectile(
                        autoFireSource, handPosition, vel,
                        autoFireProjectileType, autoFireDamage, autoFireKnockback,
                        Player.whoAmI, info.letterIndex, (float)(int)info.effects
                    );
                }

                ConsumeRogueStealth();
            }

            SoundEngine.PlaySound(SoundID.Item36 with { Pitch = 0.3f, Volume = 0.8f }, Player.Center);

            // Set double word-delay after burst
            bool hasFast = IsFastActiveAtPosition(textToFire, currentFiringIndex);
            int baseSpaceDelay = hasFast ? SPACE_DELAY_FAST : SPACE_DELAY;
            float attackSpeedMult = Player.GetAttackSpeed(DamageClass.Magic);
            int spaceDelay = (int)(baseSpaceDelay * 2 / attackSpeedMult); // Double delay for BURST
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

            // Check if current character is a space
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

            int manaCost = GetManaCostPerLetter();

            // PARTITION costs 3x mana (fires 3 letters)
            LetterEffects peekEffects = CalculateEffectsForPosition(textToFire, currentFiringIndex);
            int actualManaCost = (peekEffects & LetterEffects.Partition) != 0 ? manaCost * 3 : manaCost;

            if (Player.statMana < actualManaCost)
                return false;

            Player.statMana -= actualManaCost;
            if (Player.statMana < 0) Player.statMana = 0;
            Player.manaRegenDelay = (int)Player.maxRegenDelay;

            LetterEffects effects = CalculateEffectsForPosition(textToFire, currentFiringIndex);
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

            // PARTITION: fire 3 letters (above/center/below) dealing 1/2 damage each
            if ((effects & LetterEffects.Partition) != 0)
            {
                FirePartitionLetters(handPosition, finalVelocity, letterIndex, effects, autoFireDamage / 2);
            }
            else
            {
                Projectile.NewProjectile(
                    autoFireSource, handPosition, finalVelocity,
                    autoFireProjectileType, autoFireDamage, autoFireKnockback,
                    Player.whoAmI, letterIndex, (float)(int)effects
                );
            }

            ConsumeRogueStealth();
            SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.2f, Volume = 0.6f }, Player.Center);

            return currentFiringIndex < textToFire.Length;
        }

        // Fires 3 letters: original direction, +15 degrees above, -15 degrees below.
        // Each deals the specified damage (should be 1/3rd of base).
        private void FirePartitionLetters(Vector2 position, Vector2 velocity, int letterIndex, LetterEffects effects, int damage)
        {
            float baseAngle = velocity.ToRotation();
            float speed = velocity.Length();
            float spread = MathHelper.ToRadians(25f);
            float positionOffset = 24f; // Pixels above/below the center letter

            // Perpendicular direction for spatial offset (rotated 90 degrees from velocity)
            Vector2 perpendicular = velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);

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
            int manaCost = IsInRogueStealth() ? SpecialistsNotebook.MANA_PER_LETTER / 2 : SpecialistsNotebook.MANA_PER_LETTER;

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
            SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.2f, Volume = 0.6f }, Player.Center);
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
            // FAST applies only AFTER the word has been fully shot
            return fastIndex >= 0 && position >= fastIndex + 4;
        }

        private bool IsBurstActiveAtPosition(string text, int position)
        {
            if (string.IsNullOrEmpty(text)) return false;
            int burstIndex = text.ToUpper().IndexOf("BURST");
            // BURST applies only AFTER the word has been fully shot
            return burstIndex >= 0 && position >= burstIndex + 5;
        }

        private LetterEffects CalculateEffectsForPosition(string text, int position)
        {
            LetterEffects effects = LetterEffects.None;
            string upperText = text.ToUpper();

            // "Subsequent" words, effect applies from their position onward
            var subsequentWords = new Dictionary<string, LetterEffects>
            {
                { "SEEK", LetterEffects.Seek },
                { "FAST", LetterEffects.Fast },
                { "BIG", LetterEffects.Big },
                { "PIERCE", LetterEffects.Pierce },
                { "SPLIT", LetterEffects.Split },
                { "PARTITION", LetterEffects.Partition },
                { "BURROW", LetterEffects.Burrow },
            };

            // "Exact" words, only letters within the word get the effect
            var exactWords = new Dictionary<string, LetterEffects>
            {
                { "BOOM", LetterEffects.Boom },
                { "CRIT", LetterEffects.Crit },
            };

            // BURST is a global modifier checked separately (not a per-letter effect)

            // Effects apply only AFTER the entire keyword has been shot
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
            packet.Write((byte)SpecialistsNotebookNetHandler.MessageType.SyncStoredText);
            packet.Write((byte)Player.whoAmI);
            packet.Write(StoredText ?? "");
            packet.Send();
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)SpecialistsNotebookNetHandler.MessageType.SyncStoredText);
            packet.Write((byte)Player.whoAmI);
            packet.Write(StoredText ?? "");
            packet.Send(toWho, fromWho);
        }

        public override void SaveData(TagCompound tag)
        {
            if (!string.IsNullOrEmpty(StoredText))
            {
                tag["SpecialistsNotebook_StoredText"] = StoredText;
            }
        }

        public override void LoadData(TagCompound tag)
        {
            if (tag.ContainsKey("SpecialistsNotebook_StoredText"))
            {
                StoredText = tag.GetString("SpecialistsNotebook_StoredText");
            }
        }
    }

    public static class SpecialistsNotebookNetHandler
    {
        public enum MessageType : byte
        {
            SyncStoredText = 51 // Different from TornNotebook's 50
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
                    var notebookPlayer = player.GetModPlayer<SpecialistsNotebookPlayer>();
                    notebookPlayer.StoredText = storedText;
                    notebookPlayer.ResetFiringState();

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
