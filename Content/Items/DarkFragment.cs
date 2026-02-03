using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class DarkFragment : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 8;
            Item.height = 8;
            Item.maxStack = 999;
            Item.value = Item.sellPrice(silver: 50);
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.material = true;
            Item.scale = 0.25f;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Item[Item.type].Value;
            Vector2 position = Item.position - Main.screenPosition + new Vector2(Item.width / 2, Item.height - texture.Height / 2);
            Vector2 origin = texture.Size() * 0.5f;

            float smallScale = scale * 0.25f;
            spriteBatch.Draw(texture, position, null, Color.White, rotation, origin, smallScale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
