using Content.Shared._PS.Weapons;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;

namespace Content.Server._PS.Weapons;

/// <summary>
/// Applies <see cref="GunDamageMultiplierComponent"/> to projectiles fired by that gun.
/// </summary>
public sealed class GunDamageMultiplierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnProjectileHit(Entity<ProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (ent.Comp.Weapon is not { } weapon
            || !TryComp<GunDamageMultiplierComponent>(weapon, out var multiplier)
            || multiplier.Multiplier == 1f)
            return;

        args.Damage = args.Damage * multiplier.Multiplier;
    }
}
