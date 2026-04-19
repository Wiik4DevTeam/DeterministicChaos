using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Items
{
    // Post-Titan bow. Very slow fire rate but converts all arrows into a high-velocity
    // suctioning bolt. Every shot consumes 3 arrows. The bolt sticks to whatever it hits,
    // pulls enemies toward the impact point for ~3 seconds, then explodes for massive AoE
    // damage and scatters 5 arrows of the original ammo type upward in a 90-degree cone.
    public class Cascade : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 56;
            Item.damage = 260;
            Item.knockBack = 6f;
            Item.useTime = 85;
            Item.useAnimation = 85;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.DamageType = DamageClass.Ranged;
            Item.UseSound = new Terraria.Audio.SoundStyle("DeterministicChaos/Assets/Sounds/Crossbow") { Volume = 0.56f };
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 22f;
            Item.useAmmo = AmmoID.Arrow;
            Item.rare = ModContent.RarityType<TitanRarity>();
            Item.value = Item.buyPrice(gold: 80);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Fire the Cascade bolt. Pass the original arrow's projectile type as ai2 so
            // the bolt knows what to scatter on explosion.
            Projectile.NewProjectile(
                source, position, velocity,
                ModContent.ProjectileType<CascadeProjectile>(),
                damage, knockback, player.whoAmI,
                ai2: (float)type);

            // The base system already consumes 1 ammo; manually consume 2 more.
            ConsumeExtraAmmo(player, source.AmmoItemIdUsed, 2);

            return false;
        }

        // Searches ammo slots (54-57) then main inventory for the given arrow item type
        // and reduces its stack by up to 'count'.
        private static void ConsumeExtraAmmo(Player player, int ammoItemType, int count)
        {
            int remaining = count;
            for (int i = 54; i < 58 && remaining > 0; i++)
                ConsumeFromSlot(player.inventory[i], ammoItemType, ref remaining);
            for (int i = 0; i < 54 && remaining > 0; i++)
                ConsumeFromSlot(player.inventory[i], ammoItemType, ref remaining);
        }

        private static void ConsumeFromSlot(Item inv, int ammoItemType, ref int remaining)
        {
            if (inv.IsAir || inv.type != ammoItemType || inv.stack <= 0)
                return;
            int toConsume = Math.Min(remaining, inv.stack);
            inv.stack -= toConsume;
            remaining -= toConsume;
            if (inv.stack <= 0)
                inv.TurnToAir();
        }

        public override void AddRecipes() { }
    }
}
