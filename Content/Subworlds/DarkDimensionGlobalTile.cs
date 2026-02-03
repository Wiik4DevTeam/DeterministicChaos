using Terraria;
using Terraria.ModLoader;
using SubworldLibrary;

namespace DeterministicChaos.Content.Subworlds
{
    /// <summary>
    /// GlobalTile to prevent tile breaking in Dark World.
    /// </summary>
    public class DarkDimensionGlobalTile : GlobalTile
    {
        public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged)
        {
            // Prevent breaking any tiles in Dark World
            if (SubworldSystem.IsActive<DarkDimension>())
                return false;
            
            return base.CanKillTile(i, j, type, ref blockDamaged);
        }
        
        public override bool CanReplace(int i, int j, int type, int tileTypeBeingPlaced)
        {
            // Prevent replacing tiles in Dark World
            if (SubworldSystem.IsActive<DarkDimension>())
                return false;
            
            return base.CanReplace(i, j, type, tileTypeBeingPlaced);
        }
    }
}
