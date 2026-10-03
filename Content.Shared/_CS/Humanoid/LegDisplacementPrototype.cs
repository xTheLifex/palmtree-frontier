using Content.Shared.DisplacementMap;
using Robust.Shared.Prototypes;

namespace Content.Shared._CS;

/// <summary>
/// Palmtree/Coyote: displacement maps applied to clothing slots based on the humanoid's leg style.
/// </summary>
[Prototype("legDisplacement")]
public sealed partial class LegDisplacementPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    [DataField]
    public Dictionary<string, DisplacementData?> Displacements = new();

    [DataField]
    public Dictionary<string, DisplacementData?> FemaleDisplacements = new();

    [DataField]
    public Dictionary<string, DisplacementData?> MaleDisplacements = new();
}
