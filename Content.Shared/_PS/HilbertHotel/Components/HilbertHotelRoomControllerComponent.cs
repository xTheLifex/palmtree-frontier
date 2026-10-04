using Robust.Shared.GameStates;

namespace Content.Shared._PS.HilbertHotel.Components;

/// <summary>
/// A console placed inside a hotel room. Lets the room's owner lock the room,
/// hide it from the public listing and manage trusted guests.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HilbertHotelRoomControllerComponent : Component
{
    /// <summary>
    /// Maximum number of trusted guests a room may have.
    /// </summary>
    [DataField]
    public int MaxGuests = 16;
}
