using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class RoaringShieldPlayer : ModPlayer
    {
        // ── Public flags (set each frame by the item) ──────────────────────
        public bool HasShield = false;

        // ── Sphere animation (read by RoaringShieldSphereDrawLayer) ────────
        private int sphereAnimTick = 0;
        private int sphereAnimRow = 0;
        private const int SphereFrameCount = 5;
        private const int SphereTicksPerFrame = 5;

        public int SphereFrame => sphereAnimRow;

        // ── Afterimage trail positions (read by draw layer) ─────────────────
        private const int _trailLength = 8;
        public readonly Vector2[] TrailPositions = new Vector2[_trailLength];

        // ── Dash state machine ─────────────────────────────────────────────
        private enum DashState { None, ChargingUp, Dashing, Recovery }

        private DashState dashState = DashState.None;
        private int dashTimer = 0;
        private int cooldownTimer = 0;
        private Vector2 dashVelocity = Vector2.Zero;
        private Vector2 prevCenter = Vector2.Zero;
        private readonly HashSet<int> hitThisDash = new HashSet<int>();

        // Timing
        private const int ChargeDuration  = 10;   // sphere appears, player slows
        private const int DashDuration    = 18;   // active dash
        private const int RecoveryDuration = 8;   // decelerate out
        private const int CooldownDuration = 40;  // before next dash

        private const float DashSpeed = 22f;      // pixels / frame

        // ── Exposed helpers ────────────────────────────────────────────────
        public bool IsInSphereForm => dashState != DashState.None;
        public bool IsDashing       => dashState == DashState.Dashing;

        public float SphereAlpha
        {
            get
            {
                return dashState switch
                {
                    DashState.ChargingUp => (float)dashTimer / ChargeDuration,
                    DashState.Recovery   => 1f - (float)dashTimer / RecoveryDuration,
                    _                    => 1f
                };
            }
        }

        // ── Double-tap detection ───────────────────────────────────────────
        private int leftTapTimer  = 0;
        private int rightTapTimer = 0;
        private bool prevLeft  = false;
        private bool prevRight = false;
        private const int DoubleTapWindow = 15;

        // ── Calamity keybind integration (lazy, reflection-based) ──────────
        // When CalamityMod is installed, we prefer their dedicated dash keybind
        // over double-tap input.  Double-tap is kept as fallback.
        private static ModKeybind _calDash;
        private static bool _calKeyLoaded;

        private static ModKeybind CalamityDashKey
        {
            get
            {
                if (!_calKeyLoaded)
                {
                    _calKeyLoaded = true;
                    if (ModLoader.TryGetMod("CalamityMod", out Mod cal))
                    {
                        try
                        {
                            Type t = cal.Code.GetType("CalamityMod.CalamityKeybinds");
                            if (t != null)
                            {
                                // Current Calamity uses the public static property: DashHotkey
                                PropertyInfo p = t.GetProperty("DashHotkey", BindingFlags.Public | BindingFlags.Static)
                                    ?? t.GetProperty("DashHotKey", BindingFlags.Public | BindingFlags.Static);
                                if (p != null)
                                {
                                    _calDash = p.GetValue(null) as ModKeybind;
                                }
                                else
                                {
                                    // Fallback in case they change the implementation back to fields.
                                    FieldInfo f = t.GetField("DashHotkey", BindingFlags.Public | BindingFlags.Static)
                                        ?? t.GetField("DashHotKey", BindingFlags.Public | BindingFlags.Static);
                                    _calDash = f?.GetValue(null) as ModKeybind;
                                }
                            }
                        }
                        catch { /* Calamity API changed, double-tap will be used */ }
                    }
                }
                return _calDash;
            }
        }

        // ──────────────────────────────────────────────────────────────────
        public override void ResetEffects()
        {
            HasShield = false;
        }

        public override void PostUpdate()
        {
            // ── Cooldown ──────────────────────────────────────────────────
            if (cooldownTimer > 0)
                cooldownTimer--;

            // ── Double-tap input ──────────────────────────────────────────
            if (HasShield && dashState == DashState.None && cooldownTimer <= 0)
            {
                ModKeybind calDash = CalamityDashKey;
                if (calDash != null)
                {
                    // ── Calamity keybind mode ─────────────────────────────
                    // Direction comes from whichever movement key is held,
                    // falling back to the player's current facing direction.
                    if (calDash.JustPressed)
                    {
                        int dir = Player.controlLeft ? -1
                                : Player.controlRight ? 1
                                : Player.direction;
                        StartDash(dir);
                    }
                    // Keep double-tap state cleared so switching modes is clean.
                    prevLeft  = false;
                    prevRight = false;
                }
                else
                {
                    // ── Double-tap fallback (no Calamity) ─────────────────
                    bool left  = Player.controlLeft;
                    bool right = Player.controlRight;

                    if (left && !prevLeft)
                    {
                        if (leftTapTimer > 0)
                            StartDash(-1);
                        leftTapTimer = DoubleTapWindow;
                    }
                    if (leftTapTimer > 0) leftTapTimer--;

                    if (right && !prevRight)
                    {
                        if (rightTapTimer > 0)
                            StartDash(1);
                        rightTapTimer = DoubleTapWindow;
                    }
                    if (rightTapTimer > 0) rightTapTimer--;

                    prevLeft  = left;
                    prevRight = right;
                }
            }
            else
            {
                prevLeft  = false;
                prevRight = false;
            }

            // ── State machine ─────────────────────────────────────────────
            UpdateDash();

            // ── Sphere animation & trail update ───────────────────────────
            if (IsInSphereForm)
            {
                // Animate sphere frames
                sphereAnimTick++;
                if (sphereAnimTick >= SphereTicksPerFrame)
                {
                    sphereAnimTick = 0;
                    sphereAnimRow = (sphereAnimRow + 1) % SphereFrameCount;
                }

                // Push positions into the trail buffer
                for (int i = _trailLength - 1; i > 0; i--)
                    TrailPositions[i] = TrailPositions[i - 1];
                TrailPositions[0] = Player.Center;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        private void StartDash(int direction)
        {
            dashState    = DashState.ChargingUp;
            dashTimer    = 0;
            dashVelocity = new Vector2(DashSpeed * direction, 0f);
            prevCenter   = Player.Center;
            hitThisDash.Clear();

            // Pre-fill the trail so no zero-positions appear on first frame
            for (int i = 0; i < _trailLength; i++)
                TrailPositions[i] = Player.Center;

            SoundEngine.PlaySound(
                new SoundStyle("DeterministicChaos/Assets/Sounds/KnightTransform") { Volume = 0.7f },
                Player.Center);
        }

        private void UpdateDash()
        {
            switch (dashState)
            {
                // ── Charge: sphere appears, player nearly stops ───────────
                case DashState.ChargingUp:
                {
                    Player.velocity *= 0.5f;   // quickly decelerate
                    Player.velocity.Y = 0f;    // suppress gravity accumulation

                    // Grant immunity while charging (prevents taking damage while helpless)
                    Player.immuneTime = Math.Max(Player.immuneTime, 3);

                    dashTimer++;
                    if (dashTimer >= ChargeDuration)
                    {
                        dashState = DashState.Dashing;
                        dashTimer = 0;

                        SoundEngine.PlaySound(
                            new SoundStyle("DeterministicChaos/Assets/Sounds/KnightDash") { Volume = 0.8f },
                            Player.Center);
                    }
                    break;
                }

                // ── Dash: sphere travels at full speed ────────────────────
                case DashState.Dashing:
                {
                    // Wall-hit detection: if actual movement is far below commanded speed,
                    // the player is blocked; skip straight to recovery.
                    if (dashTimer > 2)
                    {
                        float expected = Math.Abs(dashVelocity.X);
                        float actual   = Math.Abs(Player.Center.X - prevCenter.X);
                        if (actual < expected * 0.25f)
                        {
                            dashState = DashState.Recovery;
                            dashTimer = 0;
                            prevCenter = Player.Center;
                            break;
                        }
                    }

                    prevCenter = Player.Center;

                    Player.velocity.X = dashVelocity.X;
                    Player.velocity.Y = 0f;                     // horizontal-only dash
                    Player.fallStart  = (int)(Player.position.Y / 16f); // no fall damage

                    // Full immunity frames, player passes through enemies safely
                    Player.immuneTime    = Math.Max(Player.immuneTime, 2);
                    Player.immuneNoBlink = true;

                    // Deal contact damage to overlapped enemies (server / singleplayer)
                    DealContactDamage();

                    dashTimer++;
                    if (dashTimer >= DashDuration)
                    {
                        dashState = DashState.Recovery;
                        dashTimer = 0;
                    }
                    break;
                }

                // ── Recovery: smoothly decelerate back to normal physics ──
                case DashState.Recovery:
                {
                    float t = dashTimer / (float)RecoveryDuration;
                    Player.velocity.X = MathHelper.Lerp(dashVelocity.X, 0f, t);
                    Player.velocity.Y = 0f;

                    Player.immuneTime = Math.Max(Player.immuneTime, 2);

                    dashTimer++;
                    if (dashTimer >= RecoveryDuration)
                    {
                        dashState     = DashState.None;
                        dashTimer     = 0;
                        cooldownTimer = CooldownDuration;

                        SoundEngine.PlaySound(
                            new SoundStyle("DeterministicChaos/Assets/Sounds/KnightTransform") { Volume = 0.5f },
                            Player.Center);
                    }
                    break;
                }
            }
        }

        // Deals contact damage to NPCs the player overlaps during the dash.
        // Only runs on the server (or in singleplayer).
        private void DealContactDamage()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Rectangle hitbox = Player.getRect();
            hitbox.Inflate(4, 4);

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal)
                    continue;
                if (hitThisDash.Contains(i))
                    continue;
                if (!npc.Hitbox.Intersects(hitbox))
                    continue;

                int dir    = (Player.Center.X < npc.Center.X) ? 1 : -1;
                int damage = 30 + Player.statDefense / 2;

                npc.StrikeNPC(npc.CalculateHitInfo(damage, dir, false, 8f, DamageClass.Generic));
                hitThisDash.Add(i);
            }
        }
    }
}
