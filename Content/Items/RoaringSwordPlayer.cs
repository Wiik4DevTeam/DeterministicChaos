using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class RoaringSwordPlayer : ModPlayer
    {
        public int swingCombo = 0;
        public int lungeCooldown = 0;
        
        private const int LungeCooldownTime = 45;
        
        public override void ResetEffects()
        {
            if (Player.itemAnimation <= 0)
            {
                swingCombo = 0;
            }
        }
        
        public override void PostUpdate()
        {
            if (lungeCooldown > 0)
            {
                lungeCooldown--;
            }
        }
        
        public void StartLungeCooldown()
        {
            // Only set cooldown for the local player
            if (Player.whoAmI == Main.myPlayer)
            {
                lungeCooldown = LungeCooldownTime;
            }
        }
    }
}
