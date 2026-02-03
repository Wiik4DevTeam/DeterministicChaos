using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class RoaringSummonProjectile : ModProjectile
    {
        private const float IdleJitterRange = 5f;
        private Vector2 jitterOffset;
        private int jitterTimer;
        private bool wasOwnerUsingWhip = false;
        private int lastWhipAnimationTime = 0;
        
        // Target tracking with delay for smoother transitions
        private int currentTrackedTarget = -1;
        private int targetSwitchDelay = 0;
        private const int TargetSwitchDelayFrames = 30; // Half second delay before switching targets
        private float transitionProgress = 1f; // 1 = fully transitioned, 0 = just started
        
        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = 1;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Projectile.type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 42;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override bool? CanCutTiles()
        {
            return false;
        }

        public override bool MinionContactDamage()
        {
            return true;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];

            if (!CheckActive(owner))
                return;

            Projectile.timeLeft = 2;

            int targetNPC = owner.MinionAttackTargetNPC;
            bool hasValidTarget = targetNPC != -1 && Main.npc[targetNPC].active && !Main.npc[targetNPC].friendly;

            // Handle target switching with delay
            NPC activeTarget = null;
            if (hasValidTarget)
            {
                // Check if target changed
                if (targetNPC != currentTrackedTarget)
                {
                    // New target detected, start delay countdown
                    if (targetSwitchDelay <= 0)
                    {
                        targetSwitchDelay = TargetSwitchDelayFrames;
                    }
                    
                    targetSwitchDelay--;
                    
                    // Only switch when delay expires
                    if (targetSwitchDelay <= 0)
                    {
                        currentTrackedTarget = targetNPC;
                        transitionProgress = 0f; // Start slow transition
                    }
                }
                else
                {
                    // Same target, reset delay and continue normal tracking
                    targetSwitchDelay = 0;
                }
                
                // Use current tracked target if valid, otherwise use new target immediately if we have none
                if (currentTrackedTarget != -1 && Main.npc[currentTrackedTarget].active && !Main.npc[currentTrackedTarget].friendly)
                {
                    activeTarget = Main.npc[currentTrackedTarget];
                }
                else
                {
                    // No valid tracked target, switch immediately
                    currentTrackedTarget = targetNPC;
                    activeTarget = Main.npc[targetNPC];
                    transitionProgress = 0.5f; // Partial transition since forced switch
                }
                
                // Gradually increase transition progress for smoother movement
                transitionProgress = MathHelper.Clamp(transitionProgress + 0.02f, 0f, 1f);
            }
            else
            {
                // No target, clear tracking
                if (currentTrackedTarget != -1)
                {
                    transitionProgress = 0f;
                }
                currentTrackedTarget = -1;
                targetSwitchDelay = 0;
            }

            if (activeTarget != null)
            {
                MirrorMovementAroundTarget(owner, activeTarget);
                
                // Only trigger whip attacks on the actual marked target
                if (hasValidTarget)
                {
                    CheckAndMirrorWhipAttack(owner, Main.npc[targetNPC]);
                }
                
                // Face the target
                Projectile.spriteDirection = (activeTarget.Center.X > Projectile.Center.X) ? 1 : -1;
            }
            else
            {
                IdleAtPlayer(owner);
                
                // Mirror player direction when idle
                Projectile.spriteDirection = owner.direction;
            }
            
            Lighting.AddLight(Projectile.Center, 0.3f, 0.3f, 0.3f);
        }

        private void CheckAndMirrorWhipAttack(Player owner, NPC target)
        {
            // Check if owner is using a whip
            bool isUsingWhip = false;
            int currentAnimTime = 0;
            
            if (owner.itemAnimation > 0 && owner.HeldItem != null)
            {
                // Check if the held item shoots a whip projectile
                int shootType = owner.HeldItem.shoot;
                if (shootType > 0 && ProjectileID.Sets.IsAWhip[shootType])
                {
                    isUsingWhip = true;
                    currentAnimTime = owner.itemAnimation;
                }
            }

            // Spawn whip attack when:
            // 1. Player just started using a whip (wasn't using before)
            // 2. Or animation time jumped up (new swing started with autoswing)
            bool newSwingStarted = isUsingWhip && (!wasOwnerUsingWhip || currentAnimTime > lastWhipAnimationTime);
            
            if (newSwingStarted)
            {
                SpawnMirroredWhipAttack(owner, target);
            }

            wasOwnerUsingWhip = isUsingWhip;
            lastWhipAnimationTime = currentAnimTime;
        }

        private void SpawnMirroredWhipAttack(Player owner, NPC target)
        {
            // Only spawn on the owner's client for multiplayer compatibility
            if (Main.myPlayer != Projectile.owner)
                return;
            
            // Calculate direction from clone to target
            Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            
            // Spawn a RoaringWhip attack from this clone at full damage
            int damage = Projectile.damage;
            float knockback = 1f;
            float shootSpeed = 4f;

            // Pass this clone's whoAmI in ai[1] so the whip can follow this clone
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
            
            // Sync the spawned projectile to other clients
            if (whipId >= 0 && whipId < Main.maxProjectiles && Main.netMode == NetmodeID.MultiplayerClient)
            {
                Main.projectile[whipId].netUpdate = true;
                NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, whipId);
            }
        }

        private bool CheckActive(Player owner)
        {
            if (owner.dead || !owner.active)
            {
                owner.ClearBuff(ModContent.BuffType<Buffs.RoaringSummonBuff>());
                return false;
            }

            if (owner.HasBuff(ModContent.BuffType<Buffs.RoaringSummonBuff>()))
            {
                Projectile.timeLeft = 2;
            }
            else
            {
                // Kill the minion if buff is removed
                Projectile.Kill();
                return false;
            }

            return true;
        }

        private void IdleAtPlayer(Player owner)
        {
            // Update jitter offset periodically
            jitterTimer++;
            if (jitterTimer >= 6)
            {
                jitterTimer = 0;
                jitterOffset = new Vector2(
                    Main.rand.NextFloat(-IdleJitterRange, IdleJitterRange),
                    Main.rand.NextFloat(-IdleJitterRange, IdleJitterRange)
                );
            }

            // Gradually increase transition progress for smoother return to idle
            transitionProgress = MathHelper.Clamp(transitionProgress + 0.015f, 0f, 1f);

            Vector2 targetPos = owner.Center + jitterOffset;
            // Use slower lerp during transitions for smoother movement back to player
            float lerpSpeed = MathHelper.Lerp(0.03f, 0.15f, transitionProgress);
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, lerpSpeed);
            Projectile.velocity = Vector2.Zero;
        }

        private void MirrorMovementAroundTarget(Player owner, NPC target)
        {
            // Get all active summon clones for this player
            List<Projectile> clones = new List<Projectile>();
            int myIndex = 0;
            
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == Projectile.owner && proj.type == Projectile.type)
                {
                    if (proj.whoAmI == Projectile.whoAmI)
                    {
                        myIndex = clones.Count;
                    }
                    clones.Add(proj);
                }
            }

            int totalClones = clones.Count;
            
            // Calculate formation position based on number of clones
            // Player is always position 0 in the formation, clones fill remaining spots
            Vector2 playerToTarget = target.Center - owner.Center;
            
            // Total formation members = player + clones
            // With 1 clone: clone goes opposite player (2-member formation)
            // With 2+ clones: player + clones form a shape together
            int totalMembers = totalClones + 1; // +1 for player
            int formationIndex = myIndex + 1; // Clone indices start at 1, player is 0
            
            Vector2 mirroredPos = GetFormationPosition(owner, target, formationIndex, totalMembers, playerToTarget);

            // Smoothly move to mirrored position
            // Use slower lerp during target transitions for smoother movement
            float lerpSpeed = MathHelper.Lerp(0.05f, 0.15f, transitionProgress);
            Projectile.Center = Vector2.Lerp(Projectile.Center, mirroredPos, lerpSpeed);
            Projectile.velocity = Vector2.Zero;
        }

        private Vector2 GetFormationPosition(Player owner, NPC target, int index, int total, Vector2 playerToTarget)
        {
            float distance = playerToTarget.Length();
            // playerToTarget points FROM player TO target
            // Player is at a certain angle from target, we place clones in a full circle
            float playerAngle = playerToTarget.ToRotation() + MathHelper.Pi; // Angle FROM target TO player
            
            // Index 0 is the player's position
            if (index == 0) return owner.Center;
            
            int numClones = total - 1; // Number of clones (excluding player)
            int cloneIndex = index - 1; // 0-based index for clones
            
            if (numClones == 1)
            {
                // Single clone: directly opposite the player
                float oppositeAngle = playerAngle + MathHelper.Pi;
                return target.Center + new Vector2((float)Math.Cos(oppositeAngle), (float)Math.Sin(oppositeAngle)) * distance;
            }
            else
            {
                // Multiple clones: form a full circle with player
                // Player occupies position 0, clones fill positions 1 through numClones
                // Total positions in circle = total (player + clones)
                float angleStep = MathHelper.TwoPi / total;
                
                // Clone's angle is offset from player's angle
                float cloneAngle = playerAngle + (index * angleStep);
                
                return target.Center + new Vector2((float)Math.Cos(cloneAngle), (float)Math.Sin(cloneAngle)) * distance;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Summon clones deal 1/4 damage (whip attack mirroring)
            modifiers.FinalDamage *= 0.25f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player owner = Main.player[Projectile.owner];
            SpriteBatch spriteBatch = Main.spriteBatch;
            
            Vector2 position = Projectile.Center - Main.screenPosition;
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            
            Color shadowColor = Color.Black * 0.9f;
            Color outlineColor = Color.White * 0.9f;
            
            // Load Roaring Armor textures
            Texture2D headTex = ModContent.Request<Texture2D>("DeterministicChaos/Content/Items/Armor/RoaringHelmet_Head", AssetRequestMode.ImmediateLoad).Value;
            Texture2D headExtTex = ModContent.Request<Texture2D>("DeterministicChaos/Content/Items/Armor/RoaringHelmet_Extension", AssetRequestMode.ImmediateLoad).Value;
            Texture2D bodyTex = ModContent.Request<Texture2D>("DeterministicChaos/Content/Items/Armor/RoaringBreastplate_Body", AssetRequestMode.ImmediateLoad).Value;
            Texture2D legsTex = ModContent.Request<Texture2D>("DeterministicChaos/Content/Items/Armor/RoaringLeggings_Legs", AssetRequestMode.ImmediateLoad).Value;
            
            // Armor equip textures are sprite sheets with 20 frames (40x56 each)
            int frameHeight = 56;
            int frameWidth = 40;
            
            // Get animation frame from owner (use frameHeight constant to avoid divide by zero)
            int headFrameY = owner.headFrame.Height > 0 ? owner.headFrame.Y / owner.headFrame.Height : 0;
            int legFrameY = owner.legFrame.Height > 0 ? owner.legFrame.Y / owner.legFrame.Height : 0;
            
            // Use frame 0 for body to prevent disappearing during actions
            Rectangle headFrame = new Rectangle(0, headFrameY * frameHeight, frameWidth, frameHeight);
            Rectangle bodyFrame = new Rectangle(0, 0, frameWidth, frameHeight); // Always use base frame
            Rectangle legFrame = new Rectangle(0, legFrameY * frameHeight, frameWidth, frameHeight);
            
            // Center the draw position on the projectile
            Vector2 origin = new Vector2(frameWidth / 2, frameHeight / 2);
            Vector2 extOffset = new Vector2(0, -3); // Raise extension by 3 pixels
            
            // Draw legs with outline
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    if (x == 0 && y == 0) continue;
                    Vector2 offset = new Vector2(x, y);
                    spriteBatch.Draw(legsTex, position + offset, legFrame, outlineColor, 0f, origin, 1f, effects, 0f);
                }
            }
            spriteBatch.Draw(legsTex, position, legFrame, shadowColor, 0f, origin, 1f, effects, 0f);
            
            // Draw body with outline
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    if (x == 0 && y == 0) continue;
                    Vector2 offset = new Vector2(x, y);
                    spriteBatch.Draw(bodyTex, position + offset, bodyFrame, outlineColor, 0f, origin, 1f, effects, 0f);
                }
            }
            spriteBatch.Draw(bodyTex, position, bodyFrame, shadowColor, 0f, origin, 1f, effects, 0f);
            
            // Draw head with outline
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    if (x == 0 && y == 0) continue;
                    Vector2 offset = new Vector2(x, y);
                    spriteBatch.Draw(headTex, position + offset, headFrame, outlineColor, 0f, origin, 1f, effects, 0f);
                }
            }
            spriteBatch.Draw(headTex, position, headFrame, shadowColor, 0f, origin, 1f, effects, 0f);
            
            // Draw head extension with outline (drawn last so its outline is not covered)
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    if (x == 0 && y == 0) continue;
                    Vector2 offset = new Vector2(x, y);
                    spriteBatch.Draw(headExtTex, position + offset + extOffset, headFrame, outlineColor, 0f, origin, 1f, effects, 0f);
                }
            }
            spriteBatch.Draw(headExtTex, position + extOffset, headFrame, shadowColor, 0f, origin, 1f, effects, 0f);

            return false;
        }
    }
}
