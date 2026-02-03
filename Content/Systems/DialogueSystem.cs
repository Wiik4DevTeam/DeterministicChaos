using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace DeterministicChaos.Content.Systems
{
    /// <summary>
    /// Represents a single dialogue entry with text and display options.
    /// </summary>
    public class DialogueEntry
    {
        public string Text { get; set; }
        public float LingerTime { get; set; } = 2f; // How long to display after text finishes (in seconds)
        public Color TextColor { get; set; } = Color.White;
        public string FontType { get; set; } = "VCRMono"; // "VCRMono" or "Default"
        
        public DialogueEntry(string text, float lingerTime = 2f, Color? textColor = null, string fontType = "VCRMono")
        {
            Text = text;
            LingerTime = lingerTime;
            TextColor = textColor ?? Color.White;
            FontType = fontType;
        }
    }
    
    /// <summary>
    /// Handles displaying dialogue boxes with animated text.
    /// </summary>
    public class DialogueSystem : ModSystem
    {
        // Singleton instance for easy access
        public static DialogueSystem Instance { get; private set; }
        
        // Dialogue queue
        private Queue<DialogueEntry> dialogueQueue = new Queue<DialogueEntry>();
        private DialogueEntry currentDialogue = null;
        
        // Text animation state (tick-based for consistent timing)
        private string displayedText = "";
        private int currentCharIndex = 0;
        private int charTickTimer = 0;
        private const int NormalCharDelayTicks = 3; // 3 ticks = 0.05 seconds at 60fps
        private const int PunctuationCharDelayTicks = 12; // 12 ticks = 0.2 seconds at 60fps
        
        // Box animation state
        private enum DialogueState
        {
            Closed,
            Opening,
            Typing,
            Lingering,
            Closing
        }
        private DialogueState state = DialogueState.Closed;
        
        private float boxHeightProgress = 0f; // 0 to 1
        private const int BoxOpenDurationTicks = 24; // 24 ticks = 0.4 seconds
        private const int BoxCloseDurationTicks = 24; // 24 ticks = 0.4 seconds
        private int boxAnimTickTimer = 0;
        
        // Delay between dialogues
        private int newDialogueDelayTicks = 0;
        private const int NewDialogueDelayDurationTicks = 36; // 36 ticks = 0.6 seconds
        
        // Linger timer (tick-based)
        private int lingerTickTimer = 0;
        
        // Textures
        private Asset<Texture2D> dialogueBoxTexture;
        
        // Sound effects, initialized in Load
        private static SoundStyle PixelTextSound;
        private static SoundStyle PixelNewSound;
        private static bool soundsInitialized = false;
        
        // Box dimensions
        private const int BoxWidth = 600;
        private const int BoxHeight = 120;
        private const int BoxPadding = 20;
        
        // Flag to check if dialogue is active
        public bool IsDialogueActive => state != DialogueState.Closed || dialogueQueue.Count > 0;
        
        public override void Load()
        {
            Instance = this;
            
            // Load textures
            dialogueBoxTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/Systems/DialogueBox");
            
            InitializeSounds();
        }
        
        private static void InitializeSounds()
        {
            if (soundsInitialized)
                return;
            
            // Initialize sound effects, allow multiple overlapping instances
            PixelTextSound = new SoundStyle("DeterministicChaos/Assets/Sounds/PixelText")
            {
                Volume = 0.7f,
                MaxInstances = 0, // 0 = unlimited instances
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            };
            PixelNewSound = new SoundStyle("DeterministicChaos/Assets/Sounds/PixelNewDialogue")
            {
                Volume = 0.8f,
                MaxInstances = 0,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            };
            
            soundsInitialized = true;
        }
        
        public override void Unload()
        {
            Instance = null;
        }
        
        /// <summary>
        /// Queues a dialogue to be displayed.
        /// </summary>
        public void QueueDialogue(DialogueEntry dialogue)
        {
            dialogueQueue.Enqueue(dialogue);
            
            // Start if not already running
            if (state == DialogueState.Closed && newDialogueDelayTicks <= 0)
            {
                StartNextDialogue();
            }
        }
        
        /// <summary>
        /// Queues a simple dialogue with default settings.
        /// </summary>
        public void QueueDialogue(string text, float lingerTime = 2f)
        {
            QueueDialogue(new DialogueEntry(text, lingerTime));
        }
        
        /// <summary>
        /// Queues multiple dialogues at once.
        /// </summary>
        public void QueueDialogues(params DialogueEntry[] dialogues)
        {
            foreach (var dialogue in dialogues)
            {
                dialogueQueue.Enqueue(dialogue);
            }
            
            // Start if not already running
            if (state == DialogueState.Closed && newDialogueDelayTicks <= 0)
            {
                StartNextDialogue();
            }
        }
        
        /// <summary>
        /// Clears all queued dialogues and closes any active dialogue.
        /// </summary>
        public void ClearAllDialogue()
        {
            dialogueQueue.Clear();
            currentDialogue = null;
            state = DialogueState.Closed;
            boxHeightProgress = 0f;
            displayedText = "";
            currentCharIndex = 0;
        }
        
        private void StartNextDialogue()
        {
            if (dialogueQueue.Count == 0)
            {
                state = DialogueState.Closed;
                currentDialogue = null;
                return;
            }
            
            currentDialogue = dialogueQueue.Dequeue();
            displayedText = "";
            currentCharIndex = 0;
            charTickTimer = 0;
            boxAnimTickTimer = 0;
            boxHeightProgress = 0f;
            state = DialogueState.Opening;
            
            // Ensure sounds are initialized and play new dialogue sound
            InitializeSounds();
            SoundEngine.PlaySound(PixelNewSound);
        }
        
        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;
            
            // Handle new dialogue delay (tick-based)
            if (newDialogueDelayTicks > 0)
            {
                newDialogueDelayTicks--;
                if (newDialogueDelayTicks <= 0 && state == DialogueState.Closed && dialogueQueue.Count > 0)
                {
                    StartNextDialogue();
                }
                return;
            }
            
            switch (state)
            {
                case DialogueState.Opening:
                    UpdateOpening();
                    break;
                case DialogueState.Typing:
                    UpdateTyping();
                    break;
                case DialogueState.Lingering:
                    UpdateLingering();
                    break;
                case DialogueState.Closing:
                    UpdateClosing();
                    break;
            }
        }
        
        private void UpdateOpening()
        {
            boxAnimTickTimer++;
            boxHeightProgress = MathHelper.Clamp(boxAnimTickTimer / (float)BoxOpenDurationTicks, 0f, 1f);
            
            // Apply easing for smooth animation
            boxHeightProgress = EaseOutQuad(boxHeightProgress);
            
            if (boxAnimTickTimer >= BoxOpenDurationTicks)
            {
                boxHeightProgress = 1f;
                state = DialogueState.Typing;
                boxAnimTickTimer = 0;
            }
        }
        
        private void UpdateTyping()
        {
            if (currentDialogue == null)
                return;
                
            charTickTimer++;
            
            // Determine delay based on last character (tick-based)
            int requiredDelayTicks = NormalCharDelayTicks;
            if (currentCharIndex > 0 && currentCharIndex <= currentDialogue.Text.Length)
            {
                char lastChar = currentDialogue.Text[currentCharIndex - 1];
                if (IsPunctuation(lastChar))
                {
                    requiredDelayTicks = PunctuationCharDelayTicks;
                }
            }
            
            if (charTickTimer >= requiredDelayTicks && currentCharIndex < currentDialogue.Text.Length)
            {
                charTickTimer = 0;
                currentCharIndex++;
                displayedText = currentDialogue.Text.Substring(0, currentCharIndex);
                
                // Play text sound for non-space characters
                char newChar = currentDialogue.Text[currentCharIndex - 1];
                if (!char.IsWhiteSpace(newChar))
                {
                    InitializeSounds();
                    SoundEngine.PlaySound(PixelTextSound);
                }
            }
            
            // Check if typing is complete
            if (currentCharIndex >= currentDialogue.Text.Length)
            {
                state = DialogueState.Lingering;
                lingerTickTimer = 0;
            }
        }
        
        private void UpdateLingering()
        {
            if (currentDialogue == null)
                return;
            
            lingerTickTimer++;
            
            // Convert linger time from seconds to ticks (60 ticks per second)
            int lingerDurationTicks = (int)(currentDialogue.LingerTime * 60f);
            
            if (lingerTickTimer >= lingerDurationTicks)
            {
                state = DialogueState.Closing;
                boxAnimTickTimer = 0;
            }
        }
        
        private void UpdateClosing()
        {
            boxAnimTickTimer++;
            boxHeightProgress = 1f - MathHelper.Clamp(boxAnimTickTimer / (float)BoxCloseDurationTicks, 0f, 1f);
            
            // Apply easing
            boxHeightProgress = EaseOutQuad(boxHeightProgress);
            
            if (boxAnimTickTimer >= BoxCloseDurationTicks)
            {
                boxHeightProgress = 0f;
                state = DialogueState.Closed;
                currentDialogue = null;
                
                // Check for more dialogues
                if (dialogueQueue.Count > 0)
                {
                    newDialogueDelayTicks = NewDialogueDelayDurationTicks;
                }
            }
        }
        
        private bool IsPunctuation(char c)
        {
            return c == '.' || c == ',' || c == '!' || c == '?' || c == ';' || c == ':' || c == '-';
        }
        
        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }
        
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            // Find the inventory layer and insert our dialogue above it
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1)
            {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "DeterministicChaos: Dialogue",
                    delegate
                    {
                        DrawDialogueBox(Main.spriteBatch);
                        return true;
                    },
                    InterfaceScaleType.UI));
            }
        }
        
        private void DrawDialogueBox(SpriteBatch spriteBatch)
        {
            if (state == DialogueState.Closed || currentDialogue == null || boxHeightProgress <= 0)
                return;
                
            Texture2D boxTex = dialogueBoxTexture.Value;
            
            // Calculate box position (centered horizontally, near bottom of screen)
            int screenWidth = Main.screenWidth;
            int screenHeight = Main.screenHeight;
            
            int boxX = (screenWidth - BoxWidth) / 2;
            int boxY = screenHeight - 180;
            
            // Calculate animated height
            int animatedHeight = (int)(BoxHeight * boxHeightProgress);
            int heightOffset = (BoxHeight - animatedHeight) / 2;
            
            // Draw the dialogue box background with height animation
            Rectangle destRect = new Rectangle(boxX, boxY + heightOffset, BoxWidth, animatedHeight);
            
            // Use 9-slice or stretch the texture
            spriteBatch.Draw(boxTex, destRect, Color.White);
            
            // Only draw text if box is mostly open
            if (boxHeightProgress > 0.5f && !string.IsNullOrEmpty(displayedText))
            {
                // Get the appropriate font
                DynamicSpriteFont font = GetFont();
                
                // Calculate text position
                Vector2 textPos = new Vector2(boxX + BoxPadding, boxY + heightOffset + BoxPadding);
                
                // Word wrap the text
                string wrappedText = WrapText(font, displayedText, BoxWidth - (BoxPadding * 2));
                
                // Draw text with slight transparency based on box progress
                float textAlpha = MathHelper.Clamp((boxHeightProgress - 0.5f) * 2f, 0f, 1f);
                Color textColor = currentDialogue.TextColor * textAlpha;
                
                spriteBatch.DrawString(font, wrappedText, textPos, textColor);
            }
        }
        
        private DynamicSpriteFont GetFont()
        {
            // Use Terraria's default font
            return FontAssets.MouseText.Value;
        }
        
        private string WrapText(DynamicSpriteFont font, string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text))
                return text;
                
            string[] words = text.Split(' ');
            string result = "";
            string currentLine = "";
            
            foreach (string word in words)
            {
                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                Vector2 size = font.MeasureString(testLine);
                
                if (size.X > maxWidth && !string.IsNullOrEmpty(currentLine))
                {
                    result += currentLine + "\n";
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }
            
            result += currentLine;
            return result;
        }
    }
}
