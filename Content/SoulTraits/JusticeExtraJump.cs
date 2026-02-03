using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits
{
    public class JusticeExtraJump : ExtraJump
    {
        public override Position GetDefaultPosition() => new After(CloudInABottle);

        public override float GetDurationMultiplier(Player player)
        {
            return 1.5f;
        }

        public override void UpdateHorizontalSpeeds(Player player)
        {
            player.runAcceleration *= 1.5f;
            player.maxRunSpeed *= 1.25f;
        }

        public override void OnStarted(Player player, ref bool playSound)
        {
            // Play jump sound
            playSound = true;

            // Yellow dust burst
            for (int i = 0; i < 15; i++)
            {
                Dust dust = Dust.NewDustDirect(
                    player.position,
                    player.width,
                    player.height,
                    DustID.YellowTorch,
                    player.velocity.X * 0.5f,
                    player.velocity.Y * 0.5f,
                    100,
                    default,
                    1.5f
                );
                dust.noGravity = true;
                dust.velocity *= 2f;
            }
        }

        public override void ShowVisuals(Player player)
        {
            // Continuous yellow dust while jumping
            Dust dust = Dust.NewDustDirect(
                player.position + new Vector2(Main.rand.Next(player.width), player.height),
                4,
                4,
                DustID.YellowTorch,
                0f,
                0f,
                100,
                default,
                1.2f
            );
            dust.noGravity = true;
            dust.velocity.Y = -2f;
            dust.velocity.X = Main.rand.NextFloat(-1f, 1f);
        }
    }

    public class JusticeExtraJump2 : ExtraJump
    {
        public override Position GetDefaultPosition() => new After(SandstormInABottle);

        public override float GetDurationMultiplier(Player player)
        {
            return 1.25f;
        }

        public override void UpdateHorizontalSpeeds(Player player)
        {
            player.runAcceleration *= 1.25f;
            player.maxRunSpeed *= 1.15f;
        }

        public override void OnStarted(Player player, ref bool playSound)
        {
            playSound = true;

            // Brighter yellow burst for second extra jump
            for (int i = 0; i < 12; i++)
            {
                Dust dust = Dust.NewDustDirect(
                    player.position,
                    player.width,
                    player.height,
                    DustID.GoldFlame,
                    player.velocity.X * 0.5f,
                    player.velocity.Y * 0.5f,
                    100,
                    default,
                    1.3f
                );
                dust.noGravity = true;
                dust.velocity *= 1.5f;
            }
        }

        public override void ShowVisuals(Player player)
        {
            Dust dust = Dust.NewDustDirect(
                player.position + new Vector2(Main.rand.Next(player.width), player.height),
                4,
                4,
                DustID.GoldFlame,
                0f,
                0f,
                100,
                default,
                1f
            );
            dust.noGravity = true;
            dust.velocity.Y = -1.5f;
            dust.velocity.X = Main.rand.NextFloat(-0.8f, 0.8f);
        }
    }
}
