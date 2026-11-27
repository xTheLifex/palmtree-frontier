using Robust.Client.UserInterface.Controls;

namespace Content.Client._PS.UserInterface.Controls;

/// <summary>
/// Palmtree: a slider paired with a spin box for integer values. The two controls stay in sync
/// and expose one <see cref="ValueChanged"/> event, so callers do not need their own suppression
/// flags for every slider/spin-box pair.
/// </summary>
public sealed class SliderSpinBox : BoxContainer
{
    private bool _suppress;

    public Slider Slider { get; }
    public SpinBox SpinBox { get; }

    /// <summary>Fired when the user changes the value through either control.</summary>
    public event Action<int>? ValueChanged;

    /// <summary>The current value, rounded to the nearest integer.</summary>
    public int Value
    {
        get => (int) MathF.Round(Slider.Value);
        set => SetValueWithoutEvent(value);
    }

    public SliderSpinBox(int minValue, int maxValue)
    {
        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 6;

        Slider = new Slider
        {
            HorizontalExpand = true,
            MinValue = minValue,
            MaxValue = maxValue,
            Rounded = true,
        };

        SpinBox = new SpinBox
        {
            MinWidth = 70,
            IsValid = value => value >= minValue && value <= maxValue,
        };
        SpinBox.InitDefaultButtons();

        AddChild(Slider);
        AddChild(SpinBox);

        Slider.OnValueChanged += _ =>
        {
            if (_suppress)
                return;

            var value = Value;
            _suppress = true;
            SpinBox.OverrideValue(value);
            _suppress = false;
            ValueChanged?.Invoke(value);
        };

        SpinBox.ValueChanged += args =>
        {
            if (_suppress)
                return;

            _suppress = true;
            Slider.SetValueWithoutEvent(args.Value);
            _suppress = false;
            ValueChanged?.Invoke(args.Value);
        };
    }

    /// <summary>Sets the value without raising <see cref="ValueChanged"/>.</summary>
    public void SetValueWithoutEvent(int value)
    {
        _suppress = true;
        Slider.SetValueWithoutEvent(value);
        SpinBox.OverrideValue(value);
        _suppress = false;
    }
}
