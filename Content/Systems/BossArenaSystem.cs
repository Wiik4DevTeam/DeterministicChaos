using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using ReLogic.Content;

namespace DeterministicChaos.Content.Systems
{
    // Manages rectangular boss arena borders. One-way barrier: players can enter
    // freely, but once inside they are locked in. Draws a pulsing white border.
    public class BossArenaSystem : ModSystem
    {
        public override void Load()
        {
            On_Main.DrawNPCs += DrawBackgroundBeforeNPCs;
        }

        public override void Unload()
        {
            On_Main.DrawNPCs -= DrawBackgroundBeforeNPCs;
        }

        // Hook injected before NPC drawing so the arena background appears behind entities.
        private static void DrawBackgroundBeforeNPCs(On_Main.orig_DrawNPCs orig, Main self, bool behindTiles)
        {
            // Draw background during the behind-tiles pass so tiles render on top
            if (behindTiles && (ActiveBoxes.Count > 0 || _shatterShards.Count > 0))
            {
                SpriteBatch sb = Main.spriteBatch;

                // End whatever batch the game has open
                try { sb.End(); } catch { }

                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullCounterClockwise, null,
                    Main.GameViewMatrix.TransformationMatrix);

                Texture2D pixel = TextureAssets.MagicPixel.Value;
                foreach (var box in ActiveBoxes)
                    DrawBackground(sb, pixel, box);

                // Draw background shatter shards behind entities
                if (_shatterShards.Count > 0)
                    DrawShatterShards(sb, pixel, false);

                sb.End();

                // Restart the batch the game expects to be open
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullCounterClockwise, null,
                    Main.GameViewMatrix.TransformationMatrix);
            }

