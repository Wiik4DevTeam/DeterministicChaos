using SubworldLibrary;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using DeterministicChaos.Content.Subworlds;
using DeterministicChaos.Content.Items;

namespace DeterministicChaos.Content.Subworlds
{
    public class ERAMArenaPlayer : ModPlayer
    {
        // Storage for player inventory when entering arena (per-player instance)
        private Item[] savedInventory;
        private Item[] savedArmor;
        private Item[] savedAccessories;
        private bool hasSwappedInventory = false;
        
        // Journey mode state storage
        private bool savedGodMode = false;
        private bool savedIsJourneyMode = false;
        
        // Player appearance storage
        private int savedSkinVariant;
        private int savedHair;
        private Color savedHairColor;
        private Color savedSkinColor;
        private Color savedEyeColor;
        private Color savedShirtColor;
        private Color savedUnderShirtColor;
        private Color savedPantsColor;
        private Color savedShoeColor;
        
        // Track if ERAM was defeated and player survived
        public bool defeatedERAM = false;
        
        public override void OnEnterWorld()
        {
            if (SubworldSystem.IsActive<ERAMArena>())
            {
                // Entering the arena, save and swap inventory
                if (!hasSwappedInventory)
                {
                    // Save Journey Mode god mode state
                    if (Main.GameMode == GameModeID.Creative)
                    {
                        savedIsJourneyMode = true;
                        savedGodMode = Player.creativeGodMode;
                        Player.creativeGodMode = false;
                    }
                    
                    // Clear all buffs when entering arena
                    for (int i = Player.MaxBuffs - 1; i >= 0; i--)
                    {
                        if (Player.buffType[i] > 0)
                        {
                            Player.DelBuff(i);
                        }
                    }
                    
                    SavePlayerInventory();
                    GiveArenaInventory();
                    hasSwappedInventory = true;
                }
            }
            else
            {
                // Returned to main world, restore inventory
                if (hasSwappedInventory && savedInventory != null)
                {
                    RestorePlayerInventory();
                    
                    // Restore Journey Mode god mode state
                    if (savedIsJourneyMode && Main.GameMode == GameModeID.Creative)
                    {
                        Player.creativeGodMode = savedGodMode;
                    }
                    
                    // Give ShadowMantle if ERAM was defeated and player survived
                    if (defeatedERAM)
                    {
                        int shadowMantleType = ModContent.ItemType<ShadowMantle>();
                        Player.QuickSpawnItem(Player.GetSource_GiftOrReward(), shadowMantleType);
                        defeatedERAM = false;
                    }
                    
                    savedIsJourneyMode = false;
                    hasSwappedInventory = false;
                }
            }
        }
        
        private void SavePlayerInventory()
        {
            // Save main inventory (slots 0-49)
            savedInventory = new Item[50];
            for (int i = 0; i < 50; i++)
            {
                savedInventory[i] = Player.inventory[i].Clone();
            }
            
            // Save armor (slots 0-2)
            savedArmor = new Item[3];
            for (int i = 0; i < 3; i++)
            {
                savedArmor[i] = Player.armor[i].Clone();
            }
            
            // Save accessories (slots 3-9 in armor array)
            savedAccessories = new Item[7];
            for (int i = 0; i < 7; i++)
            {
                savedAccessories[i] = Player.armor[i + 3].Clone();
            }
            
            // Save player appearance
            savedSkinVariant = Player.skinVariant;
            savedHair = Player.hair;
            savedHairColor = Player.hairColor;
            savedSkinColor = Player.skinColor;
            savedEyeColor = Player.eyeColor;
            savedShirtColor = Player.shirtColor;
            savedUnderShirtColor = Player.underShirtColor;
            savedPantsColor = Player.pantsColor;
            savedShoeColor = Player.shoeColor;
        }
        
        private void GiveArenaInventory()
        {
            // Clear entire inventory
            for (int i = 0; i < 50; i++)
            {
                Player.inventory[i].TurnToAir();
            }
            
            // Clear armor and accessories
            for (int i = 0; i < Player.armor.Length; i++)
            {
                Player.armor[i].TurnToAir();
            }
            
            // Give Platinum Shortsword in first slot
            Player.inventory[0].SetDefaults(ItemID.PlatinumShortsword);
            
            // Give Familiar armor set
            Player.armor[0].SetDefaults(ItemID.FamiliarWig);
            Player.armor[1].SetDefaults(ItemID.FamiliarShirt);
            Player.armor[2].SetDefaults(ItemID.FamiliarPants);
            
            // Give Fledgling Wings in first accessory slot (armor slot 3)
            Player.armor[3].SetDefaults(ItemID.CreativeWings);
            
            // Set familiar cosmetic set for default look
            Player.skinVariant = 0;
            Player.hair = 0;
            Player.hairColor = new Color(215, 90, 55);
            Player.skinColor = new Color(255, 125, 90);
            Player.eyeColor = new Color(105, 90, 75);
            Player.shirtColor = new Color(175, 165, 140);
            Player.underShirtColor = new Color(160, 180, 215);
            Player.pantsColor = new Color(255, 230, 175);
            Player.shoeColor = new Color(160, 105, 60);
        }
        
