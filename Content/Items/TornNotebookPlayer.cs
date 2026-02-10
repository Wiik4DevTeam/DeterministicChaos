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
    [Flags]
    public enum LetterEffects : int
    {
        None = 0,
        Seek = 1 << 0,        // Seeking behavior
        Fast = 1 << 1,        // Faster movement
        Big = 1 << 2,         // Bigger size
        Boom = 1 << 3,        // Explodes on hit
        Crit = 1 << 4,        // Guaranteed crit
        Pierce = 1 << 5,      // Pierces enemies (loses seeking on hit)
        // Debuffs
        Fire = 1 << 6,
        Frostburn = 1 << 7,
        Poison = 1 << 8,
        Venom = 1 << 9,
        Ichor = 1 << 10,
        Cursed = 1 << 11,
        Shadowflame = 1 << 12,
        Daybreak = 1 << 13,
        Betsy = 1 << 14,
    }

    public class TornNotebookPlayer : ModPlayer
    {
        // The stored text in the notebook
        public string StoredText = "";

        // Current firing position in the text
        private int currentFiringIndex = 0;

        // Whether we're currently in a firing sequence
        private bool isFiring = false;

        // Stealth mana discount - firing from stealth halves mana cost
        private bool stealthFiringActive = false;

        // Track if player is using the notebook
        private bool wasUsingNotebook = false;
        private int lastHeldItemType = -1;

        // Auto-firing system
        public bool IsAutoFiring => autoFireActive;
        private bool autoFireActive = false;
        private int autoFireTimer = 0;
        private const int LETTER_FIRE_DELAY = 12; // Ticks between letters
        private const int LETTER_FIRE_DELAY_FAST = 5; // Much faster delay with FAST effect
        private const int SPACE_DELAY = 23; // Extra ticks for space pause
        private const int SPACE_DELAY_FAST = 10; // Much faster space delay with FAST effect
        private const float STEALTH_COST_PER_LETTER = 150f; // Flat stealth consumed per letter
        
        // Cooldown after finishing a firing sequence
        private int firingCooldown = 0;
        private const int FIRING_COOLDOWN_DURATION = 300; // 5 seconds at 60fps
        public bool IsOnCooldown => firingCooldown > 0;
        public float CooldownProgress => (float)firingCooldown / FIRING_COOLDOWN_DURATION;
        
        // Stored firing parameters
        private IEntitySource autoFireSource;
        private Vector2 autoFireVelocity;
        private int autoFireProjectileType;
        private int autoFireDamage;
        private float autoFireKnockback;

        public override void ResetEffects()
        {
            // Stealth status is checked when starting a firing sequence, not here
        }

        private int GetManaCostPerLetter()
        {
            return stealthFiringActive ? TornNotebook.MANA_PER_LETTER / 2 : TornNotebook.MANA_PER_LETTER;
        }

        private bool IsInRogueStealth()
        {
            // Check for Calamity's rogue stealth
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

                                // Consider in stealth if above 50% of max
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
                            var rogueStealthMaxField = type.GetField("rogueStealthMax");

                            if (rogueStealthField != null && rogueStealthMaxField != null)
                            {
                                float stealth = (float)rogueStealthField.GetValue(modPlayer);

                                // Consume flat 40 stealth per letter
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
                // Fire single random letter immediately
                FireSingleLetter(source, velocity, projectileType, damage, knockback);
                return;
            }

            // Start the auto-fire sequence
            autoFireActive = true;
            autoFireTimer = 0;
            currentFiringIndex = 0;
            isFiring = true;
            
            // Check if starting from stealth - if so, halve mana cost for entire sequence
            stealthFiringActive = IsInRogueStealth();

            // Store parameters for later
            autoFireSource = source;
            autoFireVelocity = velocity;
            autoFireProjectileType = projectileType;
            autoFireDamage = damage;
            autoFireKnockback = knockback;

            // Fire first letter immediately
            FireNextLetter();
        }

        public override void PostUpdate()
        {
            // Decrement cooldown timer
            if (firingCooldown > 0)
                firingCooldown--;

            // Handle auto-firing sequence
            if (autoFireActive && Player.whoAmI == Main.myPlayer)
            {
                // Keep the player visually holding the item
                Player.itemAnimation = 2;
                Player.itemTime = 2;

                // Check mana before even trying - end sequence immediately if out of mana
                int manaCost = GetManaCostPerLetter();
                if (Player.statMana < manaCost)
                {
                    StopAutoFire();
                    return;
                }

                autoFireTimer++;

                // Check if FAST effect is active at current position
                // FAST only applies once we've reached or passed the first letter of FAST
                string text = GetTextToFire();
                bool hasFast = IsFastActiveAtPosition(text, currentFiringIndex);
                int baseDelay = hasFast ? LETTER_FIRE_DELAY_FAST : LETTER_FIRE_DELAY;
                
                // Apply magic attack speed modifier (higher = faster, so divide delay)
                float attackSpeedMult = Player.GetAttackSpeed(DamageClass.Magic);
                int currentDelay = (int)(baseDelay / attackSpeedMult);
                if (currentDelay < 2) currentDelay = 2; // Minimum delay

                // Check if it's time to fire next letter
                if (autoFireTimer >= currentDelay)
                {
                    autoFireTimer = 0;
                    
                    if (!FireNextLetter())
                    {
                        // No more letters to fire or failed to fire
                        StopAutoFire();
                    }
                }
            }
        }

        private bool FireNextLetter()
        {
            string textToFire = GetTextToFire();
            if (string.IsNullOrEmpty(textToFire) || currentFiringIndex >= textToFire.Length)
            {
                return false;
            }

            // Check if current character is a space - pause but continue sequence
            if (textToFire[currentFiringIndex] == ' ')
            {
                currentFiringIndex++;
                // Set timer to negative so we wait extra time before next letter
                // FAST only applies if we've reached or passed the word FAST
                bool hasFast = IsFastActiveAtPosition(textToFire, currentFiringIndex);
                int baseSpaceDelay = hasFast ? SPACE_DELAY_FAST : SPACE_DELAY;
                
                // Apply magic attack speed modifier
                float attackSpeedMult = Player.GetAttackSpeed(DamageClass.Magic);
                int spaceDelay = (int)(baseSpaceDelay / attackSpeedMult);
                if (spaceDelay < 3) spaceDelay = 3; // Minimum space delay
                
                autoFireTimer = -spaceDelay;
                // Return true to continue the sequence (there may be more letters)
                return currentFiringIndex < textToFire.Length;
            }

            // Check mana
            int manaCost = GetManaCostPerLetter();
            if (Player.statMana < manaCost)
            {
                // Not enough mana, stop firing
                return false;
            }

            // Consume mana
            Player.statMana -= manaCost;
            if (Player.statMana < 0) Player.statMana = 0;
            Player.manaRegenDelay = (int)Player.maxRegenDelay;

            // Calculate effects for this letter
            LetterEffects effects = CalculateEffectsForPosition(textToFire, currentFiringIndex);

            char letter = textToFire[currentFiringIndex];
            int letterIndex = TornNotebook.GetLetterIndex(letter);
            currentFiringIndex++;

            if (letterIndex < 0)
            {
                // Invalid character, try next
                return currentFiringIndex < textToFire.Length;
            }

            // Apply FAST effect to velocity
            Vector2 finalVelocity = autoFireVelocity;
            if ((effects & LetterEffects.Fast) != 0)
            {
                finalVelocity *= 1.5f;
            }

            // Calculate spawn position - aim toward mouse
            Vector2 toMouse = Main.MouseWorld - Player.Center;
            Vector2 direction = toMouse.SafeNormalize(Vector2.UnitX * Player.direction);
            Vector2 handPosition = Player.RotatedRelativePoint(Player.MountedCenter);
            handPosition += direction * 20f;

            // Adjust velocity to aim at mouse
            finalVelocity = direction * finalVelocity.Length();

            // Create projectile
            Projectile.NewProjectile(
                autoFireSource,
                handPosition,
                finalVelocity,
                autoFireProjectileType,
                autoFireDamage,
                autoFireKnockback,
                Player.whoAmI,
                letterIndex,
                (float)(int)effects
            );

            // Consume stealth
            ConsumeRogueStealth();

            // Play sound
            SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.2f, Volume = 0.6f }, Player.Center);

            return currentFiringIndex < textToFire.Length;
        }

        private void FireSingleLetter(IEntitySource source, Vector2 velocity, int projectileType, int damage, float knockback)
        {
            // Single letter uses stealth check at moment of fire
            int manaCost = IsInRogueStealth() ? TornNotebook.MANA_PER_LETTER / 2 : TornNotebook.MANA_PER_LETTER;
            
            // Check mana
            if (Player.statMana < manaCost)
                return;

            // Consume mana
            Player.statMana -= manaCost;
            if (Player.statMana < 0) Player.statMana = 0;
            Player.manaRegenDelay = (int)Player.maxRegenDelay;

            // Random letter A-Z
            int letterIndex = Main.rand.Next(26);

            Vector2 handPosition = Player.RotatedRelativePoint(Player.MountedCenter);
            handPosition += velocity.SafeNormalize(Vector2.Zero) * 20f;

            Projectile.NewProjectile(
                source,
                handPosition,
                velocity,
                projectileType,
                damage,
                knockback,
                Player.whoAmI,
                letterIndex,
                0f
            );

            // Consume stealth
            ConsumeRogueStealth();

            SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.2f, Volume = 0.6f }, Player.Center);
        }

        private void StopAutoFire()
        {
            autoFireActive = false;
            autoFireTimer = 0;
            firingCooldown = FIRING_COOLDOWN_DURATION; // Start cooldown
            ResetFiringState();
        }

        public bool HasLettersToFire()
        {
            string textToFire = GetTextToFire();
            
            if (string.IsNullOrEmpty(textToFire))
            {
                // No text stored, can still fire random letter
                return true;
            }

            // If not firing, can start
            if (!isFiring)
                return true;

            // Check if there are more letters
            return currentFiringIndex < textToFire.Length;
        }

        private string GetTextToFire()
        {
            if (string.IsNullOrEmpty(StoredText))
            {
                return null;
            }
            return StoredText.ToUpper();
        }

        public char? GetNextLetterToFire(out LetterEffects effects)
        {
            effects = LetterEffects.None;
            string textToFire = GetTextToFire();

            if (textToFire == null)
            {
                // Fire random letter (A-Z, no space)
                return (char)('A' + Main.rand.Next(26));
            }

            // Start firing if not already
            if (!isFiring)
            {
                isFiring = true;
                currentFiringIndex = 0;
            }

            // Skip spaces
            while (currentFiringIndex < textToFire.Length && textToFire[currentFiringIndex] == ' ')
            {
                currentFiringIndex++;
            }

            if (currentFiringIndex >= textToFire.Length)
            {
                // End of text, reset for next use
                ResetFiringState();
                return null;
            }

            // Calculate effects for this letter position
            effects = CalculateEffectsForPosition(textToFire, currentFiringIndex);

            char letter = textToFire[currentFiringIndex];
            currentFiringIndex++;

            // Check if this was the last letter
            if (currentFiringIndex >= textToFire.Length)
            {
                ResetFiringState();
            }

            return letter;
        }

        private bool IsFastActiveAtPosition(string text, int position)
        {
            if (string.IsNullOrEmpty(text)) return false;
            
            // Find FAST in the text (case insensitive)
            int fastIndex = text.ToUpper().IndexOf("FAST");
            
            // FAST applies from the first letter of the word onwards
            return fastIndex >= 0 && position >= fastIndex;
        }

        private LetterEffects CalculateEffectsForPosition(string text, int position)
        {
            LetterEffects effects = LetterEffects.None;
            
            // Convert to uppercase for case-insensitive matching
            string upperText = text.ToUpper();

            // Word definitions: keyword -> (effect, isSubsequent)
            var subsequentWords = new Dictionary<string, LetterEffects>
            {
                { "SEEK", LetterEffects.Seek },
                { "FAST", LetterEffects.Fast },
                { "BIG", LetterEffects.Big },
                { "PIERCE", LetterEffects.Pierce },
            };

            var exactWords = new Dictionary<string, LetterEffects>
            {
                { "BOOM", LetterEffects.Boom },
                { "CRIT", LetterEffects.Crit },
                { "FIRE", LetterEffects.Fire },
                { "FROSTBURN", LetterEffects.Frostburn },
                { "POISON", LetterEffects.Poison },
                { "VENOM", LetterEffects.Venom },
                { "ICHOR", LetterEffects.Ichor },
                { "CURSED", LetterEffects.Cursed },
                { "SHADOWFLAME", LetterEffects.Shadowflame },
                { "DAYBREAK", LetterEffects.Daybreak },
                { "BETSY", LetterEffects.Betsy },
            };

            // Check "subsequent" words - if the word appears before or at this position, effect applies
            foreach (var kv in subsequentWords)
            {
                int wordIndex = upperText.IndexOf(kv.Key);
                if (wordIndex >= 0 && wordIndex <= position)
                {
                    // Effects apply to all letters from that position onward
                    effects |= kv.Value;
                }
            }

            // Check "exact" words - only letters that are part of the word get the effect
            foreach (var kv in exactWords)
            {
                int wordIndex = upperText.IndexOf(kv.Key);
                if (wordIndex >= 0)
                {
                    // Check if this position is within the word
                    if (position >= wordIndex && position < wordIndex + kv.Key.Length)
                    {
                        // Check progression for debuffs
                        if (IsDebuffAvailable(kv.Value))
                        {
                            effects |= kv.Value;
                        }
                    }
                }
            }

            return effects;
        }

        private bool IsDebuffAvailable(LetterEffects effect)
        {
            switch (effect)
            {
                case LetterEffects.Fire:
                case LetterEffects.Frostburn:
                case LetterEffects.Poison:
                    return true; // Always available

                case LetterEffects.Ichor:
                case LetterEffects.Cursed:
                case LetterEffects.Shadowflame:
                    return Main.hardMode; // Hardmode required

                case LetterEffects.Venom:
                    return NPC.downedPlantBoss; // Post-Plantera

                case LetterEffects.Daybreak:
                    return NPC.downedGolemBoss; // Post-Golem

                case LetterEffects.Betsy:
                    return Terraria.GameContent.Events.DD2Event.DownedInvasionT3; // Post-Betsy

                default:
                    return true;
            }
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

            // Sync to server in multiplayer
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                SyncStoredText();
            }
        }

        private void SyncStoredText()
        {
            // Send packet to sync text
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)TornNotebookNetHandler.MessageType.SyncStoredText);
            packet.Write((byte)Player.whoAmI);
            packet.Write(StoredText ?? "");
            packet.Send();
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
        {
            // Sync stored text to other players
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)TornNotebookNetHandler.MessageType.SyncStoredText);
            packet.Write((byte)Player.whoAmI);
            packet.Write(StoredText ?? "");
            packet.Send(toWho, fromWho);
        }

        public override void SaveData(TagCompound tag)
        {
            if (!string.IsNullOrEmpty(StoredText))
            {
                tag["TornNotebook_StoredText"] = StoredText;
            }
        }

        public override void LoadData(TagCompound tag)
        {
            if (tag.ContainsKey("TornNotebook_StoredText"))
            {
                StoredText = tag.GetString("TornNotebook_StoredText");
            }
        }
    }

    public static class TornNotebookNetHandler
    {
        public enum MessageType : byte
        {
            SyncStoredText = 50 // Use high number to avoid conflicts
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
                    var notebookPlayer = player.GetModPlayer<TornNotebookPlayer>();
                    notebookPlayer.StoredText = storedText;
                    notebookPlayer.ResetFiringState();

                    // Server relays to other clients
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
