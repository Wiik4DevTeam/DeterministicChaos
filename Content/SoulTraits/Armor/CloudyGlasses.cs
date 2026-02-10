using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits.Armor
{
    public class CloudyGlasses : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 20;
            Item.value = Item.buyPrice(gold: 3);
            Item.rare = ItemRarityID.LightPurple;
            Item.accessory = true;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            // Perseverance color: Magenta (255, 0, 255)
            Color perseveranceColor = new Color(255, 0, 255);
            
            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = perseveranceColor;
                }
            }
            
            // Show immunity status if equipped
            Player player = Main.LocalPlayer;
            if (player != null && player.active)
            {
                var glassesPlayer = player.GetModPlayer<CloudyGlassesPlayer>();
                
                if (glassesPlayer.hasCloudyGlasses)
                {
                    if (player.immune)
                    {
                        float immuneSeconds = player.immuneTime / 60f;
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/FF00FF:Currently immune! ({immuneSeconds:F1}s remaining)]"));
                    }
                    else
                    {
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/800080:+0.3s immunity extension ready]"));
                    }
                }
            }
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            
            // Only grant bonuses if player has Perseverance trait
            if (traitPlayer.CurrentTrait == SoulTraitType.Perseverance)
            {
                // Grant 3 investment points to Perseverance trait
                traitPlayer.ArmorInvestment += 3;
                
                // Increases invulnerability frames by 0.3 seconds (18 ticks)
                player.GetModPlayer<CloudyGlassesPlayer>().hasCloudyGlasses = true;
            }
        }
    }

    public class CloudyGlassesPlayer : ModPlayer
    {
        public bool hasCloudyGlasses;

        public override void ResetEffects()
        {
            hasCloudyGlasses = false;
        }

        public override void PostHurt(Player.HurtInfo info)
        {
            if (hasCloudyGlasses && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Perseverance)
            {
                // Add 0.3 seconds (18 ticks) of invulnerability
                // Must set both immuneTime AND immune flag for each immunity slot
                Player.immuneTime += 18;
                
                // Also extend the immune no-blink timer so the player stays immune
                if (Player.immuneNoBlink)
                {
                    Player.immuneTime += 18;
                }
                
                // Set immunity for all damage sources
                for (int i = 0; i < Player.hurtCooldowns.Length; i++)
                {
                    if (Player.hurtCooldowns[i] > 0)
                    {
                        Player.hurtCooldowns[i] += 18;
                    }
                }
            }
        }
        
        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            // Show visual effect when the bonus immunity is active
            if (hasCloudyGlasses && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Perseverance)
            {
                // Purple dust effect when hit to indicate bonus immunity
                for (int i = 0; i < 8; i++)
                {
                    Vector2 dustVel = Main.rand.NextVector2Circular(3f, 3f);
                    Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.PurpleTorch, dustVel.X, dustVel.Y, 100, default, 1.2f);
                    dust.noGravity = true;
                }
            }
        }
    }
}
