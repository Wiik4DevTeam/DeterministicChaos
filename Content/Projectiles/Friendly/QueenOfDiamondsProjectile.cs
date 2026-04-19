using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class QueenOfDiamondsProjectile : ModProjectile
    {
        private const int MaxDiamonds = 5;
        private const float DiamondSpeed = 10f;
        private const int TrailLength = 6;
        private List<List<Vector2>> trailHistory = new List<List<Vector2>>();

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.IsAWhip[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.DefaultToWhip();
            Projectile.WhipSettings.Segments = 20;
            Projectile.WhipSettings.RangeMultiplier = 1.2f;
        }

        private float Timer
        {
            get => Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Main.player[Projectile.owner].MinionAttackTargetNPC = target.whoAmI;
            Projectile.damage = (int)(Projectile.damage * 0.7f);

            if (Main.myPlayer != Projectile.owner)
                return;

            SpawnDiamondsFromMinions(target);
        }

        private void SpawnDiamondsFromMinions(NPC target)
        {
            Player owner = Main.player[Projectile.owner];
            int type = ModContent.ProjectileType<FriendlyDiamondProjectile>();
            int damage = Projectile.damage;
            int spawned = 0;

            for (int i = 0; i < Main.maxProjectiles && spawned < MaxDiamonds; i++)
            {
                Projectile minion = Main.projectile[i];
                if (!minion.active || minion.owner != Projectile.owner)
                    continue;
                if (!minion.minion)
                    continue;

                Vector2 toTarget = (target.Center - minion.Center).SafeNormalize(Vector2.UnitX);
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    minion.Center,
                    toTarget * DiamondSpeed,
                    type,
                    damage,
                    2f,
                    Projectile.owner
                );
                spawned++;
            }
        }

        public override void PostAI()
        {
            List<Vector2> points = new List<Vector2>();
            Projectile.FillWhipControlPoints(Projectile, points);

            foreach (Vector2 point in points)
            {
                Lighting.AddLight(point, 0.6f, 0.4f, 0.1f);
            }

            // Store trail history for afterimages
            List<Vector2> currentPoints = new List<Vector2>(points);
            trailHistory.Insert(0, currentPoints);
            if (trailHistory.Count > TrailLength)
            {
                trailHistory.RemoveAt(trailHistory.Count - 1);
            }
        }

        private void DrawLine(List<Vector2> list)
        {
            Texture2D texture = TextureAssets.FishingLine.Value;
            Rectangle frame = texture.Frame();
            Vector2 origin = new Vector2(frame.Width / 2, 2);

            Vector2 pos = list[0];
            for (int i = 0; i < list.Count - 1; i++)
            {
                Vector2 element = list[i];
                Vector2 diff = list[i + 1] - element;

                float rotation = diff.ToRotation() - MathHelper.PiOver2;
                Color color = Lighting.GetColor(element.ToTileCoordinates(), new Color(230, 150, 30));
                Vector2 scale = new Vector2(1, (diff.Length() + 2) / frame.Height);

                Main.EntitySpriteDraw(texture, pos - Main.screenPosition, frame, color, rotation, origin, scale, SpriteEffects.None, 0);
                pos += diff;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            List<Vector2> list = new List<Vector2>();
            Projectile.FillWhipControlPoints(Projectile, list);

            SpriteEffects flip = Projectile.spriteDirection < 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Texture2D texture = TextureAssets.Projectile[Type].Value;

            // Draw afterimages from trail history
            for (int trail = trailHistory.Count - 1; trail >= 0; trail--)
            {
                List<Vector2> trailPoints = trailHistory[trail];
                float trailAlpha = (1f - (trail / (float)TrailLength)) * 0.35f;

                Vector2 trailPos = trailPoints[0];
                for (int i = 0; i < trailPoints.Count - 1; i++)
                {
                    Rectangle frame = GetFrameForSegment(i, trailPoints.Count);
                    Vector2 origin = new Vector2(frame.Width / 2f, frame.Height / 2f);

                    Vector2 element = trailPoints[i];
                    Vector2 diff = trailPoints[i + 1] - element;
                    float rotation = diff.ToRotation() - MathHelper.PiOver2;

                    Main.EntitySpriteDraw(texture, trailPos - Main.screenPosition, frame, new Color(230, 150, 30) * trailAlpha, rotation, origin, 1f, flip, 0);
                    trailPos += diff;
                }
            }

            DrawLine(list);

            // Draw main whip
            Vector2 pos = list[0];
            for (int i = 0; i < list.Count - 1; i++)
            {
                Rectangle frame = GetFrameForSegment(i, list.Count);
                Vector2 origin = new Vector2(frame.Width / 2f, frame.Height / 2f);
                float scale = 1;

                if (i == list.Count - 2)
                {
                    Projectile.GetWhipSettings(Projectile, out float timeToFlyOut, out int _, out float _);
                    float t = Timer / timeToFlyOut;
                    scale = MathHelper.Lerp(0.5f, 1.5f, Utils.GetLerpValue(0.1f, 0.7f, t, true) * Utils.GetLerpValue(0.9f, 0.7f, t, true));
                }

                Vector2 element = list[i];
                Vector2 diff = list[i + 1] - element;
                float rotation = diff.ToRotation() - MathHelper.PiOver2;
                Color color = Lighting.GetColor(element.ToTileCoordinates());

                Main.EntitySpriteDraw(texture, pos - Main.screenPosition, frame, color, rotation, origin, scale, flip, 0);
                pos += diff;
            }

            // Glow pass (additive)
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 glowPos = list[0];
            for (int i = 0; i < list.Count - 1; i++)
            {
                Rectangle frame = GetFrameForSegment(i, list.Count);
                Vector2 origin = new Vector2(frame.Width / 2f, frame.Height / 2f);

                Vector2 element = list[i];
                Vector2 diff = list[i + 1] - element;
                float rotation = diff.ToRotation() - MathHelper.PiOver2;

                Main.EntitySpriteDraw(texture, glowPos - Main.screenPosition, frame, new Color(230, 150, 30) * 0.4f, rotation, origin, 1f, flip, 0);
                glowPos += diff;
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        private Rectangle GetFrameForSegment(int segmentIndex, int totalSegments)
        {
            if (segmentIndex == totalSegments - 2)
                return new Rectangle(0, 74, 22, 18); // Tip
            else if (segmentIndex > 10)
                return new Rectangle(0, 58, 22, 16); // Far segment
            else if (segmentIndex > 5)
                return new Rectangle(0, 42, 22, 16); // Mid segment
            else if (segmentIndex > 0)
                return new Rectangle(0, 26, 22, 16); // Near segment
            return new Rectangle(0, 0, 22, 26); // Handle
        }
    }
}
