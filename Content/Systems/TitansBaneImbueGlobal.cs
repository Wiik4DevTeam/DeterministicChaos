using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;
using DeterministicChaos.Content.Items;
using DeterministicChaos.Content.NPCs;

namespace DeterministicChaos.Content.Systems
{
    internal static class TitansBaneImbueVFX
    {
        internal static void SpawnFlames(NPC target)
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            for (int i = 0; i < 8; i++)
            {
                Dust d = Dust.NewDustDirect(
                    target.Center - new Vector2(8f),
                    16, 16, DustID.BlueTorch,
                    Scale: Main.rand.NextFloat(1.2f, 2.4f));
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(5f, 5f);
            }
        }
    }

    // Applies Titansbane debuff when the owning player has the TitansBaneImbue flask buff.
    // Covers both projectile hits and direct melee item hits.
    public class TitansBaneImbueGlobalProjectile : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!projectile.friendly || projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
                return;

            // Skip projectiles that apply Titansbane directly (no imbue cooldown for them)
            if (projectile.type == ModContent.ProjectileType<Projectiles.Friendly.ForthcomingWrathProjectile>())
                return;

            Player owner = Main.player[projectile.owner];
            if (!owner.active)
                return;

            var globalNPC = target.GetGlobalNPC<TitansBaneGlobalNPC>();
            if (owner.GetModPlayer<TitansBaneImbuePlayer>().imbueActive && globalNPC.imbueCooldown <= 0
                && !target.HasBuff(ModContent.BuffType<TitansBane>()))
            {
                globalNPC.imbueCooldown = 600; // 10 seconds
                target.AddBuff(ModContent.BuffType<TitansBane>(), 10 * 60);
                TitansBaneImbueVFX.SpawnFlames(target);
            }
        }
    }

    // Covers direct item melee swings that don't spawn a projectile.
    public class TitansBaneImbueGlobalItem : GlobalItem
    {
        public override void OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!item.CountsAsClass(DamageClass.Melee))
                return;

            var globalNPC = target.GetGlobalNPC<TitansBaneGlobalNPC>();
            if (player.GetModPlayer<TitansBaneImbuePlayer>().imbueActive && globalNPC.imbueCooldown <= 0
                && !target.HasBuff(ModContent.BuffType<TitansBane>()))
            {
                globalNPC.imbueCooldown = 600; // 10 seconds
                target.AddBuff(ModContent.BuffType<TitansBane>(), 10 * 60);
                TitansBaneImbueVFX.SpawnFlames(target);
            }
        }
    }
}
