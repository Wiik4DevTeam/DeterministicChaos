using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
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

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    // Arrow projectile for TitansArrow ammo.
    // Phase 0 (flying)   : standard arc with slight gravity.
    // Phase 1 (stuck NPC): lodged in an enemy; counts down 60-tick fuse.
    // Phase 2 (stuck tile): lodged in a surface; counts down 60-tick fuse.
    // On fuse end: small explosion dealing 80% of the arrow's base damage to nearby enemies.
    // Impact hit is reduced to 20% via ModifyHitNPC.
    public class TitansArrowProjectile : ModProjectile
    {
        private const float ExplosionRadius = 110f;
        private const int   FuseTime        = 60;   // 1 second

        // ai[0] = state (0 / 1 / 2), ai[1] = fuse timer (counts down from FuseTime)
        private float State { get => Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private float Timer { get => Projectile.ai[1]; set => Projectile.ai[1] = value; }

        private int     stuckNPC      = -1;
        private Vector2 stuckOffset   = Vector2.Zero;
        private int     originalDamage = 0;
        private bool    hasExploded    = false;

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(stuckNPC);
            writer.WriteVector2(stuckOffset);
            writer.Write(originalDamage);
            writer.Write(hasExploded);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            stuckNPC       = reader.ReadInt32();
            stuckOffset    = reader.ReadVector2();
            originalDamage = reader.ReadInt32();
            hasExploded    = reader.ReadBoolean();
        }

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type]     = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width       = 10;
            Projectile.height      = 10;
            Projectile.friendly    = true;
            Projectile.hostile     = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.penetrate   = -1;
            Projectile.timeLeft    = 420;
            Projectile.arrow       = true;
            Projectile.aiStyle     = -1;
            Projectile.DamageType  = DamageClass.Ranged;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            originalDamage       = Projectile.damage;
            Projectile.rotation  = Projectile.velocity.ToRotation() - MathHelper.PiOver2;
            Projectile.netUpdate = true;
        }

        // Only deal an impact hit during flight.
        public override bool? CanHitNPC(NPC target) => State == 0f ? null : false;

        // Scale down the impact to 20%; explosion later delivers the remaining 80%.
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.FinalDamage *= 0.2f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            stuckNPC               = target.whoAmI;
            stuckOffset            = Projectile.Center - target.Center;
            State                  = 1f;
            Timer                  = FuseTime;
            Projectile.velocity    = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.friendly    = false;   // no further collision hits
            Projectile.netUpdate   = true;
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (State != 0f) return false;
            State                  = 2f;
            Timer                  = FuseTime;
            Projectile.velocity    = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.friendly    = false;
            Projectile.netUpdate   = true;
            return false;   // don't kill on tile impact
        }

        public override void AI()
        {
            if (State == 0f)
                FlyingAI();
            else
                StuckAI();
        }

        private void FlyingAI()
        {
            Projectile.velocity.Y += 0.18f;
            Projectile.rotation    = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

            if (Main.rand.NextBool(5))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.BlueTorch, 0f, 0f, 100, default, 0.7f);
                d.noGravity  = true;
                d.velocity  *= 0.2f;
            }
        }

        private void StuckAI()
        {
            // Follow the NPC we're lodged in.
            if (State == 1f)
            {
                if (stuckNPC >= 0 && stuckNPC < Main.maxNPCs && Main.npc[stuckNPC].active)
                    Projectile.Center = Main.npc[stuckNPC].Center + stuckOffset;
                else
                    State = 2f;   // NPC died; stay in place
            }

            Timer--;

            // Blink between blue and red, faster as the fuse runs out.
            float t           = Timer / (float)FuseTime;
            int   blinkPeriod = (int)MathHelper.Lerp(6f, 20f, t);
            if (blinkPeriod < 1) blinkPeriod = 1;
            if ((int)Timer % blinkPeriod < 2)
                Lighting.AddLight(Projectile.Center, 1.5f, 0.2f, 0.2f);
            else
                Lighting.AddLight(Projectile.Center, 0.2f, 0.4f, 0.9f);

            if (Main.rand.NextBool(6))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.BlueTorch, 0f, 0f, 100, default, 0.6f);
                d.noGravity  = true;
                d.velocity  *= 0.1f;
            }

            if (Timer <= 0f)
            {
                hasExploded      = true;
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft)
        {
            // Explosion visuals.
            SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);
            // Tight inner burst: blue and yellow.
            for (int i = 0; i < 22; i++)
            {
                int dustType = (i % 2 == 0) ? DustID.BlueTorch : DustID.IceTorch;
                Color col    = (i % 2 == 0) ? new Color(80, 160, 255) : new Color(0, 240, 240);
                Dust d = Dust.NewDustDirect(
                    Projectile.Center - new Vector2(12), 24, 24,
                    dustType, 0f, 0f, 60, col, Main.rand.NextFloat(1.2f, 2.4f));
                d.velocity  = Main.rand.NextVector2Circular(6f, 6f);
                d.noGravity = true;
                d.fadeIn    = 0.5f;
            }
            // A handful of sparks that drift outward.
            for (int i = 0; i < 8; i++)
            {
                int dustType = (i % 2 == 0) ? DustID.BlueTorch : DustID.IceTorch;
                Color col    = (i % 2 == 0) ? new Color(100, 200, 255) : new Color(0, 255, 255);
                Dust d = Dust.NewDustDirect(
                    Projectile.Center - new Vector2(8), 16, 16,
                    dustType, 0f, 0f, 40, col, Main.rand.NextFloat(1.8f, 3.0f));
                d.velocity  = Main.rand.NextVector2Circular(9f, 9f);
                d.noGravity = false;
            }

            if (!hasExploded) return;

            // AoE explosion damage + DPS meter registration.
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int explosionDamage = System.Math.Max(1, (int)(originalDamage * 0.8f));
                Player owner = Main.player[Projectile.owner];
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                    if (Vector2.Distance(npc.Center, Projectile.Center) > ExplosionRadius) continue;

                    int dir = npc.Center.X > Projectile.Center.X ? 1 : -1;
                    int dealt = npc.StrikeNPC(npc.CalculateHitInfo(explosionDamage, dir, false,
                        Projectile.knockBack * 1.5f, DamageClass.Ranged));
                    if (Projectile.owner == Main.myPlayer)
                        owner.addDPS(dealt);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2   origin  = texture.Size() * 0.5f;

            // Short blue trail during flight.
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float   alpha    = 1f - i / (float)Projectile.oldPos.Length;
                Color   tCol     = new Color(80, 140, 255, 0) * alpha * 0.4f;
                Vector2 trailPos = Projectile.oldPos[i]
                                   + new Vector2(Projectile.width, Projectile.height) * 0.5f
                                   - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, tCol,
                    Projectile.oldRot[i], origin, Projectile.scale,
                    SpriteEffects.None, 0f);
            }

            // Main arrow sprite.
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(texture, drawPos, null, lightColor,
                Projectile.rotation, origin, Projectile.scale,
                SpriteEffects.None, 0f);

            return false;
        }
    }
}
