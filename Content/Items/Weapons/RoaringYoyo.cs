using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Imbued;
using DeterministicChaos.Content.Projectiles.Friendly;
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

namespace DeterministicChaos.Content.Items.Weapons
{
    public class RoaringYoyo : ModItem
    {
        public override void SetStaticDefaults()
        {
            ItemID.Sets.Yoyo[Item.type] = true;
            ItemID.Sets.GamepadExtraRange[Item.type] = 15;
            ItemID.Sets.GamepadSmartQuickReach[Item.type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 26;
            
            Item.DamageType = DamageClass.MeleeNoSpeed;
            Item.damage = 70;
            Item.knockBack = 2.5f;
            Item.crit = 4;
            
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.autoReuse = true;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = SoundID.Item1;
            Item.channel = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;

            Item.shoot = ModContent.ProjectileType<RoaringYoyoProjectile>();
            Item.shootSpeed = 16f;

            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.value = Item.buyPrice(gold: 5);
        }

        public override void HoldItem(Player player)
        {
            // Emit bright light when held
            Lighting.AddLight(player.Center, 0.9f, 0.9f, 0.9f);

            // Base ability: right-click to pull all owned mini-stars to the yoyo (no mana, 2-second cooldown).
            // Skipped for the Perseverance Static variant, which has its own no-cooldown / mana-cost version.
            var yp = player.GetModPlayer<RoaringYoyoPlayer>();
            bool isPerseveranceStatic = yp.isHoldingStatic && yp.imbuedStaticVariant == ImbuedStaticVariant.Perseverance;
            if (!isPerseveranceStatic
                && player.whoAmI == Main.myPlayer
                && Main.mouseRight && Main.mouseRightRelease
                && yp.perseverancePullCooldown <= 0)
            {
                yp.perseverancePullCooldown = 120; // 2 seconds
                yp.PerseverancePullStars();
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.7f, Pitch = 0.4f }, player.Center);
            }
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            // Draw item at full brightness, unaffected by lighting
            Texture2D texture = Terraria.GameContent.TextureAssets.Item[Item.type].Value;
            Vector2 position = Item.position - Main.screenPosition + new Vector2(Item.width / 2, Item.height - texture.Height / 2);
            Vector2 origin = texture.Size() * 0.5f;

            spriteBatch.Draw(texture, position, null, Color.White, rotation, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            foreach (TooltipLine line in tooltips)
            {
                if (line.Name == "ItemName")
                {
                    line.OverrideColor = Color.Black;
                }
            }

            tooltips.Add(new TooltipLine(Mod, "ImbueLine", "Can be imbued at a Titan Forge.") { OverrideColor = new Color(180, 140, 255) });
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset)
        {
            if (line.Name != "ItemName")
                return true;

            Vector2 position = new Vector2(line.X, line.Y);

            // Draw white shadow outline
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    if (x == 0 && y == 0) continue;

                    Terraria.UI.Chat.ChatManager.DrawColorCodedString(
                        Main.spriteBatch,
                        line.Font,
                        line.Text,
                        position + new Vector2(x, y),
                        Color.White,
                        line.Rotation,
                        line.Origin,
                        line.BaseScale
                    );
                }
            }

            // Draw black text on top
            Terraria.UI.Chat.ChatManager.DrawColorCodedString(
                Main.spriteBatch,
                line.Font,
                line.Text,
                position,
                Color.Black,
                line.Rotation,
                line.Origin,
                line.BaseScale
            );

            return false;
        }

    }
}
