using Content.Server.Fluids.EntitySystems;
using Content.Shared._PS.Slime;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._PS.Slime;

/// <summary>
/// Palmtree: applies the Water touch reaction to slimes that stay in contact with water - standing
/// in a water puddle or on a water tile. One short splash is survivable, prolonged contact is not.
/// </summary>
public sealed class SlimeWaterContactSystem : EntitySystem
{
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly ReactiveSystem _reactive = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private const float Interval = 1f;
    private const string WaterTileProto = "FloorWaterEntity";

    private float _accumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < Interval)
            return;

        _accumulator -= Interval;

        var query = EntityQueryEnumerator<SlimeWaterContactComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var contact, out var xform))
        {
            if (!TryComp<MobStateComponent>(uid, out var mob) || !_mobState.IsAlive(uid, mob))
                continue;

            // Not in direct contact with the floor while inside a container.
            if (_container.IsEntityInContainer(uid))
                continue;

            if (!_turf.TryGetTileRef(xform.Coordinates, out var tile) || !TileHasWater(tile.Value))
                continue;

            var water = new Solution();
            water.AddReagent("Water", contact.ContactQuantity);
            _reactive.DoEntityReaction(uid, water, ReactionMethod.Touch);
        }
    }

    private bool TileHasWater(TileRef tile)
    {
        // Water puddles (thrown water, spills, mop buckets...).
        if (_puddle.TryGetPuddle(tile, out var puddleUid) && HasWaterSolution(puddleUid))
            return true;

        // Water tiles (planet water, pools).
        if (!TryComp<MapGridComponent>(tile.GridUid, out var grid))
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(tile.GridUid, grid, tile.GridIndices);
        while (anchored.MoveNext(out var ent))
        {
            if (MetaData(ent.Value).EntityPrototype?.ID == WaterTileProto)
                return true;
        }

        return false;
    }

    private bool HasWaterSolution(EntityUid uid)
    {
        if (!TryComp<SolutionContainerManagerComponent>(uid, out var manager))
            return false;

        Entity<SolutionContainerManagerComponent?> ent = (uid, manager);
        foreach (var (_, soln) in _solution.EnumerateSolutions(ent))
        {
            if (soln.Comp.Solution.ContainsReagent("Water", null))
                return true;
        }

        return false;
    }
}
