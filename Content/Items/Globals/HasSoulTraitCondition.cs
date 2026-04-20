using Terraria;
using Terraria.GameContent.ItemDropRules;
using DeterministicChaos.Content.SoulTraits;
using DeterministicChaos.Content.Items.Accessories;
using DeterministicChaos.Content.Items.BossBags;
using DeterministicChaos.Content.Items.BossSummons;
using DeterministicChaos.Content.Items.Consumables;
using DeterministicChaos.Content.Items.DamageClasses;
using DeterministicChaos.Content.Items.Globals;
using DeterministicChaos.Content.Items.Materials;
using DeterministicChaos.Content.Items.Placeable;
using DeterministicChaos.Content.Items.Rarities;
using DeterministicChaos.Content.Items.Weapons;

namespace DeterministicChaos.Content.Items.Globals
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
