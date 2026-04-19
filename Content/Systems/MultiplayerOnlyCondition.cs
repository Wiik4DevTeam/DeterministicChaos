using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;

namespace DeterministicChaos.Content.Systems
{
    // Drop condition that only passes in multiplayer (server or client).
    // Used to restrict boss bags to multiplayer worlds.
    public class MultiplayerOnlyCondition : IItemDropRuleCondition
    {
        public bool CanDrop(DropAttemptInfo info) => Main.netMode != NetmodeID.SinglePlayer;
        public bool IsAvalibleInThisContext(DropAttemptInfo info) => true;
        public bool CanShowItemDropInUI() => false;
        public string GetConditionDescription() => "Drops only in multiplayer";
    }
}
