using System.Numerics;
using Content.Server.Decals;
using Content.Shared._PS.Organs;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._PS.Organs;

/// <summary>
/// Drains stored semen (<see cref="DrippingCumComponent"/>) into SPLURT-style droplet decals, one
/// unit per second while the mob is bottomless; the drip pauses while they wear a jumpsuit.
/// </summary>
/// <remarks>
/// Drips only ever make the small droplet sprites (<c>SemenDrip1..5</c>). The full cum decals
/// (<c>SemenPuddle1..4</c>) are reserved for actual ejaculation via <see cref="SpawnCumDecals"/>.
/// Every drop is a new decal placed where the mob actually stands (with a little scatter), so the
/// floor builds up a mess instead of one decal being upgraded. Decals are cleanable (space cleaner).
/// </remarks>
public sealed class DrippingCumSystem : EntitySystem
{
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly TimeSpan DripInterval = TimeSpan.FromSeconds(1);

    /// <summary>Droplet decal ladder. Drips never escalate to the full puddle decals.</summary>
    private static readonly string[] DripDecals =
    [
        "SemenDrip1",
        "SemenDrip2",
        "SemenDrip3",
        "SemenDrip4",
        "SemenDrip5",
    ];

    /// <summary>How far a drip droplet can scatter from the mob's exact position (barely any).</summary>
    private const float DripScatter = 0.05f;

    /// <summary>How far an ejaculation decal can scatter (a proper mess).</summary>
    private const float CumScatter = 0.35f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DrippingCumComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextDrip)
                continue;

            comp.NextDrip = now + DripInterval;

            // Sandstorm pauses the drip while the groin is covered; this fork simplifies that to jumpsuits.
            if (!_inventory.TryGetSlotEntity(uid, "jumpsuit", out _) && TryDrip(uid, comp))
            {
                comp.Amount -= 1;
                comp.Dripped += 1;
                Dirty(uid, comp);
            }

            if (comp.Amount <= 0)
                RemCompDeferred<DrippingCumComponent>(uid);
        }
    }

    /// <summary>Drops one more droplet under the mob. Returns false when it cannot (e.g. in space).</summary>
    private bool TryDrip(EntityUid uid, DrippingCumComponent comp)
    {
        var stage = Math.Clamp((int) (comp.Dripped / 2.0), 0, DripDecals.Length - 1);
        return TryPlaceDecal(uid, DripDecals[stage], DripScatter);
    }

    /// <summary>
    /// Ejaculation decals: drops a full SPLURT cum decal where the target stands, scaled by how much
    /// semen the penis organ produces. Never replaces an existing decal.
    /// </summary>
    public void SpawnCumDecals(EntityUid uid, double volume)
    {
        var decalId = volume switch
        {
            >= 75 => "SemenPuddle4",
            >= 50 => "SemenPuddle3",
            >= 25 => "SemenPuddle2",
            _ => "SemenPuddle1",
        };

        TryPlaceDecal(uid, decalId, CumScatter);
    }

    /// <summary>
    /// Female ejaculation decals: drops a random SPLURT fem decal where the mob stands.
    /// Never replaces an existing decal.
    /// </summary>
    public void SpawnFemDecals(EntityUid uid)
    {
        var decalId = _random.Pick(new[] { "FemPuddle1", "FemPuddle2", "FemPuddle3", "FemPuddle4" });
        TryPlaceDecal(uid, decalId, CumScatter);
    }

    /// <summary>Adds a decal centered on the mob's position with scatter; existing decals are kept.</summary>
    private bool TryPlaceDecal(EntityUid uid, string decalId, float scatter)
    {
        var xform = Transform(uid);

        if (xform.GridUid is not { } grid)
            return false;

        // Decals are drawn with their bottom-left corner at the stored coordinate, while mob
        // sprites are centered on their transform. Shift by half a tile so the decal center lands
        // on the character instead of up-right of them (near the head).
        var pos = xform.LocalPosition;
        var centered = pos - new Vector2(0.5f, 0.5f);
        var scattered = centered +
                        new Vector2(_random.NextFloat(-scatter, scatter), _random.NextFloat(-scatter, scatter));

        if (_decals.TryAddDecal(decalId, new EntityCoordinates(grid, scattered), out _, cleanable: true))
            return true;

        // Scatter (or the shift itself, when standing on a tile edge) can land on space; retry
        // without scatter, then fall back to the mob's tile center so a drop is never lost.
        if (_decals.TryAddDecal(decalId, new EntityCoordinates(grid, centered), out _, cleanable: true))
            return true;

        var tileCorner = new Vector2(MathF.Floor(pos.X), MathF.Floor(pos.Y));
        return _decals.TryAddDecal(decalId, new EntityCoordinates(grid, tileCorner), out _, cleanable: true);
    }

    /// <summary>Adds semen to the mob's reservoir, creating the drip if needed.</summary>
    public void AddSemen(EntityUid uid, double amount)
    {
        if (amount <= 0 || !HasComp<HumanoidAppearanceComponent>(uid))
            return;

        var comp = EnsureComp<DrippingCumComponent>(uid);
        comp.Amount += amount;

        // Slight randomness so multiple recipients don't drip in lockstep.
        if (comp.NextDrip == default)
            comp.NextDrip = _timing.CurTime + TimeSpan.FromSeconds(_random.NextDouble());

        Dirty(uid, comp);
    }

    public double GetSemen(EntityUid uid)
    {
        return TryComp<DrippingCumComponent>(uid, out var comp) ? comp.Amount : 0;
    }
}
