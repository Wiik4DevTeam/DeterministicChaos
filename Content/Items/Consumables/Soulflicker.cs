using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.SoulTraits;
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

namespace DeterministicChaos.Content.Items.Consumables
{
    public class Soulflicker : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Orange;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = true;
            Item.UseSound = SoundID.Item29;
        }

        public override bool CanUseItem(Player player)
        {
            var stp = player.GetModPlayer<SoulTraitPlayer>();
            return stp.CurrentTrait != SoulTraitType.None;
        }

        public override bool? UseItem(Player player)
        {
            var stp = player.GetModPlayer<SoulTraitPlayer>();
            stp.BonusInvestment += 3;
            return true;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            Texture2D tex = TextureAssets.Item[Type].Value;
            Player player = Main.LocalPlayer;
            var stp = player.GetModPlayer<SoulTraitPlayer>();
            Color tint = SoulTraitData.GetTraitColor(stp.CurrentTrait);

            spriteBatch.Draw(tex, position, frame, tint, 0f, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            Texture2D tex = TextureAssets.Item[Type].Value;
            Vector2 pos = Item.Center - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;

            Player player = Main.LocalPlayer;
            var stp = player.GetModPlayer<SoulTraitPlayer>();
            Color tint = SoulTraitData.GetTraitColor(stp.CurrentTrait);
            Color final = Item.GetAlpha(tint);

            spriteBatch.Draw(tex, pos, null, final, rotation, origin, scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
