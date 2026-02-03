using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class RoaringWhipProjectile : ModProjectile
    {
        private const int TrailLength = 6;
        private List<List<Vector2>> trailHistory = new List<List<Vector2>>();
        private bool slashSpawned = false; // Only spawn one slash per whip swing

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.IsAWhip[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.DefaultToWhip();
            Projectile.WhipSettings.Segments = 20;
            Projectile.WhipSettings.RangeMultiplier = 1.8f; // Extended range
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
            
            // Spawn slash attack on first hit only
            if (!slashSpawned)
            {
                SpawnSlashAttack(target);
                slashSpawned = true;
            }
        }
        
        private void SpawnSlashAttack(NPC target)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            
            Player owner = Main.player[Projectile.owner];
            
            // Calculate angle from target to player (line starts facing the player)
            float angleToPlayer = (owner.Center - target.Center).ToRotation();
            
            // Slash damage is 1.2x the whip's current damage
            int slashDamage = (int)(Projectile.damage * 1.2f);
            
            // Random rotation direction
            float rotationDirection = Main.rand.NextBool() ? 1f : -1f;
            
            // Spawn the slash indicator at the target
            int id = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                target.Center,
                Vector2.Zero,
                ModContent.ProjectileType<RoaringWhipSlash>(),
                0,
                0f,
                Projectile.owner,
                slashDamage,
                angleToPlayer,
                rotationDirection
            );
            
            if (id >= 0)
                Main.projectile[id].netUpdate = true;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Tip bonus damage replaced with slash attack in OnHitNPC
        }

        public override void PostAI()
        {
            List<Vector2> points = new List<Vector2>();
            Projectile.FillWhipControlPoints(Projectile, points);

            foreach (Vector2 point in points)
            {
                Lighting.AddLight(point, 0.9f, 0.9f, 0.9f);
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
                Color color = Lighting.GetColor(element.ToTileCoordinates(), Color.White);
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
                float trailAlpha = (1f - (trail / (float)TrailLength)) * 0.4f;

                Vector2 trailPos = trailPoints[0];
                for (int i = 0; i < trailPoints.Count - 1; i++)
                {
                    Rectangle frame = GetFrameForSegment(i, trailPoints.Count);
                    Vector2 origin = new Vector2(11, 8);

                    Vector2 element = trailPoints[i];
                    Vector2 diff = trailPoints[i + 1] - element;
                    float rotation = diff.ToRotation() - MathHelper.PiOver2;

                    Main.EntitySpriteDraw(texture, trailPos - Main.screenPosition, frame, Color.White * trailAlpha, rotation, origin, 1f, flip, 0);
                    trailPos += diff;
                }
            }

            DrawLine(list);

            // Draw main whip
            Vector2 pos = list[0];

            for (int i = 0; i < list.Count - 1; i++)
            {
                Rectangle frame = GetFrameForSegment(i, list.Count);
                Vector2 origin = new Vector2(11, 8);
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
            return false;
        }

        private Rectangle GetFrameForSegment(int segmentIndex, int totalSegments)
        {
            if (segmentIndex == totalSegments - 2)
            {
                return new Rectangle(0, 74, 22, 18);
            }
            else if (segmentIndex > 10)
            {
                return new Rectangle(0, 58, 22, 16);
            }
            else if (segmentIndex > 5)
            {
                return new Rectangle(0, 42, 22, 16);
            }
            else if (segmentIndex > 0)
            {
                return new Rectangle(0, 26, 22, 16);
            }
            return new Rectangle(0, 0, 22, 26);
        }
    }
}
