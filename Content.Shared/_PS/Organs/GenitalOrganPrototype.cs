using System.Linq;
using Content.Shared._PS.Interactions;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Shared._PS.Organs;

/// <summary>
/// Catalog prototype for a player-selectable genital organ type (penis/vagina/balls/breasts/butt/belly).
/// Each entry maps the selectable <em>sizes</em> to render markings so the existing marking
/// renderer (layering, color links, clothing clamping) can draw the organ.
/// </summary>
/// <remarks>
/// This is the player-facing replacement for the old genital markings: the character editor
/// picks a type + size per organ, and <see cref="GenitalOrganSystem"/> inserts an organ entity
/// that renders through one of the <see cref="GenitalOrganSize"/> marking ids.
/// </remarks>
[Prototype("genitalOrgan")]
public sealed partial class GenitalOrganPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Locale key for the type dropdown.</summary>
    [DataField("name", required: true)]
    public LocId Name { get; private set; }

    /// <summary>Which genital slot this catalog entry belongs to.</summary>
    [DataField("genitalType", required: true)]
    public GenitalType GenitalType { get; private set; }

    /// <summary>Selectable sizes, index 0 is size 1 in the profile.</summary>
    [DataField("sizes", required: true)]
    public List<GenitalOrganSize> Sizes { get; private set; } = new();

    /// <summary>
    /// Whether this organ has any aroused render marking. Organs without one (balls, breasts) are
    /// flaccid-only: arousal is rejected and the in-game toggle is disabled.
    /// </summary>
    public bool CanArouse => Sizes.Any(size => size.Aroused != null);
}

/// <summary>One selectable size of a genital organ type.</summary>
[DataDefinition]
public sealed partial class GenitalOrganSize
{
    /// <summary>Locale key for the size dropdown entry.</summary>
    [DataField("name")]
    public LocId Name { get; private set; }

    /// <summary>Render marking used when the organ is not aroused.</summary>
    [DataField("flaccid")]
    public ProtoId<MarkingPrototype>? Flaccid { get; private set; }

    /// <summary>Render marking used when the organ is aroused. Falls back to <see cref="Flaccid"/>.</summary>
    [DataField("aroused")]
    public ProtoId<MarkingPrototype>? Aroused { get; private set; }
}
