using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
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
    public class SoulTraitRarityGlobalItem : GlobalItem
    {
        private static bool TryGetTraitColors(int rare, out Color inner, out Color outline)
        {
            if (rare == ModContent.RarityType<IntegrityRarity>())
            {
                inner = IntegrityRarity.InnerColor;
                outline = IntegrityRarity.OutlineColor;
                return true;
            }
            if (rare == ModContent.RarityType<PerseveranceRarity>())
            {
                inner = PerseveranceRarity.InnerColor;
                outline = PerseveranceRarity.OutlineColor;
                return true;
            }
            if (rare == ModContent.RarityType<BraveryRarity>())
            {
                inner = BraveryRarity.InnerColor;
                outline = BraveryRarity.OutlineColor;
                return true;
            }

            inner = default;
            outline = default;
            return false;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            if (!TryGetTraitColors(item.rare, out Color inner, out _))
                return;

            foreach (TooltipLine line in tooltips)
            {
                if (line.Name == "ItemName")
                    line.OverrideColor = inner;
            }
        }

        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset)
        {
            if (!TryGetTraitColors(item.rare, out Color inner, out Color outline))
                return true;

            if (line.Name != "ItemName")
                return true;

            Vector2 position = new Vector2(line.X, line.Y);

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
                        outline,
                        line.Rotation,
                        line.Origin,
                        line.BaseScale);
                }
            }

            Terraria.UI.Chat.ChatManager.DrawColorCodedString(
                Main.spriteBatch,
                line.Font,
                line.Text,
                position,
                inner,
                line.Rotation,
                line.Origin,
                line.BaseScale);

            return false;
        }
    }
}
