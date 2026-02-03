using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.DarkWorldEnemies
{
    public class DarkWorldEye : ModNPC
    {
        private const float VisualScale = 1.3f;
        private const float MaxSpeed = 5f;
        private const float Acceleration = 0.15f;

        private int attackCooldown = 0;
        private bool isDiving = false;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[NPC.type] = 1;
        }

        public override void SetDefaults()
        {
            NPC.width = 36;
            NPC.height = 36;
            NPC.damage = 145;
            NPC.defense = 12;
            NPC.lifeMax = 800;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.value = 0f;
            NPC.knockBackResist = 0.6f;
            NPC.noGravity = true;
            NPC.noTileCollide = false;
            NPC.scale = VisualScale;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
            {
                new FlavorTextBestiaryInfoElement("A watchful eye of the Dark World.")
            });
        }

        public override void AI()
        {
            Player target = Main.player[NPC.target];
            
            if (!NPC.HasValidTarget)
            {
                NPC.TargetClosest(true);
                target = Main.player[NPC.target];
            }

            if (!target.active || target.dead)
            {
                NPC.velocity.Y -= 0.1f;
                return;
            }

            attackCooldown--;

            Vector2 toPlayer = target.Center - NPC.Center;
            float distance = toPlayer.Length();

            if (isDiving)
            {
                DoDiveAttack(target, toPlayer);
            }
            else
            {
                DoOrbitBehavior(target, toPlayer, distance);
            }

            // The sprite faces left by default, so subtract Pi to align rotation
            NPC.rotation = NPC.velocity.ToRotation() - MathHelper.Pi;
        }

        private void DoOrbitBehavior(Player target, Vector2 toPlayer, float distance)
        {
            Vector2 predictedPos = target.Center + target.velocity * 20f;
            Vector2 toPredicted = predictedPos - NPC.Center;

            Vector2 orbitOffset = new Vector2(0, -150).RotatedBy(Main.GameUpdateCount * 0.02f);
            Vector2 orbitTarget = target.Center + orbitOffset;
            Vector2 toOrbit = orbitTarget - NPC.Center;

            if (toOrbit.Length() > 10f)
            {
                toOrbit.Normalize();
                NPC.velocity += toOrbit * Acceleration;
            }

            if (NPC.velocity.Length() > MaxSpeed)
            {
                NPC.velocity = Vector2.Normalize(NPC.velocity) * MaxSpeed;
            }

            if (attackCooldown <= 0 && distance < 300f)
            {
                isDiving = true;
                attackCooldown = 120;
                NPC.velocity = Vector2.Normalize(toPredicted) * (MaxSpeed * 2.8f);
            }
        }

        private void DoDiveAttack(Player target, Vector2 toPlayer)
        {
            if (attackCooldown <= 90)
            {
                isDiving = false;
                NPC.velocity *= 0.85f;
            }
        }



        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            return 0f;
        }
    }
}