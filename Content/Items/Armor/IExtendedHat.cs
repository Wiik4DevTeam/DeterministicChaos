using Microsoft.Xna.Framework;
using Terraria.DataStructures;

namespace DeterministicChaos.Content.Items.Armor
{
    // Interface for helmets that have an extension texture drawn above the head
    // Used for parts that extend beyond the normal head frame like horns or plumes
    public interface IExtendedHat
    {
        // Path to the extension texture
        string ExtensionTexture { get; }

        // Offset for positioning the extension sprite
        Vector2 ExtensionSpriteOffset(PlayerDrawSet drawInfo);
    }
}
