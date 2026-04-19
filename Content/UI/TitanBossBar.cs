using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ModLoader;
using DeterministicChaos.Content.NPCs.Bosses;

namespace DeterministicChaos.Content.UI
{
    public class TitanBossBar : ModBossBar
    {
        public override Asset<Texture2D> GetIconTexture(ref Rectangle? iconFrame)
        {
            return ModContent.Request<Texture2D>("DeterministicChaos/Content/NPCs/Bosses/TitanBody_Head_Boss", AssetRequestMode.ImmediateLoad);
        }

        public override bool? ModifyInfo(ref BigProgressBarInfo info, ref float life, ref float lifeMax, ref float shield, ref float shieldMax)
        {
            NPC boss = Main.npc[info.npcIndexToAimAt];
            if (!boss.active)
                return false;

            life = boss.life;
            lifeMax = boss.lifeMax;
            shield = 0f;
            shieldMax = 0f;

            return true;
        }
    }
}
