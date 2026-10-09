using System;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Wieldable.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._PS.Weapons;

/// <summary>
/// Palmtree: firing a gun swaps the active hand when the other hand also holds a gun, so
/// dual-wielded firearms (pistols, SMGs, ...) can be fired alternately without the player having
/// to manually switch hands between shots. The akimbo partner inherits the fired gun's cooldown,
/// so the pair stays paced at the weapon's fire rate while the hand-select penalty is ignored.
/// Knives, melee weapons and two-handed guns that are not wielded are ignored, so this only kicks
/// in for actually usable firearms.
/// </summary>
public sealed class GunHandSwapSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GunComponent, GunShotEvent>(OnGunShot);
    }

    private void OnGunShot(Entity<GunComponent> ent, ref GunShotEvent args)
    {
        // Don't interrupt an in-progress burst - the swap happens once the burst completes.
        if (ent.Comp.BurstActivated)
            return;

        if (!TryComp<HandsComponent>(args.User, out var hands))
            return;

        // Only ever swap for a gun fired from the user's active hand (ignores turrets, triggers, ...).
        if (!_hands.TryGetHeldItem((args.User, hands), hands.ActiveHandId, out var fired) || fired != ent.Owner)
            return;

        foreach (var handId in hands.SortedHands)
        {
            if (handId == hands.ActiveHandId)
                continue;

            if (!_hands.TryGetHeldItem((args.User, hands), handId, out var other) || other == null)
                continue;

            if (!TryComp<GunComponent>(other, out var otherGun))
                continue;

            // Two-handed guns cannot fire unless wielded, so they are not akimbo material.
            if (TryComp<GunRequiresWieldComponent>(other, out _) &&
                (!TryComp<WieldableComponent>(other, out var wieldable) || !wieldable.Wielded))
                continue;

            // Pacing: copy the cooldown the fired gun just received onto the akimbo partner, so the
            // pair alternates at the weapon's own fire rate. Never shorten a longer cooldown the
            // partner already has (e.g. a slow weapon that fired recently).
            var akimboCooldown = ent.Comp.NextFire;

            if (!_hands.TrySetActiveHand((args.User, hands), handId))
                return;

            if (akimboCooldown > otherGun.NextFire)
            {
                otherGun.NextFire = akimboCooldown;
                DirtyField(other.Value, otherGun, nameof(GunComponent.NextFire));
            }

            // Hand selection also applies the melee attack cooldown, which cancels shooting (see
            // MeleeWeaponComponent's ShotAttemptedEvent handler) - clear it so it cannot outlast the
            // copied akimbo cooldown.
            if (TryComp<MeleeWeaponComponent>(other, out var meleeAfter) &&
                meleeAfter.NextAttack > akimboCooldown)
            {
                meleeAfter.NextAttack = akimboCooldown;
                DirtyField(other.Value, meleeAfter, nameof(MeleeWeaponComponent.NextAttack));
            }

            return;
        }
    }
}
