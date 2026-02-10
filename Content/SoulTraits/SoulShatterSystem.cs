using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace DeterministicChaos.Content.SoulTraits
{
    public class SoulShatterSystem : ModSystem
    {
        private static readonly List<SoulShard> shards = new List<SoulShard>();
        private const int ShardGridX = 4; // columns to cut the sprite into
        private const int ShardGridY = 5; // rows to cut the sprite into
        private const float Gravity = 0.12f;
        private const float ShardLifetime = 3f; // seconds

        private struct SoulShard
        {
            public Vector2 WorldPosition;
            public Vector2 Velocity;
            public float Rotation;
            public float RotationSpeed;
            public float Life;
            public float MaxLife;
            public float Scale;
            public Color Color;
            public Rectangle SourceRect; // which piece of the sprite sheet
            public string TexturePath;
        }

        public static void SpawnShatter(Vector2 worldPosition, SoulTraitType trait)
        {
            if (trait == SoulTraitType.None || Main.dedServ)
                return;

            string texturePath = "DeterministicChaos/Content/SoulTraits/" + trait.ToString();
            Texture2D soulTexture = ModContent.Request<Texture2D>(texturePath, AssetRequestMode.ImmediateLoad).Value;

            Color traitColor = SoulTraitData.GetTraitColor(trait);
            int texW = soulTexture.Width;
            int texH = soulTexture.Height;
            int cellW = texW / ShardGridX;
            int cellH = texH / ShardGridY;

            // The soul draws at 0.8 scale, so shards should match
            float baseScale = 0.8f;

            for (int gx = 0; gx < ShardGridX; gx++)
            {
                for (int gy = 0; gy < ShardGridY; gy++)
                {
                    Rectangle sourceRect = new Rectangle(gx * cellW, gy * cellH, cellW, cellH);

                    // Calculate offset of this shard relative to soul center (in world space, at drawn scale)
                    float offsetX = (gx * cellW + cellW / 2f - texW / 2f) * baseScale;
                    float offsetY = (gy * cellH + cellH / 2f - texH / 2f) * baseScale;

                    // Direction outward from center, with some randomness
                    Vector2 dir = new Vector2(offsetX, offsetY);
                    if (dir.LengthSquared() < 0.01f)
                        dir = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f));
                    dir.Normalize();

                    // Fling outward — shards farther from center go faster
                    float dist = new Vector2(offsetX, offsetY).Length();
                    float speed = 2f + dist * 0.15f + Main.rand.NextFloat(0f, 2f);
                    Vector2 velocity = dir * speed + new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-3f, -1f));

                    float lifetime = ShardLifetime + Main.rand.NextFloat(-0.5f, 0.5f);

                    shards.Add(new SoulShard
                    {
                        WorldPosition = worldPosition + new Vector2(offsetX, offsetY),
                        Velocity = velocity,
                        Rotation = Main.rand.NextFloat(-MathHelper.Pi, MathHelper.Pi),
                        RotationSpeed = Main.rand.NextFloat(-0.15f, 0.15f),
                        Life = lifetime,
                        MaxLife = lifetime,
                        Scale = baseScale * Main.rand.NextFloat(0.9f, 1.1f),
                        Color = traitColor,
                        SourceRect = sourceRect,
                        TexturePath = texturePath
                    });
                }
            }
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || shards.Count == 0)
                return;

            float dt = 1f / 60f;

            for (int i = shards.Count - 1; i >= 0; i--)
            {
                var s = shards[i];

                s.WorldPosition += s.Velocity;
                s.Velocity.Y += Gravity;
                s.Velocity.X *= 0.99f; // slight air drag
                s.Rotation += s.RotationSpeed;
                s.Life -= dt;

                // Slow down rotation over time
                s.RotationSpeed *= 0.995f;

                shards[i] = s;

                if (s.Life <= 0)
                    shards.RemoveAt(i);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (shards.Count == 0)
                return;

            // Draw shards as a game-scale UI layer so they appear above everything (like the soul normally does)
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1)
            {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "DeterministicChaos: Soul Shatter",
                    delegate
                    {
                        DrawShards();
                        return true;
                    },
                    InterfaceScaleType.Game
                ));
            }
        }

        private void DrawShards()
        {
            SpriteBatch spriteBatch = Main.spriteBatch;

            foreach (var s in shards)
            {
                Texture2D tex = ModContent.Request<Texture2D>(s.TexturePath, AssetRequestMode.ImmediateLoad).Value;

                float alpha = MathHelper.Clamp(s.Life / s.MaxLife, 0f, 1f);
                // Fade out faster in the last 30% of life
                if (alpha < 0.3f)
                    alpha *= (alpha / 0.3f);

                Color drawColor = s.Color * alpha;
                Vector2 origin = new Vector2(s.SourceRect.Width / 2f, s.SourceRect.Height / 2f);
                Vector2 screenPos = s.WorldPosition - Main.screenPosition;

                // Draw a faint glow behind the shard
                Color glowColor = s.Color * alpha * 0.3f;
                spriteBatch.Draw(
                    tex,
                    screenPos,
                    s.SourceRect,
                    glowColor,
                    s.Rotation,
                    origin,
                    s.Scale * 1.3f,
                    SpriteEffects.None,
                    0f
                );

                // Draw the shard itself
                spriteBatch.Draw(
                    tex,
                    screenPos,
                    s.SourceRect,
                    drawColor,
                    s.Rotation,
                    origin,
                    s.Scale,
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
}
