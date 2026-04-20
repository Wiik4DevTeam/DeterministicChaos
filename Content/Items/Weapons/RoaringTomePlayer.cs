using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Imbued;
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
    public class RoaringTomePlayer : ModPlayer
    {
        public ImbuedClarityVariant imbuedClarityVariant = ImbuedClarityVariant.None;
        public bool isHoldingClarity = false;

        // Calamity rogue stealth reflection cache (used by Patience Clarity to grant stealth)
        private object calamityPlayer = null;
        private System.Reflection.FieldInfo rogueStealthField = null;
        private System.Reflection.FieldInfo rogueStealthMaxField = null;
        private bool reflectionInitialized = false;

        public override void ResetEffects()
        {
            isHoldingClarity = false;
        }

        public override void PostUpdate()
        {
            if (!isHoldingClarity)
                imbuedClarityVariant = ImbuedClarityVariant.None;
        }

        private void EnsureCalamityReflection()
        {
            if (reflectionInitialized)
                return;
            reflectionInitialized = true;

            try
            {
                if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
                {
                    var calPlayerType = calamity.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
                    if (calPlayerType == null) return;

                    foreach (var modPlayer in Player.ModPlayers)
                    {
                        if (modPlayer.GetType() == calPlayerType)
                        {
                            calamityPlayer = modPlayer;
                            rogueStealthField = calPlayerType.GetField("rogueStealth");
                            rogueStealthMaxField = calPlayerType.GetField("rogueStealthMax");
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        // Grant rogue stealth proportional to damage (Patience Clarity big-orb explosion).
        public void GrantPatienceStealth(int damage)
        {
            EnsureCalamityReflection();
            if (calamityPlayer == null || rogueStealthField == null || rogueStealthMaxField == null)
                return;

            try
            {
                float max = (float)rogueStealthMaxField.GetValue(calamityPlayer);
                if (max <= 0f) return;

                float current = (float)rogueStealthField.GetValue(calamityPlayer);
                // ~100 damage = 1.0 stealth (typically a full meter); capped at max
                float gain = damage * 0.01f;
                float newStealth = System.Math.Min(max, current + gain);
                if (newStealth > current)
                    rogueStealthField.SetValue(calamityPlayer, newStealth);
            }
            catch { }
        }
    }
}
