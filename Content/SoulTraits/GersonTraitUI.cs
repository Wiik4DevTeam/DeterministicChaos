using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace DeterministicChaos.Content.SoulTraits
{
    public class GersonTraitUISystem : ModSystem
    {
        internal GersonTraitUIState TraitSelectionUI;
        private UserInterface traitSelectionInterface;
        private bool isOpen = false;

        public override void Load()
        {
            if (!Main.dedServ)
            {
                TraitSelectionUI = new GersonTraitUIState();
                TraitSelectionUI.Activate();
                traitSelectionInterface = new UserInterface();
            }
        }

        public override void Unload()
        {
            TraitSelectionUI = null;
            traitSelectionInterface = null;
        }

        public void OpenTraitSelection()
        {
            isOpen = true;
            traitSelectionInterface.SetState(TraitSelectionUI);
            Main.playerInventory = false;
            Main.npcChatText = "";
        }

        public void CloseTraitSelection()
        {
            isOpen = false;
            traitSelectionInterface.SetState(null);
            
            // Fully close NPC chat interface
            Main.npcChatText = "";
            Main.editSign = false;
            Main.npcChatCornerItem = 0;
            Main.LocalPlayer.sign = -1;
            Main.LocalPlayer.SetTalkNPC(-1);
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (isOpen)
            {
                traitSelectionInterface?.Update(gameTime);
                
                // Close if player presses escape or moves too far
                if (Main.LocalPlayer.controlInv || Main.LocalPlayer.controlUseItem)
                {
                    CloseTraitSelection();
                }
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryIndex != -1)
            {
                layers.Insert(inventoryIndex + 1, new LegacyGameInterfaceLayer(
                    "DeterministicChaos: Trait Selection",
                    delegate
                    {
                        if (isOpen)
                        {
                            traitSelectionInterface.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI)
                );
            }
        }
    }

    public class GersonTraitUIState : UIState
    {
        private UIPanel mainPanel;
        private UIText titleText;
        private UIPanel descriptionPanel;
        private UIText descriptionTitle;
        private UIText descriptionText;
        private List<TraitButton> traitButtons = new List<TraitButton>();
        private SoulTraitType hoveredTrait = SoulTraitType.None;

        public override void OnInitialize()
        {
            // Main panel, taller and wider
            mainPanel = new UIPanel();
            mainPanel.Width.Set(800, 0f);
            mainPanel.Height.Set(540, 0f);
            mainPanel.HAlign = 0.5f;
            mainPanel.VAlign = 0.5f;
            mainPanel.BackgroundColor = new Color(30, 30, 50, 220);
            Append(mainPanel);

            // Title, smaller text
            titleText = new UIText("Choose Your Soul Trait", 0.8f, true);
            titleText.HAlign = 0.5f;
            titleText.Top.Set(12, 0f);
            mainPanel.Append(titleText);

            // Create buttons for each trait
            SoulTraitType[] traits = new SoulTraitType[]
            {
                SoulTraitType.Justice,
                SoulTraitType.Kindness,
                SoulTraitType.Bravery,
                SoulTraitType.Patience,
                SoulTraitType.Integrity,
                SoulTraitType.Perseverance,
                SoulTraitType.Determination
            };

            int buttonWidth = 360;
            int buttonHeight = 36;
            int startY = 50;
            int spacing = 42;

            for (int i = 0; i < traits.Length; i++)
            {
                int column = i % 2;
                int row = i / 2;

                TraitButton button = new TraitButton(traits[i], this);
                button.Width.Set(buttonWidth, 0f);
                button.Height.Set(buttonHeight, 0f);
                button.Left.Set(20 + column * (buttonWidth + 20), 0f);
                button.Top.Set(startY + row * spacing, 0f);
                mainPanel.Append(button);
                traitButtons.Add(button);
            }

            // Description panel at the bottom
            descriptionPanel = new UIPanel();
            descriptionPanel.Width.Set(760, 0f);
            descriptionPanel.Height.Set(150, 0f);
            descriptionPanel.HAlign = 0.5f;
            descriptionPanel.Top.Set(230, 0f);
            descriptionPanel.BackgroundColor = new Color(20, 20, 35, 200);
            mainPanel.Append(descriptionPanel);

            // Description title
            descriptionTitle = new UIText("Hover over a trait to see details", 0.9f, false);
            descriptionTitle.HAlign = 0.5f;
            descriptionTitle.Top.Set(8, 0f);
            descriptionTitle.TextColor = Color.Gray;
            descriptionPanel.Append(descriptionTitle);

            // Description text (will be updated on hover)
            descriptionText = new UIText("", 0.75f, false);
            descriptionText.Left.Set(10, 0f);
            descriptionText.Top.Set(32, 0f);
            descriptionText.TextColor = Color.White;
            descriptionPanel.Append(descriptionText);

            // Clear trait button
            UITextPanel<string> clearButton = new UITextPanel<string>("Remove Soul Trait", 0.85f, false);
            clearButton.Width.Set(180, 0f);
            clearButton.Height.Set(32, 0f);
            clearButton.HAlign = 0.5f;
            clearButton.Top.Set(390, 0f);
            clearButton.BackgroundColor = new Color(80, 40, 40);
            clearButton.OnLeftClick += ClearTrait;
            clearButton.OnMouseOver += (evt, elem) => ((UITextPanel<string>)elem).BackgroundColor = new Color(120, 60, 60);
            clearButton.OnMouseOut += (evt, elem) => ((UITextPanel<string>)elem).BackgroundColor = new Color(80, 40, 40);
            mainPanel.Append(clearButton);

            // Close button
            UITextPanel<string> closeButton = new UITextPanel<string>("X", 0.9f, false);
            closeButton.Width.Set(30, 0f);
            closeButton.Height.Set(30, 0f);
            closeButton.Left.Set(-45, 1f);
            closeButton.Top.Set(10, 0f);
            closeButton.BackgroundColor = new Color(100, 40, 40);
            closeButton.OnLeftClick += CloseUI;
            closeButton.OnMouseOver += (evt, elem) => ((UITextPanel<string>)elem).BackgroundColor = new Color(150, 60, 60);
            closeButton.OnMouseOut += (evt, elem) => ((UITextPanel<string>)elem).BackgroundColor = new Color(100, 40, 40);
            mainPanel.Append(closeButton);
        }

        public void SetHoveredTrait(SoulTraitType trait)
        {
            hoveredTrait = trait;
            UpdateDescriptionPanel();
        }

        public void ClearHoveredTrait()
        {
            hoveredTrait = SoulTraitType.None;
            UpdateDescriptionPanel();
        }

        private void UpdateDescriptionPanel()
        {
            if (hoveredTrait == SoulTraitType.None)
            {
                descriptionTitle.SetText("Hover over a trait to see details");
                descriptionTitle.TextColor = Color.Gray;
                descriptionText.SetText("");
            }
            else
            {
                Color traitColor = SoulTraitData.GetTraitColor(hoveredTrait);
                string traitName = SoulTraitData.GetTraitName(hoveredTrait);
                descriptionTitle.SetText(traitName);
                descriptionTitle.TextColor = traitColor;

                string[] bonuses = SoulTraitData.GetTraitBonusDescriptions(hoveredTrait);
                int[] thresholds = SoulTraitData.GetInvestmentThresholds();

                StringBuilder fullDescription = new StringBuilder();
                float maxWidth = 740f;
                for (int i = 0; i < bonuses.Length && i < thresholds.Length; i++)
                {
                    string line = $"[{thresholds[i]} pts] {bonuses[i]}";
                    string wrapped = TextWrapUtility.WrapText(line, maxWidth, 0.75f);
                    fullDescription.AppendLine(wrapped);
                }
                descriptionText.SetText(fullDescription.ToString());
                descriptionText.TextColor = traitColor * 0.9f;
            }
        }

        private void ClearTrait(UIMouseEvent evt, UIElement listeningElement)
        {
            Player player = Main.LocalPlayer;
            SoulTraitPlayer traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            if (!traitPlayer.TraitLocked)
            {
                traitPlayer.ClearTrait();
                SoundEngine.PlaySound(SoundID.MenuClose);
                ModContent.GetInstance<GersonTraitUISystem>().CloseTraitSelection();
            }
        }

        private void CloseUI(UIMouseEvent evt, UIElement listeningElement)
        {
            SoundEngine.PlaySound(SoundID.MenuClose);
            ModContent.GetInstance<GersonTraitUISystem>().CloseTraitSelection();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Keep the mouse interface active when panel is visible
            if (mainPanel.ContainsPoint(Main.MouseScreen))
            {
                Main.LocalPlayer.mouseInterface = true;
            }
        }
    }

    public class TraitButton : UIPanel
    {
        private SoulTraitType trait;
        private UIText nameText;
        private Color traitColor;
        private GersonTraitUIState parentUI;

        public TraitButton(SoulTraitType traitType, GersonTraitUIState parent)
        {
            trait = traitType;
            traitColor = SoulTraitData.GetTraitColor(trait);
            parentUI = parent;
            BackgroundColor = new Color(traitColor.R / 4, traitColor.G / 4, traitColor.B / 4, 200);
        }

        public override void OnInitialize()
        {
            nameText = new UIText(SoulTraitData.GetTraitName(trait), 0.85f, false);
            nameText.Left.Set(28, 0f);
            nameText.VAlign = 0.5f;
            nameText.TextColor = traitColor;
            Append(nameText);
        }

        public override void MouseOver(UIMouseEvent evt)
        {
            base.MouseOver(evt);
            BackgroundColor = new Color(traitColor.R / 2, traitColor.G / 2, traitColor.B / 2, 220);
            SoundEngine.PlaySound(SoundID.MenuTick);
            parentUI.SetHoveredTrait(trait);
        }

        public override void MouseOut(UIMouseEvent evt)
        {
            base.MouseOut(evt);
            BackgroundColor = new Color(traitColor.R / 4, traitColor.G / 4, traitColor.B / 4, 200);
            parentUI.ClearHoveredTrait();
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);

            Player player = Main.LocalPlayer;
            SoulTraitPlayer traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            if (!traitPlayer.TraitLocked)
            {
                traitPlayer.SetTrait(trait);
                SoundEngine.PlaySound(SoundID.Item4);
                ModContent.GetInstance<GersonTraitUISystem>().CloseTraitSelection();
            }
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            // Draw trait icon on the left
            CalculatedStyle dimensions = GetDimensions();
            Vector2 iconPos = new Vector2(dimensions.X + 10, dimensions.Y + dimensions.Height / 2);

            string texturePath = "DeterministicChaos/Content/SoulTraits/" + trait.ToString();
            Texture2D soulTexture = ModContent.Request<Texture2D>(texturePath, AssetRequestMode.ImmediateLoad).Value;
            Vector2 origin = new Vector2(soulTexture.Width / 2f, soulTexture.Height / 2f);
            float scale = 0.5f;

            spriteBatch.Draw(soulTexture, iconPos, null, traitColor, 0f, origin, scale, SpriteEffects.None, 0f);
        }
    }
}
