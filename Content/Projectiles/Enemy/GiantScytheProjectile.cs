using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using DeterministicChaos.Content.NPCs.Bosses;
using DeterministicChaos.Content.Systems;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    // Massive scythe that descends from arena top to bottom over 5 seconds.
    // Screen fades to white during descent. On impact: drops all players to 1 HP,
    // triggers Jevil death (and loot), clears the fade.
    // ai[0] = Jevil NPC whoAmI
    public class GiantScytheProjectile : ModProjectile
    {
        public override string Texture => "DeterministicChaos/Content/Projectiles/Enemy/ScytheProjectileFalling";

        private const int DescentDuration = 300; // 5 seconds
        private const float DrawScale = 8f;
        private const float ShakeIntensity = 5f;
        private const float SpinSpeed = 0.08f;

        private int timer;
        private bool impacted;
        private float arenaTop;
        private float arenaBottom;
        private float arenaCenterX;

        public override void SetDefaults()
        {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = DescentDuration + 120;
        }

        public override bool? CanDamage() => false;

        public override void AI()
        {
            // Cache arena bounds on first tick
            if (timer == 0 && BossArenaSystem.ActiveBoxes.Count > 0)
            {
                var box = BossArenaSystem.ActiveBoxes[0];
                arenaTop = box.Center.Y - box.HalfHeight;
                arenaBottom = box.Center.Y + box.HalfHeight;
                arenaCenterX = box.Center.X;
                Projectile.Center = new Vector2(arenaCenterX, arenaTop);
            }

            // If Jevil despawned, clean up
            int jevilIndex = (int)Projectile.ai[0];
            if (jevilIndex < 0 || jevilIndex >= Main.maxNPCs || !Main.npc[jevilIndex].active)
            {
                Jevil.WhiteFadeAlpha = 0f;
                Projectile.Kill();
                return;
            }

            timer++;

            float progress = MathHelper.Clamp(timer / (float)DescentDuration, 0f, 1f);

            // Ease-in: starts slow, accelerates toward bottom
            float eased = progress * progress;

            // Update position
            float currentY = MathHelper.Lerp(arenaTop, arenaBottom, eased);
            Projectile.Center = new Vector2(arenaCenterX, currentY);

            // Spin
            Projectile.rotation += SpinSpeed;

            // Screen fades to white, ramps up to 90% opacity
            Jevil.WhiteFadeAlpha = MathHelper.Clamp(progress * 0.9f, 0f, 0.9f);

            // Screen shake, intensifies as scythe approaches bottom
            if (Main.netMode != NetmodeID.Server)
            {
                float shakeMagnitude = progress * 4f;
                Main.screenPosition += new Vector2(
                    Main.rand.NextFloat(-shakeMagnitude, shakeMagnitude),
                    Main.rand.NextFloat(-shakeMagnitude, shakeMagnitude)
                );
            }

            if (timer >= DescentDuration && !impacted)
            {
                impacted = true;
                Impact();
            }
        }

        private void Impact()
        {
            // Stop white fade
            Jevil.WhiteFadeAlpha = 0f;

            // Big impact sound
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.5f, Pitch = -0.5f }, Projectile.Center);

            // Dust burst
            for (int i = 0; i < 60; i++)
            {
                Vector2 dustVel = new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-10f, -2f));
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Cloud, dustVel.X, dustVel.Y, 100, Color.White, 2f);
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // Damage all arena players to 1 HP
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player p = Main.player[i];
                    if (!p.active || p.dead) continue;
                    if (BossArenaSystem.IsPlayerLockedIn(i) && p.statLife > 1)
                    {
                        int damage = p.statLife - 1;
                        p.Hurt(Terraria.DataStructures.PlayerDeathReason.ByCustomReason(p.name + " was consumed by chaos."), damage, 0, armorPenetration: 9999f);
                    }
                }

                // Kill Jevil NPC to trigger loot drop
                int jevilIndex = (int)Projectile.ai[0];
                if (jevilIndex >= 0 && jevilIndex < Main.maxNPCs)
                {
                    NPC jevil = Main.npc[jevilIndex];
                    if (jevil.active && jevil.ModNPC is Jevil jevilMod)
                    {
                        // Move Jevil to impact point so loot drops here
                        jevil.Center = Projectile.Center;
                        jevilMod.allowFinalDeath = true;
                        jevil.dontTakeDamage = false;
                        jevil.life = 0;
                        jevil.checkDead();
                        jevil.netUpdate = true;
                    }
                }
            }

            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 pos = Projectile.Center - Main.screenPosition;

            // Visual shake on the scythe itself, intensifies over time
            float progress = MathHelper.Clamp(timer / (float)DescentDuration, 0f, 1f);
            float shake = ShakeIntensity * (0.3f + 0.7f * progress);
            pos += new Vector2(
                Main.rand.NextFloat(-shake, shake),
                Main.rand.NextFloat(-shake, shake)
            );

            // Glow behind
            Color glowColor = Color.White * 0.4f;
            Main.EntitySpriteDraw(tex, pos, null, glowColor, Projectile.rotation, origin, DrawScale * 1.3f, SpriteEffects.None, 0);

            // Main scythe
            Main.EntitySpriteDraw(tex, pos, null, Color.White, Projectile.rotation, origin, DrawScale, SpriteEffects.None, 0);

            return false;
        }
    }

    // Draws a full-screen white overlay when Jevil.WhiteFadeAlpha > 0.
    public class JevilWhiteFadeSystem : ModSystem
    {
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (Jevil.WhiteFadeAlpha <= 0f)
                return;

            int mouseIndex = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
            if (mouseIndex >= 0)
            {
                layers.Insert(mouseIndex, new LegacyGameInterfaceLayer(
                    "DeterministicChaos: White Fade",
                    delegate
                    {
                        Main.spriteBatch.Draw(
                            TextureAssets.MagicPixel.Value,
                            new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                            Color.White * Jevil.WhiteFadeAlpha
                        );
                        return true;
                    },
                    InterfaceScaleType.UI
                ));
            }
        }
    }
}
