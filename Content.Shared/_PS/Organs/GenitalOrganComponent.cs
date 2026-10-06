using System.Numerics;
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
    public GenitalVisibility Visibility = GenitalVisibility.HiddenByUnderwear;

    /// <summary>Primary sprite color; null follows the mob's skin color.</summary>
    [DataField, AutoNetworkedField]
    public Color? Color;

    /// <summary>Secondary/detail sprite color (e.g. nipples); null falls back to <see cref="Color"/>.</summary>
    [DataField, AutoNetworkedField]
    public Color? DetailColor;

    /// <summary>Sprite offset applied on top of the organ's render marking (like marking offsets).</summary>
    [DataField, AutoNetworkedField]
    public Vector2 Offset;

    /// <summary>Sprite scale multiplier applied to the organ's render marking (like marking scale).</summary>
    [DataField, AutoNetworkedField]
    public float Scale = 1f;

    /// <summary>Glow level (0-1) for the primary color group.</summary>
    [DataField, AutoNetworkedField]
    public float Glow;

    /// <summary>Glow level (0-1) for the detail color group.</summary>
    [DataField, AutoNetworkedField]
    public float DetailGlow;

    /// <summary>Draw the skin-toned render marking variant when the catalog offers one.</summary>
    [DataField, AutoNetworkedField]
    public bool SkinTone;

    /// <summary>Semen output per climax for a penis organ, copied from the profile.</summary>
    [DataField, AutoNetworkedField]
    public int SemenVolume = GenitalOrganSettings.DefaultSemenVolume;
}