            orig(self, behindTiles);
        }

        public class ArenaBox
        {
            public Vector2 Center;

            public float HalfWidth;
            public float HalfHeight;

            public float TargetHalfWidth;
            public float TargetHalfHeight;

            public Color BorderColor = Color.White;
            public float BorderThickness = 10f;

            public Func<bool> RemovalCondition;

            public float LerpSpeed = 0.02f;

            // Optional background texture path (e.g. "DeterministicChaos/Content/NPCs/Bosses/JevilBG").
            // When set, the texture is drawn with a liquid warp effect and tinted by BackgroundTint.
            public string BackgroundTexturePath;
            public Color BackgroundTint = Color.White;

            // Controls the scroll speed of the background (pixels/sec). Default 60.
            public float BackgroundScrollSpeed = 200f;

            // Multiplier for the warp distortion. 1.0 = normal, higher = more extreme.
            public float BackgroundWarpIntensity = 1f;

            // Set of player whoAmI indices that have entered the arena
            // and are now locked inside.
            public HashSet<int> LockedPlayers = new HashSet<int>();

            public Rectangle Bounds => new Rectangle(
                (int)(Center.X - HalfWidth),
                (int)(Center.Y - HalfHeight),
                (int)(HalfWidth * 2),
                (int)(HalfHeight * 2));
        }

        private static float pulseTimer;

        public static List<ArenaBox> ActiveBoxes = new List<ArenaBox>();

        // ── Arena Split System ──

        private class ArenaSplit
        {
            public Vector2 Center;
            public float Angle;
            public Vector2 Normal;
            public int Timer;
            public int MaxTimer;
            public float MaxDisplacement;
            public float PrevEase;
        }

        private static readonly List<ArenaSplit> _activeSplits = new List<ArenaSplit>();
        private const int SplitLifetime = 50;
        private const float SplitMaxDisp = 105f;
        private const int SegmentsPerSide = 50;

        // ── Arena Shatter System ──

        private class ShatterShard
        {
            public Vector2 WorldPos;
            public Vector2 Velocity;
            public float Rotation;
            public float RotSpeed;
            public float Width;
            public float Height;
            public Color Color;
            public float Alpha;
            public float Life;     // remaining life (0..1)
            public float MaxLife;  // total lifetime in seconds
            public bool IsBorder;  // true = drawn in border pass, false = drawn in bg pass
            // Optional texture region for background shards
            public string TexturePath;
            public Rectangle? SourceRect;
        }

        private static readonly List<ShatterShard> _shatterShards = new List<ShatterShard>();
        private const int ShatterShardCount = 80;        // border shards
        private const int ShatterBgShardCount = 120;     // background shards
        private const float ShatterLifetime = 2.0f;      // seconds
        private const float ShatterGravity = 280f;       // pixels/sec²
        private const float ShatterBurstSpeed = 350f;    // initial outward speed
        private const float ArenaLightStrength = 0.95f;
        private const int ArenaLightTileStep = 1;

        // Spawns shatter shards from the arena's border and background.
        // Called when an arena's RemovalCondition triggers.
        private static void SpawnShatterShards(ArenaBox box)
        {
            float left = box.Center.X - box.HalfWidth;
            float right = box.Center.X + box.HalfWidth;
            float top = box.Center.Y - box.HalfHeight;
            float bottom = box.Center.Y + box.HalfHeight;
            float arenaW = box.HalfWidth * 2f;
            float arenaH = box.HalfHeight * 2f;

            var rng = Main.rand;

            // ── Border shards ──
            // Distribute evenly along the 4 sides
            int perSide = ShatterShardCount / 4;
            for (int side = 0; side < 4; side++)
            {
                for (int i = 0; i < perSide; i++)
                {
                    float t = (i + (float)rng.NextDouble()) / perSide;
                    Vector2 pos;
                    Vector2 outward;
                    float w, h;

                    switch (side)
                    {
                        case 0: // top
                            pos = new Vector2(MathHelper.Lerp(left, right, t), top);
                            outward = new Vector2(rng.NextFloat(-0.3f, 0.3f), -1f);
                            w = rng.NextFloat(8f, 20f);
                            h = rng.NextFloat(4f, 10f);
                            break;
                        case 1: // bottom
                            pos = new Vector2(MathHelper.Lerp(left, right, t), bottom);
                            outward = new Vector2(rng.NextFloat(-0.3f, 0.3f), 1f);
                            w = rng.NextFloat(8f, 20f);
                            h = rng.NextFloat(4f, 10f);
                            break;
                        case 2: // left
                            pos = new Vector2(left, MathHelper.Lerp(top, bottom, t));
                            outward = new Vector2(-1f, rng.NextFloat(-0.3f, 0.3f));
                            w = rng.NextFloat(4f, 10f);
                            h = rng.NextFloat(8f, 20f);
                            break;
                        default: // right
                            pos = new Vector2(right, MathHelper.Lerp(top, bottom, t));
                            outward = new Vector2(1f, rng.NextFloat(-0.3f, 0.3f));
                            w = rng.NextFloat(4f, 10f);
                            h = rng.NextFloat(8f, 20f);
                            break;
                    }

                    outward.Normalize();
                    float speed = ShatterBurstSpeed * rng.NextFloat(0.6f, 1.2f);

                    _shatterShards.Add(new ShatterShard
                    {
                        WorldPos = pos,
                        Velocity = outward * speed,
                        Rotation = rng.NextFloat(-0.3f, 0.3f),
                        RotSpeed = rng.NextFloat(-4f, 4f),
                        Width = w,
                        Height = h,
                        Color = box.BorderColor,
                        Alpha = 1f,
                        Life = 1f,
                        MaxLife = ShatterLifetime * rng.NextFloat(0.7f, 1.0f),
                        IsBorder = true,
                        TexturePath = null,
                        SourceRect = null
                    });
                }
            }

            // ── Background shards ──
            // Sample grid cells from the background
            Color bgColor = Color.Black;
            if (!string.IsNullOrEmpty(box.BackgroundTexturePath))
                bgColor = box.BackgroundTint;

            int gridSide = (int)Math.Ceiling(Math.Sqrt(ShatterBgShardCount));
            float cellW = arenaW / gridSide;
            float cellH = arenaH / gridSide;

            for (int row = 0; row < gridSide; row++)
            {
                for (int col = 0; col < gridSide; col++)
                {
                    if (_shatterShards.Count(s => !s.IsBorder) >= ShatterBgShardCount)
                        break;

                    float cx = left + (col + 0.5f) * cellW + rng.NextFloat(-cellW * 0.3f, cellW * 0.3f);
                    float cy = top + (row + 0.5f) * cellH + rng.NextFloat(-cellH * 0.3f, cellH * 0.3f);

                    // Direction: outward from center with some randomness
                    Vector2 dir = new Vector2(cx, cy) - box.Center;
                    if (dir.LengthSquared() < 1f)
                        dir = new Vector2(rng.NextFloat(-1f, 1f), rng.NextFloat(-1f, 1f));
                    dir.Normalize();
                    dir += new Vector2(rng.NextFloat(-0.4f, 0.4f), rng.NextFloat(-0.4f, 0.4f));
                    dir.Normalize();

                    float speed = ShatterBurstSpeed * rng.NextFloat(0.3f, 0.9f);
                    float shardW = cellW * rng.NextFloat(0.6f, 1.5f);
                    float shardH = cellH * rng.NextFloat(0.6f, 1.5f);

                    _shatterShards.Add(new ShatterShard
                    {
                        WorldPos = new Vector2(cx, cy),
                        Velocity = dir * speed + new Vector2(0f, rng.NextFloat(-60f, -20f)),
                        Rotation = rng.NextFloat(0f, MathHelper.TwoPi),
                        RotSpeed = rng.NextFloat(-5f, 5f),
                        Width = shardW,
                        Height = shardH,
                        Color = bgColor,
                        Alpha = 1f,
                        Life = 1f,
                        MaxLife = ShatterLifetime * rng.NextFloat(0.6f, 1.0f),
                        IsBorder = false,
                        TexturePath = box.BackgroundTexturePath,
                        SourceRect = null
                    });
                }
            }
        }

        private static void TickShatterShards()
        {
            float dt = 0.016f; // ~60fps
            for (int i = _shatterShards.Count - 1; i >= 0; i--)
            {
                var s = _shatterShards[i];
                s.Life -= dt / s.MaxLife;
                if (s.Life <= 0f)
                {
                    _shatterShards.RemoveAt(i);
                    continue;
                }

                s.Velocity.Y += ShatterGravity * dt;
                s.WorldPos += s.Velocity * dt;
                s.Rotation += s.RotSpeed * dt;

                // Fade out in the last 40% of life
                s.Alpha = MathHelper.Clamp(s.Life / 0.4f, 0f, 1f);
            }
        }

        private static void DrawShatterShards(SpriteBatch sb, Texture2D pixel, bool borderPass)
        {
            Vector2 scr = Main.screenPosition;

            foreach (var s in _shatterShards)
            {
                if (s.IsBorder != borderPass)
                    continue;

                Vector2 screenPos = s.WorldPos - scr;
                Vector2 origin = new Vector2(s.Width * 0.5f, s.Height * 0.5f);
                Color drawColor = s.Color * s.Alpha;

                if (!s.IsBorder && !string.IsNullOrEmpty(s.TexturePath))
                {
                    // Draw textured bg shard
                    Texture2D tex = null;
                    try { tex = ModContent.Request<Texture2D>(s.TexturePath, AssetRequestMode.ImmediateLoad).Value; }
                    catch { }

                    if (tex != null)
                    {
                        // Use a region of the texture mapped to shard size
                        Rectangle destRect = new Rectangle(
                            (int)(screenPos.X - origin.X),
                            (int)(screenPos.Y - origin.Y),
                            (int)s.Width, (int)s.Height);

                        // Sample a random source region if not set
                        if (s.SourceRect == null)
                        {
                            int sw = Math.Max(1, (int)(tex.Width * s.Width / 2000f));
                            int sh = Math.Max(1, (int)(tex.Height * s.Height / 2000f));
                            int sx = Main.rand.Next(0, Math.Max(1, tex.Width - sw));
                            int sy = Main.rand.Next(0, Math.Max(1, tex.Height - sh));
                            s.SourceRect = new Rectangle(sx, sy, sw, sh);
                        }

                        sb.Draw(tex, screenPos, s.SourceRect, drawColor, s.Rotation, 
                            new Vector2(s.SourceRect.Value.Width * 0.5f, s.SourceRect.Value.Height * 0.5f),
                            new Vector2(s.Width / s.SourceRect.Value.Width, s.Height / s.SourceRect.Value.Height),
                            SpriteEffects.None, 0f);
                        continue;
                    }
                }

                // Fallback: solid color rectangle
                Rectangle destRect2 = new Rectangle(
                    (int)(screenPos.X - s.Width * 0.5f),
                    (int)(screenPos.Y - s.Height * 0.5f),
                    (int)s.Width, (int)s.Height);
                sb.Draw(pixel, destRect2, drawColor);
            }
        }

        // Called when a split-screen slash fires to create an arena split effect.
        public static void TriggerArenaSplit(Vector2 center, float worldAngle)
        {
            // Normalize angle to [0, PI) for deduplication (a line at θ == θ+PI)
            float norm = worldAngle % MathHelper.Pi;
            if (norm < 0f) norm += MathHelper.Pi;

            foreach (var s in _activeSplits)
            {
                float sNorm = s.Angle % MathHelper.Pi;
                if (sNorm < 0f) sNorm += MathHelper.Pi;
                float diff = Math.Abs(norm - sNorm);
                if (diff < 0.2f || Math.Abs(diff - MathHelper.Pi) < 0.2f)
                    return; // Same line already active
            }

            if (_activeSplits.Count >= 2) return;

            float normalAngle = worldAngle + MathHelper.PiOver2;
            _activeSplits.Add(new ArenaSplit
            {
                Center = center,
                Angle = worldAngle,
                Normal = new Vector2((float)Math.Cos(normalAngle), (float)Math.Sin(normalAngle)),
                Timer = SplitLifetime,
                MaxTimer = SplitLifetime,
                MaxDisplacement = SplitMaxDisp
            });
        }

        // Returns the displacement vector for a world point based on active splits.
        // Points on opposite sides of a split line are displaced in opposite directions.
        private static Vector2 GetSplitDisplacement(Vector2 worldPoint)
        {
            Vector2 total = Vector2.Zero;
            foreach (var split in _activeSplits)
            {
                float dot = Vector2.Dot(worldPoint - split.Center, split.Normal);
                float sign = dot >= 0f ? 1f : -1f;

                float progress = 1f - (split.Timer / (float)split.MaxTimer);
                float ease = (float)Math.Sin(Math.PI * progress);

                total += split.Normal * sign * split.MaxDisplacement * ease;
            }
            return total;
        }

        public static ArenaBox CreateArena(Vector2 bottomCenter, float width, float height, Func<bool> removal)
        {
            var box = new ArenaBox
            {
                Center = bottomCenter - new Vector2(0f, height / 2f),
                HalfWidth = width / 2f,
                HalfHeight = height / 2f,
                TargetHalfWidth = width / 2f,
                TargetHalfHeight = height / 2f,
                RemovalCondition = removal
            };
            ActiveBoxes.Add(box);
            return box;
        }

        public override void PostUpdateNPCs()
        {
            for (int i = ActiveBoxes.Count - 1; i >= 0; i--)
            {
                if (ActiveBoxes[i].RemovalCondition != null && ActiveBoxes[i].RemovalCondition())
                {
                    SpawnShatterShards(ActiveBoxes[i]);
                    ActiveBoxes.RemoveAt(i);
                }
            }

            TickShatterShards();

            foreach (var box in ActiveBoxes)
            {
                box.HalfWidth = MathHelper.Lerp(box.HalfWidth, box.TargetHalfWidth, box.LerpSpeed);
                box.HalfHeight = MathHelper.Lerp(box.HalfHeight, box.TargetHalfHeight, box.LerpSpeed);
            }

            // Tick down active splits
            for (int i = _activeSplits.Count - 1; i >= 0; i--)
            {
                _activeSplits[i].Timer--;
                if (_activeSplits[i].Timer <= 0)
                    _activeSplits.RemoveAt(i);
            }
        }

        // After all player updates: track who has entered, then clamp locked players.
        public override void PostUpdatePlayers()
        {
            pulseTimer++;

            foreach (var box in ActiveBoxes)
            {
                float left = box.Center.X - box.HalfWidth;
                float right = box.Center.X + box.HalfWidth;
                float top = box.Center.Y - box.HalfHeight;
                float bottom = box.Center.Y + box.HalfHeight;

                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player p = Main.player[i];
                    if (!p.active || p.dead)
                        continue;

                    bool inside = p.position.X >= left
                        && p.position.X + p.width <= right
                        && p.position.Y >= top
                        && p.position.Y + p.height <= bottom;

                    // Once a player enters, lock them in
                    if (inside && !box.LockedPlayers.Contains(i))
                        box.LockedPlayers.Add(i);

                    // Only clamp players that are locked in
                    if (box.LockedPlayers.Contains(i))
                    {
                        // Carry the player with the arena split movement (apply delta)
                        foreach (var split in _activeSplits)
                        {
                            float progress = 1f - (split.Timer / (float)split.MaxTimer);
                            float ease = (float)Math.Sin(Math.PI * progress);
                            float delta = (ease - split.PrevEase) * split.MaxDisplacement;

                            float dot = Vector2.Dot(p.Center - split.Center, split.Normal);
                            float sign = dot >= 0f ? 1f : -1f;

                            p.position += split.Normal * sign * delta;
                        }

                        // Clamp to displaced bounds
                        Vector2 disp = GetSplitDisplacement(p.Center);
                        ClampPlayer(p, left + disp.X, right + disp.X, top + disp.Y, bottom + disp.Y);
                    }
                }
            }

            // Update PrevEase for next frame
            foreach (var split in _activeSplits)
            {
                float progress = 1f - (split.Timer / (float)split.MaxTimer);
                split.PrevEase = (float)Math.Sin(Math.PI * progress);
            }
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || ActiveBoxes.Count == 0)
                return;

            foreach (var box in ActiveBoxes)
                LightArenaInterior(box);
        }

        private static void LightArenaInterior(ArenaBox box)
        {
            int minTileX = (int)Math.Floor((box.Center.X - box.HalfWidth) / 16f);
            int maxTileX = (int)Math.Ceiling((box.Center.X + box.HalfWidth) / 16f);
            int minTileY = (int)Math.Floor((box.Center.Y - box.HalfHeight) / 16f);
            int maxTileY = (int)Math.Ceiling((box.Center.Y + box.HalfHeight) / 16f);

            minTileX = Utils.Clamp(minTileX, 0, Main.maxTilesX - 1);
            maxTileX = Utils.Clamp(maxTileX, 0, Main.maxTilesX - 1);
            minTileY = Utils.Clamp(minTileY, 0, Main.maxTilesY - 1);
            maxTileY = Utils.Clamp(maxTileY, 0, Main.maxTilesY - 1);

            for (int tileY = minTileY; tileY <= maxTileY; tileY += ArenaLightTileStep)
            {
                for (int tileX = minTileX; tileX <= maxTileX; tileX += ArenaLightTileStep)
                {
                    Vector2 samplePos = new Vector2(tileX * 16f + 8f, tileY * 16f + 8f);
                    Vector2 displacement = GetSplitDisplacement(samplePos);
                    Lighting.AddLight(samplePos + displacement, ArenaLightStrength, ArenaLightStrength, ArenaLightStrength);
                }
            }
        }

        private static void ClampPlayer(Player p, float left, float right, float top, float bottom)
        {
            if (p.position.X < left)
            {
                p.position.X = left;
                if (p.velocity.X < 0f) p.velocity.X = 0f;
            }
            if (p.position.X + p.width > right)
            {
                p.position.X = right - p.width;
                if (p.velocity.X > 0f) p.velocity.X = 0f;
            }
            if (p.position.Y < top)
            {
                p.position.Y = top;
                if (p.velocity.Y < 0f)
                {
                    p.velocity.Y = 0.01f; // simulate real ceiling bounce
                    p.wingTime = 0; // kill wing flight so player can't hover at ceiling
                }
            }
            if (p.position.Y + p.height > bottom)
            {
                p.position.Y = bottom - p.height;

                // Only simulate ground contact when actually falling
                if (p.velocity.Y > 0f)
                {
                    p.velocity.Y = 0f;
                    p.fallStart = (int)(p.position.Y / 16f);
                    p.fallStart2 = (int)(p.position.Y / 16f);
                }
            }
        }

        // Returns true if the given player index is locked inside any active arena.
        public static bool IsPlayerLockedIn(int playerWhoAmI)
        {
            foreach (var box in ActiveBoxes)
            {
                if (box.LockedPlayers.Contains(playerWhoAmI))
                    return true;
            }
            return false;
        }

        public static bool IsInsideArena(ArenaBox box, Vector2 worldPos)
        {
            return worldPos.X >= box.Center.X - box.HalfWidth
                && worldPos.X <= box.Center.X + box.HalfWidth
                && worldPos.Y >= box.Center.Y - box.HalfHeight
                && worldPos.Y <= box.Center.Y + box.HalfHeight;
        }

        public override void PostDrawTiles()
        {
            if (ActiveBoxes.Count == 0 && _shatterShards.Count == 0)
                return;

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            foreach (var box in ActiveBoxes)
            {
                DrawBorder(sb, pixel, box);
            }

            // Draw border shatter shards on top
            if (_shatterShards.Count > 0)
                DrawShatterShards(sb, pixel, true);

            sb.End();
        }

        private static float _bgTimer;
        private static float _scrollFrac; // 0..1 fractional scroll position

        private static void DrawBackground(SpriteBatch sb, Texture2D pixel, ArenaBox box)
        {
            _bgTimer += 0.016f; // ~60fps time accumulator

            float arenaW = box.HalfWidth * 2f;
            float arenaH = box.HalfHeight * 2f;
            float left = box.Center.X - box.HalfWidth;
            float top = box.Center.Y - box.HalfHeight;
            Vector2 scr = Main.screenPosition;

            // Draw solid black underlay first (always)
            Color bg = Color.Black;
            float cellW = arenaW / SegmentsPerSide;
            float cellH = arenaH / SegmentsPerSide;

            for (int row = 0; row < SegmentsPerSide; row++)
            {
                float wy = top + row * cellH;
                for (int col = 0; col < SegmentsPerSide; col++)
                {
                    float wx = left + col * cellW;
                    Vector2 d = GetSplitDisplacement(new Vector2(wx + cellW * 0.5f, wy + cellH * 0.5f));
                    int px = (int)(wx + d.X - scr.X);
                    int py = (int)(wy + d.Y - scr.Y);
                    sb.Draw(pixel, new Rectangle(px, py, (int)cellW + 1, (int)cellH + 1), bg);
                }
            }

            // If no background texture, stop here
            if (string.IsNullOrEmpty(box.BackgroundTexturePath))
                return;

            Texture2D bgTex;
            try
            {
                bgTex = ModContent.Request<Texture2D>(box.BackgroundTexturePath, AssetRequestMode.ImmediateLoad).Value;
            }
            catch { return; }
            if (bgTex == null) return;

            int texW = bgTex.Width;
            int texH = bgTex.Height;

            // Fixed reference scale so width doesn't jitter during arena resize
            const float referenceH = 2000f;
            float scale = referenceH / texH;
            float scaledW = texW * scale;

            Color tint = box.BackgroundTint;

            // Accumulate scroll as a fraction (0..1) of texture width, immune to scale changes
            float scrollSpeed = box.BackgroundScrollSpeed;
            _scrollFrac += (scrollSpeed * 0.016f) / scaledW;
            _scrollFrac %= 1f;

            // Grid-based background rendering (supports splits at all angles)
            int gridRes = SegmentsPerSide;
            float bgCellW = arenaW / gridRes;
            float bgCellH = arenaH / gridRes;
            int srcCellH = Math.Max(1, texH / gridRes);

            for (int row = 0; row < gridRes; row++)
            {
                float wy = top + row * bgCellH;
                float normalY = row / (float)gridRes;
                int srcY = (int)(normalY * texH);
                int srcH = Math.Min(srcCellH, texH - srcY);
                if (srcH <= 0) continue;

                for (int col = 0; col < gridRes; col++)
                {
                    float wx = left + col * bgCellW;

                    // Per-cell split displacement (handles vertical + diagonal splits)
                    Vector2 disp = GetSplitDisplacement(new Vector2(wx + bgCellW * 0.5f, wy + bgCellH * 0.5f));

                    int destX = (int)(wx + disp.X - scr.X);
                    int destY = (int)(wy + disp.Y - scr.Y);
                    Rectangle dest = new Rectangle(destX, destY, (int)bgCellW + 1, (int)bgCellH + 1);

                    // Map cell's world X position to source texture X (with scrolling + tiling)
                    float relX = wx - box.Center.X + scaledW * 0.5f + _scrollFrac * scaledW;
                    relX = ((relX % scaledW) + scaledW) % scaledW;
                    int srcX = (int)(relX / scaledW * texW);
                    int srcW = Math.Max(1, (int)(bgCellW / scaledW * texW));
                    if (srcX + srcW > texW) srcW = texW - srcX;
                    if (srcW <= 0) continue;

                    Rectangle src = new Rectangle(srcX, srcY, srcW, srcH);
                    sb.Draw(bgTex, dest, src, tint);
                }
            }
        }

        private static void DrawBorder(SpriteBatch sb, Texture2D pixel, ArenaBox box)
        {
            // Pulse: oscillate alpha and thickness
            float pulse = (float)Math.Sin(pulseTimer * 0.05f);
            float alphaBase = 0.7f + 0.3f * pulse;
            float thickPulse = box.BorderThickness + 2f * pulse;

            int t = Math.Max(1, (int)thickPulse);
            Color c = box.BorderColor * alphaBase;
            int g = t + 4;
            Color glow = box.BorderColor * (alphaBase * 0.25f);

            float left = box.Center.X - box.HalfWidth;
            float right = box.Center.X + box.HalfWidth;
            float top = box.Center.Y - box.HalfHeight;
            float bottom = box.Center.Y + box.HalfHeight;
            float totalW = box.HalfWidth * 2f;
            float totalH = box.HalfHeight * 2f;
            float segW = totalW / SegmentsPerSide;
            float segH = totalH / SegmentsPerSide;
            Vector2 scr = Main.screenPosition;

            // ── Glow pass (behind) ──
            for (int i = 0; i < SegmentsPerSide; i++)
            {
                float wx, wy;
                Vector2 d;
                int px, py;

                // Top glow
                wx = left + i * segW;
                d = GetSplitDisplacement(new Vector2(wx + segW * 0.5f, top));
                px = (int)(wx + d.X - scr.X);
                py = (int)(top + d.Y - scr.Y);
                sb.Draw(pixel, new Rectangle(px, py - g, (int)segW + 1, g), glow);

                // Bottom glow
                d = GetSplitDisplacement(new Vector2(wx + segW * 0.5f, bottom));
                px = (int)(wx + d.X - scr.X);
                py = (int)(bottom + d.Y - scr.Y);
                sb.Draw(pixel, new Rectangle(px, py, (int)segW + 1, g), glow);

                // Left glow
                wy = top + i * segH;
                d = GetSplitDisplacement(new Vector2(left, wy + segH * 0.5f));
                px = (int)(left + d.X - scr.X);
                py = (int)(wy + d.Y - scr.Y);
                sb.Draw(pixel, new Rectangle(px - g, py, g, (int)segH + 1), glow);

                // Right glow
                d = GetSplitDisplacement(new Vector2(right, wy + segH * 0.5f));
                px = (int)(right + d.X - scr.X);
                py = (int)(wy + d.Y - scr.Y);
                sb.Draw(pixel, new Rectangle(px, py, g, (int)segH + 1), glow);
            }

            // ── Border pass (on top) ──
            for (int i = 0; i < SegmentsPerSide; i++)
            {
                float wx, wy;
                Vector2 d;
                int px, py;

                // Top border
                wx = left + i * segW;
                d = GetSplitDisplacement(new Vector2(wx + segW * 0.5f, top));
                px = (int)(wx + d.X - scr.X);
                py = (int)(top + d.Y - scr.Y);
                sb.Draw(pixel, new Rectangle(px, py - t, (int)segW + 1, t), c);

                // Bottom border
                d = GetSplitDisplacement(new Vector2(wx + segW * 0.5f, bottom));
                px = (int)(wx + d.X - scr.X);
                py = (int)(bottom + d.Y - scr.Y);
                sb.Draw(pixel, new Rectangle(px, py, (int)segW + 1, t), c);

                // Left border
                wy = top + i * segH;
                d = GetSplitDisplacement(new Vector2(left, wy + segH * 0.5f));
                px = (int)(left + d.X - scr.X);
                py = (int)(wy + d.Y - scr.Y);
                sb.Draw(pixel, new Rectangle(px - t, py, t, (int)segH + 1), c);

                // Right border
                d = GetSplitDisplacement(new Vector2(right, wy + segH * 0.5f));
                px = (int)(right + d.X - scr.X);
                py = (int)(wy + d.Y - scr.Y);
                sb.Draw(pixel, new Rectangle(px, py, t, (int)segH + 1), c);
            }
        }

        public override void OnWorldUnload()
        {
            ActiveBoxes.Clear();
            _activeSplits.Clear();
            _shatterShards.Clear();
            pulseTimer = 0f;
        }
    }
}
