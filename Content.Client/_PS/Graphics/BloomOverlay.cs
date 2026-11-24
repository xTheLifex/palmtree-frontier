using Content.Shared._PS.CCVar;
using Content.Shared.Eye.Blinding.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._PS.Graphics;

/// <summary>
/// Palmtree: optional soft bloom, driven by the Bloom graphics option.
/// The shader samples the light render target so lights glow and bleed; unlike the welding
/// cataracts effect it never distorts the screen.
/// </summary>
public sealed partial class BloomOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> BloomShader = "PSBloom";

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IConfigurationManager _configManager = default!;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance _shader;
    private float _strength;

    public BloomOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(BloomShader).InstanceUnique();
        _configManager.OnValueChanged(PSCVars.Bloom, OnBloomChanged, invokeImmediately: true);
    }

    private void OnBloomChanged(float value)
    {
        _strength = Math.Clamp(value, 0f, PSCVars.BloomMax);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_strength <= 0f)
            return false;

        var playerEntity = _playerManager.LocalSession?.AttachedEntity;
        if (playerEntity == null)
            return false;

        if (!_entityManager.TryGetComponent<EyeComponent>(playerEntity.Value, out var eyeComp)
            || args.Viewport.Eye != eyeComp.Eye)
            return false;

        // Only draw for in-game player mobs, and let the blindness overlay own the screen when
        // the player cannot see at all.
        if (!_entityManager.TryGetComponent<BlindableComponent>(playerEntity.Value, out var blindable)
            || blindable.IsBlind)
            return false;

        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var zoom = 1f;
        if (_entityManager.TryGetComponent<EyeComponent>(_playerManager.LocalSession?.AttachedEntity, out var eyeComponent))
            zoom = eyeComponent.Zoom.X;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("LIGHT_TEXTURE", args.Viewport.LightRenderTarget.Texture);
        _shader.SetParameter("BloomStrength", _strength);
        _shader.SetParameter("Zoom", zoom);

        var worldHandle = args.WorldHandle;
        worldHandle.UseShader(_shader);
        worldHandle.DrawRect(args.WorldBounds, Color.White);
        worldHandle.UseShader(null);
    }
}
