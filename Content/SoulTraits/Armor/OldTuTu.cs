using CalamityMod;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits.Armor
{
    public class OldTuTu : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 24;
            Item.value = Item.buyPrice(gold: 3);
            Item.rare = ItemRarityID.Blue;
            Item.accessory = true;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            // Integrity color: Blue (30, 144, 255)
            Color integrityColor = new Color(30, 144, 255);
            
            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = integrityColor;
                }
            }
            
            // Show current dash status if equipped
            Player player = Main.LocalPlayer;
            if (player != null && player.active)
            {
                var tutuPlayer = player.GetModPlayer<OldTuTuPlayer>();
                var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
                
                // Debug: Show if accessory is active
                bool isIntegrity = traitPlayer.CurrentTrait == SoulTraitType.Integrity;
                
                if (tutuPlayer.hasOldTuTu && isIntegrity)
                {
                    if (tutuPlayer.dashTimer > 0)
                    {
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/1E90FF:DASHING!]"));
                    }
                    else if (tutuPlayer.dashCooldown > 0)
                    {
                        float cooldownSeconds = tutuPlayer.dashCooldown / 60f;
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/4169E1:Dash on cooldown ({cooldownSeconds:F1}s)]"));
                    }
                    else
                    {
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/1E90FF:Dash ready! (Calamity Dash key)]"));
                    }
                }
                else if (!isIntegrity)
                {
                    tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/FF6666:Requires Integrity trait (Current: {traitPlayer.CurrentTrait})]"));
                }
            }
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            
            // Only grant bonuses if player has Integrity trait
            if (traitPlayer.CurrentTrait == SoulTraitType.Integrity)
            {
                // Grant 3 investment points to Integrity trait
                traitPlayer.ArmorInvestment += 3;
                
                // Enable dash
                player.GetModPlayer<OldTuTuPlayer>().hasOldTuTu = true;
            }
        }
    }

    public class OldTuTuPlayer : ModPlayer
    {
        public bool hasOldTuTu;
        public int dashCooldown;
        public int dashTimer;
        public Vector2 dashDirection; // Omnidirectional
        
        // Pre-hardmode balanced values
        private const int DashCooldownDuration = 60;
        private const int DashDuration = 12; // Short dash duration
        private const float DashSpeed = 12f;
        
        // Track previous frame's keybind state
        private bool dashKeybindWasPressed = false;

        public override void ResetEffects()
        {
            hasOldTuTu = false;
        }

        public override void PreUpdateMovement()
        {
            // Handle active dash movement - just provide immunity and visual effects
            if (dashTimer > 0)
            {
                // Brief immunity during dash
                Player.immune = true;
                Player.immuneTime = 2;
                
                // Spawn dust during dash
                if (Main.rand.NextBool(2))
                {
                    Dust dust = Dust.NewDustDirect(
                        Player.position + Vector2.UnitY * 4f, 
                        Player.width, 
                        Player.height - 8, 
                        DustID.BlueTorch, 
                        0f, 0f, 0, default, 1f
                    );
                    dust.velocity = -Player.velocity * Main.rand.NextFloat(0.1f, 0.5f);
                    dust.noGravity = true;
                    dust.scale *= Main.rand.NextFloat(0.8f, 1.1f);
                }
                
                dashTimer--;
            }
        }

        public override void PostUpdate()
        {
            if (dashCooldown > 0)
                dashCooldown--;
            
            // Check for dash input here (after movement processing)
            CheckDashInput();
        }
        
        private void CheckDashInput()
        {
            // Only check for dash input if we have the accessory, not currently dashing, and off cooldown
            if (!hasOldTuTu || dashCooldown > 0 || dashTimer > 0)
                return;
            
            if (Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Integrity)
                return;
            
            // Use Calamity's dash keybind directly
            bool keybindCurrentlyPressed = CalamityKeybinds.DashHotkey.Current;
            bool keybindJustPressed = keybindCurrentlyPressed && !dashKeybindWasPressed;
            dashKeybindWasPressed = keybindCurrentlyPressed;
            
            if (keybindJustPressed)
            {
                // Reverse current velocity at half speed
                Vector2 reversedVelocity = -Player.velocity * 1.0f;
                
                // Only dash if we have some velocity to reverse
                if (reversedVelocity.Length() > 0.5f)
                {
                    dashDirection = reversedVelocity.SafeNormalize(Vector2.UnitX);
                    // Use the reversed velocity magnitude as the dash speed base
                    dashTimer = DashDuration;
                    dashCooldown = DashCooldownDuration;
                    
                    // Immediately apply the reversed velocity
                    Player.velocity = reversedVelocity;
                    
                    // Initial burst of dust
                    for (int i = 0; i < 8; i++)
                    {
                        Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.BlueTorch);
                        dust.velocity = -dashDirection * Main.rand.NextFloat(2f, 4f) + new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f));
                        dust.noGravity = true;
                        dust.scale = Main.rand.NextFloat(1f, 1.3f);
                    }
                    
                    // Play dash sound
                    SoundEngine.PlaySound(SoundID.Item24 with { Volume = 0.5f, Pitch = 0.2f }, Player.Center);
                }
            }
        }
    }
}
