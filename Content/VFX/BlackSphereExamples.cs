using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.VFX;

namespace DeterministicChaos.Content.VFX
{
    public static class BlackSphereExamples
    {
        public static int CreateStaticSphere(Vector2 worldPosition, float size)
        {
            return BlackSphereSystem.AddSphere(worldPosition, size, size);
        }

        public static int CreateOval(Vector2 worldPosition, float width, float height)
        {
            return BlackSphereSystem.AddSphere(worldPosition, width, height);
        }

        public static int CreatePulsingSphere(Vector2 worldPosition, float baseWidth, float height, 
            float pulseAmount = 20f, float pulseSpeed = 2f)
        {
            return BlackSphereSystem.AddSphere(worldPosition, baseWidth, height, pulseAmount, pulseSpeed);
        }

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
