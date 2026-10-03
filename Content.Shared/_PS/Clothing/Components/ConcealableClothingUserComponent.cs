using Robust.Shared.GameStates;

namespace Content.Shared._PS.Clothing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedConcealableClothingSystem))]
public sealed partial class ConcealableClothingUserComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public HashSet<string> Categories = [];
}
