using System.Linq;
using Content.Shared._PS.Interactions;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._PS.Organs;

/// <summary>
/// Owns profile-driven genital organs: inserts/updates/removes the organ entities from the
/// character profile and keeps their render markings in sync.
/// </summary>
/// <remarks>
/// Rendering deliberately reuses the marking system: each <see cref="GenitalOrganSize"/> points at
/// one of the existing genital marking prototypes, which already handle layering, color links and
/// clothing occlusion. Organ state (type/size/arousal/visibility/colors/semen volume) lives in
/// <see cref="GenitalOrganComponent"/> and is the source of truth for gameplay.
/// </remarks>
public sealed class GenitalOrganSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    /// <summary>
    /// Profiles applied before the body parts exist (admin respawns, deferred MapInit). Retried each
    /// tick until the body is ready so a spawned character never ends up without genitals.
    /// </summary>
    private readonly Dictionary<EntityUid, HumanoidCharacterProfile?> _pendingProfiles = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, HumanoidProfileAppliedEvent>(OnProfileApplied);
        SubscribeLocalEvent<GenitalOrganComponent, OrganRemovedFromBodyEvent>(OnOrganRemoved);
        SubscribeLocalEvent<HumanoidAppearanceComponent, DidEquipEvent>(OnDidEquip);
        SubscribeLocalEvent<HumanoidAppearanceComponent, DidUnequipEvent>(OnDidUnequip);
        SubscribeLocalEvent<HumanoidAppearanceComponent, MarkingVisibilityChangedEvent>(OnMarkingVisibilityChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingProfiles.Count == 0)
            return;

        foreach (var uid in _pendingProfiles.Keys.ToList())
        {
            if (!_pendingProfiles.TryGetValue(uid, out var profile))
                continue;

            if (Deleted(uid) || TrySyncFromProfile(uid, profile))
                _pendingProfiles.Remove(uid);
        }
    }

    private void OnProfileApplied(EntityUid uid, HumanoidAppearanceComponent component, HumanoidProfileAppliedEvent args)
    {
        if (TrySyncFromProfile(uid, args.Profile))
        {
            // A successful sync supersedes anything queued earlier (e.g. the default profile
            // applied during ComponentInit before the real character profile arrives).
            _pendingProfiles.Remove(uid);
            return;
        }

        _pendingProfiles[uid] = args.Profile;
    }

    /// <summary>Syncs organs if the body is ready; otherwise returns false so the caller can retry.</summary>
    private bool TrySyncFromProfile(EntityUid mob, HumanoidCharacterProfile? profile)
    {
        if (!TryComp<BodyComponent>(mob, out var body) ||
            body.RootContainer == null ||
            _body.GetRootPartOrNull(mob, body) == null)
        {
            return false;
        }

        SyncFromProfile(mob, profile);
        return true;
    }

    private void OnOrganRemoved(Entity<GenitalOrganComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (args.OldBody.IsValid())
            SyncRender(args.OldBody);
    }

    private void OnDidEquip(EntityUid uid, HumanoidAppearanceComponent component, DidEquipEvent args)
    {
        if (args.Slot == "jumpsuit")
            SyncRender(uid);
    }

    private void OnDidUnequip(EntityUid uid, HumanoidAppearanceComponent component, DidUnequipEvent args)
    {
        if (args.Slot == "jumpsuit")
            SyncRender(uid);
    }

    private void OnMarkingVisibilityChanged(EntityUid uid, HumanoidAppearanceComponent component, MarkingVisibilityChangedEvent args)
    {
        SyncRender(uid);
    }

    /// <summary>
    /// Makes the mob's organs match the profile exactly: adds/updates missing organs, removes
    /// organs that are no longer selected.
    /// </summary>
    public void SyncFromProfile(EntityUid mob, HumanoidCharacterProfile? profile)
    {
        var settings = profile?.Genitals ?? new GenitalOrganSettings();

        // The appearance component applies a default profile during ComponentInit, before the
        // body parts exist (RootContainer is null until MapInit). Skip until the body is ready.
        if (!TryComp<BodyComponent>(mob, out var body) || body.RootContainer == null || _body.GetRootPartOrNull(mob, body) == null)
            return;

        var torso = _body.GetBodyChildrenOfType(mob, BodyPartType.Torso, body).FirstOrDefault();
        if (torso.Id == default)
            return;

        var existing = new Dictionary<GenitalType, (EntityUid Uid, GenitalOrganComponent Comp)>();
        foreach (var (organUid, _) in _body.GetBodyOrgans(mob))
        {
            if (TryComp<GenitalOrganComponent>(organUid, out var organComp))
                existing[organComp.GenitalType] = (organUid, organComp);
        }

        foreach (var type in Enum.GetValues<GenitalType>())
        {
            var data = settings.Get(type);

            if (data == null)
            {
                if (existing.TryGetValue(type, out var toRemove))
                {
                    _body.RemoveOrgan(toRemove.Uid);
                    QueueDel(toRemove.Uid);
                }

                continue;
            }

            if (!_prototype.TryIndex<GenitalOrganPrototype>(data.Prototype, out var catalog) ||
                catalog.GenitalType != type)
            {
                continue;
            }

            var size = Math.Clamp(data.Size, 1, Math.Max(catalog.Sizes.Count, 1));

            if (existing.TryGetValue(type, out var organ))
            {
                var changed = organ.Comp.OrganPrototype != data.Prototype || organ.Comp.Size != size;

                organ.Comp.OrganPrototype = data.Prototype;
                organ.Comp.Size = size;
                organ.Comp.Visibility = data.Visibility;
                organ.Comp.Color = data.Color;
                organ.Comp.DetailColor = data.DetailColor;

                if (changed)
                    organ.Comp.Aroused = false;

                if (type == GenitalType.Penis)
                    organ.Comp.SemenVolume = settings.SemenVolume;

                Dirty(organ.Uid, organ.Comp);
                continue;
            }

            var slot = GetOrganSlot(type);
            if (!_body.TryCreateOrganSlot(torso.Id, slot, out _, torso.Component))
                continue;

            var spawned = Spawn(GetOrganEntity(type), Transform(torso.Id).Coordinates);
            var comp = EnsureComp<GenitalOrganComponent>(spawned);
            comp.GenitalType = type;
            comp.OrganPrototype = data.Prototype;
            comp.Size = size;
            comp.Aroused = false;
            comp.Visibility = data.Visibility;
            comp.Color = data.Color;
            comp.DetailColor = data.DetailColor;
            comp.SemenVolume = settings.SemenVolume;
            Dirty(spawned, comp);

            if (!_body.InsertOrgan(torso.Id, spawned, slot, torso.Component))
                QueueDel(spawned);
        }

        SyncRender(mob);
    }

    /// <summary>Rebuilds the genital render markings from the mob's current organs.</summary>
    public void SyncRender(EntityUid mob)
    {
        if (!TryComp<HumanoidAppearanceComponent>(mob, out var humanoid))
            return;

        humanoid.MarkingSet.RemoveCategory(MarkingCategories.Genital);

        foreach (var (_, organ) in GetOrgans(mob))
        {
            // The visibility rule controls rendering directly: covered organs are not drawn at all
            // (layer overlap alone lets oversized sprites poke out of clothing).
            if (!IsExposed(mob, organ.GenitalType, organ.Visibility, humanoid))
                continue;

            if (!_prototype.TryIndex<GenitalOrganPrototype>(organ.OrganPrototype, out var catalog))
                continue;

            if (organ.Size < 1 || organ.Size > catalog.Sizes.Count)
                continue;

            var size = catalog.Sizes[organ.Size - 1];
            var markingId = organ.Aroused && size.Aroused != null ? size.Aroused.Value : size.Flaccid;

            if (markingId == null)
                continue;

            if (!_markingManager.Markings.TryGetValue(markingId.Value, out var markingProto))
                continue;

            var colors = BuildMarkingColors(organ, markingProto, humanoid.SkinColor);
            var marking = new Marking(markingId.Value, colors)
            {
                Forced = true,
                Visible = true,
                CanToggleVisible = false,
                OtherCanToggleVisible = false,
                RenderOverClothing = organ.Visibility == GenitalVisibility.NeverHidden,
            };

            humanoid.MarkingSet.AddBack(MarkingCategories.Genital, marking);
        }

        Dirty(mob, humanoid);
    }

    /// <summary>
    /// Expands the organ's primary/detail colors into one color per sprite, following the marking's
    /// color links so linked states (e.g. front/behind pairs, nipples) use the same color group.
    /// </summary>
    private static List<Color> BuildMarkingColors(GenitalOrganComponent organ, MarkingPrototype prototype, Color skin)
    {
        var primary = organ.Color ?? skin;
        var detail = organ.DetailColor ?? primary;
        var colors = new List<Color>(prototype.Sprites.Count);
        var groups = new Dictionary<string, int>();

        foreach (var sprite in prototype.Sprites)
        {
            var root = ResolveColorRoot(prototype, sprite);

            if (!groups.TryGetValue(root, out var group))
            {
                group = groups.Count;
                groups.Add(root, group);
            }

            colors.Add(group == 0 ? primary : detail);
        }

        return colors;
    }

    /// <summary>Number of independent color groups (color links resolved) in a marking prototype.</summary>
    public static int CountColorGroups(MarkingPrototype prototype)
    {
        var groups = new HashSet<string>();
        foreach (var sprite in prototype.Sprites)
        {
            groups.Add(ResolveColorRoot(prototype, sprite));
        }

        return Math.Max(groups.Count, 1);
    }

    private static string ResolveColorRoot(MarkingPrototype prototype, SpriteSpecifier sprite)
    {
        var root = sprite is SpriteSpecifier.Rsi rsi ? rsi.RsiState : string.Empty;
        var guard = 0;

        while (prototype.ColorLinks != null &&
               prototype.ColorLinks.TryGetValue(root, out var parent) &&
               parent != root &&
               guard++ < 8)
        {
            root = parent;
        }

        return root;
    }

    /// <summary>Applies a SPLURT-style visibility rule against the mob's current clothing/underwear.</summary>
    public bool IsExposed(EntityUid uid, GenitalType type, GenitalVisibility visibility, HumanoidAppearanceComponent? humanoid = null)
    {
        if (humanoid == null && !TryComp(uid, out humanoid))
            return false;

        switch (visibility)
        {
            case GenitalVisibility.AlwaysHidden:
                return false;

            case GenitalVisibility.NeverHidden:
                return true;

            case GenitalVisibility.HiddenByUnderwear:
                return !IsWearingUnderwear(humanoid, type) && !IsWearingJumpsuit(uid);

            case GenitalVisibility.HiddenByJumpsuit:
            default:
                return !IsWearingJumpsuit(uid);
        }
    }

    public bool IsWearingJumpsuit(EntityUid uid)
    {
        if (!TryComp<InventoryComponent>(uid, out var inventory))
            return false;

        return _inventory.TryGetSlotEntity(uid, "jumpsuit", out _, inventory);
    }

    /// <summary>
    /// Whether the entity wears a visible undergarment marking relevant to the organ. SS14 has no
    /// underwear slot; the fork's undergarment markings fill that role.
    /// </summary>
    public bool IsWearingUnderwear(HumanoidAppearanceComponent humanoid, GenitalType type)
    {
        var category = type == GenitalType.Breasts
            ? MarkingCategories.UndergarmentTop
            : MarkingCategories.UndergarmentBottom;

        if (!humanoid.MarkingSet.TryGetCategory(category, out var markings))
            return false;

        foreach (var marking in markings)
        {
            if (marking.Visible)
                return true;
        }

        return false;
    }

    /// <summary>Sets the arousal state of one of the mob's organs and refreshes its sprite.</summary>
    public bool SetAroused(EntityUid mob, GenitalType type, bool aroused)
    {
        if (!TryGetOrgan(mob, type, out var organUid, out var organ))
            return false;

        if (organ.Aroused == aroused)
            return false;

        organ.Aroused = aroused;
        Dirty(organUid, organ);
        SyncRender(mob);
        return true;
    }

    /// <summary>Sets the SPLURT-style visibility rule of one of the mob's organs.</summary>
    public bool SetVisibility(EntityUid mob, GenitalType type, GenitalVisibility visibility)
    {
        if (!TryGetOrgan(mob, type, out var organUid, out var organ))
            return false;

        if (!Enum.IsDefined(visibility))
            visibility = GenitalVisibility.HiddenByJumpsuit;

        if (organ.Visibility == visibility)
            return false;

        organ.Visibility = visibility;
        Dirty(organUid, organ);
        SyncRender(mob);
        return true;
    }

    public bool TryGetOrgan(
        EntityUid mob,
        GenitalType type,
        out EntityUid organUid,
        out GenitalOrganComponent organ)
    {
        foreach (var (uid, comp) in GetOrgans(mob))
        {
            if (comp.GenitalType != type)
                continue;

            organUid = uid;
            organ = comp;
            return true;
        }

        organUid = default;
        organ = default!;
        return false;
    }

    public bool HasGenital(EntityUid mob, GenitalType type)
    {
        return TryGetOrgan(mob, type, out _, out _);
    }

    public List<(EntityUid Uid, GenitalOrganComponent Comp)> GetOrgans(EntityUid mob)
    {
        var result = new List<(EntityUid, GenitalOrganComponent)>();

        if (!HasComp<BodyComponent>(mob))
            return result;

        foreach (var (organUid, _) in _body.GetBodyOrgans(mob))
        {
            if (TryComp<GenitalOrganComponent>(organUid, out var organComp))
                result.Add((organUid, organComp));
        }

        return result;
    }

    /// <summary>Semen produced per climax by the mob's penis organ (profile default when absent).</summary>
    public int GetSemenVolume(EntityUid mob)
    {
        return TryGetOrgan(mob, GenitalType.Penis, out _, out var penis)
            ? penis.SemenVolume
            : GenitalOrganSettings.DefaultSemenVolume;
    }

    public static string GetOrganSlot(GenitalType type)
    {
        return $"genital_{type.ToString().ToLowerInvariant()}";
    }

    public static string GetOrganEntity(GenitalType type)
    {
        return type switch
        {
            GenitalType.Penis => "OrganGenitalPenis",
            GenitalType.Vagina => "OrganGenitalVagina",
            GenitalType.Balls => "OrganGenitalBalls",
            GenitalType.Breasts => "OrganGenitalBreasts",
            GenitalType.Butt => "OrganGenitalButt",
            GenitalType.Belly => "OrganGenitalBelly",
            _ => "OrganGenitalPenis",
        };
    }
}
