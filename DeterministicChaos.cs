using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ModLoader;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Microsoft.Xna.Framework.Graphics;
using DeterministicChaos.Content.Systems;
using DeterministicChaos.Content.NPCs.Bosses;
using DeterministicChaos.Content.SoulTraits;
using DeterministicChaos.Content.Items;

namespace DeterministicChaos
{
	public class DeterministicChaos : Mod
	{
		public override void Load()
		{
			// VHS filter shader temporarily disabled
			/*
			if (!Main.dedServ)
			{
				Ref<Effect> vhsShader = new Ref<Effect>(Assets.Request<Effect>("Assets/Effects/VHSFilter", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value);
				Filters.Scene["DeterministicChaos:VHSFilter"] = new Filter(new ScreenShaderData(vhsShader, "VHSPass"), EffectPriority.VeryHigh);
				Filters.Scene["DeterministicChaos:VHSFilter"].Load();
			}
			*/
		}

		public override void Unload()
		{
			/*
			if (!Main.dedServ && Filters.Scene["DeterministicChaos:VHSFilter"] != null)
			{
				Filters.Scene["DeterministicChaos:VHSFilter"] = null;
			}
			*/
		}

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			byte packetType = reader.ReadByte();

						switch (packetType)
			{
				case ERAMNetworkHandler.ERAMSummonPacket:
					ERAMNetworkHandler.HandleERAMSummonPacket(reader, whoAmI);
					break;
				case ERAMNetworkHandler.DarkWorldCutscenePacket:
					ERAMNetworkHandler.HandleDarkWorldCutscenePacket(reader, whoAmI);
					break;
				case ERAMNetworkHandler.DialogueSyncPacket:
					ERAMNetworkHandler.HandleDialogueSyncPacket(reader, whoAmI);
					break;
				case 3: // Sphere damage sync packet
					HandleSphereDamagePacket(reader, whoAmI);
					break;
				case 10: // Soul Trait sync packets
				case 11:
				case 12:
					SoulTraitNetworkHandler.HandlePacket(reader, whoAmI);
					break;
				case (byte)TornNotebookNetHandler.MessageType.SyncStoredText:
					TornNotebookNetHandler.HandlePacket(reader, whoAmI);
					break;
			}
		}
		
		private void HandleSphereDamagePacket(BinaryReader reader, int whoAmI)
		{
			int sphereNpcIndex = reader.ReadInt32();
			int damage = reader.ReadInt32();
			bool crit = reader.ReadBoolean();
			
			// Only server processes this
			if (Main.netMode != Terraria.ID.NetmodeID.Server)
				return;
			
			if (sphereNpcIndex < 0 || sphereNpcIndex >= Main.maxNPCs)
				return;
				
			NPC sphere = Main.npc[sphereNpcIndex];
			if (!sphere.active || sphere.ModNPC is not RoaringKnightSphere sphereNpc)
				return;
			
			// Apply the damage via the sphere's sync method
			sphereNpc.ReceiveDamageSync(damage, crit);
		}
	}
}
