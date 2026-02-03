using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits
{
    public class SoulTraitNetworkHandler : ModSystem
    {
        public const byte SyncTrait = 10;
        public const byte SyncMarks = 11;
        public const byte SyncKindnessEffect = 12;

        public static void SendTraitSync(int playerIndex, int toClient = -1, int ignoreClient = -1)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
                return;

            ModPacket packet = ModContent.GetInstance<DeterministicChaos>().GetPacket();
            packet.Write((byte)SyncTrait);
            packet.Write((byte)playerIndex);

            Player player = Main.player[playerIndex];
            SoulTraitPlayer traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            packet.Write((byte)traitPlayer.CurrentTrait);
            packet.Write(traitPlayer.TraitLocked);

            packet.Send(toClient, ignoreClient);
        }

        public static void SendMarkSync(int playerIndex, int toClient = -1, int ignoreClient = -1)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
                return;

            ModPacket packet = ModContent.GetInstance<DeterministicChaos>().GetPacket();
            packet.Write((byte)SyncMarks);
            packet.Write((byte)playerIndex);

            Player player = Main.player[playerIndex];
            SoulTraitPlayer traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            packet.Write(traitPlayer.JusticeMarkActive);
            packet.Write(traitPlayer.KindnessMarkTimer);
            packet.Write(traitPlayer.BraveryMarkActive);
            packet.Write(traitPlayer.PatienceMarkStacks);
            packet.Write(traitPlayer.IntegrityMarkStacks);
            packet.Write(traitPlayer.PerseveranceMarkActive);
            packet.Write(traitPlayer.DeterminationMarkActive);
            packet.Write(traitPlayer.DeterminationSavedPosition.X);
            packet.Write(traitPlayer.DeterminationSavedPosition.Y);

            packet.Send(toClient, ignoreClient);
        }

        public static void SendKindnessAllyEffect(int sourcePlayer, int targetPlayer)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
                return;

            ModPacket packet = ModContent.GetInstance<DeterministicChaos>().GetPacket();
            packet.Write((byte)SyncKindnessEffect);
            packet.Write((byte)sourcePlayer);
            packet.Write((byte)targetPlayer);

            packet.Send();
        }

        public static void HandlePacket(BinaryReader reader, int whoAmI)
        {
            byte messageType = reader.ReadByte();

            switch (messageType)
            {
                case SyncTrait:
                    HandleSyncTrait(reader, whoAmI);
                    break;
                case SyncMarks:
                    HandleSyncMarks(reader, whoAmI);
                    break;
                case SyncKindnessEffect:
                    HandleKindnessEffect(reader);
                    break;
            }
        }

        private static void HandleSyncTrait(BinaryReader reader, int whoAmI)
        {
            int playerIndex = reader.ReadByte();
            SoulTraitType trait = (SoulTraitType)reader.ReadByte();
            bool locked = reader.ReadBoolean();

            Player player = Main.player[playerIndex];
            SoulTraitPlayer traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            traitPlayer.CurrentTrait = trait;
            traitPlayer.TraitLocked = locked;

            // Relay to other clients if server
            if (Main.netMode == NetmodeID.Server)
            {
                SendTraitSync(playerIndex, -1, whoAmI);
            }
        }

        private static void HandleSyncMarks(BinaryReader reader, int whoAmI)
        {
            int playerIndex = reader.ReadByte();

            Player player = Main.player[playerIndex];
            SoulTraitPlayer traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            traitPlayer.JusticeMarkActive = reader.ReadBoolean();
            traitPlayer.KindnessMarkTimer = reader.ReadInt32();
            traitPlayer.BraveryMarkActive = reader.ReadBoolean();
            traitPlayer.PatienceMarkStacks = reader.ReadInt32();
            traitPlayer.IntegrityMarkStacks = reader.ReadInt32();
            traitPlayer.PerseveranceMarkActive = reader.ReadBoolean();
            traitPlayer.DeterminationMarkActive = reader.ReadBoolean();
            traitPlayer.DeterminationSavedPosition = new Vector2(reader.ReadSingle(), reader.ReadSingle());

            // Relay to other clients if server
            if (Main.netMode == NetmodeID.Server)
            {
                SendMarkSync(playerIndex, -1, whoAmI);
            }
        }

        private static void HandleKindnessEffect(BinaryReader reader)
        {
            int sourcePlayer = reader.ReadByte();
            int targetPlayer = reader.ReadByte();

            // Apply kindness mark to target player
            if (targetPlayer >= 0 && targetPlayer < Main.maxPlayers)
            {
                Player target = Main.player[targetPlayer];
                if (target.active && !target.dead)
                {
                    var traitPlayer = target.GetModPlayer<SoulTraitPlayer>();
                    traitPlayer.KindnessMarkTimer = SoulTraitPlayer.KindnessMarkDuration;
                }
            }
        }
    }
}
