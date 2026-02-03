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
            Player player = Main.LocalPlayer;
            SoulTraitPlayer traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            if (traitPlayer.TraitLocked)
            {
                button = Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.ViewTrait");
            }
            else
            {
                button = Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.ChooseTrait");
            }
            
            button2 = Language.GetTextValue("LegacyInterface.28"); // "Shop"
        }

        public override void OnChatButtonClicked(bool firstButton, ref string shopName)
        {
            if (firstButton)
            {
                Player player = Main.LocalPlayer;
                SoulTraitPlayer traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

                if (traitPlayer.TraitLocked)
                {
                    // Just show info about current trait
                    string traitName = SoulTraitData.GetTraitName(traitPlayer.CurrentTrait);
                    Main.npcChatText = Language.GetTextValue("Mods.DeterministicChaos.Dialogue.Gerson.TraitInfo", traitName);
                }
                else
                {
                    // Open trait selection UI
                    ModContent.GetInstance<GersonTraitUISystem>().OpenTraitSelection();
                }
            }
            else
            {
                // Open shop
                shopName = ShopName;
            }
        }

        public override void AddShops()
        {
            var shop = new NPCShop(Type, ShopName);
            
            // Soul Trait Accessories, one for each trait
            shop.Add<CloudyGlasses>();   // Perseverance
            shop.Add<CowboyHat>();       // Justice
            shop.Add<FadedRibbon>();     // Patience
            shop.Add<HeartLocket>();     // Determination
            shop.Add<ManlyBandana>();    // Bravery
            shop.Add<OldTuTu>();         // Integrity
            shop.Add<StainedApron>();    // Kindness
            
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
