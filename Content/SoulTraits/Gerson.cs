using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Utilities;
using DeterministicChaos.Content.Items;
using DeterministicChaos.Content.Systems;
using DeterministicChaos.Content.SoulTraits.Armor;

namespace DeterministicChaos.Content.SoulTraits
{
    [AutoloadHead]
    public class Gerson : ModNPC
    {
        public const string ShopName = "Shop";
        
        private int frameCounter = 0;
        private int currentFrame = 0;
        private bool isWaving = false;
        private int waveTimer = 0;

        // Menu state: 0 = main menu, 1 = traits sub-menu, 2 = dialogue pages
        private int menuState = 0;
        private int dialoguePageIndex = 0;

        private List<string> GetDialogueLines()
        {
            var lines = new List<string>();

            if (ERAMProgressSystem.ERAMDefeated)
            {
                lines.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.HelpPostERAM1"));
            }
            else if (Main.hardMode)
            {
                lines.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.HelpPostHardmode1"));
                lines.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.HelpPostHardmode2"));
                lines.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.HelpPostHardmode3"));
            }
            else if (NPC.downedBoss3)
            {
                lines.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.HelpPreHardmode1"));
                lines.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.HelpPreHardmode2"));
            }
            else
            {
                lines.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.HelpPreSkeletron1"));
                lines.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.HelpPreSkeletron2"));
                lines.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.HelpPreSkeletron3"));
            }

            return lines;
        }

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 6;
            NPCID.Sets.ExtraFramesCount[Type] = 0;
            NPCID.Sets.AttackFrameCount[Type] = 0;
            NPCID.Sets.DangerDetectRange[Type] = 700;
            NPCID.Sets.AttackType[Type] = -1;
            NPCID.Sets.AttackTime[Type] = -1;
            NPCID.Sets.AttackAverageChance[Type] = 1;
            NPCID.Sets.HatOffsetY[Type] = 4;

            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers()
            {
                Velocity = 1f,
                Direction = 1
            };

            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);

