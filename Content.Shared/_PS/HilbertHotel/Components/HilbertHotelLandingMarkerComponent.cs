using Robust.Shared.GameStates;

namespace Content.Shared._PS.HilbertHotel.Components;

/// <summary>
/// Marks the spot in a hotel room map where arriving players should be placed.
/// If a room map has no marker, a spawn point (latejoin preferred) is used,
/// then the center of the map's first grid.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HilbertHotelLandingMarkerComponent : Component
{
}
