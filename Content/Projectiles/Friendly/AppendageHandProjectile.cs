using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    
    // Summon minion that renders two Titan hands flanking the player.
    // Hands fire lasers every 3 seconds at the nearest enemy or whip-tagged target.
    // Takes 2 minion slots; only one pair may be active at a time.
    
    public class AppendageHandProjectile : ModProjectile
    {
        // ── Scale & positioning ──────────────────────────────────────
        private const float HAND_SCALE = 0.4f;
        private const float IDLE_OFFSET_X = 120f;
        private const float IDLE_OFFSET_Y = -10f;
        private const float IDLE_SWAY_AMP = 6f;

        // ── Laser constants ──────────────────────────────────────────
        private const int LASER_COOLDOWN = 180;        // 3 seconds between laser sequences
        private const int LASER_CHARGE_TICKS = 30;     // 0.5s charge-up
        private const int LASER_FIRE_TICKS = 60;       // 1s beam duration
        private const float TARGET_RANGE = 800f;

        // ── Spring physics ───────────────────────────────────────────
        private const float SPRING_STIFFNESS = 0.1f;
        private const float SPRING_DAMPING = 0.82f;

        // ── Sprite dimensions ────────────────────────────────────────
        private const int HAND_W = 202;
        private const int HAND_H = 206;
        private const int FINGER_W = 166;
        private const int FINGER_H = 48;
        private const int FINGER_FRAMES = 8;

        // Finger anchor offsets from hand center (same as TitanHand.FingerOffsets)
        private static readonly Vector2[] FingerOffsets = new Vector2[]
        {
            new Vector2(31, -1),
            new Vector2(43, 47),
            new Vector2(43, 101),
            new Vector2(17, 149),
        };

        // ── Hand state enum ──────────────────────────────────────────
        private enum HandState { Idle, LaserCharge, LaserFire }

        // ── Per-hand tracking ────────────────────────────────────────
        public Vector2 LeftHandPos;
        public Vector2 RightHandPos;
        private float leftHandRot;
        private float rightHandRot;
        private Vector2 leftVel;
        private Vector2 rightVel;
        private HandState leftState = HandState.Idle;
        private HandState rightState = HandState.Idle;

        // ── Laser state ─────────────────────────────────────────────
        private int laserCooldown = LASER_COOLDOWN / 2; // First laser fires faster
        private int laserTimer;
        private int laserTargetNPC = -1;

        // ── Finger animation ────────────────────────────────────────
        private readonly float[] fingerTimers = new float[8];
        private readonly int[] fingerFrames = new int[8];

        public override string Texture => "DeterministicChaos/Content/NPCs/Bosses/TitanHand";

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = 1;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Projectile.type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.minionSlots = 2f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.hide = true;
        }

        // Draw hands on top of everything (above lasers)
        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI)
        {
            overPlayers.Add(index);
        }

        public override bool? CanCutTiles() => false;

        public override bool MinionContactDamage() => false;

        // ─────────────────────────────────────────────────────────────
        //  AI
        // ─────────────────────────────────────────────────────────────
        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];

            if (!CheckActive(owner))
                return;

            Projectile.timeLeft = 2;
            Projectile.Center = owner.Center;

            UpdateFingerAnimations();

            // Idle target positions
            float sway = (float)Math.Sin(Main.GameUpdateCount * 0.03f) * IDLE_SWAY_AMP;
            Vector2 leftIdle = owner.Center + new Vector2(-IDLE_OFFSET_X, IDLE_OFFSET_Y + sway);
            Vector2 rightIdle = owner.Center + new Vector2(IDLE_OFFSET_X, IDLE_OFFSET_Y - sway);

            // Initialise hand positions on first tick
            if (Projectile.localAI[0] == 0f)
            {
                LeftHandPos = leftIdle;
                RightHandPos = rightIdle;
                leftHandRot = MathHelper.PiOver2;    // Flipped sprite, +90° = faces up
                rightHandRot = -MathHelper.PiOver2;  // Normal sprite, -90° = faces up
                Projectile.localAI[0] = 1f;
            }

            // Find target: prefer whip-tagged, else nearest enemy
            int targetNPC = GetTarget(owner);

            // ── Laser logic ──────────────────────────────────────────
            bool inLaser = leftState == HandState.LaserCharge || leftState == HandState.LaserFire
                        || rightState == HandState.LaserCharge || rightState == HandState.LaserFire;

            if (inLaser)
            {
                UpdateLaserState(owner, leftIdle, rightIdle);
            }
            else if (targetNPC != -1)
            {
                laserCooldown--;
                if (laserCooldown <= 0)
                {
                    leftState = HandState.LaserCharge;
                    rightState = HandState.LaserCharge;
                    laserTimer = 0;
                    laserTargetNPC = targetNPC;

                    if (Main.netMode != NetmodeID.Server)
                    {
                        SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/TitanLaserCharge")
                        {
                            Volume = 0.3f
                        }, owner.Center);
                    }
                }
            }

            // ── Idle spring movement ────────────────────────────────
            if (leftState == HandState.Idle)
            {
                SpringToward(ref LeftHandPos, ref leftVel, leftIdle);
                leftHandRot = LerpAngle(leftHandRot, MathHelper.PiOver2, 0.08f);
            }
            if (rightState == HandState.Idle)
            {
                SpringToward(ref RightHandPos, ref rightVel, rightIdle);
                rightHandRot = LerpAngle(rightHandRot, -MathHelper.PiOver2, 0.08f);
            }

            Lighting.AddLight(LeftHandPos, 0.3f, 0.3f, 0.4f);
            Lighting.AddLight(RightHandPos, 0.3f, 0.3f, 0.4f);
        }

        // ─────────────────────────────────────────────────────────────
        //  Target selection: whip-tagged > nearest enemy
        // ─────────────────────────────────────────────────────────────
        private int GetTarget(Player owner)
        {
            int whipTarget = owner.MinionAttackTargetNPC;
            if (whipTarget != -1 && whipTarget < Main.maxNPCs && Main.npc[whipTarget].active && !Main.npc[whipTarget].friendly)
                return whipTarget;

            return FindNearestEnemy(owner.Center, TARGET_RANGE);
        }

        // ─────────────────────────────────────────────────────────────
        //  Laser
        // ─────────────────────────────────────────────────────────────
        private void UpdateLaserState(Player owner, Vector2 leftIdle, Vector2 rightIdle)
        {
            laserTimer++;

            NPC target = null;
            if (laserTargetNPC >= 0 && laserTargetNPC < Main.maxNPCs && Main.npc[laserTargetNPC].active)
                target = Main.npc[laserTargetNPC];

            if (target == null)
            {
                leftState = HandState.Idle;
                rightState = HandState.Idle;
                laserCooldown = LASER_COOLDOWN;
                return;
            }

            // Left hand is flipped (forward = Pi), right hand is normal (forward = 0)
            float leftAim = (target.Center - LeftHandPos).ToRotation() - MathHelper.Pi;
            float rightAim = (target.Center - RightHandPos).ToRotation();

            if (leftState == HandState.LaserCharge || rightState == HandState.LaserCharge)
            {
                leftHandRot = LerpAngle(leftHandRot, leftAim, 0.12f);
                rightHandRot = LerpAngle(rightHandRot, rightAim, 0.12f);
                SpringToward(ref LeftHandPos, ref leftVel, leftIdle);
                SpringToward(ref RightHandPos, ref rightVel, rightIdle);

                if (laserTimer >= LASER_CHARGE_TICKS)
                {
                    leftState = HandState.LaserFire;
                    rightState = HandState.LaserFire;
                    laserTimer = 0;

                    if (Projectile.owner == Main.myPlayer)
                    {
                        Projectile.NewProjectile(
                            Projectile.GetSource_FromThis(),
                            LeftHandPos, Vector2.Zero,
                            ModContent.ProjectileType<AppendageLaser>(),
                            Projectile.damage, Projectile.knockBack,
                            Projectile.owner,
                            ai0: Projectile.whoAmI, ai1: 0f);

                        Projectile.NewProjectile(
                            Projectile.GetSource_FromThis(),
                            RightHandPos, Vector2.Zero,
                            ModContent.ProjectileType<AppendageLaser>(),
                            Projectile.damage, Projectile.knockBack,
                            Projectile.owner,
                            ai0: Projectile.whoAmI, ai1: 1f);
                    }
                }
            }
            else // LaserFire
            {
                leftHandRot = LerpAngle(leftHandRot, leftAim, 0.15f);
                rightHandRot = LerpAngle(rightHandRot, rightAim, 0.15f);
                SpringToward(ref LeftHandPos, ref leftVel, leftIdle);
                SpringToward(ref RightHandPos, ref rightVel, rightIdle);

                if (laserTimer >= LASER_FIRE_TICKS)
                {
                    leftState = HandState.Idle;
                    rightState = HandState.Idle;
                    laserCooldown = LASER_COOLDOWN;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Drawing, hands draw on top (default projectile layer)
        // ─────────────────────────────────────────────────────────────
        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch sb = Main.spriteBatch;

            Texture2D handTex = ModContent.Request<Texture2D>(
                "DeterministicChaos/Content/NPCs/Bosses/TitanHand", AssetRequestMode.ImmediateLoad).Value;
            Texture2D fingerTex = ModContent.Request<Texture2D>(
                "DeterministicChaos/Content/NPCs/Bosses/TitanFinger", AssetRequestMode.ImmediateLoad).Value;

            Vector2 handOrigin = new Vector2(HAND_W / 2f, HAND_H / 2f);

            // Left side: flipped sprite (left-hand model), fingers extend left
            DrawHand(sb, handTex, fingerTex, handOrigin, LeftHandPos, leftHandRot, true, 0);

            // Right side: normal sprite (right-hand model), fingers extend right
            DrawHand(sb, handTex, fingerTex, handOrigin, RightHandPos, rightHandRot, false, 4);

            return false;
        }

        private void DrawHand(SpriteBatch sb, Texture2D handTex, Texture2D fingerTex,
            Vector2 handOrigin, Vector2 handPos, float handRot, bool isLeft, int fingerStart)
        {
            Vector2 drawPos = handPos - Main.screenPosition;
            SpriteEffects fx = isLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Color drawColor = Lighting.GetColor((int)(handPos.X / 16f), (int)(handPos.Y / 16f));

            // Draw fingers behind hand
            for (int f = 0; f < 4; f++)
            {
                Vector2 anchor = FingerOffsets[f];
                anchor.Y -= FINGER_H * 0.5f; // vertical offset like TitanFinger

                if (isLeft)
                    anchor.X = -anchor.X;

                Vector2 worldAnchor = handPos + (anchor * HAND_SCALE).RotatedBy(handRot);

                Vector2 centerOff = isLeft
                    ? new Vector2(-FINGER_W / 2f, 0f)
                    : new Vector2(FINGER_W / 2f, 0f);

                Vector2 fingerCenter = worldAnchor + (centerOff * HAND_SCALE).RotatedBy(handRot);

                int frame = fingerFrames[fingerStart + f];
                Rectangle src = new Rectangle(frame * FINGER_W, 0, FINGER_W, FINGER_H);
                Vector2 fOrigin = new Vector2(FINGER_W / 2f, FINGER_H / 2f);

                sb.Draw(fingerTex, fingerCenter - Main.screenPosition, src, drawColor,
                    handRot, fOrigin, HAND_SCALE, fx, 0f);
            }

            // Draw hand on top
            sb.Draw(handTex, drawPos, null, drawColor, handRot, handOrigin, HAND_SCALE, fx, 0f);
        }

        // ─────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────
        private bool CheckActive(Player owner)
        {
            if (owner.dead || !owner.active)
            {
                owner.ClearBuff(ModContent.BuffType<Buffs.AppendageBuff>());
                return false;
            }

            if (owner.HasBuff(ModContent.BuffType<Buffs.AppendageBuff>()))
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

        private void UpdateFingerAnimations()
        {
            for (int i = 0; i < 8; i++)
            {
                fingerTimers[i] += 10f / 60f;
                if (fingerTimers[i] >= 1f)
                {
                    fingerTimers[i] -= 1f;
                    fingerFrames[i] = (fingerFrames[i] + 1) % FINGER_FRAMES;
                }
            }
        }

        private static void SpringToward(ref Vector2 pos, ref Vector2 vel, Vector2 target)
        {
            Vector2 diff = target - pos;
            vel += diff * SPRING_STIFFNESS;
            vel *= SPRING_DAMPING;
            pos += vel;
        }

        private static int FindNearestEnemy(Vector2 center, float range)
        {
            int nearest = -1;
            float nearestDist = range;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;

                float dist = Vector2.Distance(center, npc.Center);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = i;
                }
            }

            return nearest;
        }

        private static float LerpAngle(float current, float target, float amount)
        {
            float diff = MathHelper.WrapAngle(target - current);
            return current + diff * amount;
        }
    }
}
