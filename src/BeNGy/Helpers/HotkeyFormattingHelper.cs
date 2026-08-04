using System.Collections.Generic;
using BeNGy.Services.Interfaces;

namespace BeNGy.Helpers;

public static class HotkeyFormattingHelper
{
    public static string GetKeyName(HotkeyKey key)
    {
        return key switch
        {
            HotkeyKey.None => "None",
            HotkeyKey.Enter => "Enter",
            HotkeyKey.Escape => "Esc",
            HotkeyKey.Space => "Space",
            HotkeyKey.Backspace => "Backspace",
            HotkeyKey.Tab => "Tab",
            HotkeyKey.Delete => "Del",
            HotkeyKey.Insert => "Ins",
            HotkeyKey.Home => "Home",
            HotkeyKey.End => "End",
            HotkeyKey.PageUp => "PageUp",
            HotkeyKey.PageDown => "PageDown",
            HotkeyKey.Left => "Left",
            HotkeyKey.Right => "Right",
            HotkeyKey.Up => "Up",
            HotkeyKey.Down => "Down",
            >= HotkeyKey.F1 and <= HotkeyKey.F12 => key.ToString(),
            >= HotkeyKey.D0 and <= HotkeyKey.D9 => ((char)key).ToString(),
            >= HotkeyKey.A and <= HotkeyKey.Z => ((char)key).ToString(),
            _ => key.ToString() // Fallback to enum name for others
        };
    }

    public static string FormatHotkey(HotkeyModifiers modifiers, HotkeyKey key)
    {
        var parts = new List<string>();

        if (modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");

        parts.Add(GetKeyName(key));
        return string.Join("+", parts);
    }
}
