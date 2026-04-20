using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using ReLogic.Content;
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

namespace DeterministicChaos.Content.VFX
{
    // A visual-only projectile used for TitanStar shard effects.
    // ai[0] = mode: 0 = outward (burst), 1 = inward (converge)
    // ai[1] = sprite variant (0-2 for StarShard1-3)
    // localAI[0] = lifetime timer
    public class StarShardParticle : ModProjectile
    {
        public override string Texture => "DeterministicChaos/Content/NPCs/Bosses/StarShard1";

        private static Asset<Texture2D>[] shardTextures;

        private const float LIFETIME = 45f; // Ticks

        private bool IsOutward => Projectile.ai[0] == 0f;
        private int SpriteVariant => (int)Projectile.ai[1];

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = 1;
        }

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = (int)LIFETIME;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
        }

        public override void AI()
        {
            Projectile.localAI[0]++;
            float progress = Projectile.localAI[0] / LIFETIME;

            if (IsOutward)
            {
                // Outward: starts full size, shrinks to nothing
                Projectile.scale = MathHelper.Lerp(1f, 0f, progress);
            }
            else
            {
                // Inward: starts at nothing, grows to full size
                Projectile.scale = MathHelper.Lerp(0f, 1f, progress);
            }

            // Rotate based on velocity direction
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (shardTextures == null)
            {
                shardTextures = new Asset<Texture2D>[3];
                for (int i = 0; i < 3; i++)
                    shardTextures[i] = ModContent.Request<Texture2D>(
                        $"DeterministicChaos/Content/NPCs/Bosses/StarShard{i + 1}", AssetRequestMode.ImmediateLoad);
            }

            int variant = (int)MathHelper.Clamp(SpriteVariant, 0, 2);
            Texture2D tex = shardTextures[variant].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);

            // Full opacity always, no transparency changes
            Color drawColor = Color.White;

            Main.EntitySpriteDraw(tex, drawPos, null, drawColor, Projectile.rotation,
                origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }

        // Spawns outward-bursting shards in 4 cardinal directions from a center point.
        public static void SpawnOutwardBurst(IEntitySource source, Vector2 center, float speed = 6f)
        {
            Vector2[] directions = { -Vector2.UnitY, Vector2.UnitY, -Vector2.UnitX, Vector2.UnitX }; // N, S, W, E

            for (int d = 0; d < directions.Length; d++)
            {
                for (int v = 0; v < 3; v++) // 3 sprite variants
                {
                    // Slight offset per variant so they don't perfectly overlap
                    Vector2 vel = directions[d] * (speed + v * 0.7f);
                    Projectile.NewProjectile(source, center, vel,
                        ModContent.ProjectileType<StarShardParticle>(),
                        0, 0f, Main.myPlayer,
                        ai0: 0f,  // outward
                        ai1: v);  // variant
                }
            }
        }

        // Spawns inward-converging shards from 4 cardinal directions toward a center point.
        public static void SpawnInwardConverge(IEntitySource source, Vector2 center, float distance = 200f)
        {
            Vector2[] directions = { -Vector2.UnitY, Vector2.UnitY, -Vector2.UnitX, Vector2.UnitX }; // N, S, W, E
            float speed = distance / LIFETIME; // Arrive at center exactly when lifetime ends

            for (int d = 0; d < directions.Length; d++)
            {
                for (int v = 0; v < 3; v++)
                {
                    Vector2 spawnPos = center + directions[d] * (distance + v * 8f);
                    Vector2 vel = -directions[d] * (speed + v * (8f / LIFETIME));
                    Projectile.NewProjectile(source, spawnPos, vel,
                        ModContent.ProjectileType<StarShardParticle>(),
                        0, 0f, Main.myPlayer,
                        ai0: 1f,  // inward
                        ai1: v);  // variant
                }
            }
        }
    }
}
