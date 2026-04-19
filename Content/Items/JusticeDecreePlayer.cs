using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.SoulTraits;
using DeterministicChaos.Content.SoulTraits.Armor;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Items
{
    public class JusticeDecreePlayer : ModPlayer
    {
        // List of marked NPC indices (up to 6 marks total, can have duplicates for multi-marks on same enemy).
        private readonly List<int> markedTargets = new List<int>();

        private int markDecayTimer = 0;
        private const int MarkDuration = 300; // 5 seconds before all marks expire
        private const float MarkRange = 600f; // ~37.5 tiles
        private const int MaxMarks = 6;

        // Animation for marker rotation (same feel as HollowGun)
        private float markerRotation = 0f;
        private int rotationTimer = 0;
        private const int RotationDuration = 15;

        // Jump detection (same as HollowGunPlayer)
        private bool wasOnGround = true;
        private bool hasTriggeredThisJump = false;
        private int lastJumpValue = 0;
        private bool wasPressingJump = false;

        // Burst fire state
        public bool IsBurstFiring { get; private set; } = false;
        private int burstTimer = 0;
        private const int BaseBurstInterval = 23; // 30 useTime * 0.75 = 25% faster between shots
        private EntitySource_ItemUse_WithAmmo burstSource;
        private int burstDamage;
        private float burstKnockback;

        public bool HasMarkedTargets => markedTargets.Count > 0 && markDecayTimer > 0;

        // Returns a read-only view of current marks for rendering.
        public IReadOnlyList<int> MarkedTargets => markedTargets;

        public override void ResetEffects()
        {
            // Countdown mark timer
            if (markDecayTimer > 0)
            {
                markDecayTimer--;
                if (markDecayTimer <= 0)
                {
                    ClearAllMarks();
                }
            }

            // Clean up dead/inactive marks
            for (int i = markedTargets.Count - 1; i >= 0; i--)
            {
                int idx = markedTargets[i];
                if (idx < 0 || idx >= Main.maxNPCs || !Main.npc[idx].active || Main.npc[idx].friendly || !Main.npc[idx].CanBeChasedBy())
                {
                    markedTargets.RemoveAt(i);
                }
            }

            // Animate rotation toward 0
            if (rotationTimer < RotationDuration)
            {
                rotationTimer++;
                float t = (float)rotationTimer / RotationDuration;
                t = 1f - (1f - t) * (1f - t); // Ease out
                markerRotation = MathHelper.Lerp(MathHelper.PiOver2, 0f, t);
            }
            else
            {
                markerRotation = 0f;
            }
        }

        public override void PostUpdate()
        {
            // Handle burst fire
            if (IsBurstFiring)
            {
                // Keep the gun visually held during burst
                if (Player.itemAnimation <= 2)
                {
                    Player.itemAnimation = 3;
                    Player.itemTime = 3;
                }

                burstTimer--;
                if (burstTimer <= 0)
                {
                    FireNextBurstShot();
                }
            }

            // Only process jump detection if holding JusticeDecree with Justice trait
            if (Player.HeldItem.type != ModContent.ItemType<JusticeDecree>()
                || Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Justice)
            {
                wasOnGround = true;
                hasTriggeredThisJump = false;
                lastJumpValue = Player.jump;
                wasPressingJump = Player.controlJump;
                return;
            }

            bool onGround = Player.velocity.Y == 0f;
            bool justPressedJump = Player.controlJump && !wasPressingJump;

            if (onGround)
            {
                wasOnGround = true;
                hasTriggeredThisJump = false;
            }
            else
            {
                bool jumpReset = lastJumpValue <= 0 && Player.jump > 0 && !wasOnGround;
                bool jumpPressedWhileRising = justPressedJump && Player.velocity.Y < 0 && !wasOnGround;

                if (!hasTriggeredThisJump && (jumpReset || jumpPressedWhileRising))
                {
                    hasTriggeredThisJump = true;
                    OnDoubleJump();
                }

                if (!Player.controlJump)
                {
                    hasTriggeredThisJump = false;
                }

                wasOnGround = false;
            }

            lastJumpValue = Player.jump;
            wasPressingJump = Player.controlJump;
        }

        private void OnDoubleJump()
        {
            // Find all valid enemies in range, sorted by health descending
            var candidates = new List<(int index, int life)>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc == null || !npc.active || npc.friendly || !npc.CanBeChasedBy())
                    continue;

                float dist = Vector2.Distance(Player.Center, npc.Center);
                if (dist > MarkRange)
                    continue;

                candidates.Add((i, npc.life));
            }

            if (candidates.Count == 0)
                return;

            // Sort by health descending (highest HP gets marks first)
            candidates.Sort((a, b) => b.life.CompareTo(a.life));

            // Clear previous marks and distribute 6 marks across enemies
            ClearAllMarks();

            if (candidates.Count >= MaxMarks)
            {
                // 6+ enemies: one mark each on the top 6
                for (int i = 0; i < MaxMarks; i++)
                {
                    markedTargets.Add(candidates[i].index);
                }
            }
            else
            {
                // Fewer enemies than 6: distribute marks equally
                int marksPerEnemy = MaxMarks / candidates.Count;
                int remainder = MaxMarks % candidates.Count;

                for (int i = 0; i < candidates.Count; i++)
                {
                    int marks = marksPerEnemy + (i < remainder ? 1 : 0);
                    for (int m = 0; m < marks; m++)
                    {
                        markedTargets.Add(candidates[i].index);
                    }
                }
            }

            markDecayTimer = MarkDuration;

            // Reset rotation animation
            markerRotation = MathHelper.PiOver2;
            rotationTimer = 0;

            // Audio/visual feedback
            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.5f, Volume = 0.42f }, Player.Center);

            // Dust at each unique marked target
            var dusted = new HashSet<int>();
            foreach (int idx in markedTargets)
            {
                if (!dusted.Add(idx))
                    continue;

                NPC target = Main.npc[idx];
                if (target != null && target.active)
                {
                    for (int d = 0; d < 20; d++)
                    {
                        Vector2 dustPos = target.Center + Main.rand.NextVector2CircularEdge(target.width, target.height);
                        Dust dust = Dust.NewDustPerfect(dustPos, DustID.YellowTorch, Vector2.Zero, 0, default, 1.5f);
                        dust.noGravity = true;
                    }
                }
            }
        }

        // Consumes and returns the next marked target index. Returns -1 if no marks remain.
        public int ConsumeNextMark()
        {
            while (markedTargets.Count > 0)
            {
                int idx = markedTargets[0];
                markedTargets.RemoveAt(0);

                // Validate the target is still alive
                if (idx >= 0 && idx < Main.maxNPCs)
                {
                    NPC npc = Main.npc[idx];
                    if (npc != null && npc.active && !npc.friendly && npc.CanBeChasedBy())
                    {
                        return idx;
                    }
                }
            }

            return -1;
        }

        public void ClearAllMarks()
        {
            markedTargets.Clear();
            markDecayTimer = 0;
        }

        // Begins burst-firing all current marks. Fires the first beam immediately.
        public void StartBurst(EntitySource_ItemUse_WithAmmo source, int damage, float knockback)
        {
            if (!HasMarkedTargets)
                return;

            burstSource = source;
            burstDamage = damage;
            burstKnockback = knockback;
            IsBurstFiring = true;

            // Fire the first shot immediately
            FireNextBurstShot();
        }

        private void FireNextBurstShot()
        {
            int nextMark = ConsumeNextMark();
            if (nextMark < 0)
            {
                // No more marks, end burst
                StopBurst();
                return;
            }

            NPC target = Main.npc[nextMark];
            if (target == null || !target.active || target.friendly || !target.CanBeChasedBy())
            {
                // Target invalid, try next mark immediately
                FireNextBurstShot();
                return;
            }

            // Aim the player toward the target
            Player.direction = (target.Center.X > Player.Center.X) ? 1 : -1;
            Vector2 aimDir = (target.Center - Player.Center).SafeNormalize(Vector2.UnitX);
            Player.itemRotation = aimDir.ToRotation();
            if (Player.direction == -1)
                Player.itemRotation += MathHelper.Pi;

            int proj = Projectile.NewProjectile(
                burstSource,
                Player.Center,
                Vector2.Zero,
                ModContent.ProjectileType<JusticeBeam>(),
                burstDamage,
                burstKnockback,
                Player.whoAmI,
                nextMark,
                Player.Center.X
            );

            if (proj >= 0 && proj < Main.maxProjectiles)
            {
                Main.projectile[proj].localAI[0] = Player.Center.Y;
            }

            SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/ChargeShot") { Volume = 0.6f }, Player.Center);

            // Set timer for next shot, scaled by attack speed
            if (markedTargets.Count > 0)
            {
                float attackSpeed = Player.GetAttackSpeed(DamageClass.Generic);
                burstTimer = Math.Max(2, (int)(BaseBurstInterval / attackSpeed));
            }
            else
            {
                StopBurst();
            }
        }

        private void StopBurst()
        {
            IsBurstFiring = false;
            burstTimer = 0;
            burstSource = null;
        }

        public float GetMarkerRotation() => markerRotation;

        // Returns how many marks are currently on a given NPC index.
        public int GetMarkCount(int npcIndex)
        {
            int count = 0;
            foreach (int idx in markedTargets)
            {
                if (idx == npcIndex)
                    count++;
            }
            return count;
        }

        // Hypercrit system: while holding JusticeDecree, crit chance is converted into hypercrit chance.
        // Since all hits from JusticeBeam are guaranteed crits, the player's crit chance instead determines
        // whether the crit becomes a Hypercrit (3x damage, bright yellow text).
        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (Player.HeldItem.type != ModContent.ItemType<JusticeDecree>()
                || Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Justice)
                return;

            // Only apply to JusticeBeam or tagged HollowGun bullets
            bool isJusticeBeam = proj.type == ModContent.ProjectileType<JusticeBeam>();
            bool isTaggedBullet = false;
            if (!isJusticeBeam)
            {
                var globalProj = proj.GetGlobalProjectile<HollowGunGlobalProjectile>();
                isTaggedBullet = globalProj.isHollowGunBullet;
            }

            if (!isJusticeBeam && !isTaggedBullet)
                return;

            // Get player's crit chance as hypercrit chance
            float hypercritChance = Player.GetTotalCritChance(proj.DamageType);

            // Roll for hypercrit
            if (Main.rand.Next(100) < (int)hypercritChance)
            {
                // Hypercrit: 4x total damage instead of 2x
                // Since crits already do 2x, we add another 2x multiplier (2x * 2 = 4x)
                modifiers.FinalDamage *= 2f;
                proj.localAI[1] = 1f; // Flag for OnHitNPC to show yellow text
            }
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Player.HeldItem.type != ModContent.ItemType<JusticeDecree>()
                || Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Justice)
                return;

            // Check the hypercrit flag
            if (proj.localAI[1] == 1f)
            {
                proj.localAI[1] = 0f; // Reset flag

                // Bright yellow burst for hypercrit
                for (int i = 0; i < 25; i++)
                {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
                    Dust dust = Dust.NewDustPerfect(target.Center, DustID.YellowTorch, vel, 0, default, 2f);
                    dust.noGravity = true;
                }

                // Show bright yellow combat text
                CombatText.NewText(target.Hitbox, new Color(255, 255, 50), damageDone, dramatic: true);

                // Bright flash sound
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/Hypercrit") { Volume = 0.6f }, target.Center);

                // Sheriff Hat hypercrit fire rate bonus
                var hatPlayer = Player.GetModPlayer<CowboyHatPlayer>();
                if (hatPlayer.hasSheriffHat)
                {
                    hatPlayer.hypercritAttackSpeedTimer = 36; // 0.6 seconds of +40% attack speed
                }
            }
        }
    }

    // Draws Justice Decree mark indicators on marked NPCs (supports multiple marks per NPC).
    public class JusticeDecreeMarkerNPC : GlobalNPC
    {
        public override bool InstancePerEntity => false;

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (HollowGunMarkerDrawSystem.markerTexture == null)
                return;

            Player localPlayer = Main.LocalPlayer;
            if (localPlayer == null || !localPlayer.active)
                return;

            var decreePlayer = localPlayer.GetModPlayer<JusticeDecreePlayer>();
            if (!decreePlayer.HasMarkedTargets)
                return;

            int markCount = decreePlayer.GetMarkCount(npc.whoAmI);
            if (markCount <= 0)
                return;

            Texture2D tex = HollowGunMarkerDrawSystem.markerTexture;
            Vector2 drawPos = npc.Center - screenPos;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            float baseRotation = decreePlayer.GetMarkerRotation();
            float pulse = 1f + (float)Math.Sin(Main.GameUpdateCount * 0.1f) * 0.1f;

            if (markCount == 1)
            {
                // Single mark: draw centered
                spriteBatch.Draw(tex, drawPos, null, Color.White, baseRotation, origin, pulse, SpriteEffects.None, 0f);
            }
            else
            {
                // Multiple marks: fan them out in a circle around the NPC
                float angleStep = MathHelper.TwoPi / markCount;
                float offsetRadius = 16f;

                for (int i = 0; i < markCount; i++)
                {
                    float angle = angleStep * i + Main.GameUpdateCount * 0.02f;
                    Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * offsetRadius;
                    float markScale = pulse * 0.85f;
                    spriteBatch.Draw(tex, drawPos + offset, null, Color.White, baseRotation, origin, markScale, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
