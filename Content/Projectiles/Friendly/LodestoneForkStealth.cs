using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    // Lodestone magnetizer, stealth strike from LodestoneFork.
    // Sticks to the first tile, NPC, or nearby friendly player it encounters,
    // then creates a blue/yellow electromagnetic field that pulls nearby
    // LodestoneForkSpam chunks toward it.
    //
    // ai[0] = state: 0=flying, 1=stuck to tile, 2=stuck to NPC, 3=stuck to player
    // ai[1] = whoAmI of the NPC/player when stuck (states 2 & 3)
    public class LodestoneForkStealth : ModProjectile
    {
        private const float MagnetRadius   = 380f;
        private const float MagnetStrength = 0.72f;
        private const float AttachRadius   = 28f;

        private int   State       { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float TargetIndex => ref Projectile.ai[1];

        public override void SetDefaults()
        {
            Projectile.width        = 20;
            Projectile.height       = 20;
            Projectile.friendly     = false; // magnetizer deals no direct damage
            Projectile.hostile      = false;
            Projectile.DamageType   = DamageClass.Throwing;
            Projectile.tileCollide  = true;
            Projectile.ignoreWater  = true;
            Projectile.penetrate    = -1;
            Projectile.timeLeft     = 720;  // 12 seconds
            Projectile.alpha        = 255;
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (State == 0)
            {
                State = 1;
                Projectile.velocity    = Vector2.Zero;
                Projectile.tileCollide = false;
                Projectile.netUpdate   = true;
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/ForkHit"), Projectile.Center);
            }
            return false;
        }

        public override void AI()
        {
            // Fade in
            Projectile.alpha = Math.Max(0, Projectile.alpha - 22);

            // Grace period before the owner can be attached to (counts up to 60)
            if (Projectile.localAI[0] < 60f)
                Projectile.localAI[0]++;

            switch (State)
            {
                case 0: // ── Flying ──────────────────────────────────────────────────────
                    Projectile.velocity.Y  = Math.Min(Projectile.velocity.Y + 0.18f, 14f);
                    Projectile.rotation    = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

                    // NPC attachment (owner-authoritative)
                    if (Main.myPlayer == Projectile.owner)
                    {
                        for (int i = 0; i < Main.maxNPCs; i++)
                        {
                            NPC npc = Main.npc[i];
                            if (!npc.active || !npc.CanBeChasedBy()) continue;
                            if (Vector2.Distance(Projectile.Center, npc.Center) < AttachRadius + npc.width * 0.45f)
                            {
                                AttachToNPC(i);
                                break;
                            }
                        }
                    }

                    // Player attachment, includes the owner after the 1-second grace period
                    if (State == 0 && Main.myPlayer == Projectile.owner)
                    {
                        for (int i = 0; i < Main.maxPlayers; i++)
                        {
                            Player p = Main.player[i];
                            if (!p.active || p.dead) continue;
                            if (i == Projectile.owner && Projectile.localAI[0] < 60f) continue;
                            if (Vector2.Distance(Projectile.Center, p.Center) < AttachRadius + p.width * 0.4f)
                            {
                                State        = 3;
                                TargetIndex  = i;
                                Projectile.velocity    = Vector2.Zero;
                                Projectile.tileCollide = false;
                                Projectile.netUpdate   = true;
                                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/ForkHit"), Projectile.Center);
                                break;
                            }
                        }
                    }
                    break;

                case 1: // ── Stuck to tile ────────────────────────────────────────────────
                    Projectile.velocity = Vector2.Zero;
                    break;

                case 2: // ── Stuck to NPC ─────────────────────────────────────────────────
                    int npcIdx = (int)TargetIndex;
                    if (npcIdx < 0 || npcIdx >= Main.maxNPCs) { State = 1; break; }
                    NPC targetNPC = Main.npc[npcIdx];
                    if (!targetNPC.active)
                    {
                        State            = 1;
                        Projectile.netUpdate = true;
                    }
                    else
                    {
                        Projectile.Center   = targetNPC.Center + new Vector2(0f, -targetNPC.height * 0.35f);
                        Projectile.velocity = Vector2.Zero;
                    }
                    break;

                case 3: // ── Stuck to player ──────────────────────────────────────────────
                    int plrIdx = (int)TargetIndex;
                    if (plrIdx < 0 || plrIdx >= Main.maxPlayers) { State = 1; break; }
                    Player targetPlayer = Main.player[plrIdx];
                    if (!targetPlayer.active || targetPlayer.dead)
                    {
                        State            = 1;
                        Projectile.netUpdate = true;
                    }
                    else
                    {
                        Projectile.Center   = targetPlayer.Center + new Vector2(0f, -targetPlayer.height * 0.35f);
                        Projectile.velocity = Vector2.Zero;
                    }
                    break;
            }

            // ── Magnetize nearby LodestoneForkSpam (all states) ─────────────────────
            if (Main.netMode == NetmodeID.SinglePlayer || Main.myPlayer == Projectile.owner)
            {
                int spamType = ModContent.ProjectileType<LodestoneForkSpam>();
                float radSq  = MagnetRadius * MagnetRadius;
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile spam = Main.projectile[i];
                    if (!spam.active || spam.type != spamType || spam.owner != Projectile.owner)
                        continue;
                    float distSq = Vector2.DistanceSquared(spam.Center, Projectile.Center);
                    if (distSq > radSq || distSq < 4f) continue;

                    Vector2 dir   = (Projectile.Center - spam.Center).SafeNormalize(Vector2.Zero);
                    spam.velocity += dir * MagnetStrength;
                    float speed   = spam.velocity.Length();
                    if (speed > 15f)
                        spam.velocity *= 15f / speed;
                }
            }

            // ── VFX ─────────────────────────────────────────────────────────────────
            if (Main.netMode != NetmodeID.Server && Projectile.alpha < 200)
            {
                // Blue/yellow electric sparks
                if (Main.rand.NextBool(3))
                {
                    int dustID = Main.rand.NextBool() ? DustID.BlueTorch : DustID.Electric;
                    Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(8f), 16, 16, dustID,
                        Scale: Main.rand.NextFloat(0.7f, 1.6f));
                    d.noGravity = true;
                    d.velocity  = Main.rand.NextVector2Circular(3f, 3f);
                }
            }

            // Strong magnetic light
            Lighting.AddLight(Projectile.Center, 0.28f, 0.55f, 1.0f);
        }

        private void AttachToNPC(int npcIndex)
        {
            State           = 2;
            TargetIndex     = npcIndex;
            Projectile.velocity    = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.netUpdate   = true;
            SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/ForkHit"), Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.alpha >= 250) return false;

            float opacity = 1f - Projectile.alpha / 255f;
            Texture2D tex   = TextureAssets.Projectile[Type].Value;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            SpriteBatch sb  = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // Pulsing aura behind the magnetizer
            float pulse = 0.35f + 0.18f * (float)Math.Sin(Main.GameUpdateCount * 0.14f);
            sb.Draw(tex, drawPos, null,
                new Color(0.2f, 0.55f, 1f, 0f) * pulse * opacity,
                Projectile.rotation, tex.Size() * 0.5f, 2.4f, SpriteEffects.None, 0f);

            // Lightning arcs to nearby spam chunks
            int spamType = ModContent.ProjectileType<LodestoneForkSpam>();
            float radSq  = MagnetRadius * MagnetRadius;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile spam = Main.projectile[i];
                if (!spam.active || spam.type != spamType || spam.owner != Projectile.owner) continue;
                float distSq = Vector2.DistanceSquared(spam.Center, Projectile.Center);
                if (distSq > radSq) continue;

                float dist    = (float)Math.Sqrt(distSq);
                float arcFade = (1f - dist / MagnetRadius) * opacity;
                DrawLightningArc(sb, pixel, Projectile.Center, spam.Center, arcFade, spam.whoAmI);
            }

            // Main sprite
            sb.Draw(tex, drawPos, null, lightColor * opacity,
                Projectile.rotation, tex.Size() * 0.5f, 1f, SpriteEffects.None, 0f);

            return false;
        }

        // Draws a jagged blue-outer / yellow-inner lightning arc between two world points.
        // Deterministic per projectile, flickers every 3 frames. No heap allocations.
        private static readonly Vector2[] _arcPts = new Vector2[10]; // Segments + 1
        private static void DrawLightningArc(SpriteBatch sb, Texture2D pixel,
            Vector2 from, Vector2 to, float alpha, int seed)
        {
            const int Segments = 9;
            float len = Vector2.Distance(from, to);
            if (len < 8f) return;

            Vector2 dir  = (to - from) / len;
            Vector2 perp = new Vector2(-dir.Y, dir.X);

            // Inline LCG hash, no heap allocation
            uint rngState = (uint)(seed * 6701 + (int)(Main.GameUpdateCount / 3) * 44179);

            _arcPts[0]        = from;
            _arcPts[Segments] = to;
            for (int i = 1; i < Segments; i++)
            {
                rngState = rngState * 1664525u + 1013904223u;
                float noise = ((rngState >> 1) / (float)int.MaxValue) - 1f; // -1..1
                Vector2 base1 = from + dir * (len * (i / (float)Segments));
                _arcPts[i] = base1 + perp * (noise * len * 0.11f);
            }

            Rectangle src       = new Rectangle(0, 0, 1, 1);
            Vector2 beamOrigin  = new Vector2(0f, 0.5f);
            for (int i = 0; i < Segments; i++)
            {
                Vector2 segA = _arcPts[i]     - Main.screenPosition;
                Vector2 segB = _arcPts[i + 1] - Main.screenPosition;
                Vector2 diff = segB - segA;
                float segLen = diff.Length();
                if (segLen < 1f) continue;
                float rot = diff.ToRotation();

                sb.Draw(pixel, segA, src,
                    new Color(0.2f, 0.58f, 1f, 0f) * alpha * 0.50f,
                    rot, beamOrigin, new Vector2(segLen, 2.8f), SpriteEffects.None, 0f);
                sb.Draw(pixel, segA, src,
                    new Color(1f, 0.92f, 0.35f, 0f) * alpha * 0.75f,
                    rot, beamOrigin, new Vector2(segLen, 1.1f), SpriteEffects.None, 0f);
            }
        }
    }
}