        private void RestorePlayerInventory()
        {
            // Restore main inventory
            if (savedInventory != null)
            {
                for (int i = 0; i < 50; i++)
                {
                    Player.inventory[i] = savedInventory[i].Clone();
                }
            }
            
            // Restore armor
            if (savedArmor != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    Player.armor[i] = savedArmor[i].Clone();
                }
            }
            
            // Restore accessories
            if (savedAccessories != null)
            {
                for (int i = 0; i < 7; i++)
                {
                    Player.armor[i + 3] = savedAccessories[i].Clone();
                }
            }
            
            // Restore player appearance
            Player.skinVariant = savedSkinVariant;
            Player.hair = savedHair;
            Player.hairColor = savedHairColor;
            Player.skinColor = savedSkinColor;
            Player.eyeColor = savedEyeColor;
            Player.shirtColor = savedShirtColor;
            Player.underShirtColor = savedUnderShirtColor;
            Player.pantsColor = savedPantsColor;
            Player.shoeColor = savedShoeColor;
            
            // Clear saved data
            savedInventory = null;
            savedArmor = null;
            savedAccessories = null;
        }
        
        public override void ResetEffects()
        {
            if (SubworldSystem.IsActive<ERAMArena>() && hasSwappedInventory)
            {
                // Reset armor set bonuses and accessory effects
                Player.setBonus = "";
                
                // Reset damage bonuses from equipment
                Player.GetDamage(DamageClass.Generic) = Player.GetDamage(DamageClass.Generic).Scale(1f);
                Player.GetDamage(DamageClass.Melee) = Player.GetDamage(DamageClass.Melee).Scale(1f);
                Player.GetDamage(DamageClass.Ranged) = Player.GetDamage(DamageClass.Ranged).Scale(1f);
                Player.GetDamage(DamageClass.Magic) = Player.GetDamage(DamageClass.Magic).Scale(1f);
                Player.GetDamage(DamageClass.Summon) = Player.GetDamage(DamageClass.Summon).Scale(1f);
            }
        }
        
        public override void PostUpdateEquips()
        {
            if (SubworldSystem.IsActive<ERAMArena>() && hasSwappedInventory)
            {
                // Strip all armor and accessory effects after they have been applied
                
                // Reset set bonus text
                Player.setBonus = "";
                
                // Reset movement speed bonuses
                Player.moveSpeed = 1f;
                Player.maxRunSpeed = 3f;
                Player.runAcceleration = 0.08f;
                
                // Reset jump bonuses
                Player.jumpSpeedBoost = 0f;
                Player.extraFall = 0;
                
                // Reset minion and sentry slots to base
                Player.maxMinions = 1;
                Player.maxTurrets = 1;
                
                // Reset crit chances to base
                Player.GetCritChance(DamageClass.Generic) = 4f;
                Player.GetCritChance(DamageClass.Melee) = 4f;
                Player.GetCritChance(DamageClass.Ranged) = 4f;
                Player.GetCritChance(DamageClass.Magic) = 4f;
                
                // Disable special accessory effects
                Player.accWatch = 0;
                Player.accCompass = 0;
                Player.accDepthMeter = 0;
                Player.accDivingHelm = false;
                Player.accFishingLine = false;
                Player.accTackleBox = false;
                Player.accFlipper = false;
                Player.accMerman = false;
                Player.arcticDivingGear = false;
                Player.accDreamCatcher = false;
                Player.accOreFinder = false;
                Player.accStopwatch = false;
                Player.accCritterGuide = false;
                Player.accThirdEye = false;
                Player.accJarOfSouls = false;
                Player.accCalendar = false;
                Player.accWeatherRadio = false;
                
                // Disable damage reduction accessories
                Player.endurance = 0f;
                Player.brainOfConfusionItem = null;
                Player.starCloakItem = null;
                
                // Disable dash abilities from accessories
                Player.dashType = 0;
                
                // Disable double jump accessories via the jump state helper
                Player.RefreshExtraJumps();
                
                // Disable grappling hook speed bonuses
                Player.rocketBoots = 0;
                
                // Disable immunity accessories
                Player.fireWalk = false;
                Player.lavaImmune = false;
                Player.buffImmune[BuffID.OnFire] = false;
                Player.buffImmune[BuffID.Burning] = false;
                
                // Disable light sources
                Player.nightVision = false;
                Player.detectCreature = false;
                
                // Hide visual effects from armor
                Player.cHead = -1;
                Player.cBody = -1;
                Player.cLegs = -1;
            }
        }
        
        public override void PreUpdate()
        {
            if (SubworldSystem.IsActive<ERAMArena>())
            {
                // Prevent building and breaking
                Player.noBuilding = true;
                
                // Extra fall distance so player does not take fall damage
                Player.extraFall += 50;
            }
        }
        
        public override void PostUpdate()
        {
            if (SubworldSystem.IsActive<ERAMArena>())
            {
                // Double-check building prevention
                Player.noBuilding = true;
            }
        }
        
        public override bool CanUseItem(Item item)
        {
            if (SubworldSystem.IsActive<ERAMArena>())
            {
                // Block placing items
                if (item.createTile >= 0 || item.createWall >= 0)
                    return false;
            }
            return true;
        }
        
        public override void Kill(double damage, int hitDirection, bool pvp, Terraria.DataStructures.PlayerDeathReason damageSource)
        {
            if (SubworldSystem.IsActive<ERAMArena>())
            {
                SubworldSystem.Exit();
            }
        }
    }
}
