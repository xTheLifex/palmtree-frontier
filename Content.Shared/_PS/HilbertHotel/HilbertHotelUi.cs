using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._PS.HilbertHotel;

public static class HilbertHotelConstants
{
    public const int MinRoomCode = 1;
    public const int MaxRoomCode = 999999;
}

[Serializable, NetSerializable]
public enum HilbertHotelTeleporterUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum HilbertHotelRoomControllerUiKey : byte
{
    Key,
}

/// <summary>
/// Who is allowed to enter a room with the matching code.
/// </summary>
[Serializable, NetSerializable]
public enum HilbertHotelRoomStatus : byte
{
    /// <summary>Anyone who knows the code may enter.</summary>
    Open,

    /// <summary>Only the owner and trusted guests may enter.</summary>
    GuestsOnly,

    /// <summary>Only the owner may enter (the room is locked).</summary>
    Locked,
}

/// <summary>
/// Actions the room controller UI can request.
/// </summary>
[Serializable, NetSerializable]
public enum HilbertHotelRoomAction : byte
{
    CycleStatus,
    ToggleVisibility,
    AddGuest,
    RemoveGuest,
    ClearGuests,
    TransferOwnership,
    DeleteRoom,
}

/// <summary>
/// One selectable room archetype in the check-in terminal.
/// </summary>
[Serializable, NetSerializable]
public sealed class HilbertHotelRoomTemplateEntry
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public float Order;

    public HilbertHotelRoomTemplateEntry()
    {
    }

    public HilbertHotelRoomTemplateEntry(string id, string name, float order)
    {
        Id = id;
        Name = name;
        Order = order;
    }
}

/// <summary>
/// A joinable room shown in the check-in terminal's public listing.
/// </summary>
[Serializable, NetSerializable]
public sealed class HilbertHotelRoomEntry
{
    public int Code;
    public string Name = string.Empty;
    public string OwnerName = string.Empty;
    public HilbertHotelRoomStatus Status;
    public int Occupants;
    public bool CanJoin;
    public bool IsOwner;

    public HilbertHotelRoomEntry()
    {
    }

    public HilbertHotelRoomEntry(
        int code,
        string name,
        string ownerName,
        HilbertHotelRoomStatus status,
        int occupants,
        bool canJoin,
        bool isOwner)
    {
        Code = code;
        Name = name;
        OwnerName = ownerName;
        Status = status;
        Occupants = occupants;
        CanJoin = canJoin;
        IsOwner = isOwner;
    }
}

[Serializable, NetSerializable]
public sealed class HilbertHotelGuestEntry
{
    public NetUserId UserId;
    public string Name = string.Empty;

    public HilbertHotelGuestEntry()
    {
    }

    public HilbertHotelGuestEntry(NetUserId userId, string name)
    {
        UserId = userId;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class HilbertHotelOccupantEntry
{
    public string Name = string.Empty;
    public NetUserId UserId;
    public bool Trusted;
    public bool IsOwner;

    public HilbertHotelOccupantEntry()
    {
    }

    public HilbertHotelOccupantEntry(string name, NetUserId userId, bool trusted, bool isOwner)
    {
        Name = name;
        UserId = userId;
        Trusted = trusted;
        IsOwner = isOwner;
    }
}

/// <summary>
/// Personalized state of a check-in terminal, sent only to the player looking at it.
/// </summary>
[Serializable, NetSerializable]
public sealed class HilbertHotelTeleporterStateMessage : BoundUserInterfaceMessage
{
    public List<HilbertHotelRoomTemplateEntry> Templates = new();
    public List<HilbertHotelRoomEntry> Rooms = new();
    public int RoomCount;
    public int MaxRooms;
}

/// <summary>
/// Personalized state of a room controller, sent only to the player looking at it.
/// </summary>
[Serializable, NetSerializable]
public sealed class HilbertHotelRoomControllerStateMessage : BoundUserInterfaceMessage
{
    public int Code;
    public HilbertHotelRoomStatus Status;
    public bool Visible;
    public bool IsOwner;
    public string OwnerName = string.Empty;
    public List<HilbertHotelGuestEntry> Guests = new();
    public List<HilbertHotelOccupantEntry> Occupants = new();
}

/// <summary>
/// Request to enter (or create) the room with the given code.
/// </summary>
[Serializable, NetSerializable]
public sealed class HilbertHotelCheckInMessage : BoundUserInterfaceMessage
{
    public int Code;
    public string? Template;

    public HilbertHotelCheckInMessage()
    {
    }

    public HilbertHotelCheckInMessage(int code, string? template)
    {
        Code = code;
        Template = template;
    }
}

/// <summary>
/// Request to re-send the terminal's room listing.
/// </summary>
[Serializable, NetSerializable]
public sealed class HilbertHotelRefreshMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Request from a room's owner to delete it from a check-in terminal.
/// </summary>
[Serializable, NetSerializable]
public sealed class HilbertHotelDeleteRoomMessage : BoundUserInterfaceMessage
{
    public int Code;

    public HilbertHotelDeleteRoomMessage()
    {
    }

    public HilbertHotelDeleteRoomMessage(int code)
    {
        Code = code;
    }
}

/// <summary>
/// Request to modify a room through its controller.
/// </summary>
[Serializable, NetSerializable]
public sealed class HilbertHotelRoomActionMessage : BoundUserInterfaceMessage
{
    public HilbertHotelRoomAction Action;
    public NetUserId TargetUser;

    public HilbertHotelRoomActionMessage()
    {
    }

    public HilbertHotelRoomActionMessage(HilbertHotelRoomAction action, NetUserId targetUser = default)
    {
        Action = action;
        TargetUser = targetUser;
    }
}
