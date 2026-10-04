using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._PS.HilbertHotel;

/// <summary>
/// Describes a map file that can be loaded as a Hilbert's Hotel room.
/// </summary>
/// <remarks>
/// To add a new room to the check-in selection list, save a map with
/// <c>savemap</c> (a format 7 map file) and add a prototype of this type pointing at it.
/// See <c>.ai/guides/adding-hotel-rooms.md</c>.
/// </remarks>
[Prototype("hilbertHotelRoom")]
public sealed partial class HilbertHotelRoomPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Localized name shown in the check-in terminal's archetype list.
    /// </summary>
    [DataField(required: true)]
    public LocId Name { get; private set; }

    /// <summary>
    /// Path to the map file that is loaded for this room.
    /// </summary>
    [DataField(required: true)]
    public ResPath MapPath { get; private set; }

    /// <summary>
    /// Offset added to the resolved landing point. Mostly useful for maps whose
    /// landing marker/spawn point is not where you want arrivals to appear.
    /// </summary>
    [DataField]
    public Vector2 LandingOffset { get; private set; }

    /// <summary>
    /// Sort order in the selection list. Lower values come first.
    /// </summary>
    [DataField]
    public float Order { get; private set; }
}
