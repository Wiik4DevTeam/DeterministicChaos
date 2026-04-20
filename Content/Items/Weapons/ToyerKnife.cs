using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;
using DeterministicChaos.Content.SoulTraits;
using DeterministicChaos.Content.Buffs;
using DeterministicChaos.Content.Items.Prefixes;
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
    public class ToyerKnife : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.damage = 14;
            Item.knockBack = 2.5f;
            Item.useTime = 15;
            Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(gold: 10);
            Item.UseSound = SoundID.Item1;
            Item.shoot = ModContent.ProjectileType<ToyerKnifeProjectile>();
            Item.shootSpeed = 14f;
            Item.DamageType = ModContent.GetInstance<MagicRogueDamageClass>();
        }

        public override void SetStaticDefaults()
        {
            // Register +6 Patience weapon investment (hardmode upgrade)
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 6, SoulTraitType.Patience);
        }

        public override bool CanUseItem(Player player)
        {
            // Requires Patience trait to use
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait != SoulTraitType.Patience)
            {
                return false;
            }

            return true;
        }

        public override void HoldItem(Player player)
        {
            // Requires Patience trait
            if (player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Patience)
                return;

            // While holding, add 1 second (60 ticks) of Virtue buff per frame, capped at 10 seconds (600 ticks)
            var toyerPlayer = player.GetModPlayer<ToyerKnifePlayer>();
            toyerPlayer.isHoldingToyerKnife = true;

            int buffType = ModContent.BuffType<VirtueBuff>();
            int currentTime = 0;

            // Check if already have the buff
            int buffIndex = player.FindBuffIndex(buffType);
            if (buffIndex >= 0)
                currentTime = player.buffTime[buffIndex];

            // Add 2 ticks per frame: 1 counteracts natural drain, 1 for actual gain
            // Net: +1 second per second held, -1 second per second when swapped away
            int maxDuration = player.GetModPlayer<PrefixEffectPlayer>().ScaleBuffDuration(600, buffType);
            int newTime = System.Math.Min(currentTime + 2, maxDuration);
            player.AddBuff(buffType, newTime);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Preserve rogue stealth
            var toyerPlayer = player.GetModPlayer<ToyerKnifePlayer>();
            toyerPlayer.PreserveStealthOnShoot();

            // Spawn the knife projectile
            Projectile.NewProjectile(
                source,
                position,
                velocity,
                type,
                damage,
                knockback,
                player.whoAmI
            );

            // Restore rogue stealth after shooting
            toyerPlayer.RestoreStealthAfterShoot();

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            // Patience color: Cyan (0, 255, 255)
            Color patienceColor = new Color(0, 255, 255);

            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = patienceColor;
                }
            }

            // Show current Virtue buff duration
            Player player = Main.LocalPlayer;
            if (player != null && player.active)
            {
                int buffType = ModContent.BuffType<VirtueBuff>();
                int buffIndex = player.FindBuffIndex(buffType);
                if (buffIndex >= 0)
                {
                    int seconds = player.buffTime[buffIndex] / 60;
                    tooltips.Add(new TooltipLine(Mod, "VirtueInfo", $"[c/00FFFF:Virtue: {seconds}s / 10s]"));
                }
            }
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<ToyKnife>(), 1)
                .AddIngredient(ModContent.ItemType<SoulCatalyst>(), 1)
                .AddIngredient(ItemID.CobaltBar, 10)
                .AddTile(TileID.Anvils)
                .Register();

            CreateRecipe()
                .AddIngredient(ModContent.ItemType<ToyKnife>(), 1)
                .AddIngredient(ModContent.ItemType<SoulCatalyst>(), 1)
                .AddIngredient(ItemID.PalladiumBar, 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
