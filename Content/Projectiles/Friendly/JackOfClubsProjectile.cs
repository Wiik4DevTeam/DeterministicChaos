using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class JackOfClubsProjectile : ModProjectile
    {
        // --- Tuning ---
        private const float SpriteSize = 70f;
        private const float DrawScale = 1.3f;

        // Swing arc: wind up behind, swing forward and down to hit ground
        private const float WindUpAngle = -150f;   // degrees behind player (up and back)
        private const float ImpactAngle = 75f;     // degrees in front (down toward ground)
        private const int WindUpTicks = 14;         // frames to raise weapon
        private const int WindUpElasticTicks = 8;   // frames for overshoot-and-snap-back
        private const float WindUpOvershootDeg = -18f; // extra degrees past peak
        private const int SwingTicks = 20;          // frames to swing down
        private const int ImpactStallTicks = 18;    // frames frozen in ground after impact
        private const float PlayerCarrySpeed = 3.5f;// horizontal speed player is dragged during swing

        // Earthquake
        private const int EarthquakeColumns = 9;    // number of club projectiles
        private const float EarthquakeSpacing = 38f; // pixels between each column
        private const float EarthquakeBaseSpeed = 8f;
        private const float EarthquakeSpeedVariance = 3f;

        // Offset from player shoulder to hand grip
        private const float ShoulderOffsetX = 6f;
        private const float ShoulderOffsetY = -6f;

        // Trail
        private const int TrailLength = 6;

        // Hit cooldown
        private const int HitCooldown = 10;

        // --- Phases in ai[0] ---
        private const float PhaseWindUp = 0f;
        private const float PhaseSwing = 1f;
        private const float PhaseImpact = 2f;

        // ai slots
        private ref float Phase => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];
        private ref float Dir => ref Projectile.ai[2]; // 1 or -1

        // Synced fields
        private float swingAngle;
        private bool hasImpacted;

        // Scale tween
        private float currentScale;
        private const float MinDrawScale = 0.3f;
        private const float MaxDrawScale = DrawScale;

        // Client-only
        private float drawRotation;
        private readonly List<Vector2> trailPositions = new();
        private readonly List<float> trailRotations = new();
        private bool playedSwingSound;

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 999999;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = HitCooldown;
            Projectile.ownerHitCheck = true;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(swingAngle);
            writer.Write(hasImpacted);
            writer.Write(currentScale);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            swingAngle = reader.ReadSingle();
            hasImpacted = reader.ReadBoolean();
            currentScale = reader.ReadSingle();
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            player.heldProj = Projectile.whoAmI;
            player.itemTime = 2;
            player.itemAnimation = 2;

            int dir = (int)Dir;
            if (dir == 0) dir = player.direction;

            switch (Phase)
            {
                case PhaseWindUp:
                    WindUpPhase(player, dir);
                    break;
                case PhaseSwing:
                    SwingPhase(player, dir);
                    break;
                case PhaseImpact:
                    ImpactPhase(player, dir);
                    break;
            }

            Timer++;
            UpdateTrail();
        }

        private void WindUpPhase(Player player, int dir)
        {
            float angleDeg;

            if (Timer < WindUpTicks)
            {
                // Phase 1: raise the mace to peak angle
                float t = Timer / (float)WindUpTicks;
                float eased = t * t; // ease-in
                angleDeg = MathHelper.Lerp(30f, WindUpAngle, eased);

                currentScale = MathHelper.Lerp(MinDrawScale, MaxDrawScale, eased);
            }
            else
            {
                // Phase 2: elastic overshoot past peak, then snap back
                float elapsed = Timer - WindUpTicks;
                float t = Math.Min(elapsed / (float)WindUpElasticTicks, 1f);

                // Sine-based overshoot: goes past, comes back to rest
                // sin(t * PI) peaks at 0.5, returns to 0 at 1.0
                float overshoot = (float)Math.Sin(t * MathHelper.Pi) * WindUpOvershootDeg;
                angleDeg = WindUpAngle + overshoot;

                currentScale = MaxDrawScale;
            }

            swingAngle = MathHelper.ToRadians(angleDeg) * dir;

            PositionMace(player, dir);
            player.direction = dir;

            if (Timer >= WindUpTicks + WindUpElasticTicks)
            {
                Phase = PhaseSwing;
                Timer = 0;
                playedSwingSound = false;
                Projectile.netUpdate = true;
            }
        }

        private void SwingPhase(Player player, int dir)
        {
            if (!playedSwingSound)
            {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = -0.5f }, player.Center);
                playedSwingSound = true;
            }

            float t = Math.Min(Timer / (float)SwingTicks, 1f);
            // Ease-in-out: fast in middle, smooth start/end
            float eased = t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f;

            float startDeg = WindUpAngle;
            float endDeg = ImpactAngle;
            float angleDeg = MathHelper.Lerp(startDeg, endDeg, eased);
            swingAngle = MathHelper.ToRadians(angleDeg) * dir;

            // Keep at full scale during swing
            currentScale = MaxDrawScale;

            PositionMace(player, dir);
            player.direction = dir;

            // Check for ground impact once past the midpoint of the swing
            if (t > 0.5f && CheckGroundHit(player))
            {
                Phase = PhaseImpact;
                Timer = 0;
                hasImpacted = true;
                DoGroundImpact(player, dir);
                Projectile.netUpdate = true;
                return;
            }

            // If swing completes without hitting ground, just end
            if (Timer >= SwingTicks)
            {
                // Still do a weaker impact effect at final position
                Phase = PhaseImpact;
                Timer = 0;
                Projectile.netUpdate = true;
            }
        }

        private void ImpactPhase(Player player, int dir)
        {
            // Tween scale down after impact
            float shrinkT = Math.Min(Timer / (float)ImpactStallTicks, 1f);
            currentScale = MathHelper.Lerp(MaxDrawScale, MinDrawScale * 0.5f, shrinkT * shrinkT);

            // Freeze in place with slight shake
            if (Timer < ImpactStallTicks)
            {
                float shake = Math.Max(0f, (6f - Timer) * 0.4f);
                Projectile.Center += new Vector2(Main.rand.NextFloat(-shake, shake), Main.rand.NextFloat(-shake, shake));
            }
            else
            {
                Projectile.Kill();
            }

            player.direction = dir;
            player.heldProj = Projectile.whoAmI;
        }

        private void PositionMace(Player player, int dir)
        {
            // Anchor at the player's shoulder joint, offset slightly outward
            Vector2 shoulder = player.Center + new Vector2(ShoulderOffsetX * dir, ShoulderOffsetY);
            Projectile.Center = shoulder;

            // The sprite is 70x70 with hilt at bottom-left.
            // For dir=1 (right): origin bottom-left, rotation = swingAngle + 45°
            // For dir=-1 (left): flip horizontally, origin bottom-right
            if (dir >= 0)
                drawRotation = swingAngle + MathHelper.PiOver4;
            else
                drawRotation = swingAngle - MathHelper.PiOver4;

            // Composite arm follows the swing angle
            // For left-facing, mirror the arm angle so the hand faces correctly
            float armRotation = dir >= 0 ? swingAngle - MathHelper.PiOver2 : swingAngle + MathHelper.PiOver2;
            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);
        }

        private Vector2 GetMaceHeadPosition(Player player, int dir)
        {
            // Calculate where the mace head actually is (end of the 70x70 sprite from the hilt)
            Vector2 shoulder = player.Center + new Vector2(ShoulderOffsetX * dir, ShoulderOffsetY);
            float headAngle = dir >= 0 ? swingAngle + MathHelper.PiOver4 : swingAngle - MathHelper.PiOver4;
            // The head is at the far corner diagonally from the hilt
            // For a 70x70 sprite, the diagonal from bottom-left to top-right is ~99px, scaled by DrawScale
            float headDist = SpriteSize * DrawScale * 0.85f; // approximate distance to mace head center
            return shoulder + headAngle.ToRotationVector2() * headDist;
        }

        private bool CheckGroundHit(Player player)
        {
            int dir = (int)Dir;
            if (dir == 0) dir = player.direction;

            // Check at the mace HEAD position, not the hilt
            Vector2 headPos = GetMaceHeadPosition(player, dir);
            int tileX = (int)(headPos.X / 16f);
            int tileY = (int)(headPos.Y / 16f);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = 0; dy <= 2; dy++)
                {
                    int cx = tileX + dx;
                    int cy = tileY + dy;
                    if (cx < 0 || cx >= Main.maxTilesX || cy < 0 || cy >= Main.maxTilesY)
                        continue;
                    Tile tile = Main.tile[cx, cy];
                    if (tile.HasTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                        return true;
                }
            }

            // Also check near the player's feet as fallback for flat ground
            int feetTileX = (int)((player.Bottom.X + dir * 16f) / 16f);
            int feetTileY = (int)(player.Bottom.Y / 16f);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = 0; dy <= 1; dy++)
                {
                    int cx = feetTileX + dx;
                    int cy = feetTileY + dy;
                    if (cx < 0 || cx >= Main.maxTilesX || cy < 0 || cy >= Main.maxTilesY)
                        continue;
                    Tile tile = Main.tile[cx, cy];
                    if (tile.HasTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                        return true;
                }
            }

            return false;
        }

        private void DoGroundImpact(Player player, int dir)
        {
            // --- Sound ---
            SoundEngine.PlaySound(SoundID.Item167 with { Volume = 1f, Pitch = -0.5f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = -0.6f }, Projectile.Center);

            // --- Visual dust burst at impact point ---
            Vector2 headPos = GetMaceHeadPosition(player, dir);
            for (int i = 0; i < 25; i++)
            {
                Vector2 dustVel = new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-8f, -2f));
                Dust d = Dust.NewDustDirect(headPos + new Vector2(Main.rand.NextFloat(-16f, 16f), 0f), 0, 0, DustID.GreenTorch, dustVel.X, dustVel.Y, 100, default, 1.8f);
                d.noGravity = true;
                d.fadeIn = 1.4f;
            }

            // Ground debris from impact point
            for (int i = 0; i < 12; i++)
            {
                Vector2 debrisVel = new Vector2(Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-4f, -0.5f));
                Dust d = Dust.NewDustDirect(headPos + new Vector2(Main.rand.NextFloat(-30f, 30f), 4f), 0, 0, DustID.Dirt, debrisVel.X, debrisVel.Y, 0, default, 1.6f);
                d.noGravity = false;
            }

            // Impact flash, big white burst at the mace head
            for (int i = 0; i < 14; i++)
            {
                Vector2 flashVel = Main.rand.NextVector2CircularEdge(5f, 5f);
                Dust flash = Dust.NewDustDirect(headPos, 0, 0, DustID.WhiteTorch, flashVel.X, flashVel.Y, 100, default, 2.2f);
                flash.noGravity = true;
                flash.fadeIn = 1.6f;
            }

            // Radial green shockwave dust ring
            for (int i = 0; i < 16; i++)
            {
                float angle = MathHelper.TwoPi * i / 16f;
                Vector2 ringVel = new Vector2((float)Math.Cos(angle) * 4f, (float)Math.Sin(angle) * 2.5f - 1f);
                Dust ring = Dust.NewDustDirect(headPos, 0, 0, DustID.GreenTorch, ringVel.X, ringVel.Y, 80, default, 1.4f);
                ring.noGravity = true;
                ring.fadeIn = 1.2f;
            }

            // Rock chunks
            for (int i = 0; i < 6; i++)
            {
                Vector2 chunkVel = new Vector2(Main.rand.NextFloat(-3f, 3f) + dir * 2f, Main.rand.NextFloat(-6f, -2f));
                Gore.NewGore(Projectile.GetSource_FromThis(), headPos, chunkVel, Main.rand.Next(61, 64), 0.8f);
            }

            // --- Spawn earthquake club projectiles + shockwave damage (server/singleplayer only) ---
            if (Projectile.owner == Main.myPlayer)
            {
                // Find the ground Y at the player's feet
                float groundY = FindGroundY(new Vector2(player.Center.X, player.Bottom.Y - 16f));
                if (groundY < 0f) groundY = player.Bottom.Y; // fallback to player feet
                int damage = Projectile.damage;

                for (int i = 0; i < EarthquakeColumns; i++)
                {
                    float xOff = (i + 1) * EarthquakeSpacing * dir;
                    float spawnX = headPos.X + xOff;

                    // Find ground at each column position, scanning from above
                    float colGroundY = FindGroundY(new Vector2(spawnX, player.Top.Y - 64f));
                    if (colGroundY < 0f) colGroundY = groundY; // fallback

                    Vector2 spawnPos = new Vector2(spawnX, colGroundY);
                    float speed = EarthquakeBaseSpeed + Main.rand.NextFloat(EarthquakeSpeedVariance);
                    float delay = i * 3f; // stagger each column

                    // Visual club projectile (flies upward, half damage)
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        spawnPos,
                        new Vector2(0f, -speed),
                        ModContent.ProjectileType<ClubEarthquakeProjectile>(),
                        damage,
                        Projectile.knockBack * 0.6f,
                        Projectile.owner,
                        delay, // ai[0] = spawn delay
                        0f
                    );

                    // Invisible earthquake hitbox (full damage, hits once per NPC)
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        spawnPos,
                        Vector2.Zero,
                        ModContent.ProjectileType<ClubEarthquakeHitbox>(),
                        damage,
                        Projectile.knockBack,
                        Projectile.owner,
                        delay, // ai[0] = spawn delay
                        0f
                    );
                }

                // Push player backward (away from facing direction)
                player.velocity.X -= dir * 4f;
                player.velocity.Y -= 3.5f;
            }

            // Sparks at mace head
            for (int i = 0; i < 8; i++)
            {
                Vector2 sparkVel = Main.rand.NextVector2CircularEdge(7f, 5f);
                sparkVel.Y = -Math.Abs(sparkVel.Y);
                Dust s = Dust.NewDustDirect(headPos, 0, 0, DustID.TerraBlade, sparkVel.X, sparkVel.Y, 0, default, 1.5f);
                s.noGravity = true;
            }
        }

        private float FindGroundY(Vector2 startPos)
        {
            int tileX = (int)(startPos.X / 16f);
            int startTileY = (int)(startPos.Y / 16f);
            if (tileX < 0 || tileX >= Main.maxTilesX) return -1f;

            // Scan downward to find first solid tile or platform
            for (int ty = Math.Max(0, startTileY); ty < Math.Min(Main.maxTilesY, startTileY + 30); ty++)
            {
                Tile tile = Main.tile[tileX, ty];
                if (tile.HasTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                    return ty * 16f; // top of this tile
            }

            return -1f;
        }

        public override bool? CanDamage()
        {
            // Only deal damage during the swing phase
            return Phase == PhaseSwing ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Player player = Main.player[Projectile.owner];
            int dir = (int)Dir;
            if (dir == 0) dir = player.direction;
            Vector2 start = Projectile.Center;
            Vector2 end = GetMaceHeadPosition(player, dir);
            float width = 36f * DrawScale;
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, width, ref _);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Launch enemies upward
            modifiers.HitDirectionOverride = 0;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Launch enemy into the air
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                float launchPower = Math.Min(14f, Projectile.knockBack * 1.2f);
                target.velocity.Y = -launchPower;
                target.velocity.X = (int)Dir * 2f;
                target.netUpdate = true;
            }

            SoundEngine.PlaySound(SoundID.Item167 with { Volume = 0.9f, Pitch = -0.3f }, target.Center);

            for (int i = 0; i < 10; i++)
            {
                Vector2 dustVel = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-6f, -2f));
                Dust d = Dust.NewDustDirect(target.Center, 0, 0, DustID.GreenTorch, dustVel.X, dustVel.Y, 100, default, 1.4f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;

            int dir = (int)Dir;
            if (dir == 0) dir = 1;

            // For right-facing: origin at bottom-left (hilt). Sprite extends up-right.
            // For left-facing: flip horizontally, origin at bottom-right.
            Vector2 origin;
            SpriteEffects flip;
            if (dir >= 0)
            {
                origin = new Vector2(0f, tex.Height);
                flip = SpriteEffects.None;
            }
            else
            {
                origin = new Vector2(tex.Width, tex.Height);
                flip = SpriteEffects.FlipHorizontally;
            }

            Color tint = Color.Lerp(lightColor, new Color(60, 200, 60), 0.4f);

            // Draw green trail during swing
            if (Phase == PhaseSwing)
            {
                for (int i = trailPositions.Count - 1; i >= 0; i--)
                {
                    float progress = (float)(i + 1) / TrailLength;
                    float alpha = (1f - progress) * 0.3f;
                    Color trailColor = new Color(40, 180, 40) * alpha;
                    Main.EntitySpriteDraw(tex, trailPositions[i] - Main.screenPosition, null, trailColor, trailRotations[i], origin, currentScale * (1f - progress * 0.08f), flip, 0);
                }
            }

            // Main sprite
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, tint, drawRotation, origin, currentScale, flip, 0);

            return false;
        }

        private void UpdateTrail()
        {
            trailPositions.Insert(0, Projectile.Center);
            trailRotations.Insert(0, drawRotation);
            while (trailPositions.Count > TrailLength)
            {
                trailPositions.RemoveAt(trailPositions.Count - 1);
                trailRotations.RemoveAt(trailRotations.Count - 1);
            }
        }
    }
}
