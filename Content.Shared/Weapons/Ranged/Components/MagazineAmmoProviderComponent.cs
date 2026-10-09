using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;

namespace Content.Shared.Weapons.Ranged;

/// <summary>
/// Wrapper around a magazine (handled via ItemSlot). Passes all AmmoProvider logic onto it.
/// </summary>
[RegisterComponent, Virtual]
[Access(typeof(SharedGunSystem))]
public partial class MagazineAmmoProviderComponent : AmmoProviderComponent
{
    [ViewVariables(VVAccess.ReadWrite), DataField("soundAutoEject")]
    public SoundSpecifier? SoundAutoEject = new SoundPathSpecifier("/Audio/Weapons/Guns/EmptyAlarm/smg_empty_alarm.ogg");

    /// <summary>
    /// Should the magazine automatically eject when empty.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("autoEject")]
    public bool AutoEject = false;

    // Palmtree begin - defer auto-eject until the chambered round has been fired too
    /// <summary>
    /// Normally auto-ejection happens as soon as the magazine runs dry, even though its last
    /// round has just been moved into the chamber. If true, ejection (and
    /// <see cref="SoundAutoEject"/>) waits until that chambered round has been fired as well -
    /// the M1 Garand's en-bloc clip should only eject and ping after the final shot.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("autoEjectDeferChambered")]
    public bool AutoEjectDeferChambered = false;
    // Palmtree end
}
