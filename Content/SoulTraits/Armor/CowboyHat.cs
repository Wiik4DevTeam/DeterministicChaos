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
        public int critAttackSpeedTimer;
        private const int CritAttackSpeedDuration = 12; // 0.2 seconds

        public override void ResetEffects()
        {
            hasCowboyHat = false;
        }

        public override void PostUpdate()
        {
            if (critAttackSpeedTimer > 0)
            {
                critAttackSpeedTimer--;
            }
        }

        public override void ModifyWeaponDamage(Item item, ref StatModifier damage)
        {
            // Attack speed boost is applied in PostUpdateEquips
        }

        public override void PostUpdateEquips()
        {
            if (hasCowboyHat && critAttackSpeedTimer > 0 && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Justice)
            {
                // 50% increased global attack speed after crit
                Player.GetAttackSpeed(DamageClass.Generic) += 0.50f;
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
