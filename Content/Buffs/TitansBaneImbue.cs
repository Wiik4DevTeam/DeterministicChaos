using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;

namespace DeterministicChaos.Content.Buffs
{
    // Player-side imbue buff applied when you drink a Flask of Titansbane.
    // While active, melee weapons and projectiles inflict Titansbane on enemies.
    // Actual application is handled in TitansBaneImbueGlobal.
    public class TitansBaneImbue : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = false; // Persists through death like vanilla flask buffs
            Main.debuff[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.GetModPlayer<TitansBaneImbuePlayer>().imbueActive = true;
        }
    }
}
