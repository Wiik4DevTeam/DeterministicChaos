using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class RoaringSummonWhipAttack : ModProjectile
    {
        public override string Texture => "DeterministicChaos/Content/Projectiles/Friendly/RoaringWhipProjectile";

        private int parentCloneWhoAmI = -1;
        private bool initialized = false;
        private HashSet<int> hitNPCs = new HashSet<int>(); // Track which NPCs we've hit
        private bool slashSpawned = false; // Only spawn one slash per whip swing

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.IsAWhip[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.DefaultToWhip();
            Projectile.WhipSettings.Segments = 20;
            Projectile.WhipSettings.RangeMultiplier = 1.8f; // Extended range to match main whip
            
            // Change to summon damage
            Projectile.DamageType = DamageClass.Summon;
            Projectile.friendly = true;
            
            // Use ID-static immunity so it doesn't share with player's whip
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 10;
        }
        
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(parentCloneWhoAmI);
            writer.Write(slashSpawned);
        }
        
        public override void ReceiveExtraAI(BinaryReader reader)
        {
            parentCloneWhoAmI = reader.ReadInt32();
            slashSpawned = reader.ReadBoolean();
        }
        
        private bool HasSummonerSetBonus()
        {
            Player owner = Main.player[Projectile.owner];
            return owner.GetModPlayer<Items.Armor.RoaringArmorPlayer>().roaringSummonerSet;
        }

        private float Timer
        {
            get => Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        public override bool PreAI()
        {
            // On first frame, store which clone spawned us
            if (!initialized)
            {
                parentCloneWhoAmI = (int)Projectile.ai[1];
                initialized = true;
            }

            return true;
        }
        
        public override void PostAI()
        {
            // Calculate offset from player to clone for lighting
            Player owner = Main.player[Projectile.owner];
            Vector2 anchorPosition = GetAnchorPosition();
            Vector2 offset = anchorPosition - owner.MountedCenter;
            
            List<Vector2> points = new List<Vector2>();
            Projectile.FillWhipControlPoints(Projectile, points);

            // Add lighting for all clients
            foreach (Vector2 point in points)
            {
                Vector2 offsetPoint = point + offset;
                Lighting.AddLight(offsetPoint, 0.4f, 0.4f, 0.4f);
            }
            
            // Damage processing only on owner or server for multiplayer compatibility
            if (Main.myPlayer != Projectile.owner && Main.netMode != NetmodeID.Server)
                return;
            
            // Check each NPC for collision with offset whip
            for (int n = 0; n < Main.maxNPCs; n++)
            {
                NPC npc = Main.npc[n];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || hitNPCs.Contains(n))
                    continue;
                
                Rectangle npcHitbox = npc.Hitbox;
                
                // Check collision along whip segments
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Vector2 start = points[i] + offset;
                    Vector2 end = points[i + 1] + offset;
                    
                    float collisionPoint = 0f;
                    if (Collision.CheckAABBvLineCollision(npcHitbox.TopLeft(), npcHitbox.Size(), start, end, 22f, ref collisionPoint))
                    {
                        // Deal damage directly
                        int damage = Projectile.damage;
                        float knockback = Projectile.knockBack;
                        int direction = (npc.Center.X > anchorPosition.X) ? 1 : -1;
                        
                        // Spawn slash attack on first hit if owner has summoner set bonus
                        if (HasSummonerSetBonus() && !slashSpawned)
                        {
                            SpawnSlashAttack(npc, anchorPosition);
                            slashSpawned = true;
                        }
                        
                        // Use proper multiplayer damage method
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            npc.StrikeNPC(npc.CalculateHitInfo(damage, direction, false, knockback, Projectile.DamageType, true));
                            
                            if (Main.netMode == NetmodeID.Server)
                            {
                                NetMessage.SendData(MessageID.DamageNPC, -1, -1, null, n, damage, knockback, direction, 0);
                            }
                        }
                        
                        // Mark as hit so we don't hit again
                        hitNPCs.Add(n);
                        
                        // Set minion target
                        owner.MinionAttackTargetNPC = n;
                        
                        break;
                    }
                }
            }
        }

        private Vector2 GetAnchorPosition()
        {
            // Get the current position of the parent clone
            if (parentCloneWhoAmI >= 0 && parentCloneWhoAmI < Main.maxProjectiles)
            {
                Projectile clone = Main.projectile[parentCloneWhoAmI];
                if (clone.active && clone.type == ModContent.ProjectileType<RoaringSummonProjectile>())
                {
                    return clone.Center;
                }
            }
            
            // Fallback to player position if clone is gone
            return Main.player[Projectile.owner].MountedCenter;
        }
        
        private void SpawnSlashAttack(NPC target, Vector2 clonePosition)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            
            // Calculate angle from target to clone (line starts facing the clone)
            float angleToClone = (clonePosition - target.Center).ToRotation();
            
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
                angleToClone,
                rotationDirection
            );
            
            if (id >= 0)
                Main.projectile[id].netUpdate = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // This may not be called since we handle damage manually, but keep for safety
            Main.player[Projectile.owner].MinionAttackTargetNPC = target.whoAmI;
            hitNPCs.Add(target.whoAmI);
        }

        public override bool? CanHitNPC(NPC target)
        {
            // Prevent normal whip collision from dealing damage, we handle it manually in AI
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            // Custom collision that uses offset control points
            Player owner = Main.player[Projectile.owner];
            Vector2 anchorPosition = GetAnchorPosition();
            Vector2 offset = anchorPosition - owner.MountedCenter;
            
            List<Vector2> points = new List<Vector2>();
            Projectile.FillWhipControlPoints(Projectile, points);
            
            // Check collision along the offset whip path with larger hitbox
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 start = points[i] + offset;
                Vector2 end = points[i + 1] + offset;
                
                float collisionPoint = 0f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 22f, ref collisionPoint))
                {
                    return true;
                }
            }
            
            // Also check the tip area with a rectangle collision
            if (points.Count > 0)
            {
                Vector2 tipPos = points[points.Count - 1] + offset;
                Rectangle tipHitbox = new Rectangle((int)(tipPos.X - 20), (int)(tipPos.Y - 20), 40, 40);
                if (tipHitbox.Intersects(targetHitbox))
                {
                    return true;
                }
            }
            
            return null; // Fall back to default collision if our checks fail
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player owner = Main.player[Projectile.owner];
            Vector2 anchorPosition = GetAnchorPosition();
            Vector2 offset = anchorPosition - owner.MountedCenter;
            
            List<Vector2> list = new List<Vector2>();
            Projectile.FillWhipControlPoints(Projectile, list);
            
            // Offset all control points to anchor position
            for (int i = 0; i < list.Count; i++)
            {
                list[i] += offset;
            }

            SpriteEffects flip = Projectile.spriteDirection < 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Texture2D texture = TextureAssets.Projectile[Type].Value;

            Color outlineColor = Color.White * 0.8f;
            Color shadowColor = Color.Black * 0.9f;

            // First pass: draw white outline
            Vector2 outlinePos = list[0];
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

                // Draw white outline offset in all directions
                for (int x = -2; x <= 2; x++)
                {
                    for (int y = -2; y <= 2; y++)
                    {
                        if (x == 0 && y == 0) continue;
                        Vector2 outlineOffset = new Vector2(x, y);
                        Main.EntitySpriteDraw(texture, outlinePos - Main.screenPosition + outlineOffset, frame, outlineColor, rotation, origin, scale, flip, 0);
                    }
                }

                outlinePos += diff;
            }

            // Second pass: draw black whip on top
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

                Main.EntitySpriteDraw(texture, pos - Main.screenPosition, frame, shadowColor, rotation, origin, scale, flip, 0);

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
