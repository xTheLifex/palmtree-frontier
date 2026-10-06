using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.Shared._PS.Slime;

/// <summary>
/// Palmtree: marks a slime that takes periodic water damage while standing in water (puddles, water
/// tiles). The per-tick damage comes from applying <see cref="ContactQuantity"/> units of the Water
/// reagent as a touch reaction, so it scales with the slime water weakness.
/// </summary>
[RegisterComponent]
public sealed partial class SlimeWaterContactComponent : Component
{
    /// <summary>
    /// Units of Water applied as a touch reaction every second while in contact with water.
    /// </summary>
    [DataField]
    public FixedPoint2 ContactQuantity = FixedPoint2.New(10);
}
