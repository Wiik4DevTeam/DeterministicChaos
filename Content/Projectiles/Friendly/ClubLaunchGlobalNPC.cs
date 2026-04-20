using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    
    // GlobalNPC that enforces upward launch velocity AFTER an NPC's own AI runs,
    // preventing grounded enemies from immediately canceling the launch.
    
    public class ClubLaunchGlobalNPC : GlobalNPC
    {
        private const float LaunchSpeedY = -10f;
        private const float SustainForceY = -2f;

        // NPC index -> remaining frames of forced upward velocity
        private static readonly Dictionary<int, int> launchedNPCs = new();

        public static void RegisterLaunch(int npcIndex, int durationFrames)
        {
            // Always refresh/extend the launch duration
            launchedNPCs[npcIndex] = durationFrames;
        }

        public override void PostAI(NPC npc)
        {
            if (!launchedNPCs.TryGetValue(npc.whoAmI, out int framesLeft))
                return;

            if (!npc.active || framesLeft <= 0)
            {
                launchedNPCs.Remove(npc.whoAmI);
                return;
            }

            // On the first frame, apply the big initial launch
            // On subsequent frames, sustain upward force to fight gravity and NPC AI
            if (npc.knockBackResist > 0f)
            {
                if (framesLeft == launchedNPCs[npc.whoAmI] || npc.velocity.Y >= 0f)
                {
                    // Either first registered frame or NPC somehow going down, force it up
                    npc.velocity.Y = LaunchSpeedY * npc.knockBackResist;
                }
                else
                {
                    // Already going up, just counteract gravity
                    npc.velocity.Y += SustainForceY * npc.knockBackResist;
                }
            }

            launchedNPCs[npc.whoAmI] = framesLeft - 1;
        }

        public override void OnKill(NPC npc)
        {
            launchedNPCs.Remove(npc.whoAmI);
        }

        
        // Clear all tracked launches (e.g. on world unload).
        
        public static void ClearAll()
        {
            launchedNPCs.Clear();
        }
    }
}