            NPC.Happiness
                .SetBiomeAffection<ForestBiome>(AffectionLevel.Like)
                .SetBiomeAffection<UndergroundBiome>(AffectionLevel.Love)
                .SetBiomeAffection<DesertBiome>(AffectionLevel.Dislike)
                .SetNPCAffection(NPCID.Guide, AffectionLevel.Like)
                .SetNPCAffection(NPCID.Wizard, AffectionLevel.Love)
                .SetNPCAffection(NPCID.ArmsDealer, AffectionLevel.Dislike);
        }

        public override void SetDefaults()
        {
            NPC.townNPC = true;
            NPC.friendly = true;
            NPC.width = 14;
            NPC.height = 32;
            NPC.aiStyle = 7;
            NPC.damage = 150;
            NPC.defense = 100;
            NPC.lifeMax = 30000;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0.5f;
            NPC.scale = 0.8f;
            AnimationType = NPCID.Guide;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
            {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Underground,
                new FlavorTextBestiaryInfoElement("Mods.DeterministicChaos.Bestiary.Gerson")
            });
        }

        public override bool CanTownNPCSpawn(int numTownNPCs)
        {
            // Gerson can spawn once any boss has been defeated
            return NPC.downedBoss1 || NPC.downedBoss2 || NPC.downedBoss3 || NPC.downedSlimeKing;
        }

        public override List<string> SetNPCNameList()
        {
            return new List<string>() { "Gerson" };
        }

        public override void FindFrame(int frameHeight)
        {
            frameCounter++;

            // Check if NPC is moving
            bool isMoving = NPC.velocity.X != 0;

            if (isWaving)
            {
                // Waving animation (frames 5-6, which are indices 4-5)
                if (frameCounter >= 10)
                {
                    frameCounter = 0;
                    currentFrame++;
                    if (currentFrame > 5)
                        currentFrame = 4;
                }
                
                waveTimer--;
                if (waveTimer <= 0)
                {
                    isWaving = false;
                    currentFrame = 0;
                }
            }
            else if (isMoving)
            {
                // Walking animation (frames 1-4, which are indices 0-3)
                if (frameCounter >= 6)
                {
                    frameCounter = 0;
                    currentFrame++;
                    if (currentFrame > 3)
                        currentFrame = 0;
                }
            }
            else
            {
                // Idle (frame 1, index 0)
                currentFrame = 0;
                frameCounter = 0;
            }

            NPC.frame.Y = currentFrame * frameHeight;
        }

        public override string GetChat()
        {
            Player player = Main.LocalPlayer;
            SoulTraitPlayer traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            WeightedRandom<string> chat = new WeightedRandom<string>();

            // Start waving when talked to
            isWaving = true;
            waveTimer = 120;

            // Reset menu state when opening chat
            menuState = 0;
            dialoguePageIndex = 0;

            if (traitPlayer.CurrentTrait == SoulTraitType.None)
            {
                chat.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.NoTrait1"));
                chat.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.NoTrait2"));
                chat.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.NoTrait3"));
            }
            else if (traitPlayer.TraitLocked)
            {
                chat.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.Locked1"));
                chat.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.Locked2"));
            }
            else
            {
                chat.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.HasTrait1"));
                chat.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.HasTrait2"));
            }

            // General dialogue
            chat.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.General1"));
            chat.Add(Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.General2"));

            return chat;
        }

        public override void SetChatButtons(ref string button, ref string button2)
        {
            switch (menuState)
            {
                case 0: // Main menu
                    button = Language.GetTextValue("LegacyInterface.28"); // "Shop"
                    button2 = Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.Traits");
                    break;

                case 1: // Traits sub-menu
                    button = Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.Dialogue");
                    button2 = Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.SelectTrait");
                    break;

                case 2: // Dialogue pages
                    var lines = GetDialogueLines();
                    bool isLastPage = dialoguePageIndex >= lines.Count - 1;
                    button = isLastPage
                        ? Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.Back")
                        : Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.Next");
                    button2 = Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.Back");
                    break;
            }
        }

        public override void OnChatButtonClicked(bool firstButton, ref string shopName)
        {
            switch (menuState)
            {
                case 0: // Main menu
                    if (firstButton)
                    {
                        // Shop
                        shopName = ShopName;
                    }
                    else
                    {
                        // Traits sub-menu
                        menuState = 1;
                    }
                    break;

                case 1: // Traits sub-menu
                    if (firstButton)
                    {
                        // Dialogue
                        menuState = 2;
                        dialoguePageIndex = 0;
                        var lines = GetDialogueLines();
                        if (lines.Count > 0)
                        {
                            Main.npcChatText = lines[0];
                        }
                    }
                    else
                    {
                        // Select Trait - open UI
                        ModContent.GetInstance<GersonTraitUISystem>().OpenTraitSelection();
                    }
                    break;

                case 2: // Dialogue pages
                    if (firstButton)
                    {
                        var lines = GetDialogueLines();
                        bool isLastPage = dialoguePageIndex >= lines.Count - 1;
                        
                        if (isLastPage)
                        {
                            // Go back to traits sub-menu
                            menuState = 1;
                        }
                        else
                        {
                            // Next page
                            dialoguePageIndex++;
                            if (dialoguePageIndex < lines.Count)
                            {
                                Main.npcChatText = lines[dialoguePageIndex];
                            }
                        }
                    }
                    else
                    {
                        // Back to traits sub-menu
                        menuState = 1;
                    }
                    break;
            }
        }

        public override void AddShops()
        {
            var shop = new NPCShop(Type, ShopName);
            
            // Custom conditions for each soul trait
            Condition hasJustice = new Condition("Mods.DeterministicChaos.Conditions.HasJustice", () => 
                Main.LocalPlayer.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Justice);
            Condition hasKindness = new Condition("Mods.DeterministicChaos.Conditions.HasKindness", () => 
                Main.LocalPlayer.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Kindness);
            Condition hasBravery = new Condition("Mods.DeterministicChaos.Conditions.HasBravery", () => 
                Main.LocalPlayer.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Bravery);
            Condition hasPatience = new Condition("Mods.DeterministicChaos.Conditions.HasPatience", () => 
                Main.LocalPlayer.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Patience);
            Condition hasIntegrity = new Condition("Mods.DeterministicChaos.Conditions.HasIntegrity", () => 
                Main.LocalPlayer.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Integrity);
            Condition hasPerseverance = new Condition("Mods.DeterministicChaos.Conditions.HasPerseverance", () => 
                Main.LocalPlayer.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Perseverance);
            Condition hasDetermination = new Condition("Mods.DeterministicChaos.Conditions.HasDetermination", () => 
                Main.LocalPlayer.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Determination);
            
            // Soul Trait Accessories - only show for matching trait
            shop.Add<CloudyGlasses>(hasPerseverance);   // Perseverance
            shop.Add<CowboyHat>(hasJustice);            // Justice
            shop.Add<FadedRibbon>(hasPatience);         // Patience
            shop.Add<HeartLocket>(hasDetermination);    // Determination
            shop.Add<ManlyBandana>(hasBravery);         // Bravery
            shop.Add<OldTuTu>(hasIntegrity);            // Integrity
            shop.Add<StainedApron>(hasKindness);        // Kindness

            // Soul Trait Weapons - only show for matching trait after Skeletron
            shop.Add<RustyKnife>(Condition.DownedSkeletron, hasDetermination);
            shop.Add<FryingPan>(Condition.DownedSkeletron, hasKindness);
            shop.Add<HollowGun>(Condition.DownedSkeletron, hasJustice);
            shop.Add<TornNotebook>(Condition.DownedSkeletron, hasPerseverance);
            shop.Add<BalletShoes>(Condition.DownedSkeletron, hasIntegrity);
            shop.Add<ToyKnife>(Condition.DownedSkeletron, hasPatience);
            shop.Add<ToughGlove>(Condition.DownedSkeletron, hasBravery);

            // Minor Soul Essence, always available
            shop.Add<SoulEssenceT1>();

            // Game Controller (ERAM Summon), available after entering Hardmode (for all traits)
            shop.Add<ERAMSummon>(Condition.Hardmode);
            
            shop.Register();
        }

        public override bool CanGoToStatue(bool toKingStatue)
        {
            return true;
        }

        public override void TownNPCAttackStrength(ref int damage, ref float knockback)
        {
            damage = 20;
            knockback = 4f;
        }

        public override void TownNPCAttackCooldown(ref int cooldown, ref int randExtraCooldown)
        {
            cooldown = 30;
            randExtraCooldown = 30;
        }
    }
}
