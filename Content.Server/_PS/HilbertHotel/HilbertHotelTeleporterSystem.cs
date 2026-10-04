using Content.Shared._PS.HilbertHotel;
using Content.Shared._PS.HilbertHotel.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Server._PS.HilbertHotel;

/// <summary>
/// Handles the check-in terminal's UI.
/// </summary>
public sealed class HilbertHotelTeleporterSystem : EntitySystem
{
    [Dependency] private readonly HilbertHotelSystem _hotel = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<HilbertHotelTeleporterComponent>(HilbertHotelTeleporterUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<HilbertHotelCheckInMessage>(OnCheckIn);
            subs.Event<HilbertHotelRefreshMessage>(OnRefresh);
            subs.Event<HilbertHotelDeleteRoomMessage>(OnDeleteRoom);
        });
    }

    private void OnOpened(EntityUid uid, HilbertHotelTeleporterComponent component, BoundUIOpenedEvent args)
    {
        _hotel.UpdateTeleporterUi(uid, args.Actor);
    }

    private void OnCheckIn(EntityUid uid, HilbertHotelTeleporterComponent component, HilbertHotelCheckInMessage args)
    {
        if (!_hotel.TryCheckIn(uid, args.Actor, args.Code, args.Template))
            return;

        _ui.CloseUi(uid, HilbertHotelTeleporterUiKey.Key, args.Actor);
    }

    private void OnRefresh(EntityUid uid, HilbertHotelTeleporterComponent component, HilbertHotelRefreshMessage args)
    {
        _hotel.UpdateTeleporterUi(uid, args.Actor);
    }

    private void OnDeleteRoom(EntityUid uid, HilbertHotelTeleporterComponent component, HilbertHotelDeleteRoomMessage args)
    {
        if (!_hotel.TryDeleteRoomByOwner(args.Actor, args.Code))
            return;

        _hotel.UpdateTeleporterUi(uid, args.Actor);
    }
}
