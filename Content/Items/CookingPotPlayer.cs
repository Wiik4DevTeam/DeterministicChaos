using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using DeterministicChaos.Content.Buffs;

namespace DeterministicChaos.Content.Items
{
    public class CookingPotPlayer : ModPlayer
    {
        // --- Ingredient slot state ---
        // Flask: item type of the flask added (0 = none)
        public int FlaskItemType = 0;
        // Food: item type of stuffed food added (0 = none)
        public int FoodItemType = 0;
        // Alcohol: item type of the Calamity alcohol added (0 = none)
        public int AlcoholItemType = 0;

        // Food tier determines heal amount
        // Tier 1 (WellFed) = 2 HP, Tier 2 (Plenty Satisfied) = 5 HP, Tier 3 (Exquisitely Stuffed) = 7 HP
        public int GetFoodHealAmount()
        {
            if (FoodItemType <= 0)
                return 0;

            Item testItem = new Item();
            testItem.SetDefaults(FoodItemType);

            if (testItem.buffType == BuffID.WellFed3) return 7;
            if (testItem.buffType == BuffID.WellFed2) return 5;
            if (testItem.buffType == BuffID.WellFed) return 2;

            return 2; // Fallback
        }

        // Returns the NPC debuff ID corresponding to the held flask.
        public int GetFlaskDebuffID()
        {
            if (FlaskItemType <= 0)
                return -1;

            // Check vanilla flasks first
            int vanillaDebuff = FlaskItemType switch
            {
                ItemID.FlaskofVenom => BuffID.Venom,
                ItemID.FlaskofCursedFlames => BuffID.CursedInferno,
                ItemID.FlaskofFire => BuffID.OnFire,
                ItemID.FlaskofGold => BuffID.Midas,
                ItemID.FlaskofIchor => BuffID.Ichor,
                ItemID.FlaskofNanites => BuffID.Confused,
                ItemID.FlaskofPoison => BuffID.Poisoned,
                _ => -1
            };

            if (vanillaDebuff > 0)
                return vanillaDebuff;

            // Titansbane flask
            if (FlaskItemType == ModContent.ItemType<TitansBaneFlask>())
                return ModContent.BuffType<TitansBane>();

            // For Calamity flasks, map to their respective enemy debuffs
            if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
            {
                Item testItem = new Item();
                testItem.SetDefaults(FlaskItemType);
                string className = testItem.ModItem?.GetType().Name ?? "";

                // Flask of Brimstone → BrimstoneFlames
                // Flask of Crumbling → Crumbling
                // Flask of Holy Flames → HolyFlames
                string debuffName = className switch
                {
                    "FlaskOfBrimstone" => "BrimstoneFlames",
                    "FlaskOfCrumbling" => "Crumbling",
                    "FlaskOfHolyFlames" => "HolyFlames",
                    _ => null
                };

                if (debuffName != null && calamity.TryFind<ModBuff>(debuffName, out ModBuff debuff))
                    return debuff.Type;
            }

            return -1;
        }

        // Returns the buff ID for the Calamity alcohol, resolved via reflection.
        // Returns -1 if Calamity isn't loaded or alcohol is empty.
        public int GetAlcoholBuffID()
        {
            if (AlcoholItemType <= 0)
                return -1;

            if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity))
                return -1;

            // Look up the buff type from the alcohol item's defaults
            Item testItem = new Item();
            testItem.SetDefaults(AlcoholItemType);

            if (testItem.buffType > 0)
                return testItem.buffType;

