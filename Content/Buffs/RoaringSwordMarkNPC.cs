using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Buffs
{
    public class RoaringSwordMarkGlobalNPC : GlobalNPC
    {
        public const int MaxStacks = 5;
        
        public override bool InstancePerEntity => true;
        
        public int markStacks = 0;
        
        public override void ResetEffects(NPC npc)
        {
            if (!npc.HasBuff(ModContent.BuffType<EyeDebuff>()))
            {
                markStacks = 0;
            }
        }
        
        public void AddMark(NPC npc, int stacks = 1)
        {
            markStacks = System.Math.Min(markStacks + stacks, MaxStacks);
            npc.AddBuff(ModContent.BuffType<EyeDebuff>(), 360);
        }
        
        public void ClearMarks(NPC npc)
        {
            markStacks = 0;
            int buffIndex = npc.FindBuffIndex(ModContent.BuffType<EyeDebuff>());
            if (buffIndex >= 0)
            {
                npc.DelBuff(buffIndex);
            }
        }
        
        public override void DrawEffects(NPC npc, ref Color drawColor)
        {
            if (markStacks > 0)
            {
                drawColor = Color.Lerp(drawColor, Color.White, 0.3f * (markStacks / (float)MaxStacks));
            }
        }
        
        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (markStacks <= 0)
                return;
                
            Texture2D eyeTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/Buffs/EyeDebuff").Value;
            if (eyeTexture == null)
                return;
            
            Vector2 drawPos = npc.Center - screenPos;
            drawPos.Y -= npc.height / 2f + 20f;
            
            float scale = 0.5f + (markStacks / (float)MaxStacks) * 0.5f;
            float alpha = 0.6f + (markStacks / (float)MaxStacks) * 0.4f;
            
            Vector2 origin = new Vector2(eyeTexture.Width / 2f, eyeTexture.Height / 2f);
            
            spriteBatch.Draw(
                eyeTexture,
                drawPos,
                null,
                Color.White * alpha,
                0f,
                origin,
                scale,
                SpriteEffects.None,
                0f
            );
            
            if (markStacks >= MaxStacks)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector2 offset = new Vector2(2f, 0f).RotatedBy(i * MathHelper.PiOver2);
                    spriteBatch.Draw(
                        eyeTexture,
                        drawPos + offset,
                        null,
                        Color.Purple * 0.5f,
                        0f,
                        origin,
                        scale * 1.1f,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }
    }
}
