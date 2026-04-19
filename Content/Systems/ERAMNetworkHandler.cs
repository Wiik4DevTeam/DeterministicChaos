using Microsoft.Xna.Framework;
using SubworldLibrary;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;
using DeterministicChaos.Content.Subworlds;
using DeterministicChaos.Content.VFX;

namespace DeterministicChaos.Content.Systems
{
    public class ERAMNetworkHandler : ModSystem
    {
        // Packet types
        public const byte ERAMSummonPacket = 0;
        public const byte DarkWorldCutscenePacket = 1;
        public const byte DialogueSyncPacket = 2;
        public const byte TitanCutscenePacket = 4;
        public const byte CalamityDifficultyPacket = 5;

        public static void HandleERAMSummonPacket(BinaryReader reader, int whoAmI)
        {
            byte playerIndex = reader.ReadByte();
            float centerX = reader.ReadSingle();
            float centerY = reader.ReadSingle();
            Vector2 summonerCenter = new Vector2(centerX, centerY);

            if (Main.netMode == NetmodeID.Server)
            {
                // Server received request from client, enter subworld for all players
                // SubworldLibrary handles transporting all connected players automatically
                DarkDimension.CacheCalamityDifficulty();
                ERAMProgressSystem.IsTransitioningSubworld = true;
                SubworldSystem.Enter<ERAMArena>();
            }
        }
        
        public static void SendDarkWorldCutscenePacket(Vector2 position, int originPlayerIndex)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
                return;
                
            ModPacket packet = ModContent.GetInstance<DeterministicChaos>().GetPacket();
            packet.Write(DarkWorldCutscenePacket);
            packet.Write(position.X);
            packet.Write(position.Y);
            packet.Write((byte)originPlayerIndex);
            
            if (Main.netMode == NetmodeID.Server)
            {
                // Server sends to all clients
                packet.Send();
            }
            else
            {
                // Client sends to server (who will relay to all clients)
                packet.Send();
            }
        }
        
        public static void HandleDarkWorldCutscenePacket(BinaryReader reader, int whoAmI)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            byte originPlayerIndex = reader.ReadByte();
            Vector2 position = new Vector2(x, y);
            
            if (Main.netMode == NetmodeID.Server)
            {
                // Server received from client, relay to all other clients
                ModPacket packet = ModContent.GetInstance<DeterministicChaos>().GetPacket();
                packet.Write(DarkWorldCutscenePacket);
                packet.Write(x);
                packet.Write(y);
                packet.Write(originPlayerIndex);
                packet.Send(-1, whoAmI); // Send to all except sender
            }
            
            // Start cutscene on this client (or don't do anything on server since it's headless)
            if (Main.netMode != NetmodeID.Server && originPlayerIndex < Main.maxPlayers)
            {
                Player originPlayer = Main.player[originPlayerIndex];
                DarkWorldCutscene.StartCutsceneAtPosition(position, originPlayer);
            }
        }
        
        public static void SendDialoguePacket(string[] texts, float[] lingerTimes)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                // Single player, just queue directly
                if (DialogueSystem.Instance != null)
                {
                    for (int i = 0; i < texts.Length; i++)
                    {
                        DialogueSystem.Instance.QueueDialogue(texts[i], lingerTimes[i]);
                    }
                }
                return;
            }
            
            ModPacket packet = ModContent.GetInstance<DeterministicChaos>().GetPacket();
            packet.Write(DialogueSyncPacket);
            packet.Write((byte)texts.Length);
            
            for (int i = 0; i < texts.Length; i++)
            {
                packet.Write(texts[i]);
                packet.Write(lingerTimes[i]);
            }
            
            if (Main.netMode == NetmodeID.Server)
            {
                packet.Send();
            }
            else
            {
                packet.Send();
            }
        }
        
        public static void HandleDialogueSyncPacket(BinaryReader reader, int whoAmI)
        {
            byte count = reader.ReadByte();
            string[] texts = new string[count];
            float[] lingerTimes = new float[count];
            
            for (int i = 0; i < count; i++)
            {
                texts[i] = reader.ReadString();
                lingerTimes[i] = reader.ReadSingle();
            }
            
            if (Main.netMode == NetmodeID.Server)
            {
                // Server received from client, relay to all clients
                ModPacket packet = ModContent.GetInstance<DeterministicChaos>().GetPacket();
                packet.Write(DialogueSyncPacket);
                packet.Write(count);
                
                for (int i = 0; i < count; i++)
                {
                    packet.Write(texts[i]);
                    packet.Write(lingerTimes[i]);
                }
                
                packet.Send(-1, whoAmI);
            }
            
            // Display dialogue on client
            if (Main.netMode != NetmodeID.Server && DialogueSystem.Instance != null)
            {
                for (int i = 0; i < count; i++)
                {
                    DialogueSystem.Instance.QueueDialogue(texts[i], lingerTimes[i]);
                }
            }
        }

        public static void SendTitanCutscenePacket(Vector2 position)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
                return;

            ModPacket packet = ModContent.GetInstance<DeterministicChaos>().GetPacket();
            packet.Write(TitanCutscenePacket);
            packet.Write(position.X);
            packet.Write(position.Y);
            packet.Send();
        }

        public static void HandleTitanCutscenePacket(BinaryReader reader, int whoAmI)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            Vector2 position = new Vector2(x, y);

            if (Main.netMode == NetmodeID.Server)
            {
                // Server received from client, relay to all other clients
                ModPacket packet = ModContent.GetInstance<DeterministicChaos>().GetPacket();
                packet.Write(TitanCutscenePacket);
                packet.Write(x);
                packet.Write(y);
                packet.Send(-1, whoAmI);
            }

            // Start cutscene on this side (server tracks phase/timer, clients do visuals)
            TitanSpawnCutscene.StartCutsceneAtPosition(position);
        }

        /// <summary>
        /// Sends the cached Calamity difficulty flags to all clients so they can
        /// restore Revengeance/Death mode after a subworld load.
        /// Call this on the server after RestoreCalamityDifficulty().
        /// </summary>
        public static void SendCalamityDifficultySync()
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            ModPacket packet = ModContent.GetInstance<DeterministicChaos>().GetPacket();
            packet.Write(CalamityDifficultyPacket);
            packet.Write(DarkDimension.CachedRevengeance);
            packet.Write(DarkDimension.CachedDeath);
            packet.Send();
        }

        public static void HandleCalamityDifficultyPacket(BinaryReader reader, int whoAmI)
        {
            bool revengeance = reader.ReadBoolean();
            bool death = reader.ReadBoolean();

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // Apply the difficulty flags on the client via the existing restore mechanism
                DarkDimension.CachedRevengeance = revengeance;
                DarkDimension.CachedDeath = death;
                DarkDimension._needsRestore = revengeance || death;
                DarkDimension.RestoreCalamityDifficulty();
            }
        }
    }
}
