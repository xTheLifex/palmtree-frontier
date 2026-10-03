using Robust.Shared.GameStates;

namespace Content.Shared._PS.Clothing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedConcealableClothingSystem))]
public sealed partial class ConcealableClothingImplantComponent : Component
{
    /// <summary>
    /// This component of the implant will only allow to hide clothes matching this category.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Category = string.Empty;
}