            return -1;
        }

        // Checks if an item is a valid flask for the cooking pot.
        public static bool IsFlask(Item item)
        {
            if (item == null || item.IsAir) return false;

            int type = item.type;
            // Vanilla flasks
            if (type == ItemID.FlaskofVenom
                || type == ItemID.FlaskofCursedFlames
                || type == ItemID.FlaskofFire
                || type == ItemID.FlaskofGold
                || type == ItemID.FlaskofIchor
                || type == ItemID.FlaskofNanites
                || type == ItemID.FlaskofPoison)
                return true;
            // FlaskofParty is excluded (cosmetic only)

            // Titansbane Flask
            if (type == ModContent.ItemType<TitansBaneFlask>())
                return true;

            // Calamity flasks
            if (IsCalamityFlask(item))
                return true;

            return false;
        }

        // Checks if an item is a Calamity flask. Uses reflection to avoid hard dependency.
        public static bool IsCalamityFlask(Item item)
        {
            if (item == null || item.IsAir) return false;
            if (!ModLoader.HasMod("CalamityMod")) return false;
            if (item.ModItem == null) return false;

            string fullName = item.ModItem.GetType().FullName ?? "";
            return fullName.Contains("CalamityMod.Items.Potions") && fullName.Contains("Flask");
        }

        // Checks if an item is a valid food item (gives WellFed/PlenySatisfied/ExquisitelyStuffed).
        public static bool IsFood(Item item)
        {
            if (item == null || item.IsAir) return false;

            return item.buffType == BuffID.WellFed
                || item.buffType == BuffID.WellFed2
                || item.buffType == BuffID.WellFed3;
        }

        // Checks if an item is a Calamity alcohol. Uses reflection to avoid hard dependency.
        public static bool IsCalamityAlcohol(Item item)
        {
            if (item == null || item.IsAir) return false;
            if (!ModLoader.HasMod("CalamityMod")) return false;

            // Calamity alcohols are ModItems in CalamityMod.Items.Potions.Alcohol namespace
            if (item.ModItem == null) return false;

            string fullName = item.ModItem.GetType().FullName ?? "";
            return fullName.Contains("CalamityMod.Items.Potions.Alcohol");
        }

        // Tries to add an item to the pot slot. Returns true + consumes 1 from stack if successful.
        public bool TryAddIngredient(Item item)
        {
            if (item == null || item.IsAir)
                return false;

            if (IsFlask(item))
            {
                if (FlaskItemType == item.type)
                    return false; // Already has this exact flask

                FlaskItemType = item.type;
                item.stack--;
                if (item.stack <= 0) item.TurnToAir();
                return true;
            }

            if (IsFood(item))
            {
                if (FoodItemType == item.type)
                    return false;

                FoodItemType = item.type;
                item.stack--;
                if (item.stack <= 0) item.TurnToAir();
                return true;
            }

            if (IsCalamityAlcohol(item))
            {
                if (AlcoholItemType == item.type)
                    return false;

                AlcoholItemType = item.type;
                item.stack--;
                if (item.stack <= 0) item.TurnToAir();
                return true;
            }

            return false;
        }

        public override void SaveData(TagCompound tag)
        {
            tag["CookingPotFlask"] = FlaskItemType;
            tag["CookingPotFood"] = FoodItemType;
            tag["CookingPotAlcohol"] = AlcoholItemType;
        }

        public override void LoadData(TagCompound tag)
        {
            FlaskItemType = tag.GetInt("CookingPotFlask");
            FoodItemType = tag.GetInt("CookingPotFood");
            AlcoholItemType = tag.GetInt("CookingPotAlcohol");
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)0xFF); // CookingPot sync ID
            packet.Write((byte)Player.whoAmI);
            packet.Write(FlaskItemType);
            packet.Write(FoodItemType);
            packet.Write(AlcoholItemType);
            packet.Send(toWho, fromWho);
        }

        // Handles the 0xFF sync packet for CookingPot ingredients.
        // Called from DeterministicChaos.HandlePacket.
        public static void HandleSyncPacket(System.IO.BinaryReader reader, int whoAmI)
        {
            byte playerIndex = reader.ReadByte();
            int flask = reader.ReadInt32();
            int food = reader.ReadInt32();
            int alcohol = reader.ReadInt32();

            if (playerIndex >= 0 && playerIndex < Main.maxPlayers)
            {
                Player player = Main.player[playerIndex];
                if (player.active)
                {
                    var potPlayer = player.GetModPlayer<CookingPotPlayer>();
                    potPlayer.FlaskItemType = flask;
                    potPlayer.FoodItemType = food;
                    potPlayer.AlcoholItemType = alcohol;

                    // Server relays to all other clients
                    if (Main.netMode == Terraria.ID.NetmodeID.Server)
                    {
                        ModPacket packet = ModContent.GetInstance<DeterministicChaos>().GetPacket();
                        packet.Write((byte)0xFF);
                        packet.Write(playerIndex);
                        packet.Write(flask);
                        packet.Write(food);
                        packet.Write(alcohol);
                        packet.Send(-1, whoAmI);
                    }
                }
            }
        }
    }
}
