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
    public class TheAbstractUISystem : ModSystem
    {
        internal TheAbstractUIState AbstractUI;
        private UserInterface abstractInterface;
        private bool isOpen = false;

        public bool IsUIOpen => isOpen;

        public override void Load()
        {
            if (!Main.dedServ)
            {
                AbstractUI = new TheAbstractUIState();
                AbstractUI.Activate();
                abstractInterface = new UserInterface();
            }
        }

        public override void Unload()
        {
            AbstractUI = null;
            abstractInterface = null;
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
            AbstractUI.OnOpen();
            abstractInterface.SetState(AbstractUI);
            Main.playerInventory = false;

            SoundEngine.PlaySound(SoundID.MenuOpen);
        }

        public void CloseUI()
        {
            if (!isOpen) return;

            isOpen = false;
            AbstractUI.OnClose();
            abstractInterface.SetState(null);

            SoundEngine.PlaySound(SoundID.MenuClose);
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (isOpen)
            {
                abstractInterface?.Update(gameTime);

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
                    "DeterministicChaos: The Abstract",
                    delegate
                    {
                        if (isOpen)
                        {
                            abstractInterface.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI)
                );
            }
        }
    }

    public class TheAbstractUIState : UIState
    {
        private UIPanel mainPanel;
        private UIText titleText;
        private UIPanel inputPanel;
        private UIText inputText;
        private UITextPanel<string> closeButton;
        private UITextPanel<string> wordsButton;
        private UIPanel wordListPanel;
        private UIList wordList;
        private UIScrollbar wordListScrollbar;
        private bool showingWordList = false;

        private string currentText = "";
        private int cursorBlinkTimer = 0;
        private bool cursorVisible = true;

        private KeyboardState previousKeyState;

        private int backspaceHoldTimer = 0;
        private const int BACKSPACE_REPEAT_DELAY = 30;
        private const int BACKSPACE_REPEAT_RATE = 3;

        public static readonly Dictionary<string, string> WordEffects = new Dictionary<string, string>()
        {
            { "SEEK", "This + following letters seek enemies (allies if HEAL)" },
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
            { "SPLIT", "This + following split into 2 on hit (1/4 dmg)" },
            { "PARTITION", "This + following fire 3 letters (1/2 dmg each)" },
            { "BURST", "Each word fires all at once (double word delay)" },
            { "BURROW", "This + following pass through all tiles" },
            // The Abstract words
            { "HEAL", "This + following heal allies for 2 HP (SEEK targets allies)" },
            { "AURA", "This + following spawn in an aura orbiting the player" },
            { "RAIN", "This + following x4 letters rain from sky (1/2 dmg each)" },
        };

        public override void OnInitialize()
        {
            mainPanel = new UIPanel();
            mainPanel.Width.Set(650, 0f);
            mainPanel.Height.Set(450, 0f);
            mainPanel.HAlign = 0.5f;
            mainPanel.VAlign = 0.5f;
            mainPanel.BackgroundColor = new Color(15, 10, 40, 240);
            mainPanel.BorderColor = new Color(80, 50, 180);
            Append(mainPanel);

            titleText = new UIText("The Abstract", 1.1f, true);
            titleText.HAlign = 0.5f;
            titleText.Top.Set(15, 0f);
            titleText.TextColor = new Color(140, 80, 255);
            mainPanel.Append(titleText);

            inputPanel = new UIPanel();
            inputPanel.Width.Set(580, 0f);
            inputPanel.Height.Set(100, 0f);
            inputPanel.HAlign = 0.5f;
            inputPanel.Top.Set(60, 0f);
            inputPanel.BackgroundColor = new Color(10, 5, 30, 200);
            inputPanel.BorderColor = new Color(60, 30, 140);
            mainPanel.Append(inputPanel);

            inputText = new UIText("Type your word here...", 1f, false);
            inputText.Left.Set(10, 0f);
            inputText.Top.Set(10, 0f);
            inputText.TextColor = Color.Gray * 0.7f;
            inputPanel.Append(inputText);

            closeButton = new UITextPanel<string>("Close & Save", 0.9f, false);
            closeButton.Width.Set(150, 0f);
            closeButton.Height.Set(40, 0f);
            closeButton.Left.Set(35, 0f);
            closeButton.Top.Set(180, 0f);
            closeButton.BackgroundColor = new Color(40, 20, 80);
            closeButton.OnLeftClick += OnCloseClick;
            closeButton.OnMouseOver += (evt, elem) => ((UITextPanel<string>)elem).BackgroundColor = new Color(70, 40, 140);
            closeButton.OnMouseOut += (evt, elem) => ((UITextPanel<string>)elem).BackgroundColor = new Color(40, 20, 80);
            mainPanel.Append(closeButton);

            wordsButton = new UITextPanel<string>("Word List", 0.9f, false);
            wordsButton.Width.Set(150, 0f);
            wordsButton.Height.Set(40, 0f);
            wordsButton.Left.Set(-185, 1f);
            wordsButton.Top.Set(180, 0f);
            wordsButton.BackgroundColor = new Color(40, 20, 80);
            wordsButton.OnLeftClick += OnWordsClick;
            wordsButton.OnMouseOver += (evt, elem) => ((UITextPanel<string>)elem).BackgroundColor = new Color(70, 40, 140);
            wordsButton.OnMouseOut += (evt, elem) => ((UITextPanel<string>)elem).BackgroundColor = new Color(40, 20, 80);
            mainPanel.Append(wordsButton);

            wordListPanel = new UIPanel();
            wordListPanel.Width.Set(580, 0f);
            wordListPanel.Height.Set(180, 0f);
            wordListPanel.HAlign = 0.5f;
            wordListPanel.Top.Set(230, 0f);
            wordListPanel.BackgroundColor = new Color(12, 8, 35, 230);
            wordListPanel.BorderColor = new Color(60, 30, 140);

            UIText wordListTitle = new UIText("Special Words:", 0.9f, false);
            wordListTitle.Left.Set(10, 0f);
            wordListTitle.Top.Set(5, 0f);
            wordListTitle.TextColor = new Color(255, 200, 100);
            wordListPanel.Append(wordListTitle);

            wordList = new UIList();
            wordList.Width.Set(-25, 1f);
            wordList.Height.Set(-35, 1f);
            wordList.Left.Set(5, 0f);
            wordList.Top.Set(28, 0f);
            wordList.ListPadding = 2f;
            wordListPanel.Append(wordList);

            wordListScrollbar = new UIScrollbar();
            wordListScrollbar.SetView(100f, 1000f);
            wordListScrollbar.Height.Set(-35, 1f);
            wordListScrollbar.Top.Set(28, 0f);
            wordListScrollbar.Left.Set(-20, 1f);
            wordListPanel.Append(wordListScrollbar);
            wordList.SetScrollbar(wordListScrollbar);

            PopulateWordList();
        }

        private void PopulateWordList()
        {
            wordList.Clear();
            foreach (var pair in WordEffects)
            {
                if (IsWordAvailable(pair.Key))
                {
                    AbstractWordEffectEntry entry = new AbstractWordEffectEntry(pair.Key, pair.Value);
                    wordList.Add(entry);
                }
            }
        }

        private bool IsWordAvailable(string word)
        {
            switch (word.ToUpper())
            {
                case "SEEK":
                case "FAST":
                case "BIG":
                case "BOOM":
                case "CRIT":
                case "PIERCE":
                case "FIRE":
                case "FROSTBURN":
                case "POISON":
                case "SPLIT":
                case "PARTITION":
                case "BURST":
                case "BURROW":
                case "HEAL":
                case "AURA":
                case "RAIN":
                    return true;
                case "ICHOR":
                case "CURSED":
                case "SHADOWFLAME":
                    return Main.hardMode;
                case "VENOM":
                    return NPC.downedPlantBoss;
                case "DAYBREAK":
                    return NPC.downedGolemBoss;
                case "BETSY":
                    return Terraria.GameContent.Events.DD2Event.DownedInvasionT3;
                default:
                    return true;
            }
        }

        public void OnOpen()
        {
            var player = Main.LocalPlayer;
            var abstractPlayer = player.GetModPlayer<TheAbstractPlayer>();
            currentText = abstractPlayer.StoredText ?? "";
            showingWordList = false;
            previousKeyState = Keyboard.GetState();
            PopulateWordList();
            UpdateInputTextDisplay();

            if (wordListPanel.Parent != null)
                wordListPanel.Remove();
        }

        public void OnClose()
        {
            var player = Main.LocalPlayer;
            var abstractPlayer = player.GetModPlayer<TheAbstractPlayer>();
            abstractPlayer.SetStoredText(currentText);
        }

        private void OnCloseClick(UIMouseEvent evt, UIElement listeningElement)
        {
            ModContent.GetInstance<TheAbstractUISystem>().CloseUI();
        }

        private void OnWordsClick(UIMouseEvent evt, UIElement listeningElement)
        {
            showingWordList = !showingWordList;

            if (showingWordList)
                mainPanel.Append(wordListPanel);
            else
                wordListPanel.Remove();

            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            Main.LocalPlayer.mouseInterface = true;
            HandleTextInput();

            cursorBlinkTimer++;
            if (cursorBlinkTimer >= 30)
            {
                cursorBlinkTimer = 0;
                cursorVisible = !cursorVisible;
            }

            UpdateInputTextDisplay();
        }

        private void UpdateInputTextDisplay()
        {
            if (inputText == null)
                return;

            if (string.IsNullOrEmpty(currentText))
            {
                string display = cursorVisible ? "|Type your word here..." : "Type your word here...";
                inputText.SetText(display);
                inputText.TextColor = Color.Gray * 0.7f;
            }
            else
            {
                string display = currentText + (cursorVisible ? "|" : "");
                // Wrap to next line after 50 characters
                if (display.Length > 50)
                    display = display.Substring(0, 50) + "\n" + display.Substring(50);
                inputText.SetText(display);
                inputText.TextColor = new Color(180, 160, 255);
            }
        }

        private void HandleTextInput()
        {
            KeyboardState currentKeyState = Keyboard.GetState();
            Keys[] pressedKeys = currentKeyState.GetPressedKeys();

            if (currentKeyState.IsKeyDown(Keys.Back))
            {
                backspaceHoldTimer++;
                if (!previousKeyState.IsKeyDown(Keys.Back) && currentText.Length > 0)
                {
                    currentText = currentText.Substring(0, currentText.Length - 1);
                    cursorVisible = true;
                    cursorBlinkTimer = 0;
                }
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
                if (previousKeyState.IsKeyDown(key))
                    continue;
                if (key == Keys.Back)
                    continue;
                if (key == Keys.Space && currentText.Length < 70)
                {
                    currentText += " ";
                    cursorVisible = true;
                    cursorBlinkTimer = 0;
                    continue;
                }
                if (key >= Keys.A && key <= Keys.Z && currentText.Length < 70)
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

            previousKeyState = currentKeyState;
            Main.chatRelease = false;
            PlayerInput.WritingText = true;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);
            CalculatedStyle inputDim = inputPanel.GetDimensions();
            DrawMatchingWords(spriteBatch, inputDim);
        }

        private void DrawMatchingWords(SpriteBatch spriteBatch, CalculatedStyle inputDim)
        {
            string upperText = currentText.ToUpper();
            List<string> foundWords = new List<string>();

            foreach (var pair in WordEffects)
            {
                if (upperText.Contains(pair.Key))
                    foundWords.Add(pair.Key);
            }

            if (foundWords.Count > 0)
            {
                Vector2 pos = new Vector2(inputDim.X + 15, inputDim.Y + inputDim.Height - 22);
                string text = "Active: " + string.Join(", ", foundWords);
                spriteBatch.DrawString(FontAssets.MouseText.Value, text, pos, new Color(100, 255, 100));
            }
        }
    }

    public class AbstractWordEffectEntry : UIPanel
    {
        private string wordName;
        private string wordEffect;

        public AbstractWordEffectEntry(string name, string effect)
        {
            wordName = name;
            wordEffect = effect;
            Width.Set(0, 1f);
            Height.Set(24, 0f);
            BackgroundColor = new Color(25, 15, 55, 180);
            BorderColor = Color.Transparent;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);
            CalculatedStyle dims = GetDimensions();
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            Vector2 namePos = new Vector2(dims.X + 8, dims.Y + 3);
            spriteBatch.DrawString(font, wordName + ": ", namePos, new Color(140, 80, 255));

            float nameWidth = font.MeasureString(wordName + ": ").X;
            Vector2 effectPos = new Vector2(dims.X + 8 + nameWidth, dims.Y + 3);
            spriteBatch.DrawString(font, wordEffect, effectPos, Color.White * 0.85f);
        }

        public override void MouseOver(UIMouseEvent evt)
        {
            base.MouseOver(evt);
            BackgroundColor = new Color(45, 25, 80, 200);
        }

        public override void MouseOut(UIMouseEvent evt)
        {
            base.MouseOut(evt);
            BackgroundColor = new Color(25, 15, 55, 180);
        }
    }
}
