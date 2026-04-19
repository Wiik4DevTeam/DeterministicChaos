using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.NPCs.Bosses;
using DeterministicChaos.Content.NPCs.Bosses.Trophies;
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

            List<int> jevilCollectibles = new List<int>
            {
                ModContent.ItemType<JevilTrophy>(),
                ModContent.ItemType<JevilRelic>(),
                ModContent.ItemType<OopsAllCrits>(),
                ItemID.HealingPotion
            };

            AddIfValid(jevilCollectibles, ResolveFirstItemId("LesserLuckPotion", "LuckPotionLesser"));
            AddIfValid(jevilCollectibles, ResolveFirstItemId("LuckPotion"));
            AddIfValid(jevilCollectibles, ResolveFirstItemId("GreaterLuckPotion", "LuckPotionGreater"));
            AddCalamityBossCollectibles(jevilCollectibles);

            List<int> eramCollectibles = new List<int>
            {
                ModContent.ItemType<ShadowMantle>(),
                ModContent.ItemType<SoulCatalyst>()
            };
            AddCalamityBossCollectibles(eramCollectibles);

            List<int> knightCollectibles = new List<int>
            {
                ModContent.ItemType<KnightTrophy>(),
                ModContent.ItemType<KnightRelic>(),
                ModContent.ItemType<RoaringRing>(),
                ModContent.ItemType<RoaringShield>(),
                ModContent.ItemType<RoaringLens>(),
                ModContent.ItemType<RodOfStagnation>()
            };
            AddCalamityBossCollectibles(knightCollectibles);

            List<int> titanCollectibles = new List<int>
            {
                ModContent.ItemType<TitanTrophy>(),
                ModContent.ItemType<TitanRelic>()
            };
            AddCalamityBossCollectibles(titanCollectibles);
            
            // Register Jevil late pre-hardmode, after Deerclops and Calamity's Slime God.
            bossChecklist.Call(
                "LogBoss",
                Mod,
                "Jevil",
                6.9f,
                () => ERAMProgressSystem.JevilDefeated,
                ModContent.NPCType<Jevil>(),
                new Dictionary<string, object>()
                {
                    ["spawnItems"] = ModContent.ItemType<DevilsKey>(),
                    ["spawnInfo"] = "Appears naturally during the night.",
                    ["displayName"] = "Jevil",
                    ["collectibles"] = jevilCollectibles,
                    ["customPortrait"] = (SpriteBatch sb, Rectangle rect, Color color) => {
                        Texture2D texture = ModContent.Request<Texture2D>("DeterministicChaos/Content/NPCs/Bosses/Checklist/JevilIcon", AssetRequestMode.ImmediateLoad).Value;
                        int frameWidth = texture.Width;
                        int frameHeight = texture.Height;
                        Rectangle sourceRect = new Rectangle(0, 0, frameWidth, frameHeight);
                        Vector2 center = rect.Center.ToVector2();
                        float scale = (float)rect.Width / frameWidth * 0.8f;
                        sb.Draw(texture, center, sourceRect, color, 0f, new Vector2(frameWidth / 2, frameHeight / 2), scale, SpriteEffects.None, 0f);
                    }
                }
            );

            // Register ERAM as first hardmode boss (Queen Slime is 7.0, hardmode page starts at 7.0+)
            bossChecklist.Call(
                "LogBoss",                                          // Call type
                Mod,                                                // Mod instance
                "ERAM",                                             // Internal boss name
                7.01f,                                              // Progression value (first hardmode boss, just before Queen Slime)
                () => ERAMProgressSystem.ERAMDefeated,              // Downed condition
                ModContent.NPCType<ERAM>(),                         // NPC type
                new Dictionary<string, object>()                    // Extra info
                {
                    ["spawnItems"] = ModContent.ItemType<ERAMSummon>(),
                    ["spawnInfo"] = "Use a Game Controller.",
                    ["displayName"] = "E.R.A.M",
                    ["collectibles"] = eramCollectibles,
                    ["customPortrait"] = (SpriteBatch sb, Rectangle rect, Color color) => {
                        Texture2D texture = ModContent.Request<Texture2D>("DeterministicChaos/Content/NPCs/Bosses/Checklist/ERAMIcon", AssetRequestMode.ImmediateLoad).Value;
                        int frameWidth = texture.Width;
                        int frameHeight = texture.Height;
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
                    ["spawnInfo"] = "Summon the Eye of Cthulhu with darker intent.",
                    ["displayName"] = "Roaring Knight",
                    ["collectibles"] = knightCollectibles,
                    ["customPortrait"] = (SpriteBatch sb, Rectangle rect, Color color) => {
                        Texture2D texture = ModContent.Request<Texture2D>("DeterministicChaos/Content/NPCs/Bosses/Checklist/KnightIcon", AssetRequestMode.ImmediateLoad).Value;
                        int frameWidth = texture.Width;
                        int frameHeight = texture.Height;
                        Rectangle sourceRect = new Rectangle(0, 0, frameWidth, frameHeight);
                        Vector2 center = rect.Center.ToVector2();
                        float scale = (float)rect.Width / frameWidth * 0.8f;
                        sb.Draw(texture, center, sourceRect, color, 0f, new Vector2(frameWidth / 2, frameHeight / 2), scale, SpriteEffects.None, 0f);
                    }
                }
            );
            
            // Register Titan, right after Plantera (Plantera is 12.0)
            // Using 12.5 to place it after Plantera
            bossChecklist.Call(
                "LogBoss",                                          // Call type
                Mod,                                                // Mod instance
                "Titan",                                            // Internal boss name
                12.5f,                                              // Progression value (after Plantera at 12.0)
                () => ERAMProgressSystem.TitanDefeated,             // Downed condition
                ModContent.NPCType<TitanBody>(),                    // NPC type
                new Dictionary<string, object>()                    // Extra info
                {
                    ["spawnItems"] = ModContent.ItemType<DarkShard>(),
                    ["spawnInfo"] = "Create a Dark Fountain inside a Dark World.",
                    ["displayName"] = "Titan",
                    ["collectibles"] = titanCollectibles,
                    ["customPortrait"] = (SpriteBatch sb, Rectangle rect, Color color) => {
                        Texture2D texture = ModContent.Request<Texture2D>("DeterministicChaos/Content/NPCs/Bosses/Checklist/TitanIcon", AssetRequestMode.ImmediateLoad).Value;
                        int frameWidth = texture.Width;
                        int frameHeight = texture.Height;
                        Rectangle sourceRect = new Rectangle(0, 0, frameWidth, frameHeight);
                        Vector2 center = rect.Center.ToVector2();
                        float scale = (float)rect.Width / frameWidth * 0.8f;
                        sb.Draw(texture, center, sourceRect, color, 0f, new Vector2(frameWidth / 2, frameHeight / 2), scale, SpriteEffects.None, 0f);
                    }
                }
            );
        }

        private static void AddIfValid(List<int> list, int itemId)
        {
            if (itemId > 0)
                list.Add(itemId);
        }

        private static int ResolveFirstItemId(params string[] names)
        {
            foreach (string name in names)
            {
                FieldInfo field = typeof(ItemID).GetField(name, BindingFlags.Public | BindingFlags.Static);
                if (field == null)
                    continue;

                object value = field.GetValue(null);
                if (value is int intValue)
                    return intValue;

                if (value is short shortValue)
                    return shortValue;
            }

            return -1;
        }

        private static void AddCalamityBossCollectibles(List<int> list)
        {
            AddIfValid(list, CalamityBossLootHelper.ResolveCalamityItemId("ShadowDiamond"));
            AddIfValid(list, CalamityBossLootHelper.ResolveCalamityItemId("Laudanum"));
            AddIfValid(list, CalamityBossLootHelper.ResolveCalamityItemId("HeartOfDarkness", "HeartofDarkness"));
            AddIfValid(list, CalamityBossLootHelper.ResolveCalamityItemId("StressPills"));
        }
    }
}
