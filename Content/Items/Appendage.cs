using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;
using DeterministicChaos.Content.Buffs;

namespace DeterministicChaos.Content.Items
{
    public class Appendage : ModItem
    {
        public const int BaseDamage = 110;
        public const float BaseKnockback = 3f;

        public override void SetStaticDefaults()
        {
            ItemID.Sets.GamepadWholeScreenUseRange[Item.type] = true;
            ItemID.Sets.LockOnIgnoresCollision[Item.type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 30;
            Item.damage = BaseDamage;
            Item.knockBack = BaseKnockback;
            Item.mana = 20;
            Item.useTime = 36;
            Item.useAnimation = 36;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.DamageType = DamageClass.Summon;
            Item.buffType = ModContent.BuffType<AppendageBuff>();
            Item.shoot = ModContent.ProjectileType<AppendageHandProjectile>();

            Item.rare = ModContent.RarityType<TitanRarity>();
            Item.value = Item.buyPrice(gold: 8);
            Item.UseSound = SoundID.Item44;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            player.AddBuff(Item.buffType, 2);

            // Only one pair allowed, kill any existing appendage minions
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI && proj.type == type)
                    proj.Kill();
            }

            int idx = Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                type, damage, knockback, player.whoAmI);

            if (idx >= 0 && idx < Main.maxProjectiles)
                Main.projectile[idx].originalDamage = Item.damage;

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            foreach (TooltipLine line in tooltips)
            {
                if (line.Name == "ItemName")
                    line.OverrideColor = Color.Black;
            }
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset)
        {
            if (line.Name != "ItemName")
                return true;

            Vector2 position = new Vector2(line.X, line.Y);

            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    if (x == 0 && y == 0) continue;
                    Terraria.UI.Chat.ChatManager.DrawColorCodedString(
                        Main.spriteBatch, line.Font, line.Text,
                        position + new Vector2(x, y), Color.White,
                        line.Rotation, line.Origin, line.BaseScale);
                }
            }

            Terraria.UI.Chat.ChatManager.DrawColorCodedString(
                Main.spriteBatch, line.Font, line.Text,
                position, Color.Black,
                line.Rotation, line.Origin, line.BaseScale);

            return false;
        }
    }
}
