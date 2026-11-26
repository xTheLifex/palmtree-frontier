using Content.Client.Humanoid;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;

namespace Content.Client._PS.Humanoid;

/// <summary>
/// Palmtree: some humanoid layers are ordered differently depending on the direction the sprite is
/// drawn in on the local client (south-facing genitals above hair/tails, side-facing tails under
/// hair, side-facing snouts above hair). Sprite directions are derived from world rotation plus the
/// camera rotation and raise no event, so this system watches for cardinal changes and rebuilds the
/// humanoid sprite, which re-applies the direction-aware marking layers in
/// <see cref="HumanoidAppearanceSystem"/>.
/// </summary>
public sealed class DirectionalLayerOrderSystem : EntitySystem
{
    [Dependency] private readonly HumanoidAppearanceSystem _appearance = default!;
    private readonly Dictionary<EntityUid, (RsiDirection Direction, bool SideFacing)> _lastDirection = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(EntityUid uid, HumanoidAppearanceComponent component, ComponentShutdown args)
    {
        _lastDirection.Remove(uid);
        _appearance.ForgetNorthOrganLayers(uid);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<HumanoidAppearanceComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var humanoid, out var sprite))
        {
            var direction = _appearance.GetScreenDirection(uid);
            var sideFacing = _appearance.IsSideFacing(uid);

            if (!_lastDirection.TryGetValue(uid, out var last)
                || last != (direction, sideFacing))
            {
                _lastDirection[uid] = (direction, sideFacing);

                // Without direction-sensitive markings the direction has no effect on layer order.
                if (NeedsDirectionalOrder(uid, humanoid))
                    _appearance.RefreshSprite(uid);
            }

            // Palmtree: front-mounted organs must stay behind the body in the north view. Another
            // system can re-apply a marking layer after the rebuild (for example a visibility
            // toggle), which would put a breast back over the body until the next rotation. This
            // check is idempotent and only does a few layer-map lookups while facing north.
            if (direction == RsiDirection.North)
                _appearance.EnsureNorthBreastsBehindBody(uid, humanoid, sprite, direction);
        }
    }

    private bool NeedsDirectionalOrder(EntityUid uid, HumanoidAppearanceComponent humanoid)
    {
        return _appearance.HasNorthOrganMarkings(uid, humanoid)
               || HasMarkings(humanoid, MarkingCategories.Tail)
               || HasMarkings(humanoid, MarkingCategories.TailExtras)
               || (humanoid.SnoutAboveHair
                   && (HasMarkings(humanoid, MarkingCategories.Snout)
                       || HasMarkings(humanoid, MarkingCategories.SnoutCover)));
    }

    private static bool HasMarkings(HumanoidAppearanceComponent humanoid, MarkingCategories category)
    {
        return humanoid.MarkingSet.TryGetCategory(category, out var markings) && markings.Count > 0;
    }
}
