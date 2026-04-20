using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Imbued;
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

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public abstract class ImbuedSummonProjectile : ModProjectile
    {
        protected abstract ImbuedSummonVariant Variant { get; }
        protected abstract int GetBuffType();

        public override string Texture => "DeterministicChaos/Content/Projectiles/Friendly/RoaringSummonProjectile";

        // --- Constants ---
        private const float IdleJitterRange = 5f;
        private const int TargetSwitchDelayFrames = 30;
        private const int AttackCooldownMax = 60; // 1 second
        private const int KindnessPickupInterval = 60; // 1 second
        private const int BraveryDashInterval = 180; // 3 seconds
        private const int BraveryDashDuration = 30;

        // --- State ---
        private Vector2 jitterOffset;
        private int jitterTimer;
        private int currentTrackedTarget = -1;
        private int targetSwitchDelay = 0;
        private float transitionProgress = 1f;
        private bool wasOwnerAttacking = false;
        private int lastAttackAnimTime = 0;
        private int attackCooldown = 0;

        // Kindness state
        private int kindnessAllyIndex = -1;
        private int kindnessTimer = 0;

        // Bravery state
        private int braveryDashTimer = 0;
        private bool braveryDashing = false;
        private int braveryDashTime = 0;

        protected Color GetTraitColor()
        {
            return Variant switch
            {
                ImbuedSummonVariant.Determination => new Color(255, 60, 60),
                ImbuedSummonVariant.Integrity => new Color(0, 0, 255),
                ImbuedSummonVariant.Patience => new Color(80, 255, 255),
                ImbuedSummonVariant.Perseverance => new Color(255, 80, 255),
                ImbuedSummonVariant.Kindness => new Color(80, 230, 80),
                ImbuedSummonVariant.Justice => new Color(255, 255, 80),
                ImbuedSummonVariant.Bravery => new Color(255, 190, 60),
                _ => Color.White
            };
        }

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = 1;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 42;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.minionSlots = Variant == ImbuedSummonVariant.Integrity ? 0.5f : 1f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;

            if (Variant == ImbuedSummonVariant.Perseverance)
                Projectile.DamageType = DamageClass.Magic;

            if (Variant == ImbuedSummonVariant.Bravery)
                Projectile.DamageType = DamageClass.Melee;
        }

        public override bool? CanCutTiles() => false;
        public override bool MinionContactDamage() => true;

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(braveryDashing);
            writer.Write(kindnessAllyIndex);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            braveryDashing = reader.ReadBoolean();
            kindnessAllyIndex = reader.ReadInt32();
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];

            if (!CheckActive(owner))
                return;

            Projectile.timeLeft = 2;

            if (attackCooldown > 0)
                attackCooldown--;

            // --- Kindness: orbit allies instead of enemies ---
            if (Variant == ImbuedSummonVariant.Kindness)
            {
                HandleKindnessAI(owner);
                return;
            }

            // --- Find target: whip-marked or nearest damaged NPC ---
            NPC activeTarget = FindTarget(owner);

            if (activeTarget != null)
            {
                // Bravery: special movement with periodic dashing
                if (Variant == ImbuedSummonVariant.Bravery)
                    HandleBraveryMovement(owner, activeTarget);
                else
                    MirrorMovementAroundTarget(owner, activeTarget);

                // Trigger attack on any player attack (1 second cooldown)
                CheckAndTriggerAttack(owner, activeTarget);

                Projectile.spriteDirection = (activeTarget.Center.X > Projectile.Center.X) ? 1 : -1;
            }
            else
            {
                IdleAtPlayer(owner);
                Projectile.spriteDirection = owner.direction;
            }

            Lighting.AddLight(Projectile.Center, GetTraitColor().ToVector3() * 0.4f);
        }

        // =========================================================
        // Target Finding
        // =========================================================

        private NPC FindTarget(Player owner)
        {
            // Priority 1: Whip-marked target (only from player's own whip/right-click)
            int markedTarget = owner.MinionAttackTargetNPC;
            bool hasMarked = markedTarget != -1 && Main.npc[markedTarget].active && !Main.npc[markedTarget].friendly;

            // Priority 2: Nearest damaged NPC within range (always searched)
            NPC fallback = null;
            float bestDist = 800f;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && !npc.dontTakeDamage && npc.lifeMax > 5 && npc.life < npc.lifeMax)
                {
                    float dist = Vector2.Distance(owner.Center, npc.Center);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        fallback = npc;
                    }
                }
            }

            // Prioritize whip-marked target; fall back to nearest damaged NPC
            int targetNPC = hasMarked ? markedTarget : (fallback != null ? fallback.whoAmI : -1);
            bool hasValidTarget = targetNPC != -1;

            // Handle target switching with delay for smoother transitions
            NPC result = null;
            if (hasValidTarget)
            {
                if (targetNPC != currentTrackedTarget)
                {
                    if (targetSwitchDelay <= 0)
                        targetSwitchDelay = TargetSwitchDelayFrames;

                    targetSwitchDelay--;

                    if (targetSwitchDelay <= 0)
                    {
                        currentTrackedTarget = targetNPC;
                        transitionProgress = 0f;
                    }
                }
                else
                {
                    targetSwitchDelay = 0;
                }

                if (currentTrackedTarget != -1 && Main.npc[currentTrackedTarget].active && !Main.npc[currentTrackedTarget].friendly)
                {
                    result = Main.npc[currentTrackedTarget];
                }
                else
                {
                    currentTrackedTarget = targetNPC;
                    result = Main.npc[targetNPC];
                    transitionProgress = 0.5f;
                }

                transitionProgress = MathHelper.Clamp(transitionProgress + 0.02f, 0f, 1f);
            }
            else
            {
                if (currentTrackedTarget != -1)
                    transitionProgress = 0f;
                currentTrackedTarget = -1;
                targetSwitchDelay = 0;
            }

            return result;
        }

        // =========================================================
        // Attack Triggering (FOR ALL: any player attack, 1s cooldown)
        // =========================================================

        private void CheckAndTriggerAttack(Player owner, NPC target)
        {
            bool isAttacking = owner.itemAnimation > 0;
            int currentAnimTime = owner.itemAnimation;

            // Detect new attack start (including auto-reuse re-swings)
            bool newAttack = isAttacking && (!wasOwnerAttacking || currentAnimTime > lastAttackAnimTime);
            wasOwnerAttacking = isAttacking;
            lastAttackAnimTime = currentAnimTime;

            if (newAttack && attackCooldown <= 0)
            {
                attackCooldown = AttackCooldownMax;

                if (Variant == ImbuedSummonVariant.Justice)
                    FireJusticeBeam(target);
                else
                    SpawnMirroredWhipAttack(owner, target);
            }
        }

        private void SpawnMirroredWhipAttack(Player owner, NPC target)
        {
            if (Main.myPlayer != Projectile.owner)
                return;

            Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            int damage = Projectile.damage;
            float knockback = 1f;
            float shootSpeed = 4f;

            int whipId = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                direction * shootSpeed,
                ModContent.ProjectileType<RoaringSummonWhipAttack>(),
                damage,
                knockback,
                Projectile.owner,
                ai1: Projectile.whoAmI
            );

            if (whipId >= 0 && whipId < Main.maxProjectiles && Main.netMode == NetmodeID.MultiplayerClient)
            {
                Main.projectile[whipId].netUpdate = true;
                NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, whipId);
            }
        }

        private void FireJusticeBeam(NPC target)
        {
            if (Main.myPlayer != Projectile.owner)
                return;

            // Slight offset from clone position
            Vector2 startPos = Projectile.Center + Main.rand.NextVector2Circular(15f, 15f);

            int beam = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                startPos,
                Vector2.Zero,
                ModContent.ProjectileType<JusticeBeam>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner,
                target.whoAmI,
                startPos.X
            );

            if (beam >= 0 && beam < Main.maxProjectiles)
            {
                Main.projectile[beam].localAI[0] = startPos.Y;
                Main.projectile[beam].netUpdate = true;
            }
        }

        // =========================================================
        // Movement
        // =========================================================

        private void IdleAtPlayer(Player owner)
        {
            jitterTimer++;
            if (jitterTimer >= 6)
            {
                jitterTimer = 0;
                jitterOffset = new Vector2(
                    Main.rand.NextFloat(-IdleJitterRange, IdleJitterRange),
                    Main.rand.NextFloat(-IdleJitterRange, IdleJitterRange)
                );
            }

            transitionProgress = MathHelper.Clamp(transitionProgress + 0.015f, 0f, 1f);

            Vector2 targetPos = owner.Center + jitterOffset;
            float lerpSpeed = MathHelper.Lerp(0.03f, 0.15f, transitionProgress);
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, lerpSpeed);
            Projectile.velocity = Vector2.Zero;
        }

        private void MirrorMovementAroundTarget(Player owner, NPC target)
        {
            List<Projectile> clones = new List<Projectile>();
            int myIndex = 0;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == Projectile.owner && proj.type == Projectile.type)
                {
                    if (proj.whoAmI == Projectile.whoAmI)
                        myIndex = clones.Count;
                    clones.Add(proj);
                }
            }

            int totalMembers = clones.Count + 1;
            int formationIndex = myIndex + 1;

            Vector2 playerToTarget = target.Center - owner.Center;
            Vector2 mirroredPos = GetFormationPosition(owner, target.Center, formationIndex, totalMembers, playerToTarget);

            float lerpSpeed = MathHelper.Lerp(0.05f, 0.15f, transitionProgress);
            Projectile.Center = Vector2.Lerp(Projectile.Center, mirroredPos, lerpSpeed);
            Projectile.velocity = Vector2.Zero;
        }

        private Vector2 GetFormationPosition(Player owner, Vector2 targetCenter, int index, int total, Vector2 playerToTarget)
        {
            float distance = playerToTarget.Length();
            float playerAngle = playerToTarget.ToRotation() + MathHelper.Pi;

            if (index == 0) return owner.Center;

            int numClones = total - 1;
            int cloneIndex = index - 1;

            if (numClones == 1)
            {
                float oppositeAngle = playerAngle + MathHelper.Pi;
                return targetCenter + new Vector2((float)Math.Cos(oppositeAngle), (float)Math.Sin(oppositeAngle)) * distance;
            }
            else
            {
                float angleStep = MathHelper.TwoPi / total;
                float cloneAngle = playerAngle + (index * angleStep);
                return targetCenter + new Vector2((float)Math.Cos(cloneAngle), (float)Math.Sin(cloneAngle)) * distance;
            }
        }

        // =========================================================
        // Kindness: Orbit allies + healing pickups
        // =========================================================

        private void HandleKindnessAI(Player owner)
        {
            // Find and assign an ally
            AssignKindnessAlly(owner);

            Player ally = null;
            if (kindnessAllyIndex >= 0 && kindnessAllyIndex < Main.maxPlayers)
            {
                Player candidate = Main.player[kindnessAllyIndex];
                if (candidate.active && !candidate.dead)
                    ally = candidate;
            }

            if (ally != null)
            {
                // Orbit near the assigned ally
                int myIndex = GetMyCloneIndex();
                int totalKindness = GetTotalClonesOfType();
                float angle = totalKindness > 1 ? myIndex * (MathHelper.TwoPi / totalKindness) : 0f;
                Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 60f;
                Vector2 targetPos = ally.Center + offset;

                Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.1f);
                Projectile.velocity = Vector2.Zero;
                Projectile.spriteDirection = (ally.Center.X > Projectile.Center.X) ? 1 : -1;

                // Throw healing pickup every second (only if ally is missing health)
                kindnessTimer++;
                if (kindnessTimer >= KindnessPickupInterval)
                {
                    kindnessTimer = 0;
                    if (ally.statLife < ally.statLifeMax2)
                        SpawnKindnessPickup(ally);
                }

                // Still trigger whip attacks at nearby enemies when player attacks
                NPC nearEnemy = FindNearestEnemy(Projectile.Center, 600f);
                if (nearEnemy != null)
                    CheckAndTriggerAttack(owner, nearEnemy);
            }
            else
            {
                IdleAtPlayer(owner);
                Projectile.spriteDirection = owner.direction;
            }

            Lighting.AddLight(Projectile.Center, GetTraitColor().ToVector3() * 0.4f);
        }

        private void AssignKindnessAlly(Player owner)
        {
            // Build list of valid allies
            List<int> allies = new List<int>();
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;
                if (i == Projectile.owner || (owner.team != 0 && p.team == owner.team))
                    allies.Add(i);
            }

            if (allies.Count == 0)
            {
                kindnessAllyIndex = -1;
                return;
            }

            // Distribute clones among allies
            int myIndex = GetMyCloneIndex();
            kindnessAllyIndex = allies[myIndex % allies.Count];
        }

        private void SpawnKindnessPickup(Player ally)
        {
            if (Main.myPlayer != Projectile.owner)
                return;

            Vector2 toAlly = (ally.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            Vector2 vel = toAlly * 4f + new Vector2(0, -3f);

            int p = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                vel,
                ModContent.ProjectileType<KindnessPickup>(),
                0,
                0f,
                Projectile.owner
            );

            if (p >= 0 && p < Main.maxProjectiles)
                Main.projectile[p].netUpdate = true;
        }

        // =========================================================
        // Bravery: Formation + periodic dash for melee impact
        // =========================================================

        private void HandleBraveryMovement(Player owner, NPC target)
        {
            if (braveryDashing)
            {
                // Dash toward target
                braveryDashTime++;
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Projectile.velocity = toTarget * 20f;

                // End dash when close or timeout
                if (Vector2.Distance(Projectile.Center, target.Center) < 40f || braveryDashTime >= BraveryDashDuration)
                {
                    braveryDashing = false;
                    braveryDashTime = 0;
                    braveryDashTimer = 0;
                    Projectile.velocity = Vector2.Zero;
                    Projectile.netUpdate = true;
                }
            }
            else
            {
                // Normal formation movement
                MirrorMovementAroundTarget(owner, target);

                // Count down to next dash
                braveryDashTimer++;
                if (braveryDashTimer >= BraveryDashInterval)
                {
                    braveryDashing = true;
                    braveryDashTime = 0;
                    Projectile.netUpdate = true;
                }
            }
        }

        // =========================================================
        // Helpers
        // =========================================================

        private bool CheckActive(Player owner)
        {
            if (owner.dead || !owner.active)
            {
                owner.ClearBuff(GetBuffType());
                return false;
            }

            if (owner.HasBuff(GetBuffType()))
            {
                Projectile.timeLeft = 2;
            }
            else
            {
                Projectile.Kill();
                return false;
            }

            return true;
        }

        private NPC FindNearestEnemy(Vector2 position, float range)
        {
            NPC best = null;
            float bestDist = range;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && !npc.dontTakeDamage && npc.lifeMax > 5)
                {
                    float dist = Vector2.Distance(position, npc.Center);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = npc;
                    }
                }
            }
            return best;
        }

        private int GetMyCloneIndex()
        {
            int index = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == Projectile.owner && proj.type == Projectile.type)
                {
                    if (proj.whoAmI == Projectile.whoAmI)
                        return index;
                    index++;
                }
            }
            return 0;
        }

        private int GetTotalClonesOfType()
        {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == Projectile.owner && proj.type == Projectile.type)
                    count++;
            }
            return count;
        }

        // =========================================================
        // Damage
        // =========================================================

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Bravery: full damage during dash, reduced otherwise
            if (Variant == ImbuedSummonVariant.Bravery)
            {
                if (!braveryDashing)
                    modifiers.FinalDamage *= 0.25f;
                return;
            }

            // All others: reduced contact damage (whip attacks are separate projectiles)
            modifiers.FinalDamage *= 0.25f;
        }

        // =========================================================
        // Drawing (Roaring Armor silhouette with trait color)
        // =========================================================

        public override bool PreDraw(ref Color lightColor)
        {
            Player owner = Main.player[Projectile.owner];
            SpriteBatch spriteBatch = Main.spriteBatch;

            Vector2 position = Projectile.Center - Main.screenPosition;
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Color traitColor = GetTraitColor();
            Color shadowColor = traitColor * 0.9f;
            Color outlineColor = Color.White * 0.9f;

            Texture2D headTex = ModContent.Request<Texture2D>("DeterministicChaos/Content/Items/Armor/RoaringHelmet_Head", AssetRequestMode.ImmediateLoad).Value;
            Texture2D headExtTex = ModContent.Request<Texture2D>("DeterministicChaos/Content/Items/Armor/RoaringHelmet_Extension", AssetRequestMode.ImmediateLoad).Value;
            Texture2D bodyTex = ModContent.Request<Texture2D>("DeterministicChaos/Content/Items/Armor/RoaringBreastplate_Body", AssetRequestMode.ImmediateLoad).Value;
            Texture2D legsTex = ModContent.Request<Texture2D>("DeterministicChaos/Content/Items/Armor/RoaringLeggings_Legs", AssetRequestMode.ImmediateLoad).Value;

            int frameHeight = 56;
            int frameWidth = 40;

            int headFrameY = owner.headFrame.Height > 0 ? owner.headFrame.Y / owner.headFrame.Height : 0;
            int legFrameY = owner.legFrame.Height > 0 ? owner.legFrame.Y / owner.legFrame.Height : 0;

            Rectangle headFrame = new Rectangle(0, headFrameY * frameHeight, frameWidth, frameHeight);
            Rectangle bodyFrame = new Rectangle(0, 0, frameWidth, frameHeight);
            Rectangle legFrame = new Rectangle(0, legFrameY * frameHeight, frameWidth, frameHeight);

            Vector2 origin = new Vector2(frameWidth / 2, frameHeight / 2);
            Vector2 extOffset = new Vector2(0, -3);

            // Draw legs
            DrawPieceWithOutline(spriteBatch, legsTex, position, legFrame, shadowColor, outlineColor, origin, effects);
            // Draw body
            DrawPieceWithOutline(spriteBatch, bodyTex, position, bodyFrame, shadowColor, outlineColor, origin, effects);
            // Draw head
            DrawPieceWithOutline(spriteBatch, headTex, position, headFrame, shadowColor, outlineColor, origin, effects);
            // Draw head extension
            DrawPieceWithOutline(spriteBatch, headExtTex, position + extOffset, headFrame, shadowColor, outlineColor, origin, effects);

            return false;
        }

        private void DrawPieceWithOutline(SpriteBatch spriteBatch, Texture2D tex, Vector2 position, Rectangle frame, Color shadowColor, Color outlineColor, Vector2 origin, SpriteEffects effects)
        {
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    if (x == 0 && y == 0) continue;
                    Vector2 offset = new Vector2(x, y);
                    spriteBatch.Draw(tex, position + offset, frame, outlineColor, 0f, origin, 1f, effects, 0f);
                }
            }
            spriteBatch.Draw(tex, position, frame, shadowColor, 0f, origin, 1f, effects, 0f);
        }
    }

    // --- Concrete variants ---

    public class DeterminationSummonProjectile : ImbuedSummonProjectile
    {
        protected override ImbuedSummonVariant Variant => ImbuedSummonVariant.Determination;
        protected override int GetBuffType() => ModContent.BuffType<Buffs.DeterminationSummonBuff>();
    }

    public class IntegritySummonProjectile : ImbuedSummonProjectile
    {
        protected override ImbuedSummonVariant Variant => ImbuedSummonVariant.Integrity;
        protected override int GetBuffType() => ModContent.BuffType<Buffs.IntegritySummonBuff>();
    }

    public class PatienceSummonProjectile : ImbuedSummonProjectile
    {
        protected override ImbuedSummonVariant Variant => ImbuedSummonVariant.Patience;
        protected override int GetBuffType() => ModContent.BuffType<Buffs.PatienceSummonBuff>();
    }

    public class PerseveranceSummonProjectile : ImbuedSummonProjectile
    {
        protected override ImbuedSummonVariant Variant => ImbuedSummonVariant.Perseverance;
        protected override int GetBuffType() => ModContent.BuffType<Buffs.PerseveranceSummonBuff>();
    }

    public class KindnessSummonProjectile : ImbuedSummonProjectile
    {
        protected override ImbuedSummonVariant Variant => ImbuedSummonVariant.Kindness;
        protected override int GetBuffType() => ModContent.BuffType<Buffs.KindnessSummonBuff>();
    }

    public class JusticeSummonProjectile : ImbuedSummonProjectile
    {
        protected override ImbuedSummonVariant Variant => ImbuedSummonVariant.Justice;
        protected override int GetBuffType() => ModContent.BuffType<Buffs.JusticeSummonBuff>();
    }

    public class BraverySummonProjectile : ImbuedSummonProjectile
    {
        protected override ImbuedSummonVariant Variant => ImbuedSummonVariant.Bravery;
        protected override int GetBuffType() => ModContent.BuffType<Buffs.BraverySummonBuff>();
    }
}
