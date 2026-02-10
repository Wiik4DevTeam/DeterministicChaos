using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class FryingPanGlobalTile : GlobalTile
    {
        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (fail || effectOnly)
                return;

            if (!IsPlantTile(type))
                return;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int closestPlayer = Player.FindClosest(new Microsoft.Xna.Framework.Vector2(i * 16, j * 16), 16, 16);
            if (closestPlayer < 0 || closestPlayer >= Main.maxPlayers)
                return;

            Player player = Main.player[closestPlayer];
            if (player == null || !player.active)
                return;

            var fryingPanPlayer = player.GetModPlayer<FryingPanPlayer>();
            if (!fryingPanPlayer.hasFryingPan)
                return;

            if (Main.rand.NextBool(2))
            {
                int seedCount = Main.rand.Next(1, 4);
                Item.NewItem(
                    WorldGen.GetItemSource_FromTileBreak(i, j),
                    i * 16, j * 16, 16, 16,
                    ItemID.Seed,
                    seedCount
                );
            }
        }

        private bool IsPlantTile(int type)
        {
            return type == TileID.Plants ||
                   type == TileID.Plants2 ||
                   type == TileID.JunglePlants ||
                   type == TileID.JunglePlants2 ||
                   type == TileID.HallowedPlants ||
                   type == TileID.HallowedPlants2 ||
                   type == TileID.CorruptPlants ||
                   type == TileID.CrimsonPlants ||
                   type == TileID.MushroomPlants ||
                   type == TileID.Grass ||
                   type == TileID.JungleGrass ||
                   type == TileID.HallowedGrass ||
                   type == TileID.CorruptGrass ||
                   type == TileID.CrimsonGrass;
        }
    }
}
