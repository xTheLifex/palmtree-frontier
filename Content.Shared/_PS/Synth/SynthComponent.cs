namespace Content.Shared._PS.Synth;

/// <summary>
/// Marker component for Synth entities. Synths are synthetic and do not need to breathe;
/// systems that do not apply to them (e.g. respiration) check for this component.
/// </summary>
[RegisterComponent]
public sealed partial class SynthComponent : Component
{
}
