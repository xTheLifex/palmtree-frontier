using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.FloofStation;

/// <summary>
/// Palmtree/Floof: do-after used when toggling the visibility of an undergarment or genital marking.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ModifyUndiesDoAfterEvent : DoAfterEvent
{
    /// <summary>
    ///     The marking prototype that is being modified.
    /// </summary>
    [DataField("markingId", required: true)]
    public string MarkingId = string.Empty;

    /// <summary>
    ///     Whether or not the marking is visible at the moment.
    /// </summary>
    [DataField("visible", required: true)]
    public bool IsVisible;

    private ModifyUndiesDoAfterEvent()
    {
    }

    public ModifyUndiesDoAfterEvent(string markingId, bool isVisible)
    {
        MarkingId = markingId;
        IsVisible = isVisible;
    }

    public override DoAfterEvent Clone() => this;
}
