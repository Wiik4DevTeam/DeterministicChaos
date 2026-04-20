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
    public class ToughGauntletPlayer : ModPlayer
    {
        public string CurrentCombo = "";
        public int ComboCooldown = 0;
        public int AttackCooldown = 0;
        public int ComboTimeout = 0;

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

        // Faster constants compared to ToughGlove (18→12, 60→40, 60→45)
        private const int ATTACK_COOLDOWN = 12;
        private const int COMBO_COOLDOWN = 40;
        private const int COMBO_TIMEOUT = 45;

        public override void ResetEffects()
        {
            bool wasOnCooldown = AttackCooldown > 0;

            if (ComboCooldown > 0)
                ComboCooldown--;
            if (AttackCooldown > 0)
                AttackCooldown--;
            if (ComboTimeout > 0)
            {
                ComboTimeout--;
                if (ComboTimeout <= 0 && CurrentCombo.Length > 0)
                {
                    ResetCombo(false);
                }
            }

            // Auto-queue right-click
            if (wasOnCooldown && AttackCooldown <= 0 && ComboCooldown <= 0)
            {
                if (Player.HeldItem.ModItem is ToughGauntlet && Main.mouseRight && Player.whoAmI == Main.myPlayer
                    && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Bravery)
                {
                    ProcessInput(true);
                }
            }

            if (comboDisplayTimer > 0)
                comboDisplayTimer--;
        }

        public override void PostUpdate()
        {
            // Handle ground slam
            if (IsGroundSlamming)
            {
                groundSlamTimer++;

                Player.velocity.Y = MathHelper.Clamp(Player.velocity.Y + 4f, -10f, 35f);

                if (groundSlamTimer > 5)
                {
                    bool hitGround = Player.velocity.Y == 0f;
                    bool onSolid = Collision.SolidCollision(Player.BottomLeft, Player.width, 4);

                    bool onPlatform = false;
                    int footTileY = (int)(Player.Bottom.Y / 16f);
                    int leftTileX = (int)(Player.Left.X / 16f);
                    int rightTileX = (int)(Player.Right.X / 16f);
                    for (int tx = leftTileX; tx <= rightTileX; tx++)
                    {
                        Tile tile = Framing.GetTileSafely(tx, footTileY);
                        if (tile.HasTile && Main.tileSolidTop[tile.TileType] && Player.velocity.Y >= 0f)
                        {
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
                        PerformGroundSlamImpact();
                        IsGroundSlamming = false;
                        groundSlamTimer = 0;
                    }
                }

                if (groundSlamTimer > 180)
                {
                    IsGroundSlamming = false;
                    groundSlamTimer = 0;
                }
            }

            // Handle flurry attack (LLL), faster: every 2 ticks instead of 3
            if (IsDoingFlurry)
            {
                FlurryTimer++;
                if (FlurryTimer % 2 == 0 && FlurryCount < 12)
                {
                    if (Player.whoAmI == Main.myPlayer)
                    {
                        SpawnFlurryProjectile();
                    }
                    FlurryCount++;
                }

                if (FlurryCount >= 12)
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

            // Visual effects when holding ToughGauntlet with Bravery trait
            if (Player.HeldItem.ModItem is ToughGauntlet && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Bravery)
            {
                Player.armorEffectDrawShadow = true;

                // Brighter orange-red aura
                Lighting.AddLight(Player.Center, 1.2f, 0.4f, 0.1f);

                if (Main.rand.NextBool(2))
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
                    dust.scale = 1.4f;
                    dust.alpha = 80;
                }

                // Occasional solar flare particles
                if (Main.rand.NextBool(5))
                {
                    Dust dust = Dust.NewDustDirect(
                        Player.position,
                        Player.width,
                        Player.height,
                        DustID.SolarFlare,
                        0f, 0f
                    );
                    dust.noGravity = true;
                    dust.scale = 0.8f;
                }
            }
        }

        public override void PostUpdateRunSpeeds()
        {
            // Movement bonuses when holding ToughGauntlet (slightly better than ToughGlove)
            if (Player.HeldItem.ModItem is ToughGauntlet && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Bravery)
            {
                Player.maxRunSpeed *= 1.35f;
                Player.runAcceleration *= 1.6f;
                Player.runSlowdown *= 0.6f;
            }
        }

        private void DisplayComboAboveHead(bool forceDisplay = false)
        {
            if (Player.whoAmI == Main.myPlayer && (forceDisplay || comboDisplayTimer <= 0))
            {
                comboDisplayTimer = 30;

                string displayText = CurrentCombo;
                Color textColor = new Color(255, 140, 0); // Dark orange

                Rectangle textRect = new Rectangle((int)Player.Top.X - 20, (int)Player.Top.Y - 40, 40, 20);
                CombatText.NewText(textRect, textColor, displayText, false, false);
            }
        }

        public void ProcessInput(bool isRightClick)
        {
            string input = isRightClick ? "R" : "L";
            string newCombo = CurrentCombo + input;

            AttackCooldown = ATTACK_COOLDOWN;
            ComboTimeout = COMBO_TIMEOUT;

            Vector2 direction = (Main.MouseWorld - Player.Center).SafeNormalize(Vector2.UnitX);
            int damage = Player.GetWeaponDamage(Player.HeldItem);
            float knockback = Player.GetWeaponKnockback(Player.HeldItem, Player.HeldItem.knockBack);
            float shootSpeed = Player.HeldItem.shootSpeed;

            switch (newCombo)
            {
                case "L":
                    SpawnSmallPunch(direction, damage, knockback, shootSpeed);
                    CurrentCombo = "L";
                    break;

                case "LL":
                    SpawnSmallPunch(direction, damage, knockback, shootSpeed);
                    CurrentCombo = "LL";
                    break;

                case "LLL":
                    CurrentCombo = "LLL";
                    DisplayComboAboveHead(true);
                    SpawnFlurryAttack(direction, damage, knockback, shootSpeed);
                    EndCombo();
                    break;

                case "LLR":
                    CurrentCombo = "LLR";
                    DisplayComboAboveHead(true);
                    SpawnArrowFormation(direction, damage / 2, knockback, shootSpeed);
                    EndCombo();
                    break;

                case "R":
                    CurrentCombo = "R";
                    DisplayComboAboveHead(true);
                    SpawnLargePunch(direction, damage * 3, knockback * 1.5f, shootSpeed * 1.2f);
                    EndCombo();
                    break;

                case "LR":
                    SpawnUppercut(direction, damage, knockback, shootSpeed);
                    CurrentCombo = "LR";
                    break;

                case "LRL":
                    CurrentCombo = "LRL";
                    DisplayComboAboveHead(true);
                    SpawnCircleAttack(damage, knockback);
                    EndCombo();
                    break;

                case "LRR":
                    CurrentCombo = "LRR";
                    DisplayComboAboveHead(true);
                    StartGroundSlam(damage, knockback);
                    CurrentCombo = "";
                    break;

                default:
                    ResetCombo(false);
                    break;
            }

            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.3f }, Player.Center);
        }

        private Vector2 CalculateProjectileVelocity(Vector2 direction, float baseSpeed)
        {
            float playerSpeed = Player.velocity.Length();
            float totalSpeed = baseSpeed + playerSpeed;
            return direction * totalSpeed;
        }

        private void SpawnSmallPunch(Vector2 direction, int damage, float knockback, float speed)
        {
            Vector2 projectileVelocity = CalculateProjectileVelocity(direction, speed);

            Projectile.NewProjectile(
                Player.GetSource_ItemUse(Player.HeldItem),
                Player.Center,
                projectileVelocity,
                ModContent.ProjectileType<ToughGauntletProjectile>(),
                damage,
                knockback,
                Player.whoAmI,
                0, // ai[0] = 0 for small punch
                0
            );
        }

        private void SpawnLargePunch(Vector2 direction, int damage, float knockback, float speed)
        {
            Vector2 projectileVelocity = CalculateProjectileVelocity(direction, speed);

            Projectile.NewProjectile(
                Player.GetSource_ItemUse(Player.HeldItem),
                Player.Center,
                projectileVelocity,
                ModContent.ProjectileType<ToughGauntletProjectile>(),
                damage,
                knockback,
                Player.whoAmI,
                1, // ai[0] = 1 for large punch
                0
            );
        }

        private void SpawnFlurryAttack(Vector2 direction, int damage, float knockback, float speed)
        {
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
            float baseAngle = FlurryDirection.ToRotation();
            float randomAngle = MathHelper.ToRadians(Main.rand.NextFloat(-30f, 30f));
            float angle = baseAngle + randomAngle;
            Vector2 flurryDir = angle.ToRotationVector2();
            float speedMult = Main.rand.NextFloat(0.8f, 1.2f);
            Vector2 velocity = CalculateProjectileVelocity(flurryDir, FlurrySpeed * speedMult);

            Projectile.NewProjectile(
                Player.GetSource_ItemUse(Player.HeldItem),
                Player.Center,
                velocity,
                ModContent.ProjectileType<ToughGauntletProjectile>(),
                FlurryDamage,
                FlurryKnockback,
                Player.whoAmI,
                0,
                0
            );

            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.5f + FlurryCount * 0.04f, Volume = 0.5f }, Player.Center);
        }

        private void SpawnArrowFormation(Vector2 direction, int damage, float knockback, float speed)
        {
            float baseAngle = direction.ToRotation();

            float[] angleOffsets = { -30f, -15f, 0f, 15f, 30f };

            for (int i = 0; i < 5; i++)
            {
                float angle = baseAngle + MathHelper.ToRadians(angleOffsets[i]);
                Vector2 projDir = angle.ToRotationVector2();
                Vector2 projectileVelocity = CalculateProjectileVelocity(projDir, speed);

                Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
                float verticalOffset = (i - 2) * 10f;
                Vector2 spawnPos = Player.Center + perpendicular * verticalOffset;

                Projectile.NewProjectile(
                    Player.GetSource_ItemUse(Player.HeldItem),
                    spawnPos,
                    projectileVelocity,
                    ModContent.ProjectileType<ToughGauntletProjectile>(),
                    damage,
                    knockback,
                    Player.whoAmI,
                    0,
                    0
                );
            }
        }

        private void SpawnUppercut(Vector2 direction, int damage, float knockback, float speed)
        {
            Player.velocity.Y = -14f; // Slightly higher launch than ToughGlove

            int facing = Player.direction;

            float baseAngle = -MathHelper.PiOver2;

            float facingOffset = facing > 0 ? 15f : -15f;
            float[] angleOffsets = { -20f + facingOffset, facingOffset, 20f + facingOffset };

            for (int i = 0; i < 3; i++)
            {
                float angle = baseAngle + MathHelper.ToRadians(angleOffsets[i]);
                Vector2 projDir = angle.ToRotationVector2();
                Vector2 velocity = CalculateProjectileVelocity(projDir, speed);

                Projectile.NewProjectile(
                    Player.GetSource_ItemUse(Player.HeldItem),
                    Player.Center,
                    velocity,
                    ModContent.ProjectileType<ToughGauntletProjectile>(),
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
            int circleDamage = damage / 2;
            float speed = 10f; // Faster than ToughGlove's 8

            for (int i = 0; i < 8; i++)
            {
                float angle = MathHelper.TwoPi / 8 * i;
                Vector2 projDir = angle.ToRotationVector2();
                Vector2 velocity = CalculateProjectileVelocity(projDir, speed);

                Projectile.NewProjectile(
                    Player.GetSource_ItemUse(Player.HeldItem),
                    Player.Center,
                    velocity,
                    ModContent.ProjectileType<ToughGauntletProjectile>(),
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
            GroundSlamDamage = damage * 2;
            GroundSlamKnockback = knockback * 2f;

            Player.velocity.Y = 8f;

            SoundEngine.PlaySound(SoundID.Item7, Player.Center);
        }

        private void PerformGroundSlamImpact()
        {
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f, Volume = 1f }, Player.Center);

            int slamDamage = GroundSlamDamage > 0 ? GroundSlamDamage : 60;

            if (Player.whoAmI == Main.myPlayer)
            {
                // Larger impact radius than ToughGlove (150→200)
                float impactRadius = 200f;

                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.active && !npc.friendly && !npc.dontTakeDamage)
                    {
                        float distance = Vector2.Distance(Player.Center, npc.Center);
                        if (distance < impactRadius)
                        {
                            Player.ApplyDamageToNPC(npc, slamDamage, GroundSlamKnockback, Player.direction, false);

                            // Apply Hellfire instead of OnFire
                            npc.AddBuff(BuffID.OnFire3, 180);
                        }
                    }
                }

                // Spawn two large projectiles left and right
                Projectile.NewProjectile(
                    Player.GetSource_ItemUse(Player.HeldItem),
                    Player.Center,
                    new Vector2(-14f, 0f),
                    ModContent.ProjectileType<ToughGauntletProjectile>(),
                    slamDamage,
                    GroundSlamKnockback,
                    Player.whoAmI,
                    1, // Large punch
                    0
                );

                Projectile.NewProjectile(
                    Player.GetSource_ItemUse(Player.HeldItem),
                    Player.Center,
                    new Vector2(14f, 0f),
                    ModContent.ProjectileType<ToughGauntletProjectile>(),
                    slamDamage,
                    GroundSlamKnockback,
                    Player.whoAmI,
                    1, // Large punch
                    0
                );
            }

            // Larger dust shockwave
            for (int i = 0; i < 40; i++)
            {
                float angle = MathHelper.TwoPi / 40 * i;
                Vector2 dustVel = new Vector2((float)System.Math.Cos(angle) * 10f, -2f);
                Dust dust = Dust.NewDustDirect(Player.Bottom - new Vector2(0, 4), 0, 0, DustID.Torch, dustVel.X, dustVel.Y);
                dust.noGravity = true;
                dust.scale = 2.5f;
            }

            // Solar flare particles
            for (int i = 0; i < 15; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(8f, 4f);
                Dust dust = Dust.NewDustDirect(Player.Bottom - new Vector2(0, 4), 0, 0, DustID.SolarFlare, dustVel.X, dustVel.Y);
                dust.noGravity = true;
                dust.scale = 1.5f;
            }

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
