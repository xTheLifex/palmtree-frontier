using Robust.Shared.GameStates;

namespace Content.Shared._PS.Weapons;

/// <summary>
/// Multiplies the damage of projectiles fired by this gun.
/// Damage lives on the ammunition in SS14, so this lets a specific weapon hit harder
/// without changing the shared cartridge for every other gun that uses it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunDamageMultiplierComponent : Component
{
    [DataField]
    public float Multiplier = 1f;
}
