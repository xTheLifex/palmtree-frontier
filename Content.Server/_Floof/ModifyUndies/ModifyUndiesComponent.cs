namespace Content.Server.FloofStation.ModifyUndies;

/// <summary>
/// Palmtree/Floof: marks a humanoid as being able to toggle their markings through the undies
/// verb category. Which markings can be toggled is opt-in per marking and set in the character
/// editor (<see cref="Content.Shared.Humanoid.Markings.Marking.CanToggleVisible"/> and
/// <see cref="Content.Shared.Humanoid.Markings.Marking.OtherCanToggleVisible"/>).
/// </summary>
[RegisterComponent]
public sealed partial class ModifyUndiesComponent : Component
{
}
