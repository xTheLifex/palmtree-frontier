using Content.Shared._PS.HilbertHotel;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Client._PS.HilbertHotel;

[UsedImplicitly]
public sealed class HilbertHotelTeleporterBoundUserInterface : BoundUserInterface
{
    private HilbertHotelTeleporterWindow? _window;

    public HilbertHotelTeleporterBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<HilbertHotelTeleporterWindow>();
        _window.CheckInPressed += (code, template) => SendMessage(new HilbertHotelCheckInMessage(code, template));
        _window.DeletePressed += code => SendMessage(new HilbertHotelDeleteRoomMessage(code));
        _window.RefreshPressed += () => SendMessage(new HilbertHotelRefreshMessage());
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (_window != null && message is HilbertHotelTeleporterStateMessage state)
            _window.UpdateState(state);
    }
}
