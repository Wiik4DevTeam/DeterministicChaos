using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
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

namespace DeterministicChaos.Content.Items.Accessories
{
    public class TitanicEmblemPlayer : ModPlayer
    {
        public bool hasTitanicEmblem;

        public override void ResetEffects()
        {
            hasTitanicEmblem = false;
        }

        private static bool IsTrueMelee(Item item)
        {
            return item.DamageType.CountsAsClass(DamageClass.Melee)
                && item.damage > 0
                && !item.noMelee
                && item.pick == 0 && item.axe == 0 && item.hammer == 0;
        }

        public override void ModifyItemScale(Item item, ref float scale)
        {
            if (hasTitanicEmblem && IsTrueMelee(item))
                scale += 1.12f;
        }

        public override void ModifyWeaponDamage(Item item, ref StatModifier damage)
        {
            if (hasTitanicEmblem && IsTrueMelee(item))
                damage += 0.10f;
        }

        public override void ModifyWeaponKnockback(Item item, ref StatModifier knockback)
        {
            if (hasTitanicEmblem && IsTrueMelee(item))
                knockback += 0.66f;
        }

        public override float UseSpeedMultiplier(Item item)
        {
            if (hasTitanicEmblem && IsTrueMelee(item))
                return 0.75f;

            return 1f;
        }
    }
}
