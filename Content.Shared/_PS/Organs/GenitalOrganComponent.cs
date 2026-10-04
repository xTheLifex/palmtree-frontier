using Content.Shared._PS.Interactions;
using Robust.Shared.GameStates;

namespace Content.Shared._PS.Organs;

/// <summary>
/// Runtime component for a player-configured genital organ. The matching catalog prototype
/// (<see cref="GenitalOrganPrototype"/>) supplies the selectable type/size and render markings.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GenitalOrganComponent : Component
{
    [DataField, AutoNetworkedField]
    public GenitalType GenitalType;

    /// <summary>Catalog prototype id, e.g. <c>PenisHuman</c>.</summary>
    [DataField, AutoNetworkedField]
    public string OrganPrototype = string.Empty;

    /// <summary>1-based index into the catalog's size list.</summary>
    [DataField, AutoNetworkedField]
    public int Size = 1;

    /// <summary>Whether the organ is currently aroused (switches to the aroused render marking).</summary>
    [DataField, AutoNetworkedField]
    public bool Aroused;

    /// <summary>SPLURT-style exposure rule for this organ.</summary>
    [DataField, AutoNetworkedField]
    public GenitalVisibility Visibility = GenitalVisibility.HiddenByJumpsuit;

    /// <summary>Primary sprite color; null follows the mob's skin color.</summary>
    [DataField, AutoNetworkedField]
    public Color? Color;

    /// <summary>Secondary/detail sprite color (e.g. nipples); null falls back to <see cref="Color"/>.</summary>
    [DataField, AutoNetworkedField]
    public Color? DetailColor;

    /// <summary>Semen output per climax for a penis organ, copied from the profile.</summary>
    [DataField, AutoNetworkedField]
    public int SemenVolume = GenitalOrganSettings.DefaultSemenVolume;
}
