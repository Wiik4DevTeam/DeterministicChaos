using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;
using DeterministicChaos.Content.SoulTraits;

namespace DeterministicChaos.Content.Items
{
    public class CookingPot : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 40;
            Item.height = 40;
            Item.damage = 48;
            Item.knockBack = 4f;
            Item.useTime = 28;
            Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(gold: 10);
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<CookingPotSwing>();
            Item.shootSpeed = 1f; // Direction only, no real projectile
            Item.DamageType = ModContent.GetInstance<RangedMeleeDamageClass>();
            // No ammo
        }

        public override void SetStaticDefaults()
        {
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 6, SoulTraitType.Kindness);
        }

        public override bool MeleePrefix()
        {
            return true;
        }

        public override bool CanUseItem(Player player)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            return traitPlayer.CurrentTrait == SoulTraitType.Kindness;
        }

        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        public override bool CanRightClick()
        {
            // Allow right-click in inventory to add ingredients
            return true;
        }

        public override void RightClick(Player player)
        {
            var potPlayer = player.GetModPlayer<CookingPotPlayer>();

            // Try to add the item on the cursor
            Item cursorItem = Main.mouseItem;
            if (cursorItem != null && !cursorItem.IsAir)
            {
                if (potPlayer.TryAddIngredient(cursorItem))
                {
                    SoundEngine.PlaySound(SoundID.Grab, player.Center);

                    // Green sparkle feedback
                    for (int i = 0; i < 8; i++)
                    {
                        Vector2 vel = Main.rand.NextVector2Circular(3f, 3f);
                        Dust dust = Dust.NewDustDirect(player.Center, 0, 0, DustID.GreenTorch, vel.X, vel.Y);
                        dust.noGravity = true;
                        dust.scale = 1.2f;
                    }
                }
            }

            // Prevent consuming the CookingPot itself on right-click
            Item.stack++;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Right-click (alt use) is for adding ingredients, don't shoot
            if (player.altFunctionUse == 2)
                return false;

            if (player.whoAmI != Main.myPlayer)
                return false;

            Vector2 toMouse = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            float aimAngle = toMouse.ToRotation();

            // Spawn the swing projectile (deals melee damage + knockback)
            Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                ModContent.ProjectileType<CookingPotSwing>(), damage, knockback,
                player.whoAmI, 1f, aimAngle);

            // Spawn the liquid cone (deals ranged damage + effects)
            Vector2 direction = toMouse;
            float coneHalfAngle = MathHelper.ToRadians(30f);
            float coneRange = 550f; // ~34 tiles

            Projectile.NewProjectile(source, player.Center, direction,
                ModContent.ProjectileType<CookingPotCone>(), damage, knockback,
                player.whoAmI, coneHalfAngle, coneRange);

            SoundEngine.PlaySound(SoundID.Item34 with { Pitch = 0.2f, Volume = 0.7f }, player.Center);

            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            foreach (TooltipLine line in tooltips)
            {
                if (line.Name == "ItemName" && line.Mod == "Terraria")
                {
                    line.OverrideColor = new Color(50, 205, 50);
                }
            }

            var player = Main.LocalPlayer;
            var potPlayer = player.GetModPlayer<CookingPotPlayer>();

            // Show current ingredients
            string ingredients = "";

            if (potPlayer.FlaskItemType > 0)
            {
                Item flask = new Item();
                flask.SetDefaults(potPlayer.FlaskItemType);
                ingredients += $"\n[c/32CD32:Flask: {flask.Name}]";
            }
            else
            {
                ingredients += "\n[c/808080:Flask: Empty]";
            }

            if (potPlayer.FoodItemType > 0)
            {
                Item food = new Item();
                food.SetDefaults(potPlayer.FoodItemType);
                int healAmt = potPlayer.GetFoodHealAmount();
                ingredients += $"\n[c/32CD32:Food: {food.Name} (Heals allies for {healAmt} HP)]";
            }
            else
            {
                ingredients += "\n[c/808080:Food: Empty]";
            }

            if (potPlayer.AlcoholItemType > 0)
            {
                Item alcohol = new Item();
                alcohol.SetDefaults(potPlayer.AlcoholItemType);
                ingredients += $"\n[c/32CD32:Alcohol: {alcohol.Name}]";
            }
            else
            {
                if (ModLoader.HasMod("CalamityMod"))
                    ingredients += "\n[c/808080:Alcohol: Empty]";
                else
                    ingredients += "\n[c/555555:Alcohol: Requires Calamity Mod]";
            }

            var ingredientLine = new TooltipLine(Mod, "Ingredients", ingredients);
            tooltips.Add(ingredientLine);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<FryingPan>(), 1)
                .AddIngredient(ModContent.ItemType<SoulCatalyst>(), 1)
                .AddIngredient(ItemID.CobaltBar, 10)
                .AddTile(TileID.Anvils)
                .Register();

            CreateRecipe()
                .AddIngredient(ModContent.ItemType<FryingPan>(), 1)
                .AddIngredient(ModContent.ItemType<SoulCatalyst>(), 1)
                .AddIngredient(ItemID.PalladiumBar, 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
