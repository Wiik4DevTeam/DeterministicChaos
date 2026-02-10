using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{

    public class HollowGunPlayer : ModPlayer
    {
        public bool HasMarkedTarget => MarkedTargetIndex >= 0 && markedTimer > 0;
        public int MarkedTargetIndex { get; private set; } = -1;
        
        private int markedTimer = 0;
        private const int MarkDuration = 300; // 5 seconds
        private const float MarkRange = 600f; // ~37.5 tiles

        // Animation for the target marker rotation
        private float markerRotation = MathHelper.PiOver2; // Start at 90 degrees offset
        private const float RotationSpeed = 0.15f; // How fast it rotates back to 0
        private const int RotationDuration = 15; // Ticks to complete rotation
        private int rotationTimer = 0;

        // Track jump states to detect double jump
        private bool wasOnGround = true;
        private bool hasTriggeredThisJump = false;
        private int lastJumpValue = 0;
        private bool wasPressingJump = false;

        public override void ResetEffects()
        {
            // Countdown mark timer
            if (markedTimer > 0)
            {
                markedTimer--;
                if (markedTimer <= 0)
                {
                    ClearMarkedTarget();
                }
            }

            // Animate rotation toward 0
            if (rotationTimer < RotationDuration)
            {
                rotationTimer++;
                float t = (float)rotationTimer / RotationDuration;
                // Ease out for smooth landing
                t = 1f - (1f - t) * (1f - t);
                markerRotation = MathHelper.Lerp(MathHelper.PiOver2, 0f, t);
            }
            else
            {
                markerRotation = 0f;
            }
        }

        public override void PostUpdate()
        {
            // Only process if holding HollowGun
            if (Player.HeldItem.type != ModContent.ItemType<HollowGun>())
            {
                // Reset jump tracking when not holding the gun
                wasOnGround = true;
                hasTriggeredThisJump = false;
                lastJumpValue = Player.jump;
                wasPressingJump = Player.controlJump;
                return;
            }

            bool onGround = Player.velocity.Y == 0f;
            bool justPressedJump = Player.controlJump && !wasPressingJump;

            if (onGround)
            {
                wasOnGround = true;
                hasTriggeredThisJump = false;
            }
            else
            {
                // Detect double jump by checking if Player.jump reset to a positive value
                // This happens when cloud jump, blizzard jump, rocket boots, etc. activate
                // Player.jump goes from 0 or low value back to a high value when a jump ability is used
                bool jumpReset = lastJumpValue <= 0 && Player.jump > 0 && !wasOnGround;
                
                // Alternative: pressed jump while in air AND now rising (velocity < 0)
                bool jumpPressedWhileRising = justPressedJump && Player.velocity.Y < 0 && !wasOnGround;
                
                if (!hasTriggeredThisJump && (jumpReset || jumpPressedWhileRising))
                {
                    hasTriggeredThisJump = true;
                    OnDoubleJump();
                }
                
                // Reset trigger when jump is released so next double jump can trigger
                if (!Player.controlJump)
                {
                    hasTriggeredThisJump = false;
                }
                
                wasOnGround = false;
            }

            lastJumpValue = Player.jump;
            wasPressingJump = Player.controlJump;
        }

        private void OnDoubleJump()
        {
            // Find highest health enemy in range
            int bestTarget = -1;
            int highestLife = 0;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc == null || !npc.active || npc.friendly || !npc.CanBeChasedBy())
                    continue;

                float dist = Vector2.Distance(Player.Center, npc.Center);
                if (dist > MarkRange)
                    continue;

                if (npc.life > highestLife)
                {
                    highestLife = npc.life;
                    bestTarget = i;
                }
            }

            if (bestTarget >= 0)
            {
                MarkTarget(bestTarget);
            }
        }

        public void MarkTarget(int npcIndex)
        {
            MarkedTargetIndex = npcIndex;
            markedTimer = MarkDuration;

            // Reset rotation animation
            markerRotation = MathHelper.PiOver2;
            rotationTimer = 0;

            // Visual/audio feedback
            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.5f, Volume = 0.7f }, Player.Center);

            // Create dust at target
            NPC target = Main.npc[npcIndex];
            if (target != null && target.active)
            {
                for (int i = 0; i < 20; i++)
                {
                    Vector2 dustPos = target.Center + Main.rand.NextVector2CircularEdge(target.width, target.height);
                    Dust dust = Dust.NewDustPerfect(dustPos, DustID.YellowTorch, Vector2.Zero, 0, default, 1.5f);
                    dust.noGravity = true;
                }
            }
        }

        public void ClearMarkedTarget()
        {
            MarkedTargetIndex = -1;
            markedTimer = 0;
        }

        public float GetMarkerRotation() => markerRotation;
    }

    public class HollowGunMarkerDrawSystem : ModSystem
    {
        public static Texture2D markerTexture;

        public override void Load()
        {
            if (!Main.dedServ)
            {
                markerTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/Buffs/JusticeTarget", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
            }
        }
    }

    public class HollowGunMarkerNPC : GlobalNPC
    {
        public override bool InstancePerEntity => false;

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (HollowGunMarkerDrawSystem.markerTexture == null)
                return;

            Player localPlayer = Main.LocalPlayer;
            if (localPlayer == null || !localPlayer.active)
                return;

            var hollowPlayer = localPlayer.GetModPlayer<HollowGunPlayer>();
            if (!hollowPlayer.HasMarkedTarget)
                return;

            // Only draw on the marked target
            if (hollowPlayer.MarkedTargetIndex != npc.whoAmI)
                return;

            Texture2D tex = HollowGunMarkerDrawSystem.markerTexture;
            
            // Draw at NPC center, using screen position provided by the hook
            Vector2 drawPos = npc.Center - screenPos;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            float rotation = hollowPlayer.GetMarkerRotation();

            // Pulsing scale effect
            float pulse = 1f + (float)Math.Sin(Main.GameUpdateCount * 0.1f) * 0.1f;

            spriteBatch.Draw(
                tex,
                drawPos,
                null,
                Color.White,
                rotation,
                origin,
                pulse,
                SpriteEffects.None,
                0f
            );
        }
    }
}
