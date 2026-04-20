using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Imbued;
using DeterministicChaos.Content.SoulTraits.Armor;
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

namespace DeterministicChaos.Content.Items.Weapons
{
    public class RoaringWhipPlayer : ModPlayer
    {
        public ImbuedGnomonVariant imbuedGnomonVariant = ImbuedGnomonVariant.None;
        public bool isHoldingGnomon = false;

        // Integrity: NPC index granting DR
        public int integrityTargetNPC = -1;

        // Perseverance: attack speed buff (1 = next swing is fast, consumed on use)
        public int perseveranceSpeedTimer = 0;

        // Justice: summon crit → next whip hypercrit
        public bool justiceHypercritPending = false;

        public override void ResetEffects()
        {
            isHoldingGnomon = false;
        }

        public override void PostUpdate()
        {
            if (!isHoldingGnomon)
            {
                imbuedGnomonVariant = ImbuedGnomonVariant.None;
                integrityTargetNPC = -1;
                perseveranceSpeedTimer = 0;
                justiceHypercritPending = false;
            }
        }

        public override void PostUpdateEquips()
        {
            // Integrity: +5% DR while tagged NPC is alive
            if (isHoldingGnomon && imbuedGnomonVariant == ImbuedGnomonVariant.Integrity && integrityTargetNPC >= 0)
            {
                NPC target = Main.npc[integrityTargetNPC];
                if (target.active && target.life > 0)
                    Player.endurance += 0.05f;
                else
                    integrityTargetNPC = -1;
            }
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Justice: when a summon minion crits (not from whips), flag next whip hit as hypercrit
            if (imbuedGnomonVariant == ImbuedGnomonVariant.Justice &&
                hit.Crit &&
                proj.DamageType.CountsAsClass(DamageClass.Summon) &&
                !ProjectileID.Sets.IsAWhip[proj.type])
            {
                justiceHypercritPending = true;
            }
        }
    }
}
