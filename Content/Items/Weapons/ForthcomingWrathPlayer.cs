using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
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

namespace DeterministicChaos.Content.Items.Weapons
{
    // Tracks ForthcomingWrath hit counter for the charged attack.
    // Every 9 glaive hits on enemies the next swing fires one massive charged glaive.
    public class ForthcomingWrathPlayer : ModPlayer
    {
        public const int HitsNeeded = 9;

        public int hitCount = 0;
        public bool chargeReady = false;

        // Not reset each frame, persists between attacks intentionally.

        public void RegisterHit()
        {
            if (chargeReady)
                return; // Already at max, don't count further until used

            hitCount++;
            if (hitCount >= HitsNeeded)
            {
                hitCount = 0;
                chargeReady = true;

                // Visual/audio feedback when charge becomes ready
                if (Main.myPlayer == Player.whoAmI)
                {
                    for (int i = 0; i < 20; i++)
                    {
                        Dust d = Dust.NewDustDirect(Player.Center - new Vector2(10f), 20, 20,
                            DustID.BlueTorch, Scale: Main.rand.NextFloat(1.5f, 2.5f));
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(6f, 6f);
                    }
                }
            }
        }

        public void ConsumeCharge()
        {
            chargeReady = false;
            hitCount = 0;
        }
    }
}
