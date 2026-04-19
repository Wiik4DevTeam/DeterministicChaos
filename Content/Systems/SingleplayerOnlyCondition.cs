using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;

namespace DeterministicChaos.Content.Systems
{
    public class SingleplayerOnlyCondition : IItemDropRuleCondition
    {
        public bool CanDrop(DropAttemptInfo info) => Main.netMode == NetmodeID.SinglePlayer;
        public bool IsAvalibleInThisContext(DropAttemptInfo info) => true;
        public bool CanShowItemDropInUI() => true;
        public string GetConditionDescription() => "Drops in singleplayer";
    }
}
