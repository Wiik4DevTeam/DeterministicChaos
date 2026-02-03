using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items.Armor
{
    // GlobalProjectile to handle the Roaring Ranger set bonus ricochet mechanics
    public class RoaringRangerGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        
        // Track if this projectile has already ricocheted
        public bool hasRicocheted;
        // Track if this is a ricochet projectile (spawned from ricochet)
        public bool isRicochetProjectile;
        
        public override void SetDefaults(Projectile projectile)
        {
            hasRicocheted = false;
            isRicochetProjectile = false;
        }
        
        public override bool OnTileCollide(Projectile projectile, Vector2 oldVelocity)
        {
            // Check if this projectile can ricochet
            if (!CanRicochet(projectile))
                return base.OnTileCollide(projectile, oldVelocity);
                
            // Check if owner has the ranger set bonus
            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
                return base.OnTileCollide(projectile, oldVelocity);
                
            Player player = Main.player[projectile.owner];
            if (player == null || !player.active)
                return base.OnTileCollide(projectile, oldVelocity);
                
            RoaringArmorPlayer modPlayer = player.GetModPlayer<RoaringArmorPlayer>();
            if (!modPlayer.roaringRangerSet)
                return base.OnTileCollide(projectile, oldVelocity);
            
            // Perform ricochet by modifying the existing projectile
            // This preserves weapon context for perks like Roaring Gun
            DoRicochet(projectile, oldVelocity, player, modPlayer);
            
            // Return false to prevent the projectile from dying
            return false;
        }
        
        private bool CanRicochet(Projectile projectile)
        {
            // Do not ricochet if already ricocheted or is a ricochet projectile
            if (hasRicocheted || isRicochetProjectile)
                return false;
            
            // Check if projectile is a bullet or arrow
            return IsBulletOrArrow(projectile);
        }
        
        private bool IsBulletOrArrow(Projectile projectile)
        {
            // Check if it is ranged damage
            if (!projectile.CountsAsClass(DamageClass.Ranged))
                return false;
            
            // Check common bullet projectile IDs
            if (projectile.type == ProjectileID.Bullet ||
                projectile.type == ProjectileID.MeteorShot ||
                projectile.type == ProjectileID.CrystalBullet ||
                projectile.type == ProjectileID.CursedBullet ||
                projectile.type == ProjectileID.ChlorophyteBullet ||
                projectile.type == ProjectileID.BulletHighVelocity ||
                projectile.type == ProjectileID.IchorBullet ||
                projectile.type == ProjectileID.VenomBullet ||
                projectile.type == ProjectileID.PartyBullet ||
                projectile.type == ProjectileID.NanoBullet ||
                projectile.type == ProjectileID.ExplosiveBullet ||
                projectile.type == ProjectileID.GoldenBullet ||
                projectile.type == ProjectileID.MoonlordBullet ||
                projectile.type == ProjectileID.SilverBullet)
            {
                return true;
            }
            
            // Check common arrow projectile IDs
            if (projectile.type == ProjectileID.WoodenArrowFriendly ||
                projectile.type == ProjectileID.FireArrow ||
                projectile.type == ProjectileID.UnholyArrow ||
                projectile.type == ProjectileID.JestersArrow ||
                projectile.type == ProjectileID.HellfireArrow ||
                projectile.type == ProjectileID.HolyArrow ||
                projectile.type == ProjectileID.CursedArrow ||
                projectile.type == ProjectileID.FrostburnArrow ||
                projectile.type == ProjectileID.ChlorophyteArrow ||
                projectile.type == ProjectileID.IchorArrow ||
                projectile.type == ProjectileID.VenomArrow ||
                projectile.type == ProjectileID.MoonlordArrow ||
                projectile.type == ProjectileID.BoneArrow ||
                projectile.type == ProjectileID.ShimmerArrow)
            {
                return true;
            }
            
            // Check by projectile arrow flag (covers modded arrows)
            if (projectile.arrow)
                return true;
            
            // Check by projectile name patterns for modded content
            string projName = projectile.Name.ToLower();
            if (projName.Contains("bullet") || projName.Contains("arrow") || projName.Contains("shot"))
                return true;
            
            return false;
        }
        
        private void DoRicochet(Projectile projectile, Vector2 oldVelocity, Player player, RoaringArmorPlayer modPlayer)
        {
            // Mark as ricocheted so it does not trigger again
            hasRicocheted = true;
            
            // Reflect off the surface naturally
            // Determine which axis was blocked and reflect accordingly
            if (projectile.velocity.X != oldVelocity.X)
            {
                projectile.velocity = new Vector2(-oldVelocity.X, oldVelocity.Y);
            }
            else if (projectile.velocity.Y != oldVelocity.Y)
            {
                projectile.velocity = new Vector2(oldVelocity.X, -oldVelocity.Y);
            }
            else
            {
                projectile.velocity = -oldVelocity;
            }
            
            // Increase damage by 20%
            projectile.damage = (int)(projectile.damage * 1.20f);
            
            // Extend projectile lifetime
            projectile.timeLeft = 300;
            
            // Grant the fire rate buff (only for the owner)
            if (projectile.owner == Main.myPlayer)
            {
                modPlayer.OnRicochet();
            }
            
            // Play ricochet sound
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.6f, Pitch = 0.3f }, projectile.Center);
            
            // Visual effect, dust burst
            for (int i = 0; i < 8; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(4f, 4f);
                Dust dust = Dust.NewDustDirect(projectile.Center, 1, 1, DustID.Shadowflame, dustVel.X, dustVel.Y, 100, default, 1.2f);
                dust.noGravity = true;
            }
        }
    }
}
