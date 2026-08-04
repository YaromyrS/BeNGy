namespace BeNGy.Models;

/// <summary>
/// Represents a selectable auto-close delay option for the settings picker.
/// </summary>
public class DelayOption
{
    public int Value { get; }
    public string Label { get; }

    public DelayOption(int value, string label)
    {
        Value = value;
        Label = label;
    }

    // Picker comparison uses ToString/Equals
    public override string ToString() => Label;

    public override bool Equals(object? obj) =>
        obj is DelayOption other && other.Value == Value;

    public override int GetHashCode() => Value.GetHashCode();
}
