using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ModLoader;
using static Terraria.ModLoader.ModContent;
using DeterministicChaos.Content.NPCs.Bosses;

namespace DeterministicChaos.Content.UI
{
    public class RoaringKnightBossBar : ModBossBar
    {
        private const string HeadPath = "DeterministicChaos/Content/NPCs/Bosses/KnightHead";

        public override Asset<Texture2D> GetIconTexture(ref Rectangle? iconFrame)
        {
            return Request<Texture2D>(HeadPath, AssetRequestMode.ImmediateLoad);
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
