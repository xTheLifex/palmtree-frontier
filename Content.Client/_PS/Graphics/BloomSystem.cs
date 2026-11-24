using Robust.Client.Graphics;

namespace Content.Client._PS.Graphics;

/// <summary>
/// Palmtree: keeps the optional bloom overlay registered for the whole client session.
/// The overlay itself is inert while the Bloom setting is 0.
/// </summary>
public sealed partial class BloomSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    private BloomOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new();
        _overlayMan.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay(_overlay);
    }
}
