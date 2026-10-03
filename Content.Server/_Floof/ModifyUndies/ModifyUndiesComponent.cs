using Content.Shared.Humanoid;

namespace Content.Server.FloofStation.ModifyUndies;

/// <summary>
/// Palmtree/Floof: marks a humanoid as being able to toggle their undergarment/genital markings
/// through the undies verb category.
/// </summary>
[RegisterComponent]
public sealed partial class ModifyUndiesComponent : Component
{
    /// <summary>
    ///     The body part targets for the undies.
    /// </summary>
    public List<HumanoidVisualLayers> BodyPartTargets =
    [
        HumanoidVisualLayers.UndergarmentTop,
        HumanoidVisualLayers.UndergarmentBottom,
        HumanoidVisualLayers.Genital,
    ];
}
