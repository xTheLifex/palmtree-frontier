using Content.Shared._PS.HilbertHotel;
using Content.Shared._PS.HilbertHotel.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Server._PS.HilbertHotel;

/// <summary>
/// Handles the in-room controller UI (locking, visibility and guest management).
/// </summary>
public sealed class HilbertHotelRoomControllerSystem : EntitySystem
{
    [Dependency] private readonly HilbertHotelSystem _hotel = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<HilbertHotelRoomControllerComponent>(HilbertHotelRoomControllerUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<HilbertHotelRoomActionMessage>(OnAction);
        });
    }

    private void OnOpened(EntityUid uid, HilbertHotelRoomControllerComponent component, BoundUIOpenedEvent args)
    {
        _hotel.UpdateControllerUi(uid, args.Actor);
    }

    private void OnAction(EntityUid uid, HilbertHotelRoomControllerComponent component, HilbertHotelRoomActionMessage args)
    {
        if (_hotel.TryRoomAction(uid, args.Actor, args.Action, args.TargetUser, out var error))
        {
            _hotel.UpdateControllerUi(uid, args.Actor);
            return;
        }

        if (error != null)
            _popup.PopupEntity(Loc.GetString(error.Value), args.Actor, args.Actor);
    }
}
