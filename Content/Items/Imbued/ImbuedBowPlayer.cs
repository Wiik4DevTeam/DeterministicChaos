using Terraria;
using Terraria.ModLoader;
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

namespace DeterministicChaos.Content.Items.Imbued
{
    public class ImbuedBowPlayer : ModPlayer
    {
        // Justice: Counts double jumps used since last shot
        public int doubleJumpsUsed;

        // Perseverance: Whether extra arrows were paid for this volley
        public bool perseveranceExtraArrows;

        // Track previous active state of extra jumps to detect activation
        private bool wasJump1Active;
        private bool wasJump2Active;
        private bool wasJump3Active;

        public override void PostUpdate()
        {
            // Track Justice extra jump activation
            bool jump1Active = Player.GetJumpState(ModContent.GetInstance<JusticeExtraJump>()).Active;
            bool jump2Active = Player.GetJumpState(ModContent.GetInstance<JusticeExtraJump2>()).Active;
            bool jump3Active = Player.GetJumpState(ModContent.GetInstance<JusticeExtraJump3>()).Active;

            if (jump1Active && !wasJump1Active)
                doubleJumpsUsed++;
            if (jump2Active && !wasJump2Active)
                doubleJumpsUsed++;
            if (jump3Active && !wasJump3Active)
                doubleJumpsUsed++;

            wasJump1Active = jump1Active;
            wasJump2Active = jump2Active;
            wasJump3Active = jump3Active;

            // Reset perseverance flag when player stops using the item
            if (Player.itemAnimation <= 0)
                perseveranceExtraArrows = false;
        }

        public override void ResetEffects()
        {
            // Don't reset doubleJumpsUsed here — it persists until consumed by Justice Bow shot
            // Don't reset perseveranceExtraArrows here — it persists for the full volley
        }
    }
}
