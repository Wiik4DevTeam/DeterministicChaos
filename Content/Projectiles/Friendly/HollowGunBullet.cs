using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using DeterministicChaos.Content.Items;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class HollowGunGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public bool isHollowGunBullet = false;

        // Sync the flag for multiplayer
        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(isHollowGunBullet);
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
        {
            isHollowGunBullet = bitReader.ReadBit();
        }

        public override void AI(Projectile projectile)
        {
            if (!isHollowGunBullet)
                return;

            // Yellow trail for HollowGun bullets
            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height,
                    DustID.YellowTorch, 0f, 0f, 100, default, 0.8f);
                dust.noGravity = true;
                dust.velocity = projectile.velocity * -0.1f;
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!isHollowGunBullet)
                return;

            // Apply summon tag debuff (buff system is multiplayer-synced)
            target.AddBuff(ModContent.BuffType<HollowGunTagDebuff>(), 300); // 5 seconds

            // Visual feedback
            for (int i = 0; i < 6; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
                Dust dust = Dust.NewDustPerfect(target.Center, DustID.YellowTorch, vel, 0, default, 1f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            if (!isHollowGunBullet)
                return true;

            // Draw with slight yellow tint
            Texture2D tex = TextureAssets.Projectile[projectile.type].Value;
            if (tex == null)
                return true;

            Vector2 origin = tex.Size() * 0.5f;

            Main.EntitySpriteDraw(
                tex,
                projectile.Center - Main.screenPosition,
                null,
                Color.Lerp(lightColor, Color.Yellow, 0.3f),
                projectile.rotation,
                origin,
                projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }

    public class HollowGunTagDebuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = false;
            Main.buffNoSave[Type] = true;
        }
    }

    public class HollowGunGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private const int TagDamageBonus = 8;

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            // Use the buff for multiplayer-safe detection (buffs are synced)
            bool isTagged = npc.HasBuff(ModContent.BuffType<HollowGunTagDebuff>());
            if (!isTagged)
                return;

            // Check if this is a minion/sentry projectile
            if (projectile.minion || projectile.sentry || IsSummonProjectile(projectile))
            {
                // Add flat summon tag damage
                modifiers.FlatBonusDamage += TagDamageBonus;
            }
        }

        private bool IsSummonProjectile(Projectile proj)
        {
            // Check damage type
            if (proj.DamageType == DamageClass.Summon)
                return true;

            // Check for common minion properties
            if (proj.minion || proj.sentry)
                return true;

            return false;
        }

        public override void DrawEffects(NPC npc, ref Color drawColor)
        {
            // Use the buff for multiplayer-safe detection
            bool isTagged = npc.HasBuff(ModContent.BuffType<HollowGunTagDebuff>());
            if (isTagged)
            {
                // Yellow tint while tagged
                drawColor = Color.Lerp(drawColor, Color.Yellow, 0.2f);

                // Occasional yellow dust
                if (Main.rand.NextBool(10))
                {
                    Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                        DustID.YellowTorch, 0f, -1f, 100, default, 0.6f);
                    dust.noGravity = true;
                    dust.velocity *= 0.3f;
                }
            }
        }
    }
}
