using Microsoft.Xna.Framework;
using Terraria;
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
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            
            // Only grant bonuses if player has Integrity trait
            if (traitPlayer.CurrentTrait == SoulTraitType.Integrity)
            {
                // Grant 3 investment points to Integrity trait
                traitPlayer.ArmorInvestment += 3;
                
                // Enable dash away from enemies
                player.GetModPlayer<OldTuTuPlayer>().hasOldTuTu = true;
            }
        }
    }

    public class OldTuTuPlayer : ModPlayer
    {
        public bool hasOldTuTu;
        public int dashCooldown;
        public int dashTimer;
        public Vector2 dashDirection;
        
        private const int DashCooldownDuration = 60; // 1 second cooldown
        private const int DashDuration = 12; // 0.2 seconds of dash
        private const float DashSpeed = 18f;

        public override void ResetEffects()
        {
            hasOldTuTu = false;
        }

        public override void PostUpdate()
        {
            if (dashCooldown > 0)
                dashCooldown--;

            if (dashTimer > 0)
            {
                dashTimer--;
                Player.velocity = dashDirection * DashSpeed;
                Player.immune = true;
                Player.immuneTime = 2;
            }
        }

        public override void PreUpdate()
        {
            // Dash when double-tapping a direction key while not dashing and off cooldown
            if (hasOldTuTu && dashCooldown <= 0 && dashTimer <= 0 && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Integrity)
            {
                // Check for double-tap dash using player control states
                bool dashInput = Player.controlDown && Player.releaseDown && Player.doubleTapCardinalTimer[0] > 0;
                
                if (dashInput)
                {
                    // Find closest enemy to dash away from
                    NPC closestEnemy = null;
                    float closestDist = 400f; // Max range to detect enemies

                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        NPC npc = Main.npc[i];
                        if (npc.active && !npc.friendly && npc.CanBeChasedBy())
                        {
                            float dist = Vector2.Distance(Player.Center, npc.Center);
                            if (dist < closestDist)
                            {
                                closestDist = dist;
                                closestEnemy = npc;
                            }
                        }
                    }

                    if (closestEnemy != null)
                    {
                        // Dash away from the closest enemy
                        dashDirection = (Player.Center - closestEnemy.Center).SafeNormalize(Vector2.UnitX * Player.direction);
                    }
                    else
                    {
                        // No enemy nearby, dash in the direction player is facing
                        dashDirection = new Vector2(-Player.direction, 0);
                    }

                    dashTimer = DashDuration;
                    dashCooldown = DashCooldownDuration;
                    
                    // Visual effect
                    for (int i = 0; i < 10; i++)
                    {
                        Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.BlueTorch);
                        dust.velocity = -dashDirection * Main.rand.NextFloat(2f, 5f);
                        dust.noGravity = true;
                        dust.scale = 1.2f;
                    }
                }
            }
        }
    }
}
