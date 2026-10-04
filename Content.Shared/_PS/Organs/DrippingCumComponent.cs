using Robust.Shared.GameStates;

namespace Content.Shared._PS.Organs;

/// <summary>
/// Semen left inside a mob. <see cref="DrippingCumSystem"/> drains it into floor puddles while the
/// mob is bottomless (no jumpsuit), pausing while clothed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DrippingCumComponent : Component
{
    /// <summary>Semen units still stored inside the mob.</summary>
    [DataField, AutoNetworkedField]
    public double Amount;

    /// <summary>Server-only: total units already dripped, used to pick the decal stage.</summary>
    public double Dripped;

    /// <summary>Server-only: next drip tick.</summary>
    public TimeSpan NextDrip;
}
