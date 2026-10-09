using System.Linq;
using Content.Shared._PS.Interactions;
using Content.Shared._PS.Organs;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._PS.Interactions;

/// <summary>
/// Resolves which genitals an entity has and whether they are exposed, for the interaction system.
/// Backed by <see cref="GenitalOrganSystem"/>: organs are the source of truth (including their
/// SPLURT-style visibility rule) and the existing marking renderer only draws them.
/// </summary>
public sealed class GenitalSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly GenitalOrganSystem _organs = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    /// <summary>Returns whether the entity has the given genital in any state.</summary>
    public bool HasGenital(EntityUid uid, GenitalType type)
    {
        if (type == GenitalType.Anus)
            return HasComp<HumanoidAppearanceComponent>(uid);

        return _organs.HasGenital(uid, type);
    }

    /// <summary>Returns the exposure state of a genital using the organ's visibility rule.</summary>
    public GenitalExposure GetExposure(EntityUid uid, GenitalType type)
    {
        if (!HasComp<HumanoidAppearanceComponent>(uid))
            return GenitalExposure.None;

        // The anus has no organ/visibility setting; it follows the jumpsuit rule.
        if (type == GenitalType.Anus)
            return _organs.IsWearingJumpsuit(uid) ? GenitalExposure.Unexposed : GenitalExposure.Exposed;

        if (!_organs.TryGetOrgan(uid, type, out _, out var organ))
            return GenitalExposure.None;

        return _organs.IsExposed(uid, type, organ.Visibility)
            ? GenitalExposure.Exposed
            : GenitalExposure.Unexposed;
    }

    /// <summary>
    /// Builds the list of organs shown in the panel's genital tab.
    /// </summary>
    public List<InteractionGenitalUiEntry> GetGenitalEntries(EntityUid uid, bool isSelf)
    {
        var entries = new List<InteractionGenitalUiEntry>();

        foreach (var (_, organ) in _organs.GetOrgans(uid))
        {
            if (!_prototype.TryIndex<GenitalOrganPrototype>(organ.OrganPrototype, out var catalog))
                continue;

            var sizeName = string.Empty;
            if (organ.Size >= 1 && organ.Size <= catalog.Sizes.Count)
                sizeName = Loc.GetString(catalog.Sizes[organ.Size - 1].Name);

            // Palmtree: show the genital type (Penis/Vagina/...) rather than only the marking-derived
            // catalog name; the catalog style and size become the parenthesized detail.
            var detail = string.IsNullOrEmpty(sizeName)
                ? Loc.GetString(catalog.Name)
                : $"{Loc.GetString(catalog.Name)}, {sizeName}";

            entries.Add(new InteractionGenitalUiEntry
            {
                Type = organ.GenitalType,
                Name = Loc.GetString($"genital-editor-{organ.GenitalType.ToString().ToLowerInvariant()}"),
                SizeName = detail,
                Aroused = organ.Aroused,
                Visibility = organ.Visibility,
                CanToggleArousal = isSelf && catalog.CanArouse,
            });
        }

        entries.Sort((a, b) => a.Type != b.Type ? a.Type.CompareTo(b.Type) : string.CompareOrdinal(a.Name, b.Name));
        return entries;
    }

    /// <summary>Returns whether the entity's mouth is covered by an ingestion blocker (mask).</summary>
    public bool IsMouthCovered(EntityUid uid)
    {
        if (TryComp<InventoryComponent>(uid, out var inventory) &&
            _inventory.TryGetSlotEntity(uid, "mask", out var mask, inventory) &&
            HasComp<IngestionBlockerComponent>(mask))
        {
            return true;
        }

        return false;
    }

    /// <summary>Returns whether the entity has no jumpsuit worn (simplified clothed check).</summary>
    public bool IsBottomless(EntityUid uid)
    {
        return !_organs.IsWearingJumpsuit(uid);
    }
}
