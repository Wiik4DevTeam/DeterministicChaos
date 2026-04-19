using CalamityMod.Tiles.BaseTiles;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.Bosses.Trophies
{
    public class JevilRelicTile : BaseBossRelic
    {
        public override string RelicTextureName => "DeterministicChaos/Content/NPCs/Bosses/Trophies/JevilRelic";

        public override int AssociatedItem => ModContent.ItemType<JevilRelic>();
    }
}
