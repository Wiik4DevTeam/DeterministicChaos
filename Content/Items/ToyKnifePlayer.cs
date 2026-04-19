using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.SoulTraits;

namespace DeterministicChaos.Content.Items
{
    public class ToyKnifePlayer : ModPlayer
    {
        // Stealth preservation for non-consuming shots
        private float preservedStealth = 0f;
        private object calamityPlayer = null;
        private System.Reflection.FieldInfo rogueStealthField = null;
        private System.Reflection.FieldInfo rogueStealthMaxField = null;

        // Temporary bonus system
        public int TempManaBonus = 0;
        public float TempStealthBonus = 0f;
        private int bonusTimer = 0;
        private const int BONUS_DURATION = 300; // 5 seconds at 60fps

        // Maximum temporary bonuses
        private const int MAX_TEMP_MANA = 100;
        private const float MAX_TEMP_STEALTH = 0.2f; // 20% in Calamity's scale (100 = 1.0f)

        // Mana and stealth granted per hit
        private const int MANA_PER_HIT = 20;
        private const float STEALTH_PER_HIT = 0.02f; // 2% stealth per hit

        public override void ResetEffects()
        {
            // Only apply bonuses with Patience trait
            if (Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Patience)
            {
                TempManaBonus = 0;
                TempStealthBonus = 0f;
                return;
            }

            // Apply temp mana bonus
            if (TempManaBonus > 0)
            {
                Player.statManaMax2 += TempManaBonus;
            }

            // Apply temp stealth bonus via Calamity
            if (TempStealthBonus > 0f)
            {
                ApplyTempStealthBonus();
            }
        }

        public override void PostUpdate()
        {
            // Decay bonus timer
            if (bonusTimer > 0)
            {
                bonusTimer--;
                if (bonusTimer <= 0)
                {
                    // Bonuses expired
                    TempManaBonus = 0;
                    TempStealthBonus = 0f;
                }
            }
        }

        private void EnsureCalamityReflection()
        {
            if (calamityPlayer != null && rogueStealthField != null)
                return;

            try
            {
                if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
                {
                    foreach (var modPlayer in Player.ModPlayers)
                    {
                        if (modPlayer.GetType().Name == "CalamityPlayer")
                        {
                            calamityPlayer = modPlayer;
                            var type = modPlayer.GetType();
                            rogueStealthField = type.GetField("rogueStealth");
                            rogueStealthMaxField = type.GetField("rogueStealthMax");
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        public void PreserveStealthOnShoot()
        {
            EnsureCalamityReflection();
            if (calamityPlayer != null && rogueStealthField != null)
            {
                preservedStealth = (float)rogueStealthField.GetValue(calamityPlayer);
            }
        }

        public void RestoreStealthAfterShoot()
        {
            if (calamityPlayer != null && rogueStealthField != null)
            {
                rogueStealthField.SetValue(calamityPlayer, preservedStealth);
            }
        }

        private float GetCurrentStealth()
        {
            EnsureCalamityReflection();
            if (calamityPlayer != null && rogueStealthField != null)
            {
                return (float)rogueStealthField.GetValue(calamityPlayer);
            }
            return 0f;
        }

        private float GetMaxStealth()
        {
            EnsureCalamityReflection();
            if (calamityPlayer != null && rogueStealthMaxField != null)
            {
                return (float)rogueStealthMaxField.GetValue(calamityPlayer);
            }
            return 0f;
        }

        private void AddStealth(float amount)
        {
            EnsureCalamityReflection();
            if (calamityPlayer != null && rogueStealthField != null && rogueStealthMaxField != null)
            {
                float current = (float)rogueStealthField.GetValue(calamityPlayer);
                float max = (float)rogueStealthMaxField.GetValue(calamityPlayer);
                float newStealth = System.Math.Min(current + amount, max + TempStealthBonus);
                rogueStealthField.SetValue(calamityPlayer, newStealth);
            }
        }

        private void ApplyTempStealthBonus()
        {
            EnsureCalamityReflection();
            if (calamityPlayer != null && rogueStealthMaxField != null)
            {
                float baseMax = (float)rogueStealthMaxField.GetValue(calamityPlayer);
                // Only add our temp bonus if not already applied this frame
                // Note: This is tricky since rogueStealthMax resets each frame in Calamity
                // We add our bonus on top
                rogueStealthMaxField.SetValue(calamityPlayer, baseMax + TempStealthBonus);
            }
        }

        public void OnToyKnifeHit(NPC target)
        {
            // Check if at full mana or full stealth BEFORE granting resources
            bool wasFullMana = Player.statMana >= Player.statManaMax2;
            float currentStealth = GetCurrentStealth();
            float maxStealth = GetMaxStealth();
            bool wasFullStealth = maxStealth > 0 && currentStealth >= maxStealth;

            // Grant 20 mana
            Player.statMana = System.Math.Min(Player.statMana + MANA_PER_HIT, Player.statManaMax2);

            // Grant stealth
            AddStealth(STEALTH_PER_HIT);

            // If was at full mana OR full stealth, grant temporary bonus
            if (wasFullMana || wasFullStealth)
            {
                // Add temp mana bonus (up to max)
                TempManaBonus = System.Math.Min(TempManaBonus + 10, MAX_TEMP_MANA);

                // Add temp stealth bonus (up to max)
                TempStealthBonus = System.Math.Min(TempStealthBonus + 0.02f, MAX_TEMP_STEALTH);

                // Refresh/start the 5 second timer
                bonusTimer = BONUS_DURATION;

                // Visual feedback for bonus gain
                SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.5f, Volume = 0.5f }, Player.Center);

                // Cyan sparkle effect (Patience color)
                for (int i = 0; i < 8; i++)
                {
                    Vector2 dustVel = Main.rand.NextVector2Circular(3f, 3f);
                    Dust dust = Dust.NewDustDirect(Player.Center, 0, 0, DustID.IceTorch, dustVel.X, dustVel.Y);
                    dust.noGravity = true;
                    dust.scale = 1.2f;
                }
            }
            else
            {
                // Normal hit feedback - smaller effect
                for (int i = 0; i < 3; i++)
                {
                    Vector2 dustVel = Main.rand.NextVector2Circular(2f, 2f);
                    Dust dust = Dust.NewDustDirect(target.Center, 0, 0, DustID.IceTorch, dustVel.X, dustVel.Y);
                    dust.noGravity = true;
                    dust.scale = 0.8f;
                }
            }
        }
    }
}
