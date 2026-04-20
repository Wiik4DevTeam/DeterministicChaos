using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;
using DeterministicChaos.Content.Items.Imbued;
using DeterministicChaos.Content.Projectiles.Friendly;
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
    public class RoaringSwordPlayer : ModPlayer
    {
        public int swingCombo = 0;
        public int lungeCooldown = 0;
        
        private const int LungeCooldownTime = 45;

        // Imbued Willbreaker fields
        public int imbuedWillbreakerVariant = -1;
        public int willbreakerMaxMarks = RoaringSwordMarkGlobalNPC.MaxStacks;
        public bool isHoldingWillbreaker = false;
        public float perseveranceManaBonus = 0f;
        public bool justiceHypercritPending = false;

        public override void ResetEffects()
        {
            if (Player.itemAnimation <= 0)
            {
                swingCombo = 0;
            }
            isHoldingWillbreaker = false;
            // Variant fields persist from last frame's HoldItem so PostUpdateEquips can reference them.
            // Cleared in PostUpdate when no longer holding.
        }
        
        public override void PostUpdate()
        {
            if (lungeCooldown > 0)
            {
                lungeCooldown--;
            }

            // Clear variant data when not holding a Willbreaker
            if (!isHoldingWillbreaker)
            {
                imbuedWillbreakerVariant = -1;
                willbreakerMaxMarks = RoaringSwordMarkGlobalNPC.MaxStacks;
                perseveranceManaBonus = 0f;
                justiceHypercritPending = false;
            }
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            // Integrity: 25% damage reduction while charging right click
            if (imbuedWillbreakerVariant == (int)ImbuedWillbreakerVariant.Integrity)
            {
                bool isCharging = false;
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    if (Main.projectile[i].active && Main.projectile[i].owner == Player.whoAmI &&
                        Main.projectile[i].type == ModContent.ProjectileType<RoaringSwordLungeCharge>())
                    {
                        isCharging = true;
                        break;
                    }
                }
                if (isCharging)
                    modifiers.FinalDamage *= 0.75f;
            }
        }

        public override void OnHitByNPC(NPC npc, Player.HurtInfo hurtInfo)
        {
            // Bravery: Enemies that damage you automatically receive maximum marks
            if (imbuedWillbreakerVariant == (int)ImbuedWillbreakerVariant.Bravery && isHoldingWillbreaker)
            {
                ApplyMaxMarksToNPC(npc);
            }
        }

        public override void OnHitByProjectile(Projectile proj, Player.HurtInfo hurtInfo)
        {
            // Bravery: For projectile damage, mark the closest hostile NPC
            if (imbuedWillbreakerVariant == (int)ImbuedWillbreakerVariant.Bravery && isHoldingWillbreaker)
            {
                NPC closest = FindClosestHostileNPC(800f);
                if (closest != null)
                    ApplyMaxMarksToNPC(closest);
            }
        }

        private void ApplyMaxMarksToNPC(NPC npc)
        {
            if (npc == null || !npc.active || npc.friendly || npc.immortal)
                return;

            // For NPCs that can't take damage themselves (e.g. worm body segments),
            // redirect marks to the head NPC instead of bailing out.
            if (npc.dontTakeDamage)
            {
                if (npc.realLife >= 0 && npc.realLife != npc.whoAmI)
                {
                    NPC head = Main.npc[npc.realLife];
                    if (head.active && !head.friendly && !head.immortal && !head.dontTakeDamage)
                        npc = head;
                    else
                        return;
                }
                else
                {
                    return;
                }
            }

            var markNPC = npc.GetGlobalNPC<RoaringSwordMarkGlobalNPC>();
            int prevStacks = markNPC.markStacks;

            // Use AddMark so the EyeDebuff is properly applied through the standard code path.
            // Pass a high stack count to instantly cap at willbreakerMaxMarks.
            markNPC.AddMark(npc, willbreakerMaxMarks, willbreakerMaxMarks);

            if (Main.netMode != NetmodeID.SinglePlayer)
                npc.netUpdate = true;

            // Visual feedback whenever marks were freshly raised to max
            if (prevStacks < willbreakerMaxMarks)
            {
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.7f, Pitch = 0.6f }, npc.Center);
                CombatText.NewText(npc.Hitbox, new Color(255, 190, 60), "MARKED!", false, false);

                for (int i = 0; i < 20; i++)
                {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(7f, 7f);
                    Dust dust = Dust.NewDustPerfect(npc.Center, DustID.WhiteTorch, vel, 0, new Color(255, 190, 60), 1.6f);
                    dust.noGravity = true;
                }

                // Fire a defensive counter-slash at the attacker so Bravery has immediate visible payoff
                if (Player.whoAmI == Main.myPlayer)
                {
                    Vector2 toNPC = (npc.Center - Player.Center).SafeNormalize(Vector2.UnitX);
                    float aimAngle = toNPC.ToRotation();
                    int swingCombo = this.swingCombo;
                    float swingDirection = (swingCombo % 2 == 0) ? 1f : -1f;
                    int slashDamage = (int)(Player.GetWeaponDamage(Player.HeldItem) * 0.6f);

                    Projectile.NewProjectile(
                        Player.GetSource_OnHurt(npc),
                        Player.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<RoaringSwordSwing>(),
                        slashDamage,
                        Player.GetWeaponKnockback(Player.HeldItem, Player.HeldItem.knockBack),
                        Player.whoAmI,
                        swingDirection,
                        aimAngle
                    );
                }
            }
        }

        private NPC FindClosestHostileNPC(float maxDist)
        {
            NPC closest = null;
            float closestDist = maxDist;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal)
                    continue;
                if (npc.realLife >= 0 && npc.realLife != npc.whoAmI)
                    continue;
                float dist = Vector2.Distance(Player.Center, npc.Center);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = npc;
                }
            }
            return closest;
        }

        public void StartLungeCooldown()
        {
            // Only set cooldown for the local player
            if (Player.whoAmI == Main.myPlayer)
            {
                lungeCooldown = LungeCooldownTime;
            }
        }
    }
}
