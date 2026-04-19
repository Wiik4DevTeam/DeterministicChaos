using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;
using DeterministicChaos.Content.SoulTraits;

namespace DeterministicChaos.Content.Items
{
    public class ToughGlove : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.damage = 22;
            Item.knockBack = 5f;
            Item.useTime = 18; // 0.3 seconds at 60fps
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = false;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(gold: 3);
            Item.UseSound = SoundID.Item1;
            Item.shoot = ModContent.ProjectileType<ToughGloveProjectile>();
            Item.shootSpeed = 10f;
            Item.DamageType = ModContent.GetInstance<RangedMeleeDamageClass>();
        }

        public override void SetStaticDefaults()
        {
            // Register +3 Bravery weapon investment
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 3, SoulTraitType.Bravery);
        }

        public override bool CanUseItem(Player player)
        {
            // Requires Bravery trait to use
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait != SoulTraitType.Bravery)
            {
                return false;
            }

            var glovePlayer = player.GetModPlayer<ToughGlovePlayer>();
            
            // Check cooldown
            if (glovePlayer.ComboCooldown > 0)
                return false;

            // Check attack cooldown
            if (glovePlayer.AttackCooldown > 0)
                return false;

            return true;
        }

        public override bool AltFunctionUse(Player player)
        {
            return true; // Enable right-click
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Don't auto-spawn projectile - ProcessInput handles all projectile spawning
            return false;
        }

        public override bool? UseItem(Player player)
        {
            var glovePlayer = player.GetModPlayer<ToughGlovePlayer>();
            bool isRightClick = player.altFunctionUse == 2;

            // Process the input and get the attack type
            glovePlayer.ProcessInput(isRightClick);

            return true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            // Bravery color: Orange (255, 165, 0)
            Color braveryColor = new Color(255, 165, 0);
            
            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = braveryColor;
                }
            }

            var player = Main.LocalPlayer;
            var glovePlayer = player.GetModPlayer<ToughGlovePlayer>();
            
            // Add combo info
            if (glovePlayer.CurrentCombo.Length > 0)
            {
                string comboDisplay = glovePlayer.CurrentCombo.Replace("L", "[c/FFA500:L]").Replace("R", "[c/FF6600:R]");
                var comboLine = new TooltipLine(Mod, "CurrentCombo", $"Current Combo: {comboDisplay}");
                tooltips.Add(comboLine);
            }
        }
    }
}
