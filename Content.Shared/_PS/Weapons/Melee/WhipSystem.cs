using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._PS.Weapons.Melee;

/// <summary>
/// Handles the combat-mode rules for whips: lashes outside combat mode are harmless,
/// while combat-mode lashes deal the weapon's regular damage and stamina damage.
/// Out-of-combat lashes also pull the target's held item away.
/// </summary>
public sealed class WhipSystem : EntitySystem
{
    [Dependency] private SharedCombatModeSystem _combat = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WhipComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<WhipComponent> ent, ref MeleeHitEvent args)
    {
        if (!_combat.IsInCombatMode(args.User))
        {
            // A harmless lash: cancel the damage and hit reactions, but still apply a little stamina.
            args.Handled = true;
            ApplyStamina(ent, args, ent.Comp.OutOfCombatStaminaDamage);

            // The item throw is server-authoritative; do not try to disarm from client prediction.
            if (_net.IsServer)
                DisarmTargets(args);

            if (args.IsHit &&
                args.HitEntities.Count > 0 &&
                TryComp<MeleeWeaponComponent>(ent, out var melee) &&
                melee.HitSound != null)
            {
                _audio.PlayPredicted(melee.HitSound, args.HitEntities.First(), args.User);
            }

            return;
        }

        ApplyStamina(ent, args, ent.Comp.StaminaDamage);
    }

    private void ApplyStamina(Entity<WhipComponent> ent, MeleeHitEvent args, float amount)
    {
        if (!args.IsHit || amount <= 0f)
            return;

        var targets = new List<EntityUid>();
        foreach (var target in args.HitEntities)
        {
            if (HasComp<StaminaComponent>(target))
                targets.Add(target);
        }

        if (targets.Count == 0)
            return;

        // Mirror StaminaDamageOnHit: split the stamina damage between every eligible target.
        var damage = amount / targets.Count;
        foreach (var target in targets)
        {
            _stamina.TakeStaminaDamage(target, damage, source: args.User, with: args.Weapon);
        }
    }

    private void DisarmTargets(MeleeHitEvent args)
    {
        foreach (var target in args.HitEntities)
        {
            if (target == args.User ||
                Deleted(target) ||
                _mobState.IsIncapacitated(target) ||
                !_hands.TryGetActiveItem(target, out _))
            {
                continue;
            }

            var disarm = new DisarmedEvent(target, args.User, 0f);
            RaiseLocalEvent(target, ref disarm);

            if (disarm.Handled)
                _adminLogger.Add(LogType.DisarmedAction, $"{ToPrettyString(args.User):user} disarmed {ToPrettyString(target):target} with a whip");
        }
    }
}
