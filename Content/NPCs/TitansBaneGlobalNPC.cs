using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;
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

namespace DeterministicChaos.Content.NPCs
{
    // Handles Titansbane: single damage burst on first application tick.
    // Uses InstancePerEntity so each NPC tracks its own state.
    public class TitansBaneGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Set each tick by TitansBane.Update(NPC); cleared by ResetEffects.
        public bool hasTitansBane = false;
        private bool prevHasTitansBane = false;

        // Flask imbue cooldown: prevents re-application for 10 seconds (600 ticks).
        public int imbueCooldown = 0;

        // Set by ForthcomingWrath charged strike to force the damage burst even if the
        // buff was already active (bypasses prevHasTitansBane gate).
        public bool forceReapply = false;

        public override void ResetEffects(NPC npc)
        {
            hasTitansBane = false;
        }

        public override void PostAI(NPC npc)
        {
            if (imbueCooldown > 0)
                imbueCooldown--;

            // Only deal damage on the very first tick the buff becomes active,
            // OR when a charged strike forces a re-trigger.
            if ((hasTitansBane && !prevHasTitansBane || forceReapply) && npc.active && npc.lifeMax > 0)
            {
                forceReapply = false;
                // 1% of current HP, minimum 1
                int damage = Math.Max(1, (int)(npc.life * 0.01f));

                npc.life -= damage;
                npc.netUpdate = true;

                if (Main.netMode != NetmodeID.Server)
                {
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/Titansbane"), npc.Center);
                    CombatText.NewText(npc.Hitbox, new Color(0x47, 0xEC, 0xF7), damage);
                }

                if (npc.life <= 0)
                {
                    npc.life = 0;
                    npc.HitEffect();
                    npc.active = false;
                    npc.netUpdate = true;
                }
            }

            prevHasTitansBane = hasTitansBane;
        }
    }
}
