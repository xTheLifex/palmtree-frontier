using Content.Shared.Damage;
using Robust.Shared.Audio;

namespace Content.Shared._PS.Synth.DeadStartupButton;

/// <summary>
/// Palmtree: adds a "Reboot" verb to a dead synth. Ported from the EinsteinEngines IPC
/// DeadStartupButton (via coyote-bayou); rebooting revives the synth unless the chassis is too
/// damaged.
/// </summary>
[RegisterComponent]
public sealed partial class DeadStartupButtonComponent : Component
{
    [DataField("verbText")]
    public string VerbText = "dead-startup-button-verb";

    [DataField("sound")]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_EE/Effects/Silicon/startup.ogg");

    [DataField("buttonSound")]
    public SoundSpecifier ButtonSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");

    [DataField("doAfterInterval"), ViewVariables(VVAccess.ReadWrite)]
    public float DoAfterInterval = 1f;

    [DataField("buzzSound")]
    public SoundSpecifier BuzzSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");

    /// <summary>
    /// Palmtree: small repair applied before the reboot check, like a defibrillator's zap heal.
    /// Lets a synth that died with a modest amount of overkill be rebooted.
    /// </summary>
    [DataField("rebootHeal")]
    public DamageSpecifier? RebootHeal;

    [DataField("verbPriority"), ViewVariables(VVAccess.ReadWrite)]
    public int VerbPriority = 1;
}
