using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.VFX;

namespace DeterministicChaos.Content.VFX
{
    /// <summary>
    /// Example usage of the BlackSphereSystem.
    /// Shows how to create, update, and animate black spheres with merging outlines.
    /// </summary>
    public static class BlackSphereExamples
    {
        /// <summary>
        /// Creates a simple static sphere at a position.
        /// </summary>
        public static int CreateStaticSphere(Vector2 worldPosition, float size)
        {
            return BlackSphereSystem.AddSphere(worldPosition, size, size);
        }

        /// <summary>
        /// Creates an oval sphere (ellipse) at a position.
        /// </summary>
        public static int CreateOval(Vector2 worldPosition, float width, float height)
        {
            return BlackSphereSystem.AddSphere(worldPosition, width, height);
        }

        /// <summary>
        /// Creates a pulsing sphere that expands and contracts horizontally.
        /// </summary>
        /// <param name="worldPosition">World position</param>
        /// <param name="baseWidth">Base width of the sphere</param>
        /// <param name="height">Height of the sphere</param>
        /// <param name="pulseAmount">How much the width changes (pixels)</param>
        /// <param name="pulseSpeed">Speed of pulsing (higher = faster)</param>
        public static int CreatePulsingSphere(Vector2 worldPosition, float baseWidth, float height, 
            float pulseAmount = 20f, float pulseSpeed = 2f)
        {
            return BlackSphereSystem.AddSphere(worldPosition, baseWidth, height, pulseAmount, pulseSpeed);
        }

        /// <summary>
        /// Creates a large pulsing oval effect.
        /// </summary>
        public static int CreateLargePulsingOval(Vector2 worldPosition)
        {
            return BlackSphereSystem.AddSphere(
                position: worldPosition,
                width: 200f,
                height: 300f,
                widthPulse: 50f,
                pulseSpeed: 1.5f
            );
        }

        /// <summary>
        /// Creates multiple connected spheres in a line.
        /// Since they overlap, they will merge visually.
        /// </summary>
        public static int[] CreateConnectedChain(Vector2 startPosition, int count, float spacing, float sphereSize)
        {
            int[] ids = new int[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 pos = startPosition + new Vector2(i * spacing, 0);
                ids[i] = BlackSphereSystem.AddSphere(pos, sphereSize, sphereSize);
            }
            return ids;
        }

        /// <summary>
        /// Creates a vertical blob chain.
        /// </summary>
        public static int[] CreateVerticalBlob(Vector2 startPosition, int count, float spacing, float sphereSize)
        {
            int[] ids = new int[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 pos = startPosition + new Vector2(0, i * spacing);
                ids[i] = BlackSphereSystem.AddSphere(pos, sphereSize, sphereSize);
            }
            return ids;
        }

        /// <summary>
        /// Creates a ring of spheres that will merge together.
        /// </summary>
        public static int[] CreateMergingRing(Vector2 center, float radius, int sphereCount, float sphereSize)
        {
            int[] ids = new int[sphereCount];
            float angleStep = MathHelper.TwoPi / sphereCount;
            
            for (int i = 0; i < sphereCount; i++)
            {
                float angle = i * angleStep;
                Vector2 pos = center + new Vector2(
                    (float)System.Math.Cos(angle) * radius,
                    (float)System.Math.Sin(angle) * radius
                );
                ids[i] = BlackSphereSystem.AddSphere(pos, sphereSize, sphereSize);
            }
            return ids;
        }
    }
}
