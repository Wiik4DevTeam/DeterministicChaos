using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Buffs
{
    public class GnomonTagDebuff : ModBuff
    {
        public const int TagDamage = 15;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = false;
            Main.buffNoSave[Type] = true;
        }
    }

    public class GnomonTagGlobalNPC : GlobalNPC
    {
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (projectile.npcProj || projectile.trap)
                return;

            // Summon-class projectiles deal bonus flat damage to tagged NPCs
            if (npc.HasBuff<GnomonTagDebuff>() && projectile.DamageType.CountsAsClass(DamageClass.Summon))
            {
                modifiers.FlatBonusDamage += GnomonTagDebuff.TagDamage;
            }
        }
    }
}
