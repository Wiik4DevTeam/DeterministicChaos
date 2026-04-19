using CalamityMod.Tiles.BaseTiles;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.Bosses.Trophies
{
    public class TitanRelicTile : BaseBossRelic
    {
        public override string RelicTextureName => "DeterministicChaos/Content/NPCs/Bosses/Trophies/TitanRelic";

        public override int AssociatedItem => ModContent.ItemType<TitanRelic>();
    }
}
