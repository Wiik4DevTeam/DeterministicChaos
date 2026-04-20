using Microsoft.Xna.Framework;
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

namespace DeterministicChaos.Content.Items.Rarities
{
    // Custom rarity for Titan boss drops.
    // Inner text: #47ECF7 (cyan), drawn with a #0500DB (dark blue) outline.
    // Any item with Item.rare = ModContent.RarityType<TitanRarity>() automatically
    // receives the outlined name via TitanRarityGlobalItem.
    public class TitanRarity : ModRarity
    {
        public static readonly Color InnerColor   = new Color(0x47, 0xEC, 0xF7);
        public static readonly Color OutlineColor = new Color(0x05, 0x00, 0xDB);

        public override Color RarityColor => InnerColor;

        public override int GetPrefixedRarity(int offset, float valueMult) => Type;
    }
}
