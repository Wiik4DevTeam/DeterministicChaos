using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items.Armor
{
    // Global projectile to track stealth strike projectiles for rogue set bonus
    public class RoaringRogueGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        
        public bool isStealthStrikeProjectile = false;
        
        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            // Check if this projectile is from a player with the rogue set
            if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
            {
                Player owner = Main.player[projectile.owner];
                var modPlayer = owner.GetModPlayer<RoaringArmorPlayer>();
                
                // Check if player has rogue set and stealth is ready
                if (modPlayer.roaringRogueSet && modPlayer.isStealthStrike)
                {
                    // Check if this is a rogue/throwing projectile
                    if (IsRogueDamageClass(projectile))
                    {
                        // Mark this projectile as a stealth strike
                        isStealthStrikeProjectile = true;
                    }
                }
            }
        }
        
        private bool IsRogueDamageClass(Projectile proj)
        {
            try
            {
                string damageClassName = proj.DamageType.GetType().Name;
                return damageClassName.Contains("Rogue") || damageClassName.Contains("Throwing") || proj.DamageType == DamageClass.Throwing;
            }
            catch
            {
                return false;
            }
        }
    }
}
