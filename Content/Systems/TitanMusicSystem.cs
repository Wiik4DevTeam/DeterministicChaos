using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using ReLogic.Utilities;
using DeterministicChaos.Content.NPCs.Bosses;

namespace DeterministicChaos.Content.Systems
{
    // Manages the Titan fight music outside of Terraria's built-in music system.
    // Plays the track via SoundEngine so it can't be interrupted by biome changes.
    // Singleplayer: music pauses when game pauses or window loses focus.
    // Multiplayer: autopause is force-disabled so the fight and music stay in sync.
    // Restarts the music on Damage→Parkour loop to correct any accumulated drift.
    // Final stand: switches to guardianfinal; fadeout on Titan death.
    public class TitanMusicSystem : ModSystem
    {
        private static readonly SoundStyle TitanMusic = new SoundStyle("DeterministicChaos/Assets/Music/guardian")
        {
            Volume = 0.6f,
            SoundLimitBehavior = SoundLimitBehavior.IgnoreNew,
        };

        private static readonly SoundStyle TitanFinalMusic = new SoundStyle("DeterministicChaos/Assets/Music/guardianfinal")
        {
            Volume = 0.6f,
            SoundLimitBehavior = SoundLimitBehavior.IgnoreNew,
        };

        private const float FADE_OUT_DURATION = 3f;

        private static SlotId musicSlot = SlotId.Invalid;
        private static bool savedAutoPause = false;
        private static bool autoPauseSaved = false;
        private static bool fightActive = false;
        private static bool musicPaused = false;
        private static int lastDetectedPhase = -1;
        private static bool inFinalStand = false;
        private static bool isFadingOut = false;
        private static float fadeOutTimer = 0f;

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            bool titanAlive = NPC.AnyNPCs(ModContent.NPCType<TitanBody>());

            if (titanAlive && !fightActive)
            {
                // Fight just started
                fightActive = true;
                inFinalStand = false;
                isFadingOut = false;
                fadeOutTimer = 0f;
                lastDetectedPhase = -1;
                SaveAndDisableAutoPause();
                StartMusic(TitanMusic);
            }
            else if (!titanAlive && fightActive)
            {
                // Fight just ended, begin fade-out instead of hard stop
                fightActive = false;
                inFinalStand = false;
                lastDetectedPhase = -1;
                RestoreAutoPause();

                if (!isFadingOut)
                {
                    isFadingOut = true;
                    fadeOutTimer = 0f;
                }
            }

            // Run fade-out even after fight ends
            if (isFadingOut)
            {
                fadeOutTimer += 1f / 60f;
                float progress = fadeOutTimer / FADE_OUT_DURATION;
                if (progress >= 1f)
                {
                    StopMusic();
                    isFadingOut = false;
                    fadeOutTimer = 0f;
                }
                else if (SoundEngine.TryGetActiveSound(musicSlot, out var fadingSound))
                {
                    float soundVol = Main.soundVolume > 0f ? Main.soundVolume : 0.001f;
                    fadingSound.Volume = (Main.musicVolume / soundVol) * (1f - progress);
                }
                return;
            }

            if (!fightActive)
                return;

            // In multiplayer, force-disable autopause so the fight timer stays in sync
            if (Main.netMode != NetmodeID.SinglePlayer)
                Main.autoPause = false;

            // Detect final stand transition and switch music
            bool finalStandNow = IsTitanInFinalStand();
            if (finalStandNow && !inFinalStand)
            {
                inFinalStand = true;
                StopMusic();
                StartMusic(TitanFinalMusic);
            }

            // Update sound volume to follow the music volume slider only
            if (SoundEngine.TryGetActiveSound(musicSlot, out var activeSound))
            {
                float soundVol = Main.soundVolume > 0f ? Main.soundVolume : 0.001f;
                activeSound.Volume = Main.musicVolume / soundVol;
            }
            else if (!inFinalStand)
            {
                // Normal music expired or stopped unexpectedly, restart (not during final stand)
                StartMusic(TitanMusic);
            }

            // Detect phase loop (Damage → Parkour) to restart music and correct drift
            // (only while not in final stand, final stand has its own music)
            if (!inFinalStand)
            {
                int currentPhase = GetTitanPhase();
                if (lastDetectedPhase == (int)TitanBody.FightPhase.Damage
                    && currentPhase == (int)TitanBody.FightPhase.Parkour)
                {
                    RestartMusic();
                }
                lastDetectedPhase = currentPhase;
            }
        }

        // Runs every frame even when the game is paused.
        // Handles pausing/resuming the music in singleplayer.
        public override void UpdateUI(GameTime gameTime)
        {
            if ((!fightActive && !isFadingOut) || Main.dedServ)
                return;

            // Only pause music in singleplayer
            if (Main.netMode != NetmodeID.SinglePlayer)
                return;

            bool shouldPause = Main.gamePaused || !Main.hasFocus;

            if (shouldPause && !musicPaused)
            {
                if (SoundEngine.TryGetActiveSound(musicSlot, out var sound))
                    sound.Sound?.Pause();
                musicPaused = true;
            }
            else if (!shouldPause && musicPaused)
            {
                if (SoundEngine.TryGetActiveSound(musicSlot, out var sound))
                    sound.Sound?.Resume();
                musicPaused = false;
            }
        }

        private static bool IsTitanInFinalStand()
        {
            int titanType = ModContent.NPCType<TitanBody>();
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == titanType && npc.ModNPC is TitanBody titan)
                    return titan.IsInFinalStand;
            }
            return false;
        }

        private static int GetTitanPhase()
        {
            int titanType = ModContent.NPCType<TitanBody>();
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == titanType)
                    return (int)Main.npc[i].ai[0];
            }
            return -1;
        }

        private static void StartMusic(SoundStyle style)
        {
            // Stop any existing instance first
            if (SoundEngine.TryGetActiveSound(musicSlot, out var existing))
                existing.Stop();

            musicSlot = SoundEngine.PlaySound(style);

            // Apply current music volume immediately (counteract sound volume)
            if (SoundEngine.TryGetActiveSound(musicSlot, out var newSound))
            {
                float soundVol = Main.soundVolume > 0f ? Main.soundVolume : 0.001f;
                newSound.Volume = Main.musicVolume / soundVol;
            }
        }

        private static void RestartMusic()
        {
            StopMusic();
            StartMusic(TitanMusic);
        }

        private static void StopMusic()
        {
            if (SoundEngine.TryGetActiveSound(musicSlot, out var active))
                active.Stop();
            musicSlot = SlotId.Invalid;
        }

        private static void SaveAndDisableAutoPause()
        {
            if (!autoPauseSaved)
            {
                savedAutoPause = Main.autoPause;
                autoPauseSaved = true;
            }
            // In multiplayer, disable autopause immediately; singleplayer leaves it alone
            if (Main.netMode != NetmodeID.SinglePlayer)
                Main.autoPause = false;
        }

        private static void RestoreAutoPause()
        {
            if (autoPauseSaved)
            {
                Main.autoPause = savedAutoPause;
                autoPauseSaved = false;
            }
        }

        public override void OnWorldUnload()
        {
            if (fightActive || isFadingOut)
            {
                StopMusic();
                RestoreAutoPause();
                fightActive = false;
                inFinalStand = false;
                isFadingOut = false;
                fadeOutTimer = 0f;
                musicPaused = false;
                lastDetectedPhase = -1;
            }
        }
    }
}
