using Content.Server.Popups;
using Content.Shared._PS.Synth.DeadStartupButton;
using Content.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server._PS.Synth.DeadStartupButton;

/// <summary>
/// Palmtree: server half of the synth reboot - revives a dead synth whose damage is below the
/// critical threshold, otherwise plays a failure buzz. Ported from the EinsteinEngines IPC
/// DeadStartupButton (via coyote-bayou), without the battery/electrocution parts.
/// </summary>
public sealed class DeadStartupButtonSystem : SharedDeadStartupButtonSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeadStartupButtonComponent, OnDoAfterButtonPressedEvent>(OnDoAfter);
        SubscribeLocalEvent<DeadStartupButtonComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnDoAfter(EntityUid uid, DeadStartupButtonComponent comp, OnDoAfterButtonPressedEvent args)
    {
        if (args.Handled || args.Cancelled
            || !TryComp<MobStateComponent>(uid, out var mobStateComponent)
            || !_mobState.IsDead(uid, mobStateComponent)
            || !TryComp<MobThresholdsComponent>(uid, out var mobThresholdsComponent)
            || !TryComp<DamageableComponent>(uid, out var damageable))
            return;

        // Palmtree: a small repair first (like a defib's zap heal) so a modest amount of overkill
        // can still be rebooted; only a wrecked chassis fails.
        if (comp.RebootHeal != null)
            _damageable.TryChangeDamage(uid, comp.RebootHeal, true, origin: args.User);

        if (_mobThreshold.TryGetThresholdForState(uid, MobState.Dead, out var deadThreshold, mobThresholdsComponent) &&
            damageable.TotalDamage < deadThreshold)
        {
            var alive = _mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var criticalThreshold, mobThresholdsComponent) &&
                        damageable.TotalDamage < criticalThreshold;
            _mobState.ChangeMobState(uid, alive ? MobState.Alive : MobState.Critical, mobStateComponent);
        }
        else
        {
            _audio.PlayPvs(comp.BuzzSound, uid, AudioHelpers.WithVariation(0.05f, _robustRandom));
            _popup.PopupEntity(Loc.GetString("dead-startup-system-reboot-failed", ("target", MetaData(uid).EntityName)), uid);
            Spawn("EffectSparks", Transform(uid).Coordinates);
        }
    }

    private void OnMobStateChanged(EntityUid uid, DeadStartupButtonComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            return;

        _popup.PopupEntity(Loc.GetString("dead-startup-system-reboot-success", ("target", MetaData(uid).EntityName)), uid);
        _audio.PlayPvs(comp.Sound, uid);
    }
}
