using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._PS.Clothing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConcealableClothingComponent : Component
{
    /// <summary>
    /// Action for toggling visibility of this piece of clothing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId ToggleAction = "ActionToggleConcealment";

    /// <summary>
    /// The category that this is able to hide.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Category = string.Empty;

    /// <summary>
    /// Action for toggling visibility of this piece of clothing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;

    /// <summary>
    /// When <see langword="true"/>, it will require an implant to be used.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool RequireImplant = true;

    /// <summary>
    /// Whether the clothing is to be displayed or not.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool IsConcealed;
}
