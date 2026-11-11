using Microsoft.UI.Xaml.Media;

namespace BeNGy.Platforms.Windows;

/// <summary>
/// Waits for XAML to render. Used to resize the palette only AFTER a page switch
/// has drawn: resizing an acrylic window first costs ~100 ms of composition lag
/// during which the old page shows over a black (growing) or empty (shrinking)
/// area -- frame captures, 2026-11-11.
/// </summary>
public static class FrameAwaiter
{
    /// <summary>
    /// Completes after <paramref name="frames"/> rendering ticks. Rendering fires
    /// before each frame's layout and render, so two ticks mean the first frame
    /// with the current content has been produced. Gives up after
    /// <paramref name="timeoutMs"/>: a hidden window may not render at all, and a
    /// late resize beats one that never happens. Call on the UI thread.
    /// </summary>
    public static async Task NextFramesAsync(int frames = 2, int timeoutMs = 300)
    {
        var done = new TaskCompletionSource();
        var remaining = frames;

        void OnRendering(object? sender, object e)
        {
            if (--remaining > 0) return;
            CompositionTarget.Rendering -= OnRendering;
            done.TrySetResult();
        }

        CompositionTarget.Rendering += OnRendering;
        if (await Task.WhenAny(done.Task, Task.Delay(timeoutMs)) != done.Task)
            CompositionTarget.Rendering -= OnRendering;   // back on the UI thread here
    }

    /// <summary>
    /// Like <see cref="NextFramesAsync"/>, but first waits for <paramref name="page"/>'s
    /// first layout if it has never had one: its first appearance spreads layout
    /// over more frames than two ticks cover (captured: the window still grew over
    /// ~70 ms of black on the first Settings open).
    /// </summary>
    public static async Task RenderedAsync(Microsoft.Maui.Controls.VisualElement page, int timeoutMs = 300)
    {
        if (page.Height <= 0)
        {
            var laidOut = new TaskCompletionSource();
            void OnSizeChanged(object? sender, EventArgs e)
            {
                if (page.Height > 0) laidOut.TrySetResult();
            }

            page.SizeChanged += OnSizeChanged;
            await Task.WhenAny(laidOut.Task, Task.Delay(timeoutMs));
            page.SizeChanged -= OnSizeChanged;
        }

        await NextFramesAsync(timeoutMs: timeoutMs);
    }
}
