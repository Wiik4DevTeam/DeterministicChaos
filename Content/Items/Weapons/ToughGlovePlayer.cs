using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.SoulTraits;
using DeterministicChaos.Content.Projectiles.Friendly;
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
    public class ToughGlovePlayer : ModPlayer
    {
        public string CurrentCombo = "";
        public int ComboCooldown = 0; // 60 ticks = 1 second after combo ends
        public int AttackCooldown = 0; // 18 ticks = 0.3 seconds between attacks
        public int ComboTimeout = 0; // Reset combo if no input for too long
        
        // For ground slam
        public bool IsGroundSlamming = false;
        public int GroundSlamDamage = 0;
        public float GroundSlamKnockback = 0f;
        private int groundSlamTimer = 0;

        // For flurry attack (LLL)
        public bool IsDoingFlurry = false;
        public int FlurryTimer = 0;
        public int FlurryCount = 0;
        public Vector2 FlurryDirection = Vector2.Zero;
        public int FlurryDamage = 0;
        public float FlurryKnockback = 0f;
        public float FlurrySpeed = 0f;

        // For combo display
        private int comboDisplayTimer = 0;

        private const int ATTACK_COOLDOWN = 18; // 0.3 seconds
        private const int COMBO_COOLDOWN = 60; // 1 second
        private const int COMBO_TIMEOUT = 60; // 1 second to continue combo

        public override void ResetEffects()
        {
            // Track if attack cooldown just ended while holding right-click
            bool wasOnCooldown = AttackCooldown > 0;
            
            // Count down cooldowns
            if (ComboCooldown > 0)
                ComboCooldown--;
            if (AttackCooldown > 0)
                AttackCooldown--;
            if (ComboTimeout > 0)
            {
                ComboTimeout--;
                if (ComboTimeout <= 0 && CurrentCombo.Length > 0)
                {
                    // Combo timed out without finishing
                    ResetCombo(false);
                }
            }
            
            // Auto-queue right-click when holding it, similar to left-click behavior
            if (wasOnCooldown && AttackCooldown <= 0 && ComboCooldown <= 0)
            {
                // Check if player is holding ToughGlove and right-click
                if (Player.HeldItem.ModItem is ToughGlove && Main.mouseRight && Player.whoAmI == Main.myPlayer
                    && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Bravery)
                {
                    ProcessInput(true);
                }
            }
            
            // Combo display timer
            if (comboDisplayTimer > 0)
                comboDisplayTimer--;
        }

        public override void PostUpdate()
        {
            // Handle ground slam
            if (IsGroundSlamming)
            {
                groundSlamTimer++;
                
                // Apply strong downward velocity
                Player.velocity.Y = MathHelper.Clamp(Player.velocity.Y + 4f, -10f, 35f);
                
                // Check if we hit the ground after initial launch (timer > 5 to skip initial frames)
                if (groundSlamTimer > 5)
                {
                    bool hitGround = Player.velocity.Y == 0f;
                    bool onSolid = Collision.SolidCollision(Player.BottomLeft, Player.width, 4);
                    
                    // Check for platforms: scan tiles at player's feet
                    bool onPlatform = false;
                    int footTileY = (int)(Player.Bottom.Y / 16f);
                    int leftTileX = (int)(Player.Left.X / 16f);
                    int rightTileX = (int)(Player.Right.X / 16f);
                    for (int tx = leftTileX; tx <= rightTileX; tx++)
                    {
                        Tile tile = Framing.GetTileSafely(tx, footTileY);
                        if (tile.HasTile && Main.tileSolidTop[tile.TileType] && Player.velocity.Y >= 0f)
                        {
                            // Check if player's bottom is at or below the platform top
                            float platformTop = footTileY * 16f;
                            if (Player.Bottom.Y >= platformTop && Player.Bottom.Y <= platformTop + 18f)
                            {
                                onPlatform = true;
                                break;
                            }
                        }
                    }
                    
                    if (hitGround || onSolid || onPlatform)
                    {
                        // Impact!
                        PerformGroundSlamImpact();
                        IsGroundSlamming = false;
                        groundSlamTimer = 0;
                    }
                }
                
                // Safety timeout - if still slamming after 3 seconds, cancel
                if (groundSlamTimer > 180)
                {
                    IsGroundSlamming = false;
                    groundSlamTimer = 0;
                }
            }

            // Handle flurry attack (LLL)
            if (IsDoingFlurry)
            {
                FlurryTimer++;
                // Spawn one projectile every 3 ticks (0.05 seconds) for 10 projectiles
                // Only spawn projectiles on the local player to avoid multiplayer duplication
                if (FlurryTimer % 3 == 0 && FlurryCount < 10)
                {
                    if (Player.whoAmI == Main.myPlayer)
                    {
                        SpawnFlurryProjectile();
                    }
                    FlurryCount++;
                }
                
                // End flurry after 10 projectiles
                if (FlurryCount >= 10)
                {
                    IsDoingFlurry = false;
                    FlurryTimer = 0;
                    FlurryCount = 0;
                }
            }

            // Display combo above player's head
            if (CurrentCombo.Length > 0)
            {
                DisplayComboAboveHead();
            }

            // Apply visual effects when holding ToughGlove with Bravery trait
            if (Player.HeldItem.ModItem is ToughGlove && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Bravery)
            {
                // Enable afterimage effect
                Player.armorEffectDrawShadow = true;
                
                // Orange aura glow around player
                Lighting.AddLight(Player.Center, 1f, 0.5f, 0.1f);
                
                // Spawn occasional orange dust particles
                if (Main.rand.NextBool(3))
                {
                    Dust dust = Dust.NewDustDirect(
                        Player.position,
                        Player.width,
                        Player.height,
                        DustID.Torch,
                        Player.velocity.X * 0.2f,
                        Player.velocity.Y * 0.2f
                    );
                    dust.noGravity = true;
                    dust.scale = 1.2f;
                    dust.alpha = 100;
                }
            }
        }

        public override void PostUpdateRunSpeeds()
        {
            // Movement bonuses when holding ToughGlove with Bravery trait
            if (Player.HeldItem.ModItem is ToughGlove && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Bravery)
            {
                // 25% increased movement speed
                Player.maxRunSpeed *= 1.25f;
                Player.runAcceleration *= 1.5f; // 50% better acceleration (control)
                
                // Also improve air control
                Player.runSlowdown *= 0.7f; // Less slowdown when stopping
            }
        }

        private void DisplayComboAboveHead(bool forceDisplay = false)
        {
            // Use CombatText to show combo above player
            // Only show on local player and refresh every few ticks (or force display for ending combos)
            if (Player.whoAmI == Main.myPlayer && (forceDisplay || comboDisplayTimer <= 0))
            {
                comboDisplayTimer = 30; // Refresh every 0.5 seconds
                
                string displayText = CurrentCombo;
                Color textColor = new Color(255, 165, 0); // Orange
                
                // Create combat text above player
                Rectangle textRect = new Rectangle((int)Player.Top.X - 20, (int)Player.Top.Y - 40, 40, 20);
                CombatText.NewText(textRect, textColor, displayText, false, false);
            }
        }

        public void ProcessInput(bool isRightClick)
        {
            string input = isRightClick ? "R" : "L";
            string newCombo = CurrentCombo + input;

            // Set attack cooldown
            AttackCooldown = ATTACK_COOLDOWN;
            ComboTimeout = COMBO_TIMEOUT;

            // Get direction to mouse
            Vector2 direction = (Main.MouseWorld - Player.Center).SafeNormalize(Vector2.UnitX);
            int damage = Player.GetWeaponDamage(Player.HeldItem);
            float knockback = Player.GetWeaponKnockback(Player.HeldItem, Player.HeldItem.knockBack);
            float shootSpeed = Player.HeldItem.shootSpeed;

            // Execute based on combo
            switch (newCombo)
            {
                case "L":
                    // Normal small projectile
                    SpawnSmallPunch(direction, damage, knockback, shootSpeed);
                    CurrentCombo = "L";
                    break;

                case "LL":
                    // Same as L
                    SpawnSmallPunch(direction, damage, knockback, shootSpeed);
                    CurrentCombo = "LL";
                    break;

                case "LLL":
                    // Flurry of 10 projectiles over 1 second in a cone
                    CurrentCombo = "LLL"; // Show combo before ending
                    DisplayComboAboveHead(true);
                    SpawnFlurryAttack(direction, damage, knockback, shootSpeed);
                    EndCombo();
                    break;

                case "LLR":
                    // Arrow formation - 5 projectiles in > shape, half damage
                    CurrentCombo = "LLR"; // Show combo before ending
                    DisplayComboAboveHead(true);
                    SpawnArrowFormation(direction, damage / 2, knockback, shootSpeed);
                    EndCombo();
                    break;

                case "R":
                    // Large punch, triple damage, ends combo
                    CurrentCombo = "R"; // Show combo before ending
                    DisplayComboAboveHead(true);
                    SpawnLargePunch(direction, damage * 3, knockback * 1.5f, shootSpeed * 1.2f);
                    EndCombo();
                    break;

                case "LR":
                    // Uppercut - launch player up and spawn 3 upward projectiles
                    SpawnUppercut(direction, damage, knockback, shootSpeed);
                    CurrentCombo = "LR";
                    break;

                case "LRL":
                    // 8 projectiles in a circle, half damage each
                    CurrentCombo = "LRL"; // Show combo before ending
                    DisplayComboAboveHead(true);
                    SpawnCircleAttack(damage, knockback);
                    EndCombo();
                    break;

                case "LRR":
                    // Ground slam
                    CurrentCombo = "LRR"; // Show combo before ending
                    DisplayComboAboveHead(true);
                    StartGroundSlam(damage, knockback);
                    CurrentCombo = ""; // Clear combo, cooldown applied after landing
                    break;

                default:
                    // Invalid combo, reset
                    ResetCombo(false);
                    break;
            }

            // Play sound
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.2f }, Player.Center);
        }

        private Vector2 CalculateProjectileVelocity(Vector2 direction, float baseSpeed)
        {
            // Add player's speed magnitude to projectile speed, but preserve trajectory direction
            // Minimum speed is always the base speed
            float playerSpeed = Player.velocity.Length();
            float totalSpeed = baseSpeed + playerSpeed;
            
            return direction * totalSpeed;
        }

        private void SpawnSmallPunch(Vector2 direction, int damage, float knockback, float speed)
        {
            // Add player velocity with minimum speed enforcement
            Vector2 projectileVelocity = CalculateProjectileVelocity(direction, speed);
            
            Projectile.NewProjectile(
                Player.GetSource_ItemUse(Player.HeldItem),
                Player.Center,
                projectileVelocity,
                ModContent.ProjectileType<ToughGloveProjectile>(),
                damage,
                knockback,
                Player.whoAmI,
                0, // ai[0] = 0 for small punch
                0
            );
        }

        private void SpawnLargePunch(Vector2 direction, int damage, float knockback, float speed)
        {
            // Add player velocity with minimum speed enforcement
            Vector2 projectileVelocity = CalculateProjectileVelocity(direction, speed);
            
            Projectile.NewProjectile(
                Player.GetSource_ItemUse(Player.HeldItem),
                Player.Center,
                projectileVelocity,
                ModContent.ProjectileType<ToughGloveProjectile>(),
                damage,
                knockback ,
                Player.whoAmI,
                1, // ai[0] = 1 for large punch
                0
            );
        }

        private void SpawnFlurryAttack(Vector2 direction, int damage, float knockback, float speed)
        {
            // Start the flurry - projectiles will be spawned over time in PostUpdate
            IsDoingFlurry = true;
            FlurryTimer = 0;
            FlurryCount = 0;
            FlurryDirection = direction;
            FlurryDamage = (int)(damage / 3.5);
            FlurryKnockback = knockback * 0.3f;
            FlurrySpeed = speed;
        }

        private void SpawnFlurryProjectile()
        {
            // Spread randomly across a 60 degree cone
            float baseAngle = FlurryDirection.ToRotation();
            float randomAngle = MathHelper.ToRadians(Main.rand.NextFloat(-30f, 30f));
            float angle = baseAngle + randomAngle;
            Vector2 flurryDir = angle.ToRotationVector2();
            float speedMult = Main.rand.NextFloat(0.8f, 1.2f);
            // Add player velocity with minimum speed enforcement
            Vector2 velocity = CalculateProjectileVelocity(flurryDir, FlurrySpeed * speedMult);
            
            Projectile.NewProjectile(
                Player.GetSource_ItemUse(Player.HeldItem),
                Player.Center,
                velocity,
                ModContent.ProjectileType<ToughGloveProjectile>(),
                FlurryDamage,
                FlurryKnockback,
                Player.whoAmI,
                0, // Small punch
                0
            );
            
            // Play sound for each punch
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.4f + FlurryCount * 0.05f, Volume = 0.5f }, Player.Center);
        }

        private void SpawnArrowFormation(Vector2 direction, int damage, float knockback, float speed)
        {
            // 5 projectiles in > formation
            // Center projectile goes straight, others fan out
            float baseAngle = direction.ToRotation();
            
            // Angles: -30, -15, 0, +15, +30 degrees
            float[] angleOffsets = { -30f, -15f, 0f, 15f, 30f };
            // Positions offset perpendicular to direction to form >
            float[] distanceOffsets = { 2f, 1f, 0f, 1f, 2f }; // Stagger distances
            
            for (int i = 0; i < 5; i++)
            {
                float angle = baseAngle + MathHelper.ToRadians(angleOffsets[i]);
                Vector2 projDir = angle.ToRotationVector2();
                Vector2 projectileVelocity = CalculateProjectileVelocity(projDir, speed);
                
                // Offset spawn position perpendicular to create > shape
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
                float verticalOffset = (i - 2) * 10f; // -20, -10, 0, 10, 20
                Vector2 spawnPos = Player.Center + perpendicular * verticalOffset;
                
                Projectile.NewProjectile(
                    Player.GetSource_ItemUse(Player.HeldItem),
                    spawnPos,
                    projectileVelocity,
                    ModContent.ProjectileType<ToughGloveProjectile>(),
                    damage,
                    knockback,
                    Player.whoAmI,
                    0, // Small punch
                    0
                );
            }
        }

        private void SpawnUppercut(Vector2 direction, int damage, float knockback, float speed)
        {
            // Launch player upward
            Player.velocity.Y = -12f;
            
            // Determine if player is facing left or right
            int facing = Player.direction;
            
            // Spawn exactly 3 upward projectiles in a diagonal cone
            float baseAngle = -MathHelper.PiOver2; // Straight up
            
            // Three fixed angles: -20, 0, +20 degrees from straight up, offset by facing direction
            float facingOffset = facing > 0 ? 15f : -15f;
            float[] angleOffsets = { -20f + facingOffset, facingOffset, 20f + facingOffset };

            for (int i = 0; i < 3; i++)
            {
                float angle = baseAngle + MathHelper.ToRadians(angleOffsets[i]);
                Vector2 projDir = angle.ToRotationVector2();
                // Add player velocity with minimum speed enforcement
                Vector2 velocity = CalculateProjectileVelocity(projDir, speed);
                
                Projectile.NewProjectile(
                    Player.GetSource_ItemUse(Player.HeldItem),
                    Player.Center,
                    velocity,
                    ModContent.ProjectileType<ToughGloveProjectile>(),
                    damage,
                    knockback,
                    Player.whoAmI,
                    0,
                    0
                );
            }
        }

        private void SpawnCircleAttack(int damage, float knockback)
        {
            // 8 projectiles in a circle, half damage
            int circleDamage = damage / 2;
            float speed = 8f;

            for (int i = 0; i < 8; i++)
            {
                float angle = MathHelper.TwoPi / 8 * i;
                Vector2 projDir = angle.ToRotationVector2();
                // Add player velocity with minimum speed enforcement
                Vector2 velocity = CalculateProjectileVelocity(projDir, speed);
                
                Projectile.NewProjectile(
                    Player.GetSource_ItemUse(Player.HeldItem),
                    Player.Center,
                    velocity,
                    ModContent.ProjectileType<ToughGloveProjectile>(),
                    circleDamage,
                    knockback * 0.5f,
                    Player.whoAmI,
                    0,
                    0
                );
            }
        }

        private void StartGroundSlam(int damage, float knockback)
        {
            IsGroundSlamming = true;
            groundSlamTimer = 0;
            GroundSlamDamage = damage * 2; // Double damage on impact
            GroundSlamKnockback = knockback * 2f;
            
            // Give initial downward push
            Player.velocity.Y = 8f;
            
            // Play start sound
            SoundEngine.PlaySound(SoundID.Item7, Player.Center);
        }

        private void PerformGroundSlamImpact()
        {
            // Play impact sound
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f }, Player.Center);

            int slamDamage = GroundSlamDamage > 0 ? GroundSlamDamage : 30;
            
            // Only the local player should deal damage and spawn projectiles (multiplayer safety)
            if (Player.whoAmI == Main.myPlayer)
            {
                // Damage nearby enemies
                float impactRadius = 150f;
                
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.active && !npc.friendly && !npc.dontTakeDamage)
                    {
                        float distance = Vector2.Distance(Player.Center, npc.Center);
                        if (distance < impactRadius)
                        {
                            // Apply damage
                            Player.ApplyDamageToNPC(npc, slamDamage, GroundSlamKnockback, Player.direction, false);
                            
                            // Ignite
                            npc.AddBuff(BuffID.OnFire, 180);
                        }
                    }
                }

                // Spawn two large projectiles left and right
                Projectile.NewProjectile(
                    Player.GetSource_ItemUse(Player.HeldItem),
                    Player.Center,
                    new Vector2(-12f, 0f),
                    ModContent.ProjectileType<ToughGloveProjectile>(),
                    slamDamage,
                    GroundSlamKnockback,
                    Player.whoAmI,
                    1, // Large punch
                    0
                );
                
                Projectile.NewProjectile(
                    Player.GetSource_ItemUse(Player.HeldItem),
                    Player.Center,
                    new Vector2(12f, 0f),
                    ModContent.ProjectileType<ToughGloveProjectile>(),
                    slamDamage,
                    GroundSlamKnockback,
                    Player.whoAmI,
                    1, // Large punch
                    0
                );
            }

            // Create dust shockwave
            for (int i = 0; i < 30; i++)
            {
                float angle = MathHelper.TwoPi / 30 * i;
                Vector2 dustVel = new Vector2((float)System.Math.Cos(angle) * 8f, -2f);
                Dust dust = Dust.NewDustDirect(Player.Bottom - new Vector2(0, 4), 0, 0, DustID.Torch, dustVel.X, dustVel.Y);
                dust.noGravity = true;
                dust.scale = 2f;
            }

            // Apply combo cooldown after slam
            ComboCooldown = COMBO_COOLDOWN;
        }

        private void EndCombo()
        {
            ResetCombo(true);
        }

        private void ResetCombo(bool applyCooldown)
        {
            CurrentCombo = "";
            ComboTimeout = 0;
            if (applyCooldown)
            {
                ComboCooldown = COMBO_COOLDOWN;
            }
        }
    }
}
