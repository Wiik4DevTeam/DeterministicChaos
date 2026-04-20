using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;
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
    public class ImbuedSummonPlayer : ModPlayer
    {
        public override void PostUpdate()
        {
            // Patience: Passively gain stealth based on active Patience clone count
            int patienceClones = Player.ownedProjectileCounts[ModContent.ProjectileType<PatienceSummonProjectile>()];
            if (patienceClones > 0)
            {
                float stealthGain = 0.001f * patienceClones;
                DarkShardPlayer.RefundCalamityStealth(Player, stealthGain);
            }
        }
    }
}
