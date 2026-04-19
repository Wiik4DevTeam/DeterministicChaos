using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits.Armor
{
    public class CowboyHat : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 22;
            Item.value = Item.buyPrice(gold: 3);
            Item.rare = ItemRarityID.Yellow;
            Item.accessory = true;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            // Justice color: Yellow (255, 255, 0)
            Color justiceColor = new Color(255, 255, 0);
            
            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = justiceColor;
                }
            }
            
            // Show current attack speed buff status if equipped
            Player player = Main.LocalPlayer;
            if (player != null && player.active)
            {
                var hatPlayer = player.GetModPlayer<CowboyHatPlayer>();
                
                if (hatPlayer.hasCowboyHat)
                {
                    if (hatPlayer.critAttackSpeedTimer > 0)
                    {
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/FFFF00:Attack speed buff ACTIVE! (+10%)]"));
                    }
                    else
                    {
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/808000:Attack speed buff ready (land a crit)]"));
                    }
                }
            }
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            
            // Only grant bonuses if player has Justice trait
            if (traitPlayer.CurrentTrait == SoulTraitType.Justice)
            {
                // Grant 3 investment points to Justice trait
                traitPlayer.ArmorInvestment += 3;
                
                // Critical hit attack speed bonus handled in player class
                player.GetModPlayer<CowboyHatPlayer>().hasCowboyHat = true;
            }
        }
    }

    public class CowboyHatPlayer : ModPlayer
    {
        public bool hasCowboyHat;
        public bool hasSheriffHat;
        public int critAttackSpeedTimer;
        public int hypercritAttackSpeedTimer;
        private const float CritAttackSpeedBonus = 0.10f;
        private const float HypercritAttackSpeedBonus = 0.20f;
        private const int CritAttackSpeedDuration = 12; // 0.2 seconds
        private const int HypercritAttackSpeedDuration = 36; // 0.6 seconds

        public override void ResetEffects()
        {
            hasCowboyHat = false;
            hasSheriffHat = false;
        }

        public override void PostUpdate()
        {
            if (critAttackSpeedTimer > 0)
            {
                critAttackSpeedTimer--;
            }
            if (hypercritAttackSpeedTimer > 0)
            {
                hypercritAttackSpeedTimer--;
            }
        }

        public override void ModifyWeaponDamage(Item item, ref StatModifier damage)
        {
            // Attack speed boost is applied in PostUpdateEquips
        }

        public override void PostUpdateEquips()
        {
            if (hasCowboyHat && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Justice)
            {
                if (hasSheriffHat && hypercritAttackSpeedTimer > 0)
                {
                    // Hypercrit attack speed bonus (overrides normal crit bonus)
                    Player.GetAttackSpeed(DamageClass.Generic) += HypercritAttackSpeedBonus;
                }
                else if (critAttackSpeedTimer > 0)
                {
                    // Crit attack speed bonus
                    Player.GetAttackSpeed(DamageClass.Generic) += CritAttackSpeedBonus;
                }
            }
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (hasCowboyHat && hit.Crit && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Justice)
            {
                critAttackSpeedTimer = CritAttackSpeedDuration;
            }
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (hasCowboyHat && hit.Crit && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Justice)
            {
                critAttackSpeedTimer = CritAttackSpeedDuration;
            }
        }
    }
}
