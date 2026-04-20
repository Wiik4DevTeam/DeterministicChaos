using Microsoft.Xna.Framework;
using Terraria.DataStructures;
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
