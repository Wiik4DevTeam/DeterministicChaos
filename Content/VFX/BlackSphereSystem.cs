using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using System.Collections.Generic;
using System;

namespace DeterministicChaos.Content.VFX
{

    public struct BlackSphere
    {
        public Vector2 Position;       // World position
        public float Width;            // Horizontal diameter
        public float Height;           // Vertical diameter
        public float WidthPulse;       // Pulse amount for width (0 = no pulse)
        public float PulseSpeed;       // Speed of pulsing
        public float PulseOffset;      // Phase offset for pulsing
        public bool Active;            // Whether this sphere is active
        public int ID;                 // Unique identifier
        
        public BlackSphere(Vector2 position, float width, float height)
        {
            Position = position;
            Width = width;
            Height = height;
            WidthPulse = 0f;
            PulseSpeed = 1f;
            PulseOffset = 0f;
            Active = true;
            ID = 0;
        }
        
        public float GetCurrentWidth(float time)
        {
            if (WidthPulse <= 0)
                return Width;
            return Width + (float)Math.Sin(time * PulseSpeed + PulseOffset) * WidthPulse;
        }
    }
    public class BlackSphereSystem : ModSystem
    {
        private static List<BlackSphere> spheres = new List<BlackSphere>();
        private static int nextID = 1;
        private static float outlineThickness = 3f;
        private static float interiorBrightness = 0f; // 0 = black, 1 = white
        
        public static float OutlineThickness
        {
            get => outlineThickness;
            set => outlineThickness = Math.Max(1f, value);
        }
        
        public static float InteriorBrightness
        {
            get => interiorBrightness;
            set => interiorBrightness = MathHelper.Clamp(value, 0f, 1f);
        }

        public override void Load()
        {
            if (Main.dedServ)
                return;
                
            Terraria.On_Main.DrawDust += DrawSpheres;
        }

        public override void Unload()
        {
            Terraria.On_Main.DrawDust -= DrawSpheres;
            spheres?.Clear();
        }

        public static int AddSphere(Vector2 position, float width, float height, float widthPulse = 0f, float pulseSpeed = 1f)
        {
            int id = nextID++;
            spheres.Add(new BlackSphere
            {
                Position = position,
                Width = width,
                Height = height,
                WidthPulse = widthPulse,
                PulseSpeed = pulseSpeed,
                PulseOffset = Main.rand.NextFloat() * MathHelper.TwoPi,
                Active = true,
                ID = id
            });
            return id;
        }

        public static void UpdateSphere(int id, Vector2? position = null, float? width = null, float? height = null, 
            float? widthPulse = null, float? pulseSpeed = null)
        {
            for (int i = 0; i < spheres.Count; i++)
            {
                if (spheres[i].ID == id)
                {
                    var sphere = spheres[i];
                    if (position.HasValue) sphere.Position = position.Value;
                    if (width.HasValue) sphere.Width = width.Value;
                    if (height.HasValue) sphere.Height = height.Value;
                    if (widthPulse.HasValue) sphere.WidthPulse = widthPulse.Value;
                    if (pulseSpeed.HasValue) sphere.PulseSpeed = pulseSpeed.Value;
                    spheres[i] = sphere;
                    return;
                }
            }
        }

        public static void RemoveSphere(int id)
        {
            spheres.RemoveAll(s => s.ID == id);
        }

        public static void ClearAll()
        {
            spheres.Clear();
        }

        public static int Count => spheres.Count;

        private void DrawSpheres(Terraria.On_Main.orig_DrawDust orig, Main self)
        {
            if (!Main.dedServ && spheres.Count > 0)
            {
                DrawMergingSpheres();
            }
            
            orig(self);
        }

        private void DrawMergingSpheres()
        {
            if (spheres.Count == 0)
                return;

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            SpriteBatch spriteBatch = Main.spriteBatch;

            float time = (float)Main.GameUpdateCount * 0.05f;
            
            Color interiorColor = new Color(interiorBrightness, interiorBrightness, interiorBrightness);
            
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (var sphere in spheres)
            {
                if (!sphere.Active)
                    continue;
                    
                Vector2 screenPos = sphere.Position - Main.screenPosition;
                float currentWidth = sphere.GetCurrentWidth(time);
                
                DrawFilledEllipse(spriteBatch, screenPos, currentWidth + outlineThickness * 2, 
                    sphere.Height + outlineThickness * 2, Color.White);
            }
            
            foreach (var sphere in spheres)
            {
                if (!sphere.Active)
                    continue;
                    
                Vector2 screenPos = sphere.Position - Main.screenPosition;
                float currentWidth = sphere.GetCurrentWidth(time);
                
                DrawFilledEllipse(spriteBatch, screenPos, currentWidth, sphere.Height, interiorColor);
            }
            
            spriteBatch.End();
        }

        private void DrawFilledEllipse(SpriteBatch spriteBatch, Vector2 center, float width, float height, Color color)
        {
            
            int segments = Math.Max(16, (int)(Math.Max(width, height) / 4));
            float radiusX = width / 2f;
            float radiusY = height / 2f;
            
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            
            for (int y = -(int)radiusY; y <= (int)radiusY; y++)
            {
                float normalizedY = y / radiusY;
                if (Math.Abs(normalizedY) > 1f)
                    continue;
                    
                float xExtent = radiusX * (float)Math.Sqrt(1f - normalizedY * normalizedY);
                
                if (xExtent < 0.5f)
                    continue;
                
                Rectangle rect = new Rectangle(
                    (int)(center.X - xExtent),
                    (int)(center.Y + y),
                    (int)(xExtent * 2),
                    1
                );
                
                spriteBatch.Draw(pixel, rect, color);
            }
        }
    }
}
