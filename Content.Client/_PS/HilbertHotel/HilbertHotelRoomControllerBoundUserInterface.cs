using Content.Shared._PS.HilbertHotel;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Client._PS.HilbertHotel;

[UsedImplicitly]
public sealed class HilbertHotelRoomControllerBoundUserInterface : BoundUserInterface
{
    private HilbertHotelRoomControllerWindow? _window;

    public HilbertHotelRoomControllerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<HilbertHotelRoomControllerWindow>();
        _window.ActionPressed += (action, target) => SendMessage(new HilbertHotelRoomActionMessage(action, target));
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (_window != null && message is HilbertHotelRoomControllerStateMessage state)
            _window.UpdateState(state);
    }
}
