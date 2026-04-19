using Terraria;
using Terraria.GameContent.ItemDropRules;
using DeterministicChaos.Content.SoulTraits;

namespace DeterministicChaos.Content.Items
{
    public class HasSoulTraitCondition : IItemDropRuleCondition
    {
        public bool CanDrop(DropAttemptInfo info)
        {
            if (info.player == null)
                return false;
            var stp = info.player.GetModPlayer<SoulTraitPlayer>();
            return stp.CurrentTrait != SoulTraitType.None;
        }

        public bool CanShowItemDropInUI() => true;

        public string GetConditionDescription() => "Requires a soul trait";
    }
}
