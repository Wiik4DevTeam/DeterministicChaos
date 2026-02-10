using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace DeterministicChaos.Content.Items
{
    public class TornNotebookUISystem : ModSystem
    {
        internal TornNotebookUIState NotebookUI;
        private UserInterface notebookInterface;
        private bool isOpen = false;

        public bool IsUIOpen => isOpen;

        public override void Load()
        {
            if (!Main.dedServ)
            {
                NotebookUI = new TornNotebookUIState();
                NotebookUI.Activate();
                notebookInterface = new UserInterface();
            }
        }

        public override void Unload()
        {
            NotebookUI = null;
            notebookInterface = null;
        }

        public void ToggleUI()
        {
            if (isOpen)
                CloseUI();
            else
                OpenUI();
        }

        public void OpenUI()
        {
            if (isOpen) return;

            isOpen = true;
            NotebookUI.OnOpen();
            notebookInterface.SetState(NotebookUI);
            Main.playerInventory = false;

            SoundEngine.PlaySound(SoundID.MenuOpen);
        }

        public void CloseUI()
        {
            if (!isOpen) return;

            isOpen = false;
            NotebookUI.OnClose();
            notebookInterface.SetState(null);

            SoundEngine.PlaySound(SoundID.MenuClose);
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (isOpen)
            {
                notebookInterface?.Update(gameTime);

                // Close if player presses escape
                if (Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape))
                {
                    CloseUI();
                }
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryIndex != -1)
            {
                layers.Insert(inventoryIndex + 1, new LegacyGameInterfaceLayer(
                    "DeterministicChaos: Torn Notebook",
                    delegate
                    {
                        if (isOpen)
                        {
                            notebookInterface.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI)
                );
            }
        }
    }

    public class TornNotebookUIState : UIState
    {
        private UIPanel mainPanel;
        private UIText titleText;
        private UIPanel inputPanel;
        private UIText inputText; // Dynamic text display
        private UITextPanel<string> closeButton;
        private UITextPanel<string> wordsButton;
        private UIPanel wordListPanel;
        private UIList wordList;
        private UIScrollbar wordListScrollbar;
        private bool showingWordList = false;

        // Text input handling
        private string currentText = "";
        private int cursorBlinkTimer = 0;
        private bool cursorVisible = true;
        
        // Track our own keyboard state for reliable input
        private KeyboardState previousKeyState;
        
        // Backspace repeat handling
        private int backspaceHoldTimer = 0;
        private const int BACKSPACE_REPEAT_DELAY = 30; // 0.5 seconds at 60fps
        private const int BACKSPACE_REPEAT_RATE = 3; // Delete every 3 ticks when repeating

        // Word effects list
        public static readonly Dictionary<string, string> WordEffects = new Dictionary<string, string>()
        {
            { "SEEK", "This + following letters seek enemies" },
            { "FAST", "This + following letters move faster" },
            { "BIG", "This + following letters are bigger" },
            { "BOOM", "These letters explode on hit" },
            { "CRIT", "These letters always crit" },
            { "PIERCE", "This + following pierce (loses seeking)" },
            { "FIRE", "Letters inflict On Fire!" },
            { "FROSTBURN", "Letters inflict Frostburn" },
            { "POISON", "Letters inflict Poisoned" },
            { "VENOM", "Letters inflict Venom (post-Plantera)" },
            { "ICHOR", "Letters inflict Ichor (Hardmode)" },
            { "CURSED", "Letters inflict Cursed Inferno (Hardmode)" },
            { "SHADOWFLAME", "Letters inflict Shadowflame (Hardmode)" },
            { "DAYBREAK", "Letters inflict Daybroken (post-Golem)" },
            { "BETSY", "Letters inflict Betsy Curse (post-Betsy)" },
        };

        public override void OnInitialize()
        {
            // Main panel (taller to accommodate word list)
            mainPanel = new UIPanel();
            mainPanel.Width.Set(650, 0f);
            mainPanel.Height.Set(450, 0f);
            mainPanel.HAlign = 0.5f;
            mainPanel.VAlign = 0.5f;
            mainPanel.BackgroundColor = new Color(30, 15, 40, 240);
            mainPanel.BorderColor = new Color(100, 50, 150);
            Append(mainPanel);

            // Title
            titleText = new UIText("Torn Notebook", 1.1f, true);
            titleText.HAlign = 0.5f;
            titleText.Top.Set(15, 0f);
            titleText.TextColor = new Color(200, 100, 255);
            mainPanel.Append(titleText);

            // Input panel (acts as text field background)
            inputPanel = new UIPanel();
            inputPanel.Width.Set(580, 0f);
            inputPanel.Height.Set(100, 0f);
            inputPanel.HAlign = 0.5f;
            inputPanel.Top.Set(60, 0f);
            inputPanel.BackgroundColor = new Color(20, 10, 30, 200);
            inputPanel.BorderColor = new Color(80, 40, 120);
            mainPanel.Append(inputPanel);

            // Input text display inside the input panel
            inputText = new UIText("Type your word here...", 1f, false);
            inputText.Left.Set(10, 0f);
            inputText.Top.Set(10, 0f);
            inputText.TextColor = Color.Gray * 0.7f;
            inputPanel.Append(inputText);

            // Close button
            closeButton = new UITextPanel<string>("Close & Save", 0.9f, false);
            closeButton.Width.Set(150, 0f);
            closeButton.Height.Set(40, 0f);
            closeButton.Left.Set(35, 0f);
            closeButton.Top.Set(180, 0f);
            closeButton.BackgroundColor = new Color(60, 30, 80);
            closeButton.OnLeftClick += OnCloseClick;
            closeButton.OnMouseOver += (evt, elem) => ((UITextPanel<string>)elem).BackgroundColor = new Color(100, 50, 130);
            closeButton.OnMouseOut += (evt, elem) => ((UITextPanel<string>)elem).BackgroundColor = new Color(60, 30, 80);
            mainPanel.Append(closeButton);

            // Words button
            wordsButton = new UITextPanel<string>("Word List", 0.9f, false);
            wordsButton.Width.Set(150, 0f);
            wordsButton.Height.Set(40, 0f);
            wordsButton.Left.Set(-185, 1f);
            wordsButton.Top.Set(180, 0f);
            wordsButton.BackgroundColor = new Color(60, 30, 80);
            wordsButton.OnLeftClick += OnWordsClick;
            wordsButton.OnMouseOver += (evt, elem) => ((UITextPanel<string>)elem).BackgroundColor = new Color(100, 50, 130);
            wordsButton.OnMouseOut += (evt, elem) => ((UITextPanel<string>)elem).BackgroundColor = new Color(60, 30, 80);
            mainPanel.Append(wordsButton);

            // Word list panel (hidden by default) - scrollable
            wordListPanel = new UIPanel();
            wordListPanel.Width.Set(580, 0f);
            wordListPanel.Height.Set(180, 0f);
            wordListPanel.HAlign = 0.5f;
            wordListPanel.Top.Set(230, 0f);
            wordListPanel.BackgroundColor = new Color(25, 12, 35, 230);
            wordListPanel.BorderColor = new Color(80, 40, 120);

            // Title text for word list
            UIText wordListTitle = new UIText("Special Words:", 0.9f, false);
            wordListTitle.Left.Set(10, 0f);
            wordListTitle.Top.Set(5, 0f);
            wordListTitle.TextColor = new Color(255, 200, 100);
            wordListPanel.Append(wordListTitle);

            // Scrollable list
            wordList = new UIList();
            wordList.Width.Set(-25, 1f); // Leave room for scrollbar
            wordList.Height.Set(-35, 1f); // Leave room for title
            wordList.Left.Set(5, 0f);
            wordList.Top.Set(28, 0f);
            wordList.ListPadding = 2f;
            wordListPanel.Append(wordList);

            // Scrollbar
            wordListScrollbar = new UIScrollbar();
            wordListScrollbar.SetView(100f, 1000f);
            wordListScrollbar.Height.Set(-35, 1f);
            wordListScrollbar.Top.Set(28, 0f);
            wordListScrollbar.Left.Set(-20, 1f);
            wordListPanel.Append(wordListScrollbar);
            wordList.SetScrollbar(wordListScrollbar);

            // Populate the word list
            PopulateWordList();
            // Don't append yet - only shown when Words button clicked
        }

        private void PopulateWordList()
        {
            wordList.Clear();
            foreach (var pair in WordEffects)
            {
                // Only show words that are available based on progression
                if (IsWordAvailable(pair.Key))
                {
                    WordEffectEntry entry = new WordEffectEntry(pair.Key, pair.Value);
                    wordList.Add(entry);
                }
            }
        }

        private bool IsWordAvailable(string word)
        {
            switch (word.ToUpper())
            {
                // Always available
                case "SEEK":
                case "FAST":
                case "BIG":
                case "BOOM":
                case "CRIT":
                case "PIERCE":
                case "FIRE":
                case "FROSTBURN":
                case "POISON":
                    return true;

                // Hardmode required
                case "ICHOR":
                case "CURSED":
                case "SHADOWFLAME":
                    return Main.hardMode;

                // Post-Plantera
                case "VENOM":
                    return NPC.downedPlantBoss;

                // Post-Golem
                case "DAYBREAK":
                    return NPC.downedGolemBoss;

                // Post-Betsy
                case "BETSY":
                    return Terraria.GameContent.Events.DD2Event.DownedInvasionT3;

                default:
                    return true;
            }
        }

        public void OnOpen()
        {
            // Load current text from player
            var player = Main.LocalPlayer;
            var notebookPlayer = player.GetModPlayer<TornNotebookPlayer>();
            currentText = notebookPlayer.StoredText ?? "";
            showingWordList = false;
            
            // Initialize keyboard state to current to avoid ghost inputs
            previousKeyState = Keyboard.GetState();

            // Refresh word list based on current progression
            PopulateWordList();

            // Update the text display immediately
            UpdateInputTextDisplay();

            // Remove word list panel if it was added
            if (wordListPanel.Parent != null)
            {
                wordListPanel.Remove();
            }
        }

        public void OnClose()
        {
            // Save text to player
            var player = Main.LocalPlayer;
            var notebookPlayer = player.GetModPlayer<TornNotebookPlayer>();
            notebookPlayer.SetStoredText(currentText);
        }

        private void OnCloseClick(UIMouseEvent evt, UIElement listeningElement)
        {
            ModContent.GetInstance<TornNotebookUISystem>().CloseUI();
        }

        private void OnWordsClick(UIMouseEvent evt, UIElement listeningElement)
        {
            showingWordList = !showingWordList;

            if (showingWordList)
            {
                mainPanel.Append(wordListPanel);
            }
            else
            {
                wordListPanel.Remove();
            }

            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Block game controls while we're handling text input
            Main.LocalPlayer.mouseInterface = true;
            
            // Handle text input
            HandleTextInput();

            // Cursor blink
            cursorBlinkTimer++;
            if (cursorBlinkTimer >= 30)
            {
                cursorBlinkTimer = 0;
                cursorVisible = !cursorVisible;
            }

            // Update the displayed text
            UpdateInputTextDisplay();
        }

        private void UpdateInputTextDisplay()
        {
            if (inputText == null)
                return;

            if (string.IsNullOrEmpty(currentText))
            {
                // Show placeholder with cursor
                string display = cursorVisible ? "|Type your word here..." : "Type your word here...";
                inputText.SetText(display);
                inputText.TextColor = Color.Gray * 0.7f;
            }
            else
            {
                // Show current text with cursor
                string display = currentText + (cursorVisible ? "|" : "");
                inputText.SetText(display);
                inputText.TextColor = new Color(220, 180, 255);
            }
        }

        private void HandleTextInput()
        {
            // Get fresh keyboard state directly
            KeyboardState currentKeyState = Keyboard.GetState();
            Keys[] pressedKeys = currentKeyState.GetPressedKeys();
            
            // Handle backspace hold-to-repeat
            if (currentKeyState.IsKeyDown(Keys.Back))
            {
                backspaceHoldTimer++;
                
                // First press - delete immediately
                if (!previousKeyState.IsKeyDown(Keys.Back) && currentText.Length > 0)
                {
                    currentText = currentText.Substring(0, currentText.Length - 1);
                    cursorVisible = true;
                    cursorBlinkTimer = 0;
                }
                // After delay, repeat at rate
                else if (backspaceHoldTimer >= BACKSPACE_REPEAT_DELAY && currentText.Length > 0)
                {
                    if ((backspaceHoldTimer - BACKSPACE_REPEAT_DELAY) % BACKSPACE_REPEAT_RATE == 0)
                    {
                        currentText = currentText.Substring(0, currentText.Length - 1);
                        cursorVisible = true;
                        cursorBlinkTimer = 0;
                    }
                }
            }
            else
            {
                backspaceHoldTimer = 0;
            }
            
            foreach (Keys key in pressedKeys)
            {
                // Skip if key was already pressed last frame
                if (previousKeyState.IsKeyDown(key))
                    continue;
                
                // Backspace is handled above
                if (key == Keys.Back)
                    continue;
                
                // Handle space
                if (key == Keys.Space && currentText.Length < 50)
                {
                    currentText += " ";
                    cursorVisible = true;
                    cursorBlinkTimer = 0;
                    continue;
                }
                
                // Handle letters A-Z
                if (key >= Keys.A && key <= Keys.Z && currentText.Length < 50)
                {
                    bool shift = currentKeyState.IsKeyDown(Keys.LeftShift) || currentKeyState.IsKeyDown(Keys.RightShift);
                    char c = (char)('a' + (key - Keys.A));
                    if (shift)
                        c = char.ToUpper(c);
                    currentText += c;
                    cursorVisible = true;
                    cursorBlinkTimer = 0;
                    continue;
                }
            }
            
            // Save this frame's state for next frame comparison
            previousKeyState = currentKeyState;
            
            // Suppress game actions from these keys
            Main.chatRelease = false;
            PlayerInput.WritingText = true;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            // Draw active words indicator at bottom of input panel
            CalculatedStyle inputDim = inputPanel.GetDimensions();
            DrawMatchingWords(spriteBatch, inputDim);
        }

        private void DrawMatchingWords(SpriteBatch spriteBatch, CalculatedStyle inputDim)
        {
            // Check if current text contains any special words
            string upperText = currentText.ToUpper();
            List<string> foundWords = new List<string>();

            foreach (var pair in WordEffects)
            {
                if (upperText.Contains(pair.Key))
                {
                    foundWords.Add(pair.Key);
                }
            }

            if (foundWords.Count > 0)
            {
                Vector2 pos = new Vector2(inputDim.X + 15, inputDim.Y + inputDim.Height - 22);
                string text = "Active: " + string.Join(", ", foundWords);
                spriteBatch.DrawString(FontAssets.MouseText.Value, text, pos, new Color(100, 255, 100));
            }
        }
    }

    public class WordEffectEntry : UIPanel
    {
        private string wordName;
        private string wordEffect;

        public WordEffectEntry(string name, string effect)
        {
            wordName = name;
            wordEffect = effect;

            Width.Set(0, 1f);
            Height.Set(24, 0f);
            BackgroundColor = new Color(40, 20, 55, 180);
            BorderColor = Color.Transparent;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            CalculatedStyle dims = GetDimensions();
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            // Draw word name in purple
            Vector2 namePos = new Vector2(dims.X + 8, dims.Y + 3);
            spriteBatch.DrawString(font, wordName + ": ", namePos, new Color(200, 100, 255));

            // Draw effect description in white
            float nameWidth = font.MeasureString(wordName + ": ").X;
            Vector2 effectPos = new Vector2(dims.X + 8 + nameWidth, dims.Y + 3);
            spriteBatch.DrawString(font, wordEffect, effectPos, Color.White * 0.85f);
        }

        public override void MouseOver(UIMouseEvent evt)
        {
            base.MouseOver(evt);
            BackgroundColor = new Color(60, 30, 80, 200);
        }

        public override void MouseOut(UIMouseEvent evt)
        {
            base.MouseOut(evt);
            BackgroundColor = new Color(40, 20, 55, 180);
        }
    }
}
