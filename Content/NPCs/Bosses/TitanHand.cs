using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Enemy;
using DeterministicChaos.Content.VFX;
using DeterministicChaos.Content.Systems;
using System;
using System.IO;
using Terraria.Graphics.CameraModifiers;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    // A hand attached to the TitanBody. Two are spawned: left (slot 0) and right (slot 1).
    // ai[0] = parent TitanBody whoAmI
    // ai[1] = hand slot (0 = left, 1 = right)
    // The sprite faces right with thumb on top, slot 1 (right) uses base sprite,
    // slot 0 (left) is flipped horizontally. Hands face upward on either side of the Titan.
    // Each hand spawns 4 TitanFinger children.
    // Supports multiple behaviors via HandBehavior enum.
    public class TitanHand : ModNPC
    {
        // ── Behavior system ──────────────────────────────────────────
        public enum HandBehavior
        {
            Default,
            LaserAttack,     // Parkour laser (narrow)
            ArenaLaser,      // Arena laser (2x wide)
            DualLaser,       // Two narrow lasers at once with spread
            FingerLaunch,    // Launch fingers at player one by one
            Slam,            // Position above, pause, slam down
            FloorIsLava,     // Raise up + telegraph + floor kill
        }

        public HandBehavior CurrentBehavior { get; set; } = HandBehavior.Default;

        // ── Laser attack constants ───────────────────────────────────
        private const float LASER_AIM_DURATION = 0.5f;     // Seconds to aim at player before indicator
        private const float LASER_INDICATOR_DURATION = 0.7f; // Seconds the indicator is shown
        private const float LASER_BEAM_DURATION = 0.5f;      // Seconds the beam lasts
        private const float LASER_HOLD_DURATION = 0.2f;      // Seconds to hold position after beam before returning
        private const float LASER_RETURN_DURATION = 0.4f;    // Seconds to return to default pose
        private const float HAND_FLIP_SPEED = 0.15f;        // Rotation lerp speed for flipping

        // Laser attack state (shared by LaserAttack and ArenaLaser)
        private int laserSubPhase = 0;    // 0=aim, 1=indicator, 2=beam, 3=hold, 4=return
        private float laserSubTimer = 0f;
        private float laserTargetAngle = MathHelper.PiOver2; // Angle pointing downward
        private int laserIndicatorIdx = -1;
        private int laserBeamIdx = -1;
        // Dual laser (second beam/indicator)
        private float laserTargetAngle2 = MathHelper.PiOver2;
        private int laserIndicatorIdx2 = -1;
        private int laserBeamIdx2 = -1;
        private float laserReturnStartRotation = 0f;
        private float laserAimStartRotation = 0f;          // Hand rotation when aim phase began
        private bool laserAimLocked = false;                 // True after aim angle is locked on first tick
        private Vector2 laserFireOffset = Vector2.Zero;   // Position offset during laser attack
        private Vector2 laserStartPos = Vector2.Zero;      // Hand's actual position when laser started

        // ── Finger Launch attack constants ────────────────────────────
        private const float FINGER_APPROACH_DURATION = 0.5f;
        private const float FINGER_LAUNCH_INTERVAL = 1f;       // 1 finger per second
        private const float FINGER_REGROW_DURATION = 1f;
        private const float FINGER_RETURN_DURATION = 0.5f;
        private const float FINGER_LAUNCH_DISTANCE = 320f;     // 20 tiles = 320px

        // Finger Launch state
        private int fingerLaunchSubPhase = 0;    // 0=approach, 1=launching, 2=regrow, 3=return
        private float fingerLaunchSubTimer = 0f;
        private int fingerNextLaunchIndex = 0;
        private Vector2 fingerLaunchTargetPos = Vector2.Zero;
        private int fingerLaunchLockedPlayer = -1;
        private Vector2 fingerReturnStartPos = Vector2.Zero;
        private float fingerReturnStartRotation = 0f;
        public bool[] FingerHidden = new bool[FINGER_COUNT]; // Tracks which fingers have been launched

        // ── Slam attack constants ────────────────────────────────────
        private const float SLAM_FOLLOW_DURATION = 1.5f;
        private const float SLAM_PAUSE_DURATION = 0.5f;
        private const float SLAM_SPEED = 60f;                  // Pixels per tick downward
        private const float SLAM_HOLD_DURATION = 0.5f;
        private const float SLAM_RETURN_DURATION = 0.5f;
        private const float SLAM_ABOVE_OFFSET = 320f;          // 20 tiles above player
        private const int SLAM_SHOCKWAVE_COUNT = 8;

        // Slam state
        private int slamSubPhase = 0;     // 0=follow, 1=pause, 2=slam, 3=hold, 4=return
        private float slamSubTimer = 0f;
        private float slamGroundY = 0f;
        private Vector2 slamPausePos = Vector2.Zero;
        private Vector2 slamReturnStartPos = Vector2.Zero;
        private float slamReturnStartRotation = 0f;

        // ── FloorIsLava attack constants ─────────────────────────────
        private const float FLOOR_RISE_DURATION = 0.5f;
        private const float FLOOR_TELEGRAPH_DURATION = 1.0f;
        private const float FLOOR_DANGER_DURATION = 0.5f;
        private const float FLOOR_HOLD_DURATION = 0.5f;
        private const float FLOOR_RETURN_DURATION = 0.5f;
        private const float FLOOR_RISE_Y_OFFSET = -500f;   // Rise 500px above default pos

        // FloorIsLava state
        private int floorSubPhase = 0;    // 0=rise, 1=telegraph, 2=danger, 3=hold, 4=return
        private float floorSubTimer = 0f;
        private Vector2 floorRiseStartPos = Vector2.Zero;
        private Vector2 floorRiseTargetPos = Vector2.Zero;
        private Vector2 floorReturnStartPos = Vector2.Zero;
        private float floorReturnStartRotation = 0f;

        // ── Final-stand death lasers ──────────────────────────────────
        // -1 = uninitialized. Left fires at t=0,2,4... right at t=1,3,5...  (combined: 1/s)
        private float deathLaserTimer = -1f;
        // Full beam laser during final stand (indicator + beam aimed at tower floor)
        // Left fires at t=0; right fires at t=8s (staggered); period = 16s per hand
        private float finalBeamLaserTimer = -1f;

        // ── Pending laser queue (for damage phase double-laser) ──────
        public bool PendingSecondLaser { get; set; } = false;

        // ── Default behavior constants ───────────────────────────────
        // Positioning relative to TitanBody center
        private const float DEFAULT_OFFSET_X = 380f;  // Pixels to the side of the body
        private const float DEFAULT_OFFSET_Y = 200f;  // Pixels below body center (resting on tower top)
        private const float DEFAULT_FOLLOW_LERP = 0.1f;
        private const float DEFAULT_ROTATION_LERP = 0.14f;
        private const float IDLE_SWAY_X_AMPLITUDE = 12f;
        private const float IDLE_SWAY_Y_AMPLITUDE = 8f;
        private const float IDLE_SWAY_ROTATION_AMPLITUDE = 0.05f;

        // ── Spring-based movement system ─────────────────────────────
        // All hand positioning uses spring physics, creating natural
        // overextension when reaching targets and gentle oscillation at rest.
        private Vector2 springVelocity = Vector2.Zero;
        private const float SPRING_STIFFNESS = 0.08f;      // Acceleration toward target per tick
        private const float SPRING_DAMPING = 0.78f;         // Velocity retention per tick (< 1)
        private const float SPRING_MAX_SPEED = 40f;          // Terminal velocity in px/tick
        private const float ATTACK_SWAY_SCALE = 0.4f;       // Sway amplitude during attacks (fraction of idle)

        // Sprite dimensions
        public const int SPRITE_WIDTH = 202;
        public const int SPRITE_HEIGHT = 206;

        public const int FINGER_COUNT = 4;

        // Finger anchor positions in hand sprite coords (from top-left), specified as bottom-left of finger.
        // Converted to offset from hand center (101, 103).
        public static readonly Vector2[] FingerOffsets = new Vector2[]
        {
            new Vector2(132 - 101, 102 - 103),  // Finger 0: (31, -1)
            new Vector2(144 - 101, 150 - 103),  // Finger 1: (43, 47)
            new Vector2(144 - 101, 204 - 103),  // Finger 2: (43, 101)
            new Vector2(118 - 101, 252 - 103),  // Finger 3: (17, 149)
        };

        public int HandSlot => (int)NPC.ai[1]; // 0 = left, 1 = right
        public bool IsLeft => HandSlot == 0;

        // True when the hand is in indicator, beam, or hold sub-phases (body should freeze).
        public bool IsLaserFiring => (CurrentBehavior == HandBehavior.LaserAttack || CurrentBehavior == HandBehavior.ArenaLaser || CurrentBehavior == HandBehavior.DualLaser)
            && laserSubPhase >= 1 && laserSubPhase <= 3;

        // True when the hand is performing any attack (not in Default state).
        public bool IsBusy => CurrentBehavior != HandBehavior.Default;

        // When true, the hand skips the return-to-default animation after an attack
        // and immediately becomes Default at its current position. Set by TitanBody
        // during the damage phase to chain attacks faster.
        public bool SkipReturnToDefault { get; set; } = false;

        // The hand's world rotation. Right hand: -PI/2 (faces up). Left hand: +PI/2 (faces up when flipped).
        public float HandRotation => IsLeft ? MathHelper.PiOver2 : -MathHelper.PiOver2;

        private bool fingersSpawned = false;

        // ── Multiplayer sync ─────────────────────────────────────────
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((byte)CurrentBehavior);
            // Laser state
            writer.Write((byte)laserSubPhase);
            writer.Write(laserSubTimer);
            writer.Write(laserTargetAngle);
            writer.Write(laserTargetAngle2);
            writer.Write(laserFireOffset.X);
            writer.Write(laserFireOffset.Y);
            writer.Write(laserReturnStartRotation);
            writer.Write(laserStartPos.X);
            writer.Write(laserStartPos.Y);
            // Finger launch state
            writer.Write((byte)fingerLaunchSubPhase);
            writer.Write(fingerLaunchSubTimer);
            writer.Write((byte)fingerNextLaunchIndex);
            writer.Write(fingerLaunchTargetPos.X);
            writer.Write(fingerLaunchTargetPos.Y);
            writer.Write(fingerReturnStartPos.X);
            writer.Write(fingerReturnStartPos.Y);
            writer.Write(fingerReturnStartRotation);
            writer.Write((sbyte)fingerLaunchLockedPlayer);
            byte fingerMask = 0;
            for (int i = 0; i < FINGER_COUNT; i++)
                if (FingerHidden[i]) fingerMask |= (byte)(1 << i);
            writer.Write(fingerMask);
            // Slam state
            writer.Write((byte)slamSubPhase);
            writer.Write(slamSubTimer);
            writer.Write(slamGroundY);
            writer.Write(slamPausePos.X);
            writer.Write(slamPausePos.Y);
            writer.Write(slamReturnStartPos.X);
            writer.Write(slamReturnStartPos.Y);
            writer.Write(slamReturnStartRotation);
            // FloorIsLava state
            writer.Write((byte)floorSubPhase);
            writer.Write(floorSubTimer);
            writer.Write(floorRiseStartPos.X);
            writer.Write(floorRiseStartPos.Y);
            writer.Write(floorRiseTargetPos.X);
            writer.Write(floorRiseTargetPos.Y);
            writer.Write(floorReturnStartPos.X);
            writer.Write(floorReturnStartPos.Y);
            writer.Write(floorReturnStartRotation);
            writer.Write(PendingSecondLaser);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            CurrentBehavior = (HandBehavior)reader.ReadByte();
            // Laser state
            laserSubPhase = reader.ReadByte();
            laserSubTimer = reader.ReadSingle();
            laserTargetAngle = reader.ReadSingle();
            laserTargetAngle2 = reader.ReadSingle();
            laserFireOffset = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            laserReturnStartRotation = reader.ReadSingle();
            laserStartPos = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            // Finger launch state
            fingerLaunchSubPhase = reader.ReadByte();
            fingerLaunchSubTimer = reader.ReadSingle();
            fingerNextLaunchIndex = reader.ReadByte();
            fingerLaunchTargetPos = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            fingerReturnStartPos = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            fingerReturnStartRotation = reader.ReadSingle();
            fingerLaunchLockedPlayer = reader.ReadSByte();
            byte fingerMask = reader.ReadByte();
            for (int i = 0; i < FINGER_COUNT; i++)
                FingerHidden[i] = (fingerMask & (1 << i)) != 0;
            // Slam state
            slamSubPhase = reader.ReadByte();
            slamSubTimer = reader.ReadSingle();
            slamGroundY = reader.ReadSingle();
            slamPausePos = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            slamReturnStartPos = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            slamReturnStartRotation = reader.ReadSingle();
            // FloorIsLava state
            int prevFloorPhase = floorSubPhase;
            floorSubPhase = reader.ReadByte();
            floorSubTimer = reader.ReadSingle();
            floorRiseStartPos = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            floorRiseTargetPos = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            floorReturnStartPos = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            floorReturnStartRotation = reader.ReadSingle();
            PendingSecondLaser = reader.ReadBoolean();

            // Sync the floor flash visual state on the client when phase changes
            if (Main.netMode != NetmodeID.Server && floorSubPhase != prevFloorPhase)
            {
                if (floorSubPhase == 1) // Telegraph
                    FloorIsLavaFlash.StartTelegraph();
                else if (floorSubPhase == 2) // Danger
                    FloorIsLavaFlash.StartDanger();
                else if (floorSubPhase >= 3) // Hold or Return
                    FloorIsLavaFlash.Stop();
            }
        }

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NoMultiplayerSmoothingByType[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 80;
            NPC.height = 80;
            NPC.damage = 0;
            NPC.defense = 30;
            NPC.lifeMax = 1;
            NPC.dontTakeDamage = true;
            NPC.immortal = true;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.npcSlots = 0f;
        }

        public override void AI()
        {
            int parentIdx = (int)NPC.ai[0];

            // Despawn if parent TitanBody is gone
            if (parentIdx < 0 || parentIdx >= Main.maxNPCs
                || !Main.npc[parentIdx].active
                || Main.npc[parentIdx].type != ModContent.NPCType<TitanBody>())
            {
                NPC.active = false;
                return;
            }

            NPC parent = Main.npc[parentIdx];

            // During the Titan's final stand, drop all attacks and convulse toward the body.
            if (parent.ModNPC is TitanBody titanBody && titanBody.IsInFinalStand)
            {
                // Allow a triggered beam laser to complete its full sequence during the final stand
                if (IsLaserFiring)
                    RunLaserAttackBehavior(parent);
                else
                    RunDeathShakeBehavior(parent, titanBody);
                return;
            }

            // Only deal contact damage during slam's active phases (slam down + hold)
            bool isSlamming = CurrentBehavior == HandBehavior.Slam && slamSubPhase >= 2 && slamSubPhase <= 3;
            NPC.damage = isSlamming ? TitanBody.ExpertDamage(100) : 0;

            // Spawn fingers on first tick
            if (!fingersSpawned && Main.netMode != NetmodeID.MultiplayerClient)
            {
                SpawnFingers();
                fingersSpawned = true;
            }

            // Dispatch to active behavior
            switch (CurrentBehavior)
            {
                case HandBehavior.Default:
                default:
                    RunDefaultBehavior(parent);
                    break;
                case HandBehavior.LaserAttack:
                case HandBehavior.ArenaLaser:
                case HandBehavior.DualLaser:
                    RunLaserAttackBehavior(parent);
                    break;
                case HandBehavior.FingerLaunch:
                    RunFingerLaunchBehavior(parent);
                    break;
                case HandBehavior.Slam:
                    RunSlamBehavior(parent);
                    break;
                case HandBehavior.FloorIsLava:
                    RunFloorIsLavaBehavior(parent);
                    break;
            }
        }

        // ── Behavior: Death Shake ─────────────────────────────────────
        // Hands tremble and slowly slump toward the body center during the final stand.
        private void RunDeathShakeBehavior(NPC parent, TitanBody titanBody)
        {
            NPC.damage = 0;
            NPC.dontTakeDamage = true;

            float t = MathHelper.Clamp(titanBody.finalStandTimer / TitanBody.FINAL_STAND_DURATION, 0f, 1f);

            // Gradually sink toward body center
            float sideSign  = IsLeft ? 1f : -1f;
            float targetX   = MathHelper.Lerp(parent.Center.X + sideSign * DEFAULT_OFFSET_X,
                                              parent.Center.X + sideSign * 60f, t);
            float targetY   = MathHelper.Lerp(parent.Center.Y + DEFAULT_OFFSET_Y,
                                              parent.Center.Y + 80f, t * t);

            // High-frequency shake that escalates as the Titan dies
            float shakeMag = MathHelper.Lerp(3f, 22f, t);
            float time     = (float)Main.GameUpdateCount * 0.35f;
            Vector2 shake  = new Vector2(
                (float)Math.Sin(time * 1.7f + NPC.whoAmI) * shakeMag,
                (float)Math.Cos(time * 2.3f + NPC.whoAmI) * shakeMag * 0.6f);

            UpdateSpringMovement(new Vector2(targetX, targetY) + shake);

            // Tilt inward and shudder
            float tiltTarget = sideSign * MathHelper.Lerp(0f, 0.35f, t * t);
            float jitter     = (float)Math.Sin(time * 4.1f) * MathHelper.Lerp(0.02f, 0.12f, t);
            NPC.rotation     = LerpAngle(NPC.rotation, HandRotation + tiltTarget + jitter, 0.12f);
            NPC.velocity     = Vector2.Zero;

            // Darkening flicker dust
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(NPC.Center - new Vector2(30f), 60, 60,
                    DustID.Smoke, Scale: Main.rand.NextFloat(0.6f, 1.4f));
                d.noGravity = false;
                d.velocity  = Main.rand.NextVector2Circular(2f, 2f);
            }

            // Alternating death lasers: left fires at t=0,2,4... right at t=1,3,5...
            if (deathLaserTimer < 0f)
                deathLaserTimer = IsLeft ? 0f : 1f;

            deathLaserTimer += 1f / 60f;
            if (deathLaserTimer >= 2f)
            {
                deathLaserTimer -= 2f;
                FireHandDeathLaser();
            }

            // Full beam laser at floor: left at t=0, right at t=8s, period 16s per hand
            if (finalBeamLaserTimer < 0f)
                finalBeamLaserTimer = IsLeft ? 0f : 8f;

            finalBeamLaserTimer += 1f / 60f;
            if (finalBeamLaserTimer >= 16f)
            {
                finalBeamLaserTimer -= 16f;
                // StartLaserAttack requires Default behavior, that is always the case here
                // since RunDeathShakeBehavior never changes CurrentBehavior.
                SkipReturnToDefault = true;
                StartLaserAttack();
            }
        }

        private void FireHandDeathLaser()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // Fire from the palm area
            Vector2 palmOffset = new Vector2(IsLeft ? -50f : 50f, 60f);
            Vector2 firePos = NPC.Center + palmOffset.RotatedBy(NPC.rotation - HandRotation);

            // Random direction within ±45° of straight down
            float angleOffset = Main.rand.NextFloat(-MathHelper.PiOver4, MathHelper.PiOver4);
            Vector2 dir = Vector2.UnitY.RotatedBy(angleOffset);

            int id = Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                firePos,
                dir * 14f,
                ModContent.ProjectileType<TitanFinalLaserBolt>(),
                TitanBody.ExpertDamage(50),
                2f,
                Main.myPlayer);

            // Disable tile collision so bolts pass through tower walls
            if (id >= 0 && id < Main.maxProjectiles)
                Main.projectile[id].tileCollide = false;
        }

        // ── Behavior: Default ────────────────────────────────────────
        // Hands sit at a fixed offset from the TitanBody, facing upward.
        private void RunDefaultBehavior(NPC parent)
        {
            Vector2 swayOffset = GetIdleSwayOffset();
            float swayRotation = GetIdleSwayRotation();

            // During damage phase (SkipReturnToDefault), stay between attacks
            // but still follow the Titan's X position so they drift with the body
            if (SkipReturnToDefault)
            {
                float sideSign = IsLeft ? 1f : -1f;
                float targetX = parent.Center.X + sideSign * DEFAULT_OFFSET_X + swayOffset.X * 0.7f;
                Vector2 target = new Vector2(targetX, NPC.Center.Y);
                UpdateSpringMovement(target);
                // Damp Y velocity so hands don't drift vertically between attacks
                springVelocity.Y *= 0.5f;
                NPC.rotation = LerpAngle(NPC.rotation, HandRotation + swayRotation * 0.75f, DEFAULT_ROTATION_LERP);
                NPC.velocity = Vector2.Zero;
                return;
            }

            float side = IsLeft ? 1f : -1f;
            Vector2 targetPos = parent.Center + new Vector2(side * DEFAULT_OFFSET_X, DEFAULT_OFFSET_Y) + swayOffset;

            // Spring toward default position, naturally overextends and settles
            UpdateSpringMovement(targetPos);

            NPC.rotation = LerpAngle(NPC.rotation, HandRotation + swayRotation, DEFAULT_ROTATION_LERP);
            NPC.velocity = Vector2.Zero;
        }

        private Vector2 GetIdleSwayOffset()
        {
            float sideSign = IsLeft ? 1f : -1f;
            float time = (float)Main.GameUpdateCount / 60f;
            float phase = NPC.whoAmI * 0.47f + (IsLeft ? 0f : 1.2f);

            float x = (float)Math.Sin(time * 1.2f + phase) * IDLE_SWAY_X_AMPLITUDE
                + (float)Math.Sin(time * 2.1f + phase * 0.5f) * 2.5f;
            float y = (float)Math.Cos(time * 1.05f + phase * 1.1f) * IDLE_SWAY_Y_AMPLITUDE
                + (float)Math.Sin(time * 1.7f + phase * 0.8f) * 1.5f;

            x *= sideSign;
            return new Vector2(x, y);
        }

        private float GetIdleSwayRotation()
        {
            float sideSign = IsLeft ? 1f : -1f;
            float time = (float)Main.GameUpdateCount / 60f;
            float phase = NPC.whoAmI * 0.35f + (IsLeft ? 0.2f : 1f);
            return (float)Math.Sin(time * 1.35f + phase) * IDLE_SWAY_ROTATION_AMPLITUDE * sideSign;
        }

        // Returns a smaller sway offset for use during attack behaviors.
        private Vector2 GetAttackSwayOffset()
        {
            return GetIdleSwayOffset() * ATTACK_SWAY_SCALE;
        }

        // Returns a smaller sway rotation for use during attack behaviors.
        private float GetAttackSwayRotation()
        {
            return GetIdleSwayRotation() * ATTACK_SWAY_SCALE;
        }

        // Moves NPC.Center toward the target using spring physics.
        // The spring naturally overshoots and settles, creating organic movement.
        private void UpdateSpringMovement(Vector2 target, float stiffness = SPRING_STIFFNESS)
        {
            Vector2 displacement = target - NPC.Center;
            springVelocity += displacement * stiffness;
            springVelocity *= SPRING_DAMPING;

            float speed = springVelocity.Length();
            if (speed > SPRING_MAX_SPEED)
                springVelocity *= SPRING_MAX_SPEED / speed;

            NPC.Center += springVelocity;
        }

        // ── Behavior: Laser Attack ───────────────────────────────────
        // Sequence: aim → indicator → beam → return to default
        // Hand moves 300px up + random X offset during attack.
        private void RunLaserAttackBehavior(NPC parent)
        {
            float sideSign = IsLeft ? 1f : -1f;
            Vector2 defaultPos = parent.Center + new Vector2(sideSign * DEFAULT_OFFSET_X, DEFAULT_OFFSET_Y);
            Vector2 firePos = defaultPos + laserFireOffset;

            laserSubTimer += 1f / 60f;

            // Position via spring physics, all phases spring toward their target
            switch (laserSubPhase)
            {
                case 0: // Aim, spring toward fire position
                    UpdateSpringMovement(firePos + GetAttackSwayOffset());
                    break;
                case 1: // Indicator, hold at fire position with sway
                case 2: // Beam, hold at fire position with sway
                case 3: // Hold, stay at fire position with sway
                    UpdateSpringMovement(firePos + GetAttackSwayOffset());
                    break;
                case 4: // Return, spring back to default position
                    UpdateSpringMovement(defaultPos + GetIdleSwayOffset());
                    break;
            }

            NPC.velocity = Vector2.Zero;

            // Execute phase-specific rotation and projectile logic
            switch (laserSubPhase)
            {
                case 0: LaserPhase_Aim(); break;
                case 1: LaserPhase_Indicator(); break;
                case 2: LaserPhase_Beam(); break;
                case 3: LaserPhase_Hold(); break;
                case 4: LaserPhase_Return(); break;
            }
        }

        // Computes the NPC rotation so the palm faces the given world angle.
        // Sprite palm faces right (angle 0); left hand is flipped horizontally.
        // Right hand: rotation = targetAngle. Left hand: rotation = targetAngle - PI.
        private float GetAimRotation(float targetAngle)
        {
            return IsLeft ? targetAngle - MathHelper.Pi : targetAngle;
        }

        // Shortest-path angle interpolation.
        private static float LerpAngle(float from, float to, float t)
        {
            float diff = MathHelper.WrapAngle(to - from);
            return from + diff * t;
        }

        private void LaserPhase_Aim()
        {
            // Lock target angle on first tick only, don't re-track every frame
            // so the easing can overshoot and settle visibly.
            // Calculate aim from firePos (where the beam will actually spawn),
            // not NPC.Center (which is still lerping toward firePos).
            if (!laserAimLocked)
            {
                laserAimLocked = true;

                int parentIdx = (int)NPC.ai[0];
                NPC parent = Main.npc[parentIdx];
                float sideSign = IsLeft ? 1f : -1f;
                Vector2 defaultPos = parent.Center + new Vector2(sideSign * DEFAULT_OFFSET_X, DEFAULT_OFFSET_Y);
                Vector2 firePos = defaultPos + laserFireOffset;

                // Final stand: aim at a random point on the tower floor instead of the player
                bool parentFinalStand = Main.npc[parentIdx].ModNPC is TitanBody tb && tb.IsInFinalStand;
                if (parentFinalStand && TitanSpawnCutscene.TowerPlaced)
                {
                    var towerTop = TitanSpawnCutscene.TowerTopLeft;
                    float floorY = (towerTop.Y + TitanSpawnCutscene.TOWER_HEIGHT) * 16f;
                    float floorX = Main.rand.NextFloat(towerTop.X * 16f, (towerTop.X + TitanSpawnCutscene.TOWER_WIDTH) * 16f);
                    laserTargetAngle = (new Vector2(floorX, floorY) - firePos).ToRotation();
                }
                else
                {
                    Player target = FindNearestPlayer();
                    if (target != null)
                    {
                        Vector2 toPlayer = target.Center - firePos;
                        laserTargetAngle = toPlayer.ToRotation();
                    }
                    else
                    {
                        laserTargetAngle = MathHelper.PiOver2; // Straight down
                    }
                }

                // DualLaser: spread aim angles wide and less accurate
                if (CurrentBehavior == HandBehavior.DualLaser)
                {
                    float spread = MathHelper.ToRadians(Main.rand.NextFloat(20f, 40f));
                    float baseAngle = laserTargetAngle + MathHelper.ToRadians(Main.rand.NextFloat(-15f, 15f));
                    laserTargetAngle = baseAngle - spread / 2f;
                    laserTargetAngle2 = baseAngle + spread / 2f;
                }

                laserAimStartRotation = NPC.rotation;
            }

            float aimRotation = GetAimRotation(laserTargetAngle);
            float t = MathHelper.Clamp(laserSubTimer / LASER_AIM_DURATION, 0f, 1f);
            // Back-out easing: single smooth overshoot then settle (no oscillation)
            float overshoot = 1.7f;
            float backT = 1f + (overshoot + 1f) * (float)Math.Pow(t - 1f, 3) + overshoot * (t - 1f) * (t - 1f);
            NPC.rotation = LerpAngle(laserAimStartRotation, aimRotation, backT);

            if (laserSubTimer >= LASER_AIM_DURATION)
            {
                laserSubTimer = 0f;
                laserSubPhase = 1;
                NPC.rotation = aimRotation;

                // Spawn indicator projectile(s)
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int idx = Projectile.NewProjectile(
                        NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<TitanLaserIndicator>(),
                        0, 0f, Main.myPlayer,
                        ai0: NPC.whoAmI, ai1: laserTargetAngle,
                        ai2: CurrentBehavior == HandBehavior.ArenaLaser ? 1f : 0f);
                    laserIndicatorIdx = idx;

                    // DualLaser: spawn second indicator
                    if (CurrentBehavior == HandBehavior.DualLaser)
                    {
                        int idx2 = Projectile.NewProjectile(
                            NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<TitanLaserIndicator>(),
                            0, 0f, Main.myPlayer,
                            ai0: NPC.whoAmI, ai1: laserTargetAngle2);
                        laserIndicatorIdx2 = idx2;
                    }
                }

                NPC.netUpdate = true;
            }
        }

        private void LaserPhase_Indicator()
        {
            NPC.rotation = GetAimRotation(laserTargetAngle) + GetAttackSwayRotation();

            if (laserSubTimer >= LASER_INDICATOR_DURATION)
            {
                laserSubTimer = 0f;
                laserSubPhase = 2;

                // Kill indicator(s)
                if (laserIndicatorIdx >= 0 && laserIndicatorIdx < Main.maxProjectiles
                    && Main.projectile[laserIndicatorIdx].active)
                {
                    Main.projectile[laserIndicatorIdx].Kill();
                }
                laserIndicatorIdx = -1;

                if (laserIndicatorIdx2 >= 0 && laserIndicatorIdx2 < Main.maxProjectiles
                    && Main.projectile[laserIndicatorIdx2].active)
                {
                    Main.projectile[laserIndicatorIdx2].Kill();
                }
                laserIndicatorIdx2 = -1;

                // Spawn beam projectile(s)
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int idx = Projectile.NewProjectile(
                        NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<TitanLaserBeam>(),
                        TitanBody.ExpertDamage(40), 0f, Main.myPlayer,
                        ai0: NPC.whoAmI, ai1: laserTargetAngle,
                        ai2: CurrentBehavior == HandBehavior.ArenaLaser ? 1f : 0f);
                    laserBeamIdx = idx;

                    // DualLaser: spawn second beam
                    if (CurrentBehavior == HandBehavior.DualLaser)
                    {
                        int idx2 = Projectile.NewProjectile(
                            NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<TitanLaserBeam>(),
                            TitanBody.ExpertDamage(40), 0f, Main.myPlayer,
                            ai0: NPC.whoAmI, ai1: laserTargetAngle2);
                        laserBeamIdx2 = idx2;
                    }
                }

                NPC.netUpdate = true;
            }
        }

        private void LaserPhase_Beam()
        {
            NPC.rotation = GetAimRotation(laserTargetAngle) + GetAttackSwayRotation();

            if (laserSubTimer >= LASER_BEAM_DURATION)
            {
                laserSubTimer = 0f;
                laserSubPhase = 3; // Hold phase (delay before returning)

                // Kill beam(s) if still alive (safety)
                if (laserBeamIdx >= 0 && laserBeamIdx < Main.maxProjectiles
                    && Main.projectile[laserBeamIdx].active)
                {
                    Main.projectile[laserBeamIdx].Kill();
                }
                laserBeamIdx = -1;

                if (laserBeamIdx2 >= 0 && laserBeamIdx2 < Main.maxProjectiles
                    && Main.projectile[laserBeamIdx2].active)
                {
                    Main.projectile[laserBeamIdx2].Kill();
                }
                laserBeamIdx2 = -1;

                NPC.netUpdate = true;
            }
        }

        private void LaserPhase_Hold()
        {
            // Hold aim rotation and position with subtle sway before returning
            NPC.rotation = GetAimRotation(laserTargetAngle) + GetAttackSwayRotation();

            if (laserSubTimer >= LASER_HOLD_DURATION)
            {
                // If skip-return is set (damage phase), go straight to Default
                if (SkipReturnToDefault)
                {
                    CurrentBehavior = HandBehavior.Default;
                    NPC.netUpdate = true;
                    return;
                }

                laserSubTimer = 0f;
                laserSubPhase = 4; // Return phase
                laserReturnStartRotation = NPC.rotation;
                NPC.netUpdate = true;
            }
        }

        private void LaserPhase_Return()
        {
            float t = MathHelper.Clamp(laserSubTimer / LASER_RETURN_DURATION, 0f, 1f);
            t = 1f - (1f - t) * (1f - t); // Ease-out
            NPC.rotation = LerpAngle(laserReturnStartRotation, HandRotation, t) + GetAttackSwayRotation() * (1f - t);

            if (laserSubTimer >= LASER_RETURN_DURATION)
            {
                CurrentBehavior = HandBehavior.Default;

                // If a second laser is queued (damage phase), fire it immediately
                if (PendingSecondLaser)
                {
                    PendingSecondLaser = false;
                    StartLaserAttack();
                }

                NPC.netUpdate = true;
            }
        }

        // ── Behavior: Finger Launch ──────────────────────────────────
        // Sequence: approach player palm-first → launch fingers 1/sec → regrow → return
        private void RunFingerLaunchBehavior(NPC parent)
        {
            float sideSign = IsLeft ? 1f : -1f;
            Vector2 defaultPos = parent.Center + new Vector2(sideSign * DEFAULT_OFFSET_X, DEFAULT_OFFSET_Y);

            fingerLaunchSubTimer += 1f / 60f;

            switch (fingerLaunchSubPhase)
            {
                case 0: // Approach, move to static position at arena edge, palm facing inward
                {
                    // Static X: left hand near left wall, right hand near right wall
                    var towerTop = TitanSpawnCutscene.TowerTopLeft;
                    float arenaLeftX = towerTop.X * 16f;
                    float arenaRightX = (towerTop.X + TitanSpawnCutscene.TOWER_WIDTH) * 16f;
                    float edgeOffset = 5f * 16f; // 5 tiles inward from wall
                    float targetX = IsLeft
                        ? arenaRightX - edgeOffset
                        : arenaLeftX + edgeOffset;

                    // Y: same as nearest player, clamped to arena
                    Player target = FindNearestPlayer();
                    float targetY = target != null ? target.Center.Y : NPC.Center.Y;
                    fingerLaunchTargetPos = new Vector2(targetX, targetY);

                    // Spring toward target position (naturally overextends)
                    UpdateSpringMovement(fingerLaunchTargetPos + GetAttackSwayOffset());

                    // Aim palm toward center of arena (toward titan)
                    float aimAngle = IsLeft ? MathHelper.Pi : 0f;
                    NPC.rotation = LerpAngle(NPC.rotation, GetAimRotation(aimAngle) + GetAttackSwayRotation(), HAND_FLIP_SPEED);

                    if (fingerLaunchSubTimer >= FINGER_APPROACH_DURATION)
                    {
                        fingerLaunchSubTimer = 0f;
                        fingerLaunchSubPhase = 1;
                        fingerNextLaunchIndex = 0;

                        // Lock target player for all finger launches
                        Player lockedTarget = FindNearestPlayer();
                        fingerLaunchLockedPlayer = lockedTarget?.whoAmI ?? -1;

                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 1: // Launching, fire one finger per second
                {
                    // Hold at target with sway
                    UpdateSpringMovement(fingerLaunchTargetPos + GetAttackSwayOffset());

                    // Maintain aim toward center of arena
                    float launchAimAngle = IsLeft ? MathHelper.Pi : 0f;
                    NPC.rotation = GetAimRotation(launchAimAngle) + GetAttackSwayRotation();

                    if (fingerLaunchSubTimer >= FINGER_LAUNCH_INTERVAL && fingerNextLaunchIndex < FINGER_COUNT)
                    {
                        // Launch finger projectile at locked target
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            Player projectileTarget = (fingerLaunchLockedPlayer >= 0 && fingerLaunchLockedPlayer < Main.maxPlayers
                                && Main.player[fingerLaunchLockedPlayer].active && !Main.player[fingerLaunchLockedPlayer].dead)
                                ? Main.player[fingerLaunchLockedPlayer]
                                : FindNearestPlayer();
                            Vector2 fireDir = projectileTarget != null
                                ? Vector2.Normalize(projectileTarget.Center - NPC.Center)
                                : Vector2.UnitX;

                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(), NPC.Center, fireDir * 14f,
                                ModContent.ProjectileType<TitanFingerProjectile>(),
                                TitanBody.ExpertDamage(30), 2f, Main.myPlayer,
                                ai0: fingerNextLaunchIndex, ai1: IsLeft ? 1f : 0f);
                        }

                        FingerHidden[fingerNextLaunchIndex] = true;
                        fingerNextLaunchIndex++;
                        fingerLaunchSubTimer = 0f;
                        NPC.netUpdate = true;

                        if (fingerNextLaunchIndex >= FINGER_COUNT)
                        {
                            fingerLaunchSubTimer = 0f;
                            fingerLaunchSubPhase = 2; // Regrow
                            NPC.netUpdate = true;
                        }
                    }
                    break;
                }
                case 2: // Regrow, wait, then restore fingers
                {
                    UpdateSpringMovement(fingerLaunchTargetPos + GetAttackSwayOffset());

                    if (fingerLaunchSubTimer >= FINGER_REGROW_DURATION)
                    {
                        for (int i = 0; i < FINGER_COUNT; i++)
                            FingerHidden[i] = false;

                        fingerLaunchSubTimer = 0f;
                        fingerLaunchSubPhase = 3; // Return
                        fingerReturnStartPos = NPC.Center;
                        fingerReturnStartRotation = NPC.rotation;
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 3: // Return, spring back to default position
                {
                    UpdateSpringMovement(defaultPos + GetIdleSwayOffset());
                    NPC.rotation = LerpAngle(NPC.rotation, HandRotation, 0.08f);

                    if (fingerLaunchSubTimer >= FINGER_RETURN_DURATION)
                    {
                        CurrentBehavior = HandBehavior.Default;
                        NPC.netUpdate = true;
                    }
                    break;
                }
            }

            NPC.velocity = Vector2.Zero;
        }

        // ── Behavior: Slam ───────────────────────────────────────────
        // Sequence: follow above player → pause → slam down to ground → hold → return
        private void RunSlamBehavior(NPC parent)
        {
            float sideSign = IsLeft ? 1f : -1f;
            Vector2 defaultPos = parent.Center + new Vector2(sideSign * DEFAULT_OFFSET_X, DEFAULT_OFFSET_Y);

            slamSubTimer += 1f / 60f;

            switch (slamSubPhase)
            {
                case 0: // Follow, track above player with palm facing titan
                {
                    Player target = FindNearestPlayer();
                    if (target != null)
                    {
                        Vector2 above = new Vector2(target.Center.X, target.Center.Y - SLAM_ABOVE_OFFSET);
                        UpdateSpringMovement(above + GetAttackSwayOffset(), 0.10f);
                    }

                    // Rotate so palm faces toward titan (inward)
                    float slamAimAngle = IsLeft ? MathHelper.Pi : 0f;
                    NPC.rotation = LerpAngle(NPC.rotation, GetAimRotation(slamAimAngle) + GetAttackSwayRotation(), HAND_FLIP_SPEED);

                    if (slamSubTimer >= SLAM_FOLLOW_DURATION)
                    {
                        slamSubTimer = 0f;
                        slamSubPhase = 1; // Pause
                        slamPausePos = NPC.Center;
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 1: // Pause, hold position with subtle sway, then slam
                {
                    UpdateSpringMovement(slamPausePos + GetAttackSwayOffset() * 0.5f);
                    float pauseAimAngle = IsLeft ? MathHelper.Pi : 0f;
                    NPC.rotation = GetAimRotation(pauseAimAngle) + GetAttackSwayRotation();

                    if (slamSubTimer >= SLAM_PAUSE_DURATION)
                    {
                        slamSubTimer = 0f;
                        slamSubPhase = 2; // Slam down

                        // Arena ground = tower top-left Y + 11 tiles
                        var towerTop = TitanSpawnCutscene.TowerTopLeft;
                        slamGroundY = towerTop.Y * 16f + 11f * 16f;
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 2: // Slam, move straight down at high speed
                {
                    springVelocity = Vector2.Zero; // No spring during violent slam
                    float slamDownAngle = IsLeft ? MathHelper.Pi : 0f;
                    NPC.rotation = GetAimRotation(slamDownAngle);
                    NPC.position.Y += SLAM_SPEED;

                    if (NPC.Center.Y >= slamGroundY)
                    {
                        NPC.Center = new Vector2(NPC.Center.X, slamGroundY);
                        springVelocity = Vector2.Zero;
                        slamSubTimer = 0f;
                        slamSubPhase = 3; // Hold

                        // Impact effects
                        OnSlamImpact();
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 3: // Hold, stay on ground with gentle sway
                {
                    Vector2 groundPos = new Vector2(NPC.Center.X, slamGroundY);
                    UpdateSpringMovement(groundPos + GetAttackSwayOffset() * 0.3f);
                    // Keep Y pinned to ground
                    NPC.Center = new Vector2(NPC.Center.X, slamGroundY);
                    float holdAimAngle = IsLeft ? MathHelper.Pi : 0f;
                    NPC.rotation = GetAimRotation(holdAimAngle) + GetAttackSwayRotation();

                    if (slamSubTimer >= SLAM_HOLD_DURATION)
                    {
                        slamSubTimer = 0f;
                        slamSubPhase = 4; // Return
                        slamReturnStartPos = NPC.Center;
                        slamReturnStartRotation = NPC.rotation;
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 4: // Return to default
                {
                    UpdateSpringMovement(defaultPos + GetIdleSwayOffset());
                    NPC.rotation = LerpAngle(NPC.rotation, HandRotation, 0.08f);

                    if (slamSubTimer >= SLAM_RETURN_DURATION)
                    {
                        CurrentBehavior = HandBehavior.Default;
                        NPC.netUpdate = true;
                    }
                    break;
                }
            }

            NPC.velocity = Vector2.Zero;
        }

        // Called on slam impact: spawns shockwave lines and triggers screen shake.
        private void OnSlamImpact()
        {
            // Slam sound + screen shake, must run on clients, not server
            if (Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/TitanSlam") { MaxInstances = 1 });

                PunchCameraModifier modifier = new PunchCameraModifier(
                    NPC.Center, Main.rand.NextVector2Unit(), 15f, 6f, 20, 2000f, "TitanSlam");
                Main.instance.CameraModifiers.Add(modifier);
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // Spawn shockwave lines in radial pattern (server-only, projectiles sync to clients)
            for (int i = 0; i < SLAM_SHOCKWAVE_COUNT; i++)
            {
                float angle = MathHelper.TwoPi * i / SLAM_SHOCKWAVE_COUNT;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(), NPC.Center, velocity,
                    ModContent.ProjectileType<ShockwaveLine>(),
                    0, 0f, Main.myPlayer);
            }
        }

        // ── Behavior: FloorIsLava ────────────────────────────────────
        // Sequence: rise up → telegraph (sound + flash) → danger (kill grounded players) → hold → return
        private void RunFloorIsLavaBehavior(NPC parent)
        {
            float sideSign = IsLeft ? 1f : -1f;
            Vector2 defaultPos = parent.Center + new Vector2(sideSign * DEFAULT_OFFSET_X, DEFAULT_OFFSET_Y);

            floorSubTimer += 1f / 60f;

            switch (floorSubPhase)
            {
                case 0: // Rise, spring upward, palms face down (angled slightly inward toward titan)
                {
                    UpdateSpringMovement(floorRiseTargetPos + GetAttackSwayOffset());

                    // Rotate palm to face down-and-inward (~25° toward the titan)
                    float inwardAngle = MathHelper.PiOver2 + sideSign * 0.44f;
                    NPC.rotation = LerpAngle(NPC.rotation, GetAimRotation(inwardAngle) + GetAttackSwayRotation(), 0.1f);

                    if (floorSubTimer >= FLOOR_RISE_DURATION)
                    {
                        floorSubTimer = 0f;
                        floorSubPhase = 1; // Telegraph
                        NPC.rotation = GetAimRotation(MathHelper.PiOver2 + sideSign * 0.44f);

                        // Play telegraph sound (audible to all)
                        SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/TitanSlamTelegraph") { MaxInstances = 1 });

                        // Signal the floor flash system, telegraph phase
                        FloorIsLavaFlash.StartTelegraph();

                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 1: // Telegraph, hold position with sway, flash is active
                {
                    UpdateSpringMovement(floorRiseTargetPos + GetAttackSwayOffset());
                    NPC.rotation = GetAimRotation(MathHelper.PiOver2 + sideSign * 0.44f) + GetAttackSwayRotation();

                    if (floorSubTimer >= FLOOR_TELEGRAPH_DURATION)
                    {
                        floorSubTimer = 0f;
                        floorSubPhase = 2; // Danger

                        // Move hands closer to ground
                        var towerTop = TitanSpawnCutscene.TowerTopLeft;
                        float groundY = towerTop.Y * 16f + 11f * 16f;
                        floorRiseTargetPos = new Vector2(NPC.Center.X, groundY - 100f); // 100px above ground

                        // Play danger sound
                        SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/TitanFloorDanger") { MaxInstances = 1 });

                        // Signal the floor flash system, danger phase (brighter)
                        FloorIsLavaFlash.StartDanger();

                        // Big screen shake when the floor becomes lava
                        if (Main.netMode != NetmodeID.Server)
                        {
                            PunchCameraModifier modifier = new PunchCameraModifier(
                                NPC.Center, Main.rand.NextVector2Unit(), 25f, 8f, 30, 3000f, "TitanFloorIsLava");
                            Main.instance.CameraModifiers.Add(modifier);
                        }

                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 2: // Danger, spring toward ground, grounded players die (handled by TitanBody)
                {
                    UpdateSpringMovement(floorRiseTargetPos + GetAttackSwayOffset() * 0.5f);
                    NPC.rotation = GetAimRotation(MathHelper.PiOver2 + sideSign * 0.44f) + GetAttackSwayRotation();

                    if (floorSubTimer >= FLOOR_DANGER_DURATION)
                    {
                        floorSubTimer = 0f;
                        floorSubPhase = 3; // Hold
                        FloorIsLavaFlash.Stop();
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 3: // Hold, stay near ground with sway
                {
                    UpdateSpringMovement(floorRiseTargetPos + GetAttackSwayOffset() * 0.3f);
                    NPC.rotation = GetAimRotation(MathHelper.PiOver2 + sideSign * 0.44f) + GetAttackSwayRotation();

                    if (floorSubTimer >= FLOOR_HOLD_DURATION)
                    {
                        floorSubTimer = 0f;
                        floorSubPhase = 4; // Return
                        floorReturnStartPos = NPC.Center;
                        floorReturnStartRotation = NPC.rotation;
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 4: // Return to default
                {
                    UpdateSpringMovement(defaultPos + GetIdleSwayOffset());
                    NPC.rotation = LerpAngle(NPC.rotation, HandRotation, 0.08f);

                    if (floorSubTimer >= FLOOR_RETURN_DURATION)
                    {
                        CurrentBehavior = HandBehavior.Default;
                        NPC.netUpdate = true;
                    }
                    break;
                }
            }

            NPC.velocity = Vector2.Zero;
        }

        // Finds the nearest active player to this hand.
        private Player FindNearestPlayer()
        {
            Player closest = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead)
                {
                    float dist = Vector2.DistanceSquared(NPC.Center, p.Center);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = p;
                    }
                }
            }

            return closest;
        }

        // Called by TitanBody to start a laser attack on this hand.
        // Only works when the hand is in Default behavior.
        public void StartLaserAttack()
        {
            if (CurrentBehavior != HandBehavior.Default)
                return;

            CurrentBehavior = HandBehavior.LaserAttack;
            laserSubPhase = 0;
            laserSubTimer = 0f;
            laserStartPos = NPC.Center;
            laserAimLocked = false;

            // Randomize fire position: 300px up + random X offset
            float randomX = Main.rand.NextFloat(-150f, 150f);
            laserFireOffset = new Vector2(randomX, -300f);

            NPC.netUpdate = true;
        }

        // Called by TitanBody to start an arena (2x wide) laser attack.
        public void StartArenaLaser()
        {
            if (CurrentBehavior != HandBehavior.Default)
                return;

            CurrentBehavior = HandBehavior.ArenaLaser;
            laserSubPhase = 0;
            laserSubTimer = 0f;
            laserStartPos = NPC.Center;
            laserAimLocked = false;

            float randomX = Main.rand.NextFloat(-150f, 150f);
            laserFireOffset = new Vector2(randomX, -300f);

            NPC.netUpdate = true;
        }

        // Called by TitanBody to start a dual-laser spread attack (2 narrow beams at once).
        public void StartDualLaserSpread()
        {
            if (CurrentBehavior != HandBehavior.Default)
                return;

            CurrentBehavior = HandBehavior.DualLaser;
            laserSubPhase = 0;
            laserSubTimer = 0f;
            laserStartPos = NPC.Center;
            laserAimLocked = false;
            laserIndicatorIdx2 = -1;
            laserBeamIdx2 = -1;

            float randomX = Main.rand.NextFloat(-150f, 150f);
            laserFireOffset = new Vector2(randomX, -300f);

            NPC.netUpdate = true;
        }

        // Called by TitanBody to start a finger launch attack.
        public void StartFingerLaunch()
        {
            if (CurrentBehavior != HandBehavior.Default)
                return;

            CurrentBehavior = HandBehavior.FingerLaunch;
            fingerLaunchSubPhase = 0;
            fingerLaunchSubTimer = 0f;
            fingerNextLaunchIndex = 0;
            fingerLaunchTargetPos = NPC.Center; // Will be updated on first tick

            for (int i = 0; i < FINGER_COUNT; i++)
                FingerHidden[i] = false;

            NPC.netUpdate = true;
        }

        // Called by TitanBody to start a slam attack.
        public void StartSlam()
        {
            if (CurrentBehavior != HandBehavior.Default)
                return;

            CurrentBehavior = HandBehavior.Slam;
            slamSubPhase = 0;
            slamSubTimer = 0f;
            slamGroundY = 0f;

            NPC.netUpdate = true;
        }

        // Called by TitanBody to start the FloorIsLava attack.
        // Both hands should be triggered simultaneously.
        public void StartFloorIsLava()
        {
            if (CurrentBehavior != HandBehavior.Default)
                return;

            CurrentBehavior = HandBehavior.FloorIsLava;
            floorSubPhase = 0;
            floorSubTimer = 0f;
            floorRiseStartPos = NPC.Center;

            // Rise target: high above default position, toward the titan's center X
            int parentIdx = (int)NPC.ai[0];
            NPC parent = Main.npc[parentIdx];
            float sideSign = IsLeft ? 1f : -1f;
            Vector2 defaultPos = parent.Center + new Vector2(sideSign * DEFAULT_OFFSET_X, DEFAULT_OFFSET_Y);
            // Rise target: pull slightly inward toward the titan's center
            float inwardPull = -sideSign * 80f;
            floorRiseTargetPos = defaultPos + new Vector2(inwardPull, FLOOR_RISE_Y_OFFSET);

            NPC.netUpdate = true;
        }

        private void SpawnFingers()
        {
            for (int f = 0; f < FINGER_COUNT; f++)
            {
                int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y,
                    ModContent.NPCType<TitanFinger>());

                if (idx >= 0 && idx < Main.maxNPCs)
                {
                    Main.npc[idx].ai[0] = NPC.whoAmI;
                    Main.npc[idx].ai[1] = f;

                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 drawPos = NPC.Center - screenPos;

            // Right hand = base sprite, left hand = flipped horizontally
            SpriteEffects effects = IsLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            spriteBatch.Draw(tex, drawPos, null, drawColor, NPC.rotation, origin, NPC.scale, effects, 0f);
            return false;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            // Draw the sphere above the hand when a laser beam is active
            // Find beam by scanning projectiles, safe for multiplayer (indices differ per side)
            int beamType = ModContent.ProjectileType<TitanLaserBeam>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == beamType && (int)p.ai[0] == NPC.whoAmI
                    && p.ModProjectile is TitanLaserBeam laserBeam)
                {
                    Vector2 spherePos = NPC.Center - screenPos;
                    float widthScale = laserBeam.GetCurrentWidthScale();
                    float elapsed = p.localAI[0];
                    TitanLaserBeam.DrawSphere(spriteBatch, spherePos, widthScale, elapsed);
                    break;
                }
            }
        }

        public override bool CheckActive() => false;

        public override void DrawBehind(int index)
        {
            // Draw hands above everything else
            Main.instance.DrawCacheNPCsOverPlayers.Add(index);
        }

        // Spawns both hands attached to the given TitanBody.
        public static void SpawnHands(TitanBody titan)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            NPC parent = titan.NPC;

            for (int slot = 0; slot < 2; slot++)
            {
                int idx = NPC.NewNPC(parent.GetSource_FromAI(), (int)parent.Center.X, (int)parent.Center.Y,
                    ModContent.NPCType<TitanHand>());

                if (idx >= 0 && idx < Main.maxNPCs)
                {
                    Main.npc[idx].ai[0] = parent.whoAmI;
                    Main.npc[idx].ai[1] = slot;

                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                }
            }
        }
    }
}
