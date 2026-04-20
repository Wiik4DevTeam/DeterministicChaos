using System;
using Terraria;
using Terraria.ModLoader;
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

namespace DeterministicChaos.Content.Items.Accessories
{
    public class DarkShardPlayer : ModPlayer
    {
        private static bool? calamityLoaded = null;

        public override void SetStaticDefaults()
        {
            calamityLoaded ??= ModLoader.HasMod("CalamityMod");
        }

        /// <summary>
        /// Checks if the player's Calamity stealth meter is full (stealth strike available).
        /// </summary>
        public static bool CheckCalamityStealthStrike(Player player)
        {
            if (calamityLoaded != true)
                return false;

            try
            {
                if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
                {
                    var calPlayerType = calamity.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
                    if (calPlayerType == null)
                        return false;

                    ModPlayer calPlayer = null;
                    foreach (var modPlayer in player.ModPlayers)
                    {
                        if (modPlayer.GetType() == calPlayerType)
                        {
                            calPlayer = modPlayer;
                            break;
                        }
                    }

                    if (calPlayer == null)
                        return false;

                    var stealthProp = calPlayerType.GetProperty("StealthStrikeAvailable");
                    if (stealthProp != null)
                        return (bool)stealthProp.GetValue(calPlayer);

                    var stealthField = calPlayerType.GetField("StealthStrikeAvailable");
                    if (stealthField != null)
                        return (bool)stealthField.GetValue(calPlayer);

                    var rogueStealth = calPlayerType.GetField("rogueStealth");
                    var rogueStealthMax = calPlayerType.GetField("rogueStealthMax");
                    if (rogueStealth != null && rogueStealthMax != null)
                    {
                        float stealth = (float)rogueStealth.GetValue(calPlayer);
                        float maxStealth = (float)rogueStealthMax.GetValue(calPlayer);
                        return maxStealth > 0 && stealth >= maxStealth;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Refunds stealth meter by the given flat amount via Calamity reflection.
        /// </summary>
        public static void RefundCalamityStealth(Player player, float amount)
        {
            if (calamityLoaded != true)
                return;

            try
            {
                if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
                {
                    var calPlayerType = calamity.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
                    if (calPlayerType == null)
                        return;

                    ModPlayer calPlayer = null;
                    foreach (var modPlayer in player.ModPlayers)
                    {
                        if (modPlayer.GetType() == calPlayerType)
                        {
                            calPlayer = modPlayer;
                            break;
                        }
                    }

                    if (calPlayer == null)
                        return;

                    var rogueStealthField = calPlayerType.GetField("rogueStealth");
                    var rogueStealthMaxField = calPlayerType.GetField("rogueStealthMax");
                    if (rogueStealthField != null && rogueStealthMaxField != null)
                    {
                        float currentStealth = (float)rogueStealthField.GetValue(calPlayer);
                        float maxStealth = (float)rogueStealthMaxField.GetValue(calPlayer);
                        float newStealth = System.Math.Min(currentStealth + amount, maxStealth);
                        rogueStealthField.SetValue(calPlayer, newStealth);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Consumes mana and returns true if successful. Used by Perseverance trait.
        /// </summary>
        public static bool TryConsumeMana(Player player, int amount)
        {
            if (player.statMana >= amount)
            {
                player.statMana -= amount;
                player.manaRegenDelay = (int)player.maxRegenDelay;
                return true;
            }
            return false;
        }
    }
}
