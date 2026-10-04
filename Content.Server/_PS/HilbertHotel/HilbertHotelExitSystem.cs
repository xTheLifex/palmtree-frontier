using Content.Shared._PS.HilbertHotel.Components;
using Content.Shared.Interaction;

namespace Content.Server._PS.HilbertHotel;

/// <summary>
/// Teleports players back out when they use a hotel room's exit door.
/// </summary>
public sealed class HilbertHotelExitSystem : EntitySystem
{
    [Dependency] private readonly HilbertHotelSystem _hotel = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HilbertHotelExitComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<HilbertHotelExitComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnActivate(EntityUid uid, HilbertHotelExitComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (_hotel.TryLeaveRoom(args.User))
            args.Handled = true;
    }

    private void OnInteractHand(EntityUid uid, HilbertHotelExitComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (_hotel.TryLeaveRoom(args.User))
            args.Handled = true;
    }
}
