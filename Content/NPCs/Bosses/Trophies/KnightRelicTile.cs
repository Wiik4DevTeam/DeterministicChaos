using CalamityMod.Tiles.BaseTiles;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.Bosses.Trophies
{
    public class KnightRelicTile : BaseBossRelic
    {
        public override string RelicTextureName => "DeterministicChaos/Content/NPCs/Bosses/Trophies/KnightRelic";

        public override int AssociatedItem => ModContent.ItemType<KnightRelic>();
    }
}
