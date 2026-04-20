using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
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
    // Handles the outlined item name for all items that use TitanRarity.
    // Adding Item.rare = ModContent.RarityType<TitanRarity>() to any item
    // is sufficient to get the cyan inner / dark-blue outline name rendering.
    public class TitanRarityGlobalItem : GlobalItem
    {
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            if (item.rare != ModContent.RarityType<TitanRarity>())
                return;

            foreach (TooltipLine line in tooltips)
            {
                if (line.Name == "ItemName")
                {
                    // Override so that vanilla tooltip hover colour matches the theme
                    line.OverrideColor = TitanRarity.InnerColor;
                }
            }
        }

        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset)
        {
            if (item.rare != ModContent.RarityType<TitanRarity>())
                return true;

            if (line.Name != "ItemName")
                return true;

            Vector2 position = new Vector2(line.X, line.Y);

            // Draw outline (±2 px in every direction)
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    Terraria.UI.Chat.ChatManager.DrawColorCodedString(
                        Main.spriteBatch,
                        line.Font,
                        line.Text,
                        position + new Vector2(x, y),
                        TitanRarity.OutlineColor,
                        line.Rotation,
                        line.Origin,
                        line.BaseScale
                    );
                }
            }

            // Draw inner text
            Terraria.UI.Chat.ChatManager.DrawColorCodedString(
                Main.spriteBatch,
                line.Font,
                line.Text,
                position,
                TitanRarity.InnerColor,
                line.Rotation,
                line.Origin,
                line.BaseScale
            );

            return false;
        }
    }
}
