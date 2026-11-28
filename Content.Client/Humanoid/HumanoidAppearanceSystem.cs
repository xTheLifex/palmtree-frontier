using System.Linq;
using System.Numerics;
using Content.Client._PS.Consent; // Palmtree: NSFW age gate
using Content.Client._PS.Humanoid; // Palmtree: direction-aware layer ordering
using Content.Client.DisplacementMap;
using Content.Shared._CS; // Palmtree/Coyote: leg displacement
using Content.Shared.CCVar;
using Content.Shared.DisplacementMap; // Palmtree/Coyote: leg displacement
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared._PS.Interactions; // Palmtree: GenitalType
using Content.Shared._PS.Organs; // Palmtree
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Humanoid;

public sealed partial class HumanoidAppearanceSystem : SharedHumanoidAppearanceSystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private MarkingManager _markingManager = default!;
    [Dependency] private IConfigurationManager _configurationManager = default!;
    [Dependency] private DisplacementMapSystem _displacement = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private InventorySystem _inventory = default!;

    // Palmtree: resolved north-view organ layer ids per humanoid. Building them requires prototype
    // lookups, but the per-frame north safety pass only needs a few layer-map lookups.
    private readonly Dictionary<EntityUid, string[]> _northOrganLayerIds = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<InventoryComponent, DidEquipEvent>(OnClothingEquipped);
        SubscribeLocalEvent<InventoryComponent, DidUnequipEvent>(OnClothingUnequipped);
        Subs.CVar(_configurationManager, CCVars.AccessibilityClientCensorNudity, OnCvarChanged, true);
        Subs.CVar(_configurationManager, CCVars.AccessibilityServerCensorNudity, OnCvarChanged, true);
        NsfwGate.Changed += OnNsfwGateChanged; // Palmtree
    }

    public override void Shutdown()
    {
        NsfwGate.Changed -= OnNsfwGateChanged; // Palmtree
        base.Shutdown();
    }

    private void OnHandleState(EntityUid uid, HumanoidAppearanceComponent component, ref AfterAutoHandleStateEvent args)
    {
        UpdateSprite((uid, component, Comp<SpriteComponent>(uid)));
    }

    // Palmtree: exposure (and therefore organ layer position) changes with clothing even when the
    // marking set itself is unchanged. Rebuild locally so undressing does not wait for a rotation.
    private void OnClothingEquipped(EntityUid uid, InventoryComponent component, DidEquipEvent args)
    {
        if (args.Slot is "jumpsuit" or "outerClothing")
            RefreshSprite(uid);
    }

    private void OnClothingUnequipped(EntityUid uid, InventoryComponent component, DidUnequipEvent args)
    {
        if (args.Slot is "jumpsuit" or "outerClothing")
            RefreshSprite(uid);
    }

    /// <summary>
    /// Palmtree: rebuilds the sprite layers from the current marking set. Local visibility toggles
    /// (lobby preview undergarments) do not go through component state, so the editor calls this
    /// after changing <c>Marking.Visible</c>.
    /// </summary>
    public void RefreshSprite(EntityUid uid, HumanoidAppearanceComponent? component = null)
    {
        if (!Resolve(uid, ref component) || !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        UpdateSprite((uid, component, sprite));
    }

    /// <summary>Palmtree: drop the cached north-view organ layer ids for a removed humanoid.</summary>
    public void ForgetNorthOrganLayers(EntityUid uid)
    {
        _northOrganLayerIds.Remove(uid);
    }

    /// <summary>Palmtree: whether this humanoid has any resolved front-mounted organ layers.</summary>
    public bool HasNorthOrganMarkings(EntityUid uid, HumanoidAppearanceComponent humanoid)
    {
        if (!_northOrganLayerIds.TryGetValue(uid, out var layerIds))
            TryResolveNorthOrganLayerIds(uid, humanoid, out layerIds);

        return layerIds.Length > 0;
    }

    /// <summary>
    /// Palmtree: the cardinal direction this humanoid is drawn in on the local player's screen.
    /// The client renderer uses world rotation plus the camera's eye rotation, so layer ordering
    /// must use the same angle instead of the mob's raw facing.
    /// </summary>
    public RsiDirection GetScreenDirection(EntityUid uid)
    {
        if (TryComp<HumanoidLayerDirectionComponent>(uid, out var directionOverride))
            return directionOverride.Direction;

        if (!TryComp<TransformComponent>(uid, out var xform))
            return RsiDirection.South;

        // Match the renderer exactly: it normalizes the on-screen angle before selecting the RSI
        // direction. Without this, a negative or >2pi angle makes GetDirection fall through to West.
        var angle = (_transform.GetWorldRotation(xform) + _eyeManager.CurrentEye.Rotation).Reduced().FlipPositive();
        return SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, angle);
    }

    /// <summary>
    /// Palmtree: whether the sprite is drawn from a side view (including diagonals), used for
    /// layer swaps that only apply to side-facing characters.
    /// </summary>
    public bool IsSideFacing(EntityUid uid)
    {
        if (TryComp<HumanoidLayerDirectionComponent>(uid, out var directionOverride))
            return directionOverride.Direction is RsiDirection.East or RsiDirection.West;

        if (!TryComp<TransformComponent>(uid, out var xform))
            return false;

        var angle = (_transform.GetWorldRotation(xform) + _eyeManager.CurrentEye.Rotation).Reduced().FlipPositive();
        return angle.GetDir() is Direction.East
            or Direction.West
            or Direction.NorthEast
            or Direction.SouthEast
            or Direction.NorthWest
            or Direction.SouthWest;
    }

    /// <summary>
    /// Palmtree: whether the mob currently wears a garment on a slot that is drawn over genitals.
    /// Used to keep the south-facing genital layer above hair/tails only when there is nothing
    /// covering the organ.
    /// </summary>
    private bool HasCoveringClothing(EntityUid uid)
    {
        if (!TryComp<InventoryComponent>(uid, out var inventory))
            return false;

        return _inventory.TryGetSlotEntity(uid, "jumpsuit", out _, inventory)
               || _inventory.TryGetSlotEntity(uid, "outerClothing", out _, inventory);
    }

    private bool HasVisibleUndergarment(HumanoidAppearanceComponent humanoid, MarkingCategories category)
    {
        if (humanoid.MarkingSet.TryGetCategory(category, out var markings))
        {
            foreach (var marking in markings)
            {
                if (marking.Visible)
                    return true;
            }
        }

        var censorNudity = _configurationManager.GetCVar(CCVars.AccessibilityClientCensorNudity)
                           || _configurationManager.GetCVar(CCVars.AccessibilityServerCensorNudity);

        if (!censorNudity)
            return false;

        return category == MarkingCategories.UndergarmentTop
            ? humanoid.UndergarmentTop != null
            : humanoid.UndergarmentBottom != null;
    }

    private void OnCvarChanged(bool value)
    {
        RefreshAllHumanoids();
    }

    // Palmtree: the NSFW gate adds/removes genital sprite layers, so every humanoid sprite must be
    // rebuilt when the player's consent state changes (lobby prompt answer or an admin flip).
    private void OnNsfwGateChanged()
    {
        RefreshAllHumanoids();
    }

    private void RefreshAllHumanoids()
    {
        var humanoidQuery = AllEntityQuery<HumanoidAppearanceComponent, SpriteComponent>();
        while (humanoidQuery.MoveNext(out var uid, out var humanoidComp, out var spriteComp))
        {
            UpdateSprite((uid, humanoidComp, spriteComp));
        }
    }

    private void UpdateSprite(Entity<HumanoidAppearanceComponent, SpriteComponent> entity)
    {
        _northOrganLayerIds.Remove(entity.Owner);
        UpdateLayers(entity);
        // Clear old marking layers before reordering the hand base layers. Otherwise stale
        // markings sitting between the hands are shifted when the hands are swapped.
        ClearAllMarkings(entity);
        UpdateHandLayerOrder(entity.Owner, entity.Comp2);
        ApplyMarkingSet(entity);
        UpdateLayersAgain(entity); // Palmtree/Floof: hide base layers replaced by base markings

        var humanoidAppearance = entity.Comp1;
        var sprite = entity.Comp2;

        sprite[_sprite.LayerMapReserve((entity.Owner, sprite), HumanoidVisualLayers.Eyes)].Color = humanoidAppearance.EyeColor;

        // Palmtree: character height/width sliders scale the whole sprite.
        sprite.Scale = new Vector2(humanoidAppearance.Width, humanoidAppearance.Height);
    }

    private static bool IsHidden(HumanoidAppearanceComponent humanoid, HumanoidVisualLayers layer)
        => humanoid.HiddenLayers.ContainsKey(layer) || humanoid.PermanentlyHidden.Contains(layer);

    private void UpdateLayers(Entity<HumanoidAppearanceComponent, SpriteComponent> entity)
    {
        var component = entity.Comp1;
        var sprite = entity.Comp2;

        var oldLayers = new HashSet<HumanoidVisualLayers>(component.BaseLayers.Keys);
        component.BaseLayers.Clear();
        component.HiddenBaseLayers.Clear(); // Palmtree/Floof

        // add default species layers
        var speciesProto = _prototypeManager.Index(component.Species);
        var baseSprites = _prototypeManager.Index(speciesProto.SpriteSet);
        foreach (var (key, id) in baseSprites.Sprites)
        {
            oldLayers.Remove(key);
            if (!component.CustomBaseLayers.ContainsKey(key))
                SetLayerData(entity, key, id, sexMorph: true);
        }

        // add custom layers
        foreach (var (key, info) in component.CustomBaseLayers)
        {
            oldLayers.Remove(key);
            SetLayerData(entity, key, info.Id, sexMorph: false, color: info.Color);
        }

        // hide old layers
        // TODO maybe just remove them altogether?
        foreach (var key in oldLayers)
        {
            if (_sprite.LayerMapTryGet((entity.Owner, sprite), key, out var index, false))
                sprite[index].Visible = false;
        }
    }

    private void SetLayerData(
        Entity<HumanoidAppearanceComponent, SpriteComponent> entity,
        HumanoidVisualLayers key,
        string? protoId,
        bool sexMorph = false,
        Color? color = null)
    {
        var component = entity.Comp1;
        var sprite = entity.Comp2;

        var layerIndex = _sprite.LayerMapReserve((entity.Owner, sprite), key);
        var layer = sprite[layerIndex];
        layer.Visible = !IsHidden(component, key);

        if (color != null)
            layer.Color = color.Value;

        if (protoId == null)
            return;

        if (sexMorph)
            protoId = HumanoidVisualLayersExtension.GetSexMorph(key, component.Sex, protoId);

        var proto = _prototypeManager.Index<HumanoidSpeciesSpriteLayer>(protoId);
        component.BaseLayers[key] = proto;

        if (proto.MatchSkin)
            layer.Color = component.SkinColor.WithAlpha(proto.LayerAlpha);

        if (proto.BaseSprite != null)
        {
            var appropriateSprite = proto.BaseSprite;

            // Palmtree/Coyote: swap base sprites for the current leg style (e.g. digitigrade).
            if (component.LegStyle != HumanoidLegStyle.Plantigrade && proto.AltSprites.Count > 0)
            {
                if (proto.AltSprites.TryGetValue(component.LegStyle, out var altSprite)
                    || proto.AltSprites.TryGetValue(HumanoidLegStyle.Digitigrade, out altSprite))
                {
                    if (_prototypeManager.TryIndex(altSprite, out MarkingPrototype? altMarkingProto))
                    {
                        if (altMarkingProto.BaseLayerSprite is SpriteSpecifier.Rsi)
                            appropriateSprite = altMarkingProto.BaseLayerSprite;
                        else if (altMarkingProto.Sprites.Count > 0)
                            appropriateSprite = altMarkingProto.Sprites[0];
                    }
                }
            }

            _sprite.LayerSetSprite((entity.Owner, sprite), layerIndex, appropriateSprite);
        }
    }

    /// <summary>
    /// Palmtree: the base map orders LHand then RHand. East already has the near RHand above the
    /// far LHand; west needs the opposite so the near LHand stays in front of the belly and the
    /// far RHand stays behind it.
    /// </summary>
    private void UpdateHandLayerOrder(EntityUid uid, SpriteComponent sprite)
    {
        if (!_sprite.LayerMapTryGet((uid, sprite), HumanoidVisualLayers.LHand, out var leftIndex, false)
            || !_sprite.LayerMapTryGet((uid, sprite), HumanoidVisualLayers.RHand, out var rightIndex, false))
        {
            return;
        }

        var wantSwapped = GetScreenDirection(uid) == RsiDirection.West;
        var isSwapped = leftIndex > rightIndex;
        if (wantSwapped == isSwapped)
            return;

        if (!_sprite.TryGetLayer((uid, sprite), leftIndex, out var leftLayer, false)
            || !_sprite.TryGetLayer((uid, sprite), rightIndex, out var rightLayer, false))
        {
            return;
        }

        var low = Math.Min(leftIndex, rightIndex);
        var high = Math.Max(leftIndex, rightIndex);
        if (high != low + 1)
            return;

        if (!_sprite.RemoveLayer((uid, sprite), high, out _, false)
            || !_sprite.RemoveLayer((uid, sprite), low, out _, false))
        {
            return;
        }

        if (wantSwapped)
        {
            // West: far RHand below, near LHand above.
            var right = _sprite.AddLayer((uid, sprite), rightLayer, low);
            var left = _sprite.AddLayer((uid, sprite), leftLayer, low + 1);
            _sprite.LayerMapSet((uid, sprite), HumanoidVisualLayers.RHand, right);
            _sprite.LayerMapSet((uid, sprite), HumanoidVisualLayers.LHand, left);
        }
        else
        {
            // East/non-side: base order, far LHand below, near RHand above.
            var left = _sprite.AddLayer((uid, sprite), leftLayer, low);
            var right = _sprite.AddLayer((uid, sprite), rightLayer, low + 1);
            _sprite.LayerMapSet((uid, sprite), HumanoidVisualLayers.LHand, left);
            _sprite.LayerMapSet((uid, sprite), HumanoidVisualLayers.RHand, right);
        }
    }

    /// <summary>
    /// Palmtree/Floof: hides base layers that are replaced by base markings (e.g. species adaptors).
    /// </summary>
    private void UpdateLayersAgain(Entity<HumanoidAppearanceComponent, SpriteComponent> entity)
    {
        var component = entity.Comp1;
        var sprite = entity.Comp2;

        foreach (var layer in component.HiddenBaseLayers)
        {
            if (_sprite.LayerMapTryGet((entity.Owner, sprite), layer, out var index, false))
                sprite[index].Visible = false;
        }
    }

    /// <summary>
    ///     Loads a profile directly into a humanoid.
    /// </summary>
    /// <param name="uid">The humanoid entity's UID</param>
    /// <param name="profile">The profile to load.</param>
    /// <param name="humanoid">The humanoid entity's humanoid component.</param>
    /// <remarks>
    ///     This should not be used if the entity is owned by the server. The server will otherwise
    ///     override this with the appearance data it sends over.
    /// </remarks>
    public override void LoadProfile(EntityUid uid, HumanoidCharacterProfile? profile, HumanoidAppearanceComponent? humanoid = null)
    {
        if (profile == null)
            return;

        if (!Resolve(uid, ref humanoid))
        {
            return;
        }

        var customBaseLayers = new Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo>();

        var speciesPrototype = _prototypeManager.Index<SpeciesPrototype>(profile.Species);
        var markings = new MarkingSet(speciesPrototype.MarkingPoints, _markingManager, _prototypeManager);

        // Add markings that doesn't need coloring. We store them until we add all other markings that doesn't need it.
        var markingFColored = new Dictionary<Marking, MarkingPrototype>();
        foreach (var marking in profile.Appearance.Markings)
        {
            if (_markingManager.TryGetMarking(marking, out var prototype))
            {
                if (!prototype.ForcedColoring)
                {
                    markings.AddBack(prototype.MarkingCategory, marking);
                }
                else
                {
                    markingFColored.Add(marking, prototype);
                }
            }
        }

        // legacy: remove in the future?
        //markings.RemoveCategory(MarkingCategories.Hair);
        //markings.RemoveCategory(MarkingCategories.FacialHair);

        // We need to ensure hair before applying it or coloring can try depend on markings that can be invalid
        var hairColor = _markingManager.MustMatchSkin(profile.Species, HumanoidVisualLayers.Hair, out var hairAlpha, _prototypeManager)
            ? profile.Appearance.SkinColor.WithAlpha(hairAlpha)
            : profile.Appearance.HairColor;
        var hair = new Marking(profile.Appearance.HairStyleId,
            new[] { hairColor });

        var facialHairColor = _markingManager.MustMatchSkin(profile.Species, HumanoidVisualLayers.FacialHair, out var facialHairAlpha, _prototypeManager)
            ? profile.Appearance.SkinColor.WithAlpha(facialHairAlpha)
            : profile.Appearance.FacialHairColor;
        var facialHair = new Marking(profile.Appearance.FacialHairStyleId,
            new[] { facialHairColor });

        // Frontier: Match hair and facial hair colors to the forced color if it exists
        if (_markingManager.MustMatchColor(profile.Species, HumanoidVisualLayers.Hair, out var forcedHairAlpha, _prototypeManager) is Color forcedHairColor)
        {
            profile.Appearance.SkinColor.WithAlpha(forcedHairAlpha);
            hairColor = forcedHairColor;
        }
        if (_markingManager.MustMatchColor(profile.Species, HumanoidVisualLayers.FacialHair, out var forcedFacialHairAlpha, _prototypeManager) is Color forcedFacialHairColor)
        {
            profile.Appearance.SkinColor.WithAlpha(forcedFacialHairAlpha);
            facialHairColor = forcedFacialHairColor;
        }
        // End Frontier

        if (_markingManager.CanBeApplied(profile.Species, profile.Sex, hair, _prototypeManager))
        {
            markings.AddBack(MarkingCategories.Hair, hair);
        }
        if (_markingManager.CanBeApplied(profile.Species, profile.Sex, facialHair, _prototypeManager))
        {
            markings.AddBack(MarkingCategories.FacialHair, facialHair);
        }

        // Finally adding marking with forced colors
        foreach (var (marking, prototype) in markingFColored)
        {
            var markingColors = MarkingColoring.GetMarkingLayerColors(
                prototype,
                profile.Appearance.SkinColor,
                profile.Appearance.EyeColor,
                markings
            );
            markings.AddBack(prototype.MarkingCategory, new Marking(marking.MarkingId, markingColors));
        }

        markings.EnsureSpecies(profile.Species, profile.Appearance.SkinColor, _markingManager, _prototypeManager);
        markings.EnsureSexes(profile.Sex, _markingManager);
        markings.EnsureDefault(
            profile.Appearance.SkinColor,
            profile.Appearance.EyeColor,
            _markingManager);

        DebugTools.Assert(IsClientSide(uid));

        humanoid.MarkingSet = markings;
        humanoid.PermanentlyHidden = new HashSet<HumanoidVisualLayers>();
        humanoid.HiddenLayers = new Dictionary<HumanoidVisualLayers, SlotFlags>();
        humanoid.CustomBaseLayers = customBaseLayers;
        humanoid.Sex = profile.Sex;
        humanoid.Gender = profile.Gender;
        humanoid.Age = profile.Age;
        humanoid.LegStyle = profile.Appearance.LegStyle; // Palmtree/Coyote
        humanoid.Species = profile.Species;
        humanoid.SkinColor = profile.Appearance.SkinColor;
        humanoid.EyeColor = profile.Appearance.EyeColor;
        humanoid.Height = profile.Height; // Palmtree
        humanoid.Width = profile.Width; // Palmtree
        humanoid.SnoutAboveHair = profile.SnoutAboveHair; // Palmtree

        // Palmtree: profile-driven genital organs (lobby preview). The server path uses
        // HumanoidProfileAppliedEvent; this override does not call the shared LoadProfile.
        EntityManager.System<GenitalOrganSystem>().SyncFromProfile(uid, profile);

        UpdateSprite((uid, humanoid, Comp<SpriteComponent>(uid)));
    }

    private void ApplyMarkingSet(Entity<HumanoidAppearanceComponent, SpriteComponent> entity)
    {
        var humanoid = entity.Comp1;
        var sprite = entity.Comp2;

        // I am lazy and I CBF resolving the previous mess, so I'm just going to nuke the markings.
        // Really, markings should probably be a separate component altogether.
        ClearAllMarkings(entity);

        var censorNudity = _configurationManager.GetCVar(CCVars.AccessibilityClientCensorNudity) ||
                           _configurationManager.GetCVar(CCVars.AccessibilityServerCensorNudity);
        // The reason we're splitting this up is in case the character already has undergarment equipped in that slot.
        var applyUndergarmentTop = censorNudity;
        var applyUndergarmentBottom = censorNudity;

        // Palmtree: the exposed south stack draws the penis above the belly and breasts so large
        // breasts cannot paint over it. Every other direction keeps the normal priority order.
        var southOrganStack = ShouldUseSouthOrganStack(entity.Comp1, entity.Comp2, entity.Owner, GetScreenDirection(entity.Owner));
        var penisPriority = GenitalOrganSystem.GetRenderPriority(GenitalType.Penis);

        foreach (var (category, markingList) in humanoid.MarkingSet.Markings)
        {
            // Palmtree: genital organs carry an explicit bottom-to-top RenderPriority. Applying
            // them highest-first makes the newest insertion (lowest priority) end up underneath.
            IEnumerable<Marking> markings = markingList;
            if (category == MarkingCategories.Genital)
            {
                markings = southOrganStack
                    ? markingList.OrderByDescending(m => m.RenderPriority == penisPriority ? int.MaxValue : m.RenderPriority)
                    : markingList.OrderByDescending(m => m.RenderPriority);
            }

            foreach (var marking in markings)
            {
                if (!_markingManager.TryGetMarking(marking, out var markingPrototype))
                    continue;

                // Palmtree/Coyote: swap the marking for its alternate-leg-style version if one exists.
                markingPrototype = GetMarkingForLegStyle(humanoid, markingPrototype);
                ApplyMarking(markingPrototype, marking.MarkingColors, marking.Visible, entity, marking.MarkingScaleVector, marking.MarkingOffset, marking.MarkingGlow, marking.RenderOverClothing, marking.RenderUnderTopUndergarment);
                if (markingPrototype.BodyPart == HumanoidVisualLayers.UndergarmentTop)
                    applyUndergarmentTop = false;
                else if (markingPrototype.BodyPart == HumanoidVisualLayers.UndergarmentBottom)
                    applyUndergarmentBottom = false;
            }
        }

        humanoid.ClientOldMarkings = new MarkingSet(humanoid.MarkingSet);

        AddUndergarments(entity, applyUndergarmentTop, applyUndergarmentBottom);
        MoveExposedGenitalStackToNearHand(entity);
        EnsureNorthBreastsBehindBody(entity.Owner, entity.Comp1, entity.Comp2);
    }

    /// <summary>
    /// Palmtree: exposed east/west views put the whole genital stack between the hand groups. Hand
    /// and arm adaptor markings can add several layers above/below the base hands, so insert the
    /// stack provisionally during normal marking application and move it here, after every hand
    /// marking exists. The stack keeps its priority order and lands above the far hand group and
    /// below the near hand group.
    /// </summary>
    private void MoveExposedGenitalStackToNearHand(Entity<HumanoidAppearanceComponent, SpriteComponent> entity)
    {
        var direction = GetScreenDirection(entity.Owner);
        if (direction is not (RsiDirection.East or RsiDirection.West))
            return;

        if (HasCoveringClothing(entity.Owner)
            || HasVisibleUndergarment(entity.Comp1, MarkingCategories.UndergarmentTop)
            || HasVisibleUndergarment(entity.Comp1, MarkingCategories.UndergarmentBottom))
        {
            return;
        }

        if (!entity.Comp1.MarkingSet.TryGetCategory(MarkingCategories.Genital, out var genitalMarkings))
            return;

        var layerIds = new HashSet<string>();
        foreach (var marking in genitalMarkings)
        {
            if (!_markingManager.TryGetMarking(marking, out var markingPrototype))
                continue;

            markingPrototype = GetMarkingForLegStyle(entity.Comp1, markingPrototype);
            foreach (var sprite in markingPrototype.Sprites)
            {
                if (sprite is not SpriteSpecifier.Rsi rsi)
                    continue;

                var layerId = $"{markingPrototype.ID}-{rsi.RsiState}";
                layerIds.Add(layerId);
                layerIds.Add($"{layerId}-glow");
            }
        }

        var layers = new List<(string Id, int Index, SpriteComponent.Layer Layer)>();
        foreach (var layerId in layerIds)
        {
            if (_sprite.LayerMapTryGet((entity.Owner, entity.Comp2), layerId, out var index, false)
                && _sprite.TryGetLayer((entity.Owner, entity.Comp2), index, out var layer, false))
            {
                layers.Add((layerId, index, layer));
            }
        }

        if (layers.Count == 0)
            return;

        // East's near hand is RHand; west's is LHand after UpdateHandLayerOrder. Resolve the anchor
        // before removing anything so a species without that hand does not lose the organ layers.
        var nearHand = direction == RsiDirection.East
            ? HumanoidVisualLayers.RHand
            : HumanoidVisualLayers.LHand;

        if (!_sprite.LayerMapTryGet((entity.Owner, entity.Comp2), nearHand, out var anchor, false))
            return;

        layers.Sort((a, b) => a.Index.CompareTo(b.Index));

        foreach (var (_, index, _) in layers.OrderByDescending(layer => layer.Index))
        {
            _sprite.RemoveLayer((entity.Owner, entity.Comp2), index, out _, false);
            if (index <= anchor)
                anchor--;
        }

        for (var i = 0; i < layers.Count; i++)
        {
            var (layerId, _, layer) = layers[i];
            var index = _sprite.AddLayer((entity.Owner, entity.Comp2), layer, anchor + i);
            _sprite.LayerMapSet((entity.Owner, entity.Comp2), layerId, index);
        }
    }

    /// <summary>
    /// Palmtree: every genital organ except the butt is front-mounted. In the north/back view those
    /// layers belong behind the body, which keeps sideboob visible around the silhouette without
    /// drawing a breast over the back. Also called every frame for humanoids facing north so a
    /// layer that another system re-applied above the body is corrected without a rotation.
    /// </summary>
    public void EnsureNorthBreastsBehindBody(
        EntityUid uid,
        HumanoidAppearanceComponent humanoid,
        SpriteComponent sprite,
        RsiDirection? screenDirection = null)
    {
        if ((screenDirection ?? GetScreenDirection(uid)) != RsiDirection.North)
            return;

        if (!_northOrganLayerIds.TryGetValue(uid, out var layerIds))
        {
            if (!TryResolveNorthOrganLayerIds(uid, humanoid, out layerIds))
                return;
        }

        if (layerIds.Length == 0)
            return;

        var layers = new List<(string Id, int Index, SpriteComponent.Layer Layer)>(layerIds.Length);
        foreach (var layerId in layerIds)
        {
            if (_sprite.LayerMapTryGet((uid, sprite), layerId, out var index, false)
                && _sprite.TryGetLayer((uid, sprite), index, out var layer, false))
            {
                layers.Add((layerId, index, layer));
            }
        }

        if (layers.Count == 0)
            return;

        layers.Sort((a, b) => a.Index.CompareTo(b.Index));

        // Anchor on the body itself instead of TailBehind. The body is always in front of the back
        // of a front-mounted organ, while TailBehind's position relative to Chest can differ per
        // species sprite map.
        if (!_sprite.LayerMapTryGet((uid, sprite), HumanoidVisualLayers.Chest, out var chestIndex, false))
            return;

        // Already behind the body? Nothing to do. This keeps the per-frame pass read-only in the
        // normal case.
        if (layers[^1].Index < chestIndex)
            return;

        // Remove everything first and track how the body index shifts, then re-insert the organs
        // immediately before the body. Resolve the anchor before removing so a missing body layer
        // cannot drop the organ layers entirely.
        foreach (var (_, index, _) in layers.OrderByDescending(layer => layer.Index))
        {
            _sprite.RemoveLayer((uid, sprite), index, out _, false);
            if (index < chestIndex)
                chestIndex--;
        }

        for (var i = 0; i < layers.Count; i++)
        {
            var (layerId, _, layer) = layers[i];
            var index = _sprite.AddLayer((uid, sprite), layer, chestIndex + i);
            _sprite.LayerMapSet((uid, sprite), layerId, index);
        }
    }

    /// <summary>
    /// Palmtree: resolves the sprite layer ids of every front-mounted genital marking and caches
    /// them. The butt is excluded because it is drawn on the back side. Direction and exposure do
    /// not matter here: the cached ids are only used to find the layers, and the caller decides
    /// when to move them.
    /// </summary>
    private bool TryResolveNorthOrganLayerIds(EntityUid uid, HumanoidAppearanceComponent humanoid, out string[] layerIds)
    {
        var ids = new HashSet<string>();

        // Scan every category. Organ render markings normally live in the Genital category, but
        // legacy or server-generated markings can end up elsewhere while still being front-mounted.
        foreach (var (category, markingList) in humanoid.MarkingSet.Markings)
        {
            foreach (var marking in markingList)
            {
                if (marking.MarkingId.Contains("Butt", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!_markingManager.TryGetMarking(marking, out var markingPrototype))
                    continue;

                if (category != MarkingCategories.Genital
                    && markingPrototype.BodyPart != HumanoidVisualLayers.Genital)
                {
                    continue;
                }

                markingPrototype = GetMarkingForLegStyle(humanoid, markingPrototype);
                foreach (var spriteSpec in markingPrototype.Sprites)
                {
                    if (spriteSpec is not SpriteSpecifier.Rsi rsi)
                        continue;

                    var layerId = $"{markingPrototype.ID}-{rsi.RsiState}";
                    ids.Add(layerId);
                    ids.Add($"{layerId}-glow");
                }
            }
        }

        layerIds = ids.ToArray();
        _northOrganLayerIds[uid] = layerIds;
        return layerIds.Length > 0;
    }

    private void ClearAllMarkings(Entity<HumanoidAppearanceComponent, SpriteComponent> entity)
    {
        var humanoid = entity.Comp1;

        foreach (var markingList in humanoid.ClientOldMarkings.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                RemoveMarking(marking, entity);
            }
        }

        humanoid.ClientOldMarkings.Clear();

        foreach (var markingList in humanoid.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                RemoveMarking(marking, entity);
            }
        }
    }

    private void RemoveMarking(Marking marking, Entity<HumanoidAppearanceComponent, SpriteComponent> entity)
    {
        if (!_markingManager.TryGetMarking(marking, out var prototype))
        {
            return;
        }

        // Palmtree/Coyote: layer IDs use the prototype selected for the current leg style. Remove
        // the base prototype and every alternate so changing leg style cannot leave stale layers.
        RemoveMarkingLayers(prototype.ID, prototype, (entity, entity.Comp2));

        foreach (var alternate in prototype.AlternateSprites.Values)
        {
            if (_prototypeManager.TryIndex(alternate, out MarkingPrototype? alternatePrototype))
                RemoveMarkingLayers(alternatePrototype.ID, alternatePrototype, (entity, entity.Comp2));
        }
    }

    private void RemoveMarkingLayers(string prototypeId, MarkingPrototype prototype, Entity<SpriteComponent> spriteEnt)
    {
        foreach (var sprite in prototype.Sprites)
        {
            if (sprite is not SpriteSpecifier.Rsi rsi)
            {
                continue;
            }

            var layerId = $"{prototypeId}-{rsi.RsiState}";
            if (!_sprite.LayerMapTryGet(spriteEnt.AsNullable(), layerId, out var index, false))
            {
                continue;
            }

            _sprite.LayerMapRemove(spriteEnt.AsNullable(), layerId);
            _sprite.RemoveLayer(spriteEnt.AsNullable(), index);

            // Palmtree/Coyote: remove the glow companion layer as well.
            var glowLayerId = $"{layerId}-glow";
            if (_sprite.LayerMapTryGet(spriteEnt.AsNullable(), glowLayerId, out var glowIndex, false))
            {
                _sprite.LayerMapRemove(spriteEnt.AsNullable(), glowLayerId);
                _sprite.RemoveLayer(spriteEnt.AsNullable(), glowIndex);
            }
        }
    }

    private void AddUndergarments(Entity<HumanoidAppearanceComponent, SpriteComponent> entity, bool undergarmentTop, bool undergarmentBottom)
    {
        var humanoid = entity.Comp1;

        if (undergarmentTop && humanoid.UndergarmentTop != null)
        {
            var marking = new Marking(humanoid.UndergarmentTop, new List<Color> { new Color() });
            if (_markingManager.TryGetMarking(marking, out var prototype))
            {
                // Markings are added to ClientOldMarkings because otherwise it causes issues when toggling the feature on/off.
                humanoid.ClientOldMarkings.Markings.Add(MarkingCategories.UndergarmentTop, new List<Marking> { marking });
                ApplyMarking(prototype, null, true, entity);
            }
        }

        if (undergarmentBottom && humanoid.UndergarmentBottom != null)
        {
            var marking = new Marking(humanoid.UndergarmentBottom, new List<Color> { new Color() });
            if (_markingManager.TryGetMarking(marking, out var prototype))
            {
                humanoid.ClientOldMarkings.Markings.Add(MarkingCategories.UndergarmentBottom, new List<Marking> { marking });
                ApplyMarking(prototype, null, true, entity);
            }
        }
    }

    /// <summary>
    /// Palmtree: whether an exposed south view routes the whole genital organ stack to GenitalUnder.
    /// Shared by the layer routing and the south-specific render-priority sort so they cannot drift.
    /// </summary>
    private bool ShouldUseSouthOrganStack(
        HumanoidAppearanceComponent humanoid,
        SpriteComponent sprite,
        EntityUid uid,
        RsiDirection screenDirection)
    {
        return screenDirection == RsiDirection.South
               && !HasCoveringClothing(uid)
               && !HasVisibleUndergarment(humanoid, MarkingCategories.UndergarmentBottom)
               && humanoid.BaseLayers.TryGetValue(HumanoidVisualLayers.GenitalUnder, out var underSetting)
               && underSetting.AllowsMarkings
               && _sprite.LayerMapTryGet((uid, sprite), HumanoidVisualLayers.GenitalUnder, out _, false);
    }

    private void ApplyMarking(MarkingPrototype markingPrototype,
        IReadOnlyList<Color>? colors,
        bool visible,
        Entity<HumanoidAppearanceComponent, SpriteComponent> entity,
        // Palmtree/Coyote Start: advanced marking editor
        Vector2? scale = null,
        Vector2? offset = null,
        IReadOnlyList<float>? glowLevels = null,
        bool renderOverClothing = false,
        bool renderUnderTopUndergarment = false)
        // Palmtree/Coyote End
    {
        var humanoid = entity.Comp1;
        var sprite = entity.Comp2;
        var markingScale = scale ?? Vector2.One;
        var markingOffset = offset ?? Vector2.Zero;
        var screenDirection = GetScreenDirection(entity.Owner); // Palmtree: direction-aware layers
        var isSideFacing = IsSideFacing(entity.Owner); // Palmtree: includes diagonals

        // Palmtree: NSFW age gate. Genital sprites (organ render markings and the legacy genital
        // marking art) are never drawn for players who have not enabled NSFW content.
        if (markingPrototype.BodyPart == HumanoidVisualLayers.Genital && !NsfwGate.Allowed)
            return;

        // Palmtree/Floof: map sprite states to colors so colorLinks can copy colors between sprites.
        var colorDict = new Dictionary<string, Color>();
        for (var i = 0; i < markingPrototype.Sprites.Count; i++)
        {
            var spriteName = markingPrototype.Sprites[i] switch
            {
                SpriteSpecifier.Rsi rsi => rsi.RsiState,
                SpriteSpecifier.Texture texture => texture.TexturePath.Filename,
                _ => null
            };

            if (spriteName == null)
                continue;

            colorDict[spriteName] = colors != null && i < colors.Count ? colors[i] : Color.White;
        }

        // Palmtree/Coyote: map sprite states to glow levels.
        var glowDict = new Dictionary<string, float>();
        for (var i = 0; i < markingPrototype.Sprites.Count; i++)
        {
            var spriteName = markingPrototype.Sprites[i] switch
            {
                SpriteSpecifier.Rsi rsi => rsi.RsiState,
                SpriteSpecifier.Texture texture => texture.TexturePath.Filename,
                _ => null
            };

            if (spriteName == null)
                continue;

            glowDict[spriteName] = glowLevels != null && i < glowLevels.Count
                ? Math.Clamp(glowLevels[i], 0f, 1f)
                : 0f;
        }

        if (markingPrototype.ColorLinks != null)
        {
            foreach (var (child, parent) in markingPrototype.ColorLinks)
            {
                if (colorDict.TryGetValue(parent, out var linkedColor))
                    colorDict[child] = linkedColor;

                if (glowDict.TryGetValue(parent, out var linkedGlow))
                    glowDict[child] = linkedGlow;
            }
        }

        // Palmtree/Floof: track the sprite order for markings that place several sprites on one layer.
        var layerDict = new Dictionary<string, int>();

        // Palmtree: in the side views both leg base sprites occupy the same pixels and the fixed
        // layer order always draws LLeg above RLeg. A marking on the leg nearest the camera must sit
        // above the far leg's base sprite or it is hidden underneath it. Facing east the right leg
        // is nearest; facing west the left leg already sits on top. Capture the stable top-of-leg
        // anchor once so a multi-sprite marking keeps its order without climbing above GenitalLow.
        var eastRightLegAnchor = -1;
        if (screenDirection == RsiDirection.East
            && _sprite.LayerMapTryGet((entity.Owner, sprite), HumanoidVisualLayers.GenitalLow, out var genitalLowAnchor, false))
        {
            eastRightLegAnchor = genitalLowAnchor;
        }

        for (var j = 0; j < markingPrototype.Sprites.Count; j++)
        {
            var markingSprite = markingPrototype.Sprites[j];

            if (markingSprite is not SpriteSpecifier.Rsi rsi)
            {
                continue;
            }

            var layerSlot = markingPrototype.BodyPart;

            // Palmtree/Floof: a marking may route individual sprites to arbitrary layers.
            if (markingPrototype.Layering != null
                && markingPrototype.Layering.TryGetValue(rsi.RsiState, out var parsedLayer))
            {
                layerSlot = parsedLayer;
            }

            // Palmtree: exposed south keeps the whole genital organ stack on GenitalUnder, below
            // neckwear and hair, so scarves cover the breasts. ApplyMarkingSet orders the stack by
            // the explicit RenderPriority. Covered states keep the normal Genital layer clamp.
            if (layerSlot == HumanoidVisualLayers.Genital
                && !renderOverClothing
                && ShouldUseSouthOrganStack(humanoid, sprite, entity.Owner, screenDirection))
            {
                layerSlot = HumanoidVisualLayers.GenitalUnder;
            }



            // Palmtree: in the front view the body sits between the camera and a tail hanging from
            // the back, so a tail using the general Tail layer must render behind the body instead
            // of over underwear, clothing and limbs. Split tails already send their BEHIND sprites
            // to TailBehind, and their FRONT sprites are empty facing south; this aligns the rest.
            var visibilityLayer = layerSlot;
            if (layerSlot is HumanoidVisualLayers.Tail or HumanoidVisualLayers.TailExtras
                && screenDirection == RsiDirection.South
                && humanoid.BaseLayers.TryGetValue(HumanoidVisualLayers.TailBehind, out var tailBehindSetting)
                && tailBehindSetting.AllowsMarkings
                && _sprite.LayerMapTryGet((entity.Owner, sprite), HumanoidVisualLayers.TailBehind, out _, false))
            {
                layerSlot = HumanoidVisualLayers.TailBehind;
            }

            if (!_sprite.LayerMapTryGet((entity.Owner, sprite), layerSlot, out var targetLayer, false))
            {
                continue;
            }

            var layerVisible = visible;
            layerVisible &= !IsHidden(humanoid, layerSlot);
            // Keep layer-visibility rules aimed at the original layer (e.g. harpies hiding Tail
            // when wearing a wings-hiding coat) applying to rerouted tail sprites too.
            layerVisible &= !IsHidden(humanoid, visibilityLayer);
            layerVisible &= humanoid.BaseLayers.TryGetValue(layerSlot, out var setting)
               && setting.AllowsMarkings;

            var layerId = $"{markingPrototype.ID}-{rsi.RsiState}";

            if (layerDict.TryGetValue(layerSlot.ToString(), out var layerIndex))
            {
                layerDict[layerSlot.ToString()] = layerIndex + 1;
            }
            else
            {
                layerDict.Add(layerSlot.ToString(), 0);
            }

            var layerOrder = layerDict[layerSlot.ToString()];
            var targLayerAdj = targetLayer + layerOrder + 1;

            // Palmtree: lift right-leg markings over the left-leg base while facing east.
            if (eastRightLegAnchor >= 0 && layerSlot == HumanoidVisualLayers.RLeg)
                targLayerAdj = eastRightLegAnchor + layerOrder;

            // Palmtree: tails tuck under hair when the character is seen from the side (east/west).
            // Hair is a marking layer, so insert the tail before the current hair layer instead of
            // moving the base layer itself. The per-sprite index keeps multi-sprite tails ordered.
            if (layerSlot is HumanoidVisualLayers.Tail or HumanoidVisualLayers.TailExtras
                && isSideFacing
                && _sprite.LayerMapTryGet((entity.Owner, sprite), HumanoidVisualLayers.Hair, out var hairLayer, false))
            {
                targLayerAdj = hairLayer + layerOrder;
            }

            // Palmtree: optionally draw snouts and snout covers over hair in side views. Insert them
            // just before the first layer after the hair group so hair markings (which are added
            // after the hair base layer) stay behind them.
            if (humanoid.SnoutAboveHair
                && layerSlot is HumanoidVisualLayers.Snout or HumanoidVisualLayers.SnoutCover
                && isSideFacing
                && _sprite.LayerMapTryGet((entity.Owner, sprite), HumanoidVisualLayers.HeadSide, out var headSideLayer, false))
            {
                targLayerAdj = headSideLayer + layerOrder;
            }

            // Palmtree: keep genital markings beneath clothing while they sit on the genital layer,
            // unless the organ uses the "Never hidden" visibility rule.
            if (layerSlot == HumanoidVisualLayers.Genital)
            {
                var hasCoveringClothing = HasCoveringClothing(entity.Owner);
                var hasUndergarment = HasVisibleUndergarment(humanoid,
                    renderUnderTopUndergarment
                        ? MarkingCategories.UndergarmentTop
                        : MarkingCategories.UndergarmentBottom);
                if (renderOverClothing)
                {
                    if (_sprite.LayerMapTryGet((entity.Owner, sprite), "outerClothing", out var overLayer, false))
                        targLayerAdj = Math.Max(targLayerAdj, overLayer + 1);
                }
                else if (screenDirection != RsiDirection.South
                         || hasCoveringClothing
                         || hasUndergarment)
                {
                    // Anchor under-clothing insertions to stable layers. Anchoring to the clothing
                    // layers themselves shifts their indices as we insert before them, which would
                    // make every successive organ render above the previous one.
                    // Bottom organs go below both undergarments; breasts sit between the bottom and
                    // top undergarment layers. The per-sprite offset keeps multi-sprite markings
                    // (e.g. breast + nipple, butt front + mid) ordered.
                    var layerAnchor = -1;
                    if (renderUnderTopUndergarment)
                    {
                        if (_sprite.LayerMapTryGet((entity.Owner, sprite), HumanoidVisualLayers.UndergarmentBottom, out var underBottomLayer, false))
                            layerAnchor = underBottomLayer + 1;
                    }
                    else
                    {
                        if (_sprite.LayerMapTryGet((entity.Owner, sprite), HumanoidVisualLayers.GenitalLow, out var genitalLowLayer, false))
                            layerAnchor = genitalLowLayer + 1;
                        else if (_sprite.LayerMapTryGet((entity.Owner, sprite), HumanoidVisualLayers.LLeg, out var leftLegLayer, false))
                            layerAnchor = leftLegLayer + 1;
                        else if (_sprite.LayerMapTryGet((entity.Owner, sprite), HumanoidVisualLayers.RLeg, out var rightLegLayer, false))
                            layerAnchor = rightLegLayer + 1;
                    }

                    if (layerAnchor < 0 &&
                        _sprite.LayerMapTryGet((entity.Owner, sprite), "jumpsuit", out var jumpsuitLayer, false))
                    {
                        layerAnchor = jumpsuitLayer - 1;
                    }

                    if (layerAnchor >= 0)
                        targLayerAdj = Math.Min(targLayerAdj, layerAnchor + layerOrder);

                    if (targLayerAdj < 0)
                        targLayerAdj = 0;
                }
            }

            if (!_sprite.LayerMapTryGet((entity.Owner, sprite), layerId, out _, false))
            {
                var layer = _sprite.AddLayer((entity.Owner, sprite), markingSprite, targLayerAdj);
                _sprite.LayerMapSet((entity.Owner, sprite), layerId, layer);
                _sprite.LayerSetSprite((entity.Owner, sprite), layerId, rsi);
            }

            // impstation edit begin - check if there's a shader defined in the markingPrototype's shader datafield, and if there is...
            if (markingPrototype.Shader != null)
            {
                // use spriteComponent's layersetshader function to set the layer's shader to that which is specified.
                sprite.LayerSetShader(layerId, markingPrototype.Shader);
            }
            // impstation edit end

            _sprite.LayerSetVisible((entity.Owner, sprite), layerId, layerVisible);
            // Palmtree/Coyote Start: advanced marking editor
            sprite.LayerSetScale(layerId, markingScale);
            sprite.LayerSetOffset(layerId, markingOffset);
            // Palmtree/Coyote End

            if (!layerVisible || setting == null) // this is kinda implied
            {
                // Palmtree/Coyote: clean up any glow layer when the marking is hidden.
                var hiddenGlowId = $"{layerId}-glow";
                if (_sprite.LayerMapTryGet((entity.Owner, sprite), hiddenGlowId, out var hiddenGlowIndex, false))
                {
                    _sprite.LayerMapRemove((entity.Owner, sprite), hiddenGlowId);
                    _sprite.RemoveLayer((entity.Owner, sprite), hiddenGlowIndex);
                }
                continue;
            }

            // Okay so if the marking prototype is modified but we load old marking data this may no longer be valid
            // and we need to check the index is correct.
            // So if that happens just default to white?
            var color = colorDict.TryGetValue(rsi.RsiState, out var targetColor) ? targetColor : Color.White;
            var glowFactor = glowDict.TryGetValue(rsi.RsiState, out var targetGlow) ? targetGlow : 0f;
            var clampedGlow = Math.Clamp(glowFactor, 0f, 1f);
            var glowLayerId = $"{layerId}-glow";

            // Palmtree/Coyote: split alpha between the base and glow layers so the composed
            // result keeps the original alpha. total = base + glow * (1 - base).
            var baseAlpha = color.A;
            var glowAlpha = baseAlpha * clampedGlow;
            var denom = 1f - glowAlpha;
            var baseLayerAlpha = denom > 0f ? (baseAlpha - glowAlpha) / denom : 0f;
            baseLayerAlpha = Math.Clamp(baseLayerAlpha, 0f, 1f);

            _sprite.LayerSetColor((entity.Owner, sprite), layerId, color.WithAlpha(baseLayerAlpha));

            if (clampedGlow > 0f)
            {
                if (!_sprite.LayerMapTryGet((entity.Owner, sprite), glowLayerId, out _, false))
                {
                    var glowLayer = _sprite.AddLayer((entity.Owner, sprite), markingSprite, targLayerAdj + 1);
                    _sprite.LayerMapSet((entity.Owner, sprite), glowLayerId, glowLayer);
                    _sprite.LayerSetSprite((entity.Owner, sprite), glowLayerId, rsi);
                }

                sprite.LayerSetShader(glowLayerId, "unshaded");
                _sprite.LayerSetVisible((entity.Owner, sprite), glowLayerId, layerVisible);
                _sprite.LayerSetColor((entity.Owner, sprite), glowLayerId, color.WithAlpha(glowAlpha));
                sprite.LayerSetScale(glowLayerId, markingScale);
                sprite.LayerSetOffset(glowLayerId, markingOffset);
            }
            else if (_sprite.LayerMapTryGet((entity.Owner, sprite), glowLayerId, out var glowIndex, false))
            {
                _sprite.LayerMapRemove((entity.Owner, sprite), glowLayerId);
                _sprite.RemoveLayer((entity.Owner, sprite), glowIndex);
            }

            if (humanoid.MarkingsDisplacement.TryGetValue(markingPrototype.BodyPart, out var displacementData) && markingPrototype.CanBeDisplaced)
            {
                _displacement.TryAddDisplacement(displacementData, (entity.Owner, sprite), targLayerAdj + 1, layerId, out _);
            }
        }

        // Palmtree/Floof: base markings (e.g. species adaptors) hide the base layer they replace.
        if (MarkingCategoriesConversion.Category2Layer(
                markingPrototype.MarkingCategory,
                markingPrototype.BodyPart,
                out var baseLayerToHide))
        {
            if (!humanoid.HiddenBaseLayers.Contains(baseLayerToHide))
                humanoid.HiddenBaseLayers.Add(baseLayerToHide);
        }
    }

    /// <summary>
    /// Palmtree/Coyote: returns the alternate marking prototype for the humanoid's leg style, if any.
    /// </summary>
    private MarkingPrototype GetMarkingForLegStyle(HumanoidAppearanceComponent humanoid, MarkingPrototype markingPrototype)
    {
        if (humanoid.LegStyle == HumanoidLegStyle.Plantigrade
            || markingPrototype.AlternateSprites.Count == 0)
        {
            return markingPrototype;
        }

        if (markingPrototype.AlternateSprites.TryGetValue(humanoid.LegStyle, out var altMarkingProtoId)
            || (humanoid.LegStyle != HumanoidLegStyle.Digitigrade
                && markingPrototype.AlternateSprites.TryGetValue(HumanoidLegStyle.Digitigrade, out altMarkingProtoId)))
        {
            if (_prototypeManager.TryIndex(altMarkingProtoId, out MarkingPrototype? altPrototype))
                return altPrototype;
        }

        return markingPrototype;
    }

    public override void SetSkinColor(EntityUid uid, Color skinColor, bool sync = true, bool verify = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid) || humanoid.SkinColor == skinColor)
            return;

        base.SetSkinColor(uid, skinColor, false, verify, humanoid);

        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        foreach (var (layer, spriteInfo) in humanoid.BaseLayers)
        {
            if (!spriteInfo.MatchSkin)
                continue;

            var index = _sprite.LayerMapReserve((uid, sprite), layer);
            sprite[index].Color = skinColor.WithAlpha(spriteInfo.LayerAlpha);
        }
    }

    public override void SetLayerVisibility(
        Entity<HumanoidAppearanceComponent> ent,
        HumanoidVisualLayers layer,
        bool visible,
        SlotFlags? slot,
        ref bool dirty)
    {
        base.SetLayerVisibility(ent, layer, visible, slot, ref dirty);

        var sprite = Comp<SpriteComponent>(ent);
        if (!_sprite.LayerMapTryGet((ent.Owner, sprite), layer, out var index, false))
        {
            if (!visible)
                return;
            index = _sprite.LayerMapReserve((ent.Owner, sprite), layer);
        }

        var spriteLayer = sprite[index];
        if (spriteLayer.Visible == visible)
            return;

        spriteLayer.Visible = visible;

        // I fucking hate this. I'll get around to refactoring sprite layers eventually I swear
        // Just a week away...

        foreach (var markingList in ent.Comp.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                if (_markingManager.TryGetMarking(marking, out var markingPrototype) && markingPrototype.BodyPart == layer)
                    ApplyMarking(markingPrototype, marking.MarkingColors, marking.Visible, (ent, ent.Comp, sprite), marking.MarkingScaleVector, marking.MarkingOffset, marking.MarkingGlow, marking.RenderOverClothing, marking.RenderUnderTopUndergarment);
            }
        }
    }

    // Palmtree/Coyote: override clothing displacement maps based on the humanoid's leg style.
    public void GetDisplacementForLegStyle(
        EntityUid uid,
        string slot,
        HumanoidAppearanceComponent? humanoidAppearance,
        DisplacementData? baseDisplacementDataIn,
        DisplacementData? maleDisplacementDataIn,
        DisplacementData? femaleDisplacementDataIn,
        out DisplacementData? baseDisplacementData,
        out DisplacementData? maleDisplacementData,
        out DisplacementData? femaleDisplacementData)
    {
        baseDisplacementData = baseDisplacementDataIn;
        maleDisplacementData = maleDisplacementDataIn;
        femaleDisplacementData = femaleDisplacementDataIn;

        if (!Resolve(uid, ref humanoidAppearance))
            return;

        if (!_prototypeManager.TryIndex(humanoidAppearance.Species, out SpeciesPrototype? species)
            || !species.AllowDigilegDisplacement)
        {
            return;
        }

        if (!humanoidAppearance.LegDisplacements.TryGetValue(
                humanoidAppearance.LegStyle,
                out ProtoId<LegDisplacementPrototype> displacement))
        {
            return;
        }

        if (!_prototypeManager.TryIndex(displacement, out LegDisplacementPrototype? legDisplacement))
            return;

        baseDisplacementData = legDisplacement.Displacements.GetValueOrDefault(slot, baseDisplacementDataIn);
        maleDisplacementData = legDisplacement.MaleDisplacements.GetValueOrDefault(slot, maleDisplacementDataIn);
        femaleDisplacementData = legDisplacement.FemaleDisplacements.GetValueOrDefault(slot, femaleDisplacementDataIn);
    }
}
