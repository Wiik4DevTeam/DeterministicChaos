using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.NPCs.Bosses;
using DeterministicChaos.Content.Items;

namespace DeterministicChaos.Content.Systems
{
    // Adds Boss Checklist mod compatibility for our bosses
    public class BossChecklistSystem : ModSystem
    {
        public override void PostSetupContent()
        {
            // Check if Boss Checklist is loaded
            if (!ModLoader.TryGetMod("BossChecklist", out Mod bossChecklist))
                return;
            
            // Register ERAM, right before Wall of Flesh (WoF is 6.0)
            // Using 5.9 to place it just before WoF
            bossChecklist.Call(
                "LogBoss",                                          // Call type
                Mod,                                                // Mod instance
                "ERAM",                                             // Internal boss name
                5.9f,                                               // Progression value (before WoF at 6.0)
                () => ERAMProgressSystem.ERAMDefeated,              // Downed condition
                ModContent.NPCType<ERAM>(),                         // NPC type
                new Dictionary<string, object>()                    // Extra info
                {
                    ["spawnItems"] = ModContent.ItemType<ERAMSummon>(),
                    ["displayName"] = "E R A M",
                    ["customPortrait"] = (SpriteBatch sb, Rectangle rect, Color color) => {
                        Texture2D texture = ModContent.Request<Texture2D>("DeterministicChaos/Content/NPCs/Bosses/ERAM", AssetRequestMode.ImmediateLoad).Value;
                        int frameWidth = 32;
                        int frameHeight = 32;
                        Rectangle sourceRect = new Rectangle(0, 0, frameWidth, frameHeight);
                        Vector2 center = rect.Center.ToVector2();
                        float scale = (float)rect.Width / frameWidth * 0.8f;
                        sb.Draw(texture, center, sourceRect, color, 0f, new Vector2(frameWidth / 2, frameHeight / 2), scale, SpriteEffects.None, 0f);
                    }
                }
            );
            
            // Register Roaring Knight, right before Plantera (Plantera is 12.0)
            // Using 11.9 to place it just before Plantera
            bossChecklist.Call(
                "LogBoss",                                          // Call type
                Mod,                                                // Mod instance
                "RoaringKnight",                                    // Internal boss name
                11.9f,                                              // Progression value (before Plantera at 12.0)
                () => ERAMProgressSystem.RoaringKnightDefeated,     // Downed condition
                ModContent.NPCType<RoaringKnight>(),                // NPC type
                new Dictionary<string, object>()                    // Extra info
                {
                    ["spawnItems"] = ModContent.ItemType<SuspiciousEye>(),
                    ["displayName"] = "Roaring Knight",
                    ["customPortrait"] = (SpriteBatch sb, Rectangle rect, Color color) => {
                        Texture2D texture = ModContent.Request<Texture2D>("DeterministicChaos/Content/NPCs/Bosses/RoaringKnight", AssetRequestMode.ImmediateLoad).Value;
                        int frameWidth = 100;
                        int frameHeight = 100;
                        Rectangle sourceRect = new Rectangle(0, 0, frameWidth, frameHeight);
                        Vector2 center = rect.Center.ToVector2();
                        float scale = (float)rect.Width / frameWidth * 0.8f;
                        sb.Draw(texture, center, sourceRect, color, 0f, new Vector2(frameWidth / 2, frameHeight / 2), scale, SpriteEffects.None, 0f);
                    }
                }
            );
        }
    }
}
