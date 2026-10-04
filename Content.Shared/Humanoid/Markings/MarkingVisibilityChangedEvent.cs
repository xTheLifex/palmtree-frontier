namespace Content.Shared.Humanoid.Markings;

/// <summary>
/// Raised directed on a humanoid after a marking's visibility was toggled (e.g. ModifyUndies), so
/// systems that render based on marking visibility (genital organs, undergarments) can refresh.
/// </summary>
public sealed class MarkingVisibilityChangedEvent(string markingId) : EntityEventArgs
{
    public readonly string MarkingId = markingId;
}
