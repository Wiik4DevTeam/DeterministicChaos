using Microsoft.Xna.Framework;
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

namespace DeterministicChaos.Content.Items.Weapons
{
    public class RodOfStagnation : ModItem
    {
        private const int CooldownTicks = 360; // 6 seconds

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(gold: 8);
            Item.useAnimation = 20;
            Item.useTime = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.UseSound = SoundID.Item6;
            Item.noMelee = true;
            Item.consumable = false;
            Item.autoReuse = false;
        }

        public override bool CanUseItem(Player player)
        {
            return !player.HasBuff(ModContent.BuffType<StagnationSicknessBuff>());
        }

        public override bool? UseItem(Player player)
        {
            // Only execute on the local client, server has no valid MouseWorld
            if (player.whoAmI != Main.myPlayer)
                return true;

            Vector2 destinationCenter = Main.MouseWorld;
            Vector2 targetTopLeft = destinationCenter - new Vector2(player.width * 0.5f, player.height * 0.5f);

            targetTopLeft.X = MathHelper.Clamp(targetTopLeft.X, 16f, Main.maxTilesX * 16f - player.width - 16f);
            targetTopLeft.Y = MathHelper.Clamp(targetTopLeft.Y, 16f, Main.maxTilesY * 16f - player.height - 16f);

            if (Collision.SolidCollision(targetTopLeft, player.width, player.height))
            {
                bool found = false;
                const int maxRadius = 12;
                const int step = 16;

                for (int r = 1; r <= maxRadius && !found; r++)
                {
                    for (int ox = -r; ox <= r && !found; ox++)
                    {
                        for (int oy = -r; oy <= r; oy++)
                        {
                            Vector2 probe = targetTopLeft + new Vector2(ox * step, oy * step);
                            if (!Collision.SolidCollision(probe, player.width, player.height))
                            {
                                targetTopLeft = probe;
                                found = true;
                                break;
                            }
                        }
                    }
                }

                if (!found)
                    return false;
            }

            Vector2 oldCenter = player.Center;

            // Teleport expects top-left position, not center
            player.Teleport(targetTopLeft, TeleportationStyleID.RodOfDiscord);
            player.velocity = Vector2.Zero;
            player.AddBuff(ModContent.BuffType<StagnationSicknessBuff>(), CooldownTicks);

            Vector2 finalCenter = player.Center;

            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0, player.whoAmI, targetTopLeft.X, targetTopLeft.Y, TeleportationStyleID.RodOfDiscord);
            }

            for (int i = 0; i < 28; i++)
            {
                Vector2 v = Main.rand.NextVector2Circular(4f, 4f);
                Dust.NewDustPerfect(oldCenter, DustID.PurpleTorch, v, 160, default, 1.2f).noGravity = true;
                Dust.NewDustPerfect(finalCenter, DustID.PurpleTorch, -v, 160, default, 1.2f).noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item8, finalCenter);
            return true;
        }
    }

    public class RodOfStagnationPlayer : ModPlayer
    {
        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            if (Player.HasBuff(ModContent.BuffType<StagnationSicknessBuff>()))
                modifiers.FinalDamage *= 1.25f;
        }

        public override void PostUpdate()
        {
            if (!Player.HasBuff(ModContent.BuffType<StagnationSicknessBuff>()))
                return;

            Player.armorEffectDrawShadow = true;
            Player.armorEffectDrawShadowSubtle = true;
            Player.armorEffectDrawOutlines = true;

            if (Main.rand.NextBool(3))
            {
                Vector2 pos = Player.Center + Main.rand.NextVector2Circular(10f, 18f);
                Vector2 vel = Main.rand.NextVector2Circular(0.8f, 0.8f);
                Dust d = Dust.NewDustPerfect(pos, DustID.Vortex, vel, 170, default, 0.9f);
                d.noGravity = true;
            }
        }
    }
}