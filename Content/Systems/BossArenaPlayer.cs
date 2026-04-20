using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Systems
{
    
    // Forces grounded state for players standing on the invisible arena floor.
    // 
    // The problem: Terraria's PlayerFrame() runs INSIDE Player.Update(), after gravity
    // and collision. With no real tiles, gravity makes velocity.Y ≈ 0.4 each frame.
    // TileCollision finds nothing to stop the fall. PlayerFrame sees velocity.Y != 0
    // and picks the falling animation. PostUpdatePlayers clamps velocity.Y to 0, but
    // that's a ModSystem hook that runs AFTER all Player.Update() calls — too late.
    // 
    // The fix: FrameEffects() runs at the START of PlayerFrame(), before the vanilla
    // animation frame selection code. We zero velocity.Y here so the frame selection
    // sees the player as grounded.
    // PreUpdate also zeros velocity.Y to reduce gravity accumulation jitter.
    
    public class BossArenaPlayer : ModPlayer
    {
        private bool IsOnArenaFloor(out float floorY)
        {
            floorY = 0f;

            if (BossArenaSystem.ActiveBoxes.Count == 0)
                return false;

            if (!BossArenaSystem.IsPlayerLockedIn(Player.whoAmI))
                return false;

            foreach (var box in BossArenaSystem.ActiveBoxes)
            {
                if (!box.LockedPlayers.Contains(Player.whoAmI))
                    continue;

                float bottom = box.Center.Y + box.HalfHeight;
                float playerBottom = Player.position.Y + Player.height;

                // Within a few pixels of the floor and not jumping upward
                if (playerBottom >= bottom - 6f && Player.velocity.Y >= 0f && Player.velocity.Y <= 1f)
                {
                    floorY = bottom;
                    return true;
                }
            }

            return false;
        }

        public override void PreUpdate()
        {
            // Zero velocity before gravity runs to prevent accumulation
            if (IsOnArenaFloor(out float floorY))
            {
                Player.position.Y = floorY - Player.height;
                Player.velocity.Y = 0f;
                Player.fallStart = (int)(Player.position.Y / 16f);
                Player.fallStart2 = (int)(Player.position.Y / 16f);
            }
        }

        public override void FrameEffects()
        {
            // This is the critical fix: FrameEffects runs at the start of PlayerFrame(),
            // BEFORE the vanilla code selects animation frames based on velocity.Y.
            // After gravity runs during Update(), velocity.Y ≈ 0.4 even though
            // PostUpdatePlayers will clamp it to 0 later. We zero it here so
            // the animation selection sees the player as grounded.
            if (IsOnArenaFloor(out _))
            {
                Player.velocity.Y = 0f;
            }
        }
    }
}
