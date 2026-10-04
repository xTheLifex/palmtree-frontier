using Robust.Shared.GameStates;

namespace Content.Shared._PS.HilbertHotel.Components;

/// <summary>
/// A fake door inside a hotel room. Interacting with it teleports the user back
/// to the location they checked in from.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HilbertHotelExitComponent : Component
{
}
