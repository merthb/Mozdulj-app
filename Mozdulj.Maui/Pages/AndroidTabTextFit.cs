#if ANDROID
using Android.Util;
using Android.Views;
using Android.Widget;
using Google.Android.Material.Tabs;

namespace Mozdulj.Maui.Pages;

/// <summary>Fit native top-tab titles to their real measured width, in either
/// language. Keep the system-scaled native size as an upper bound. All tabs
/// use the same reduction, and the rest of the app keeps normal font scaling.</summary>
internal sealed class AndroidTabTextFit
{
    private Android.Views.View? _root;
    private ViewTreeObserver? _observer;
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextView, OriginalSize> _sizes = new();
    private sealed record OriginalSize(float Pixels);

    public static void Attach(RootTabs tabs)
    {
        var fit = new AndroidTabTextFit();
        tabs.Loaded += (_, _) => fit.Start(tabs);
        tabs.Unloaded += (_, _) => fit.Stop();
    }

    private void Start(RootTabs tabs)
    {
        Stop();
        if (tabs.Handler?.PlatformView is not Android.Views.View root) return;
        _root = root;
        _observer = root.ViewTreeObserver;
        if (_observer?.IsAlive == true)
            _observer.GlobalLayout += OnGlobalLayout;
        Fit();
    }

    private void Stop()
    {
        if (_observer?.IsAlive == true)
            _observer.GlobalLayout -= OnGlobalLayout;
        _observer = null;
        _root = null;
        _sizes.Clear();
    }

    private void OnGlobalLayout(object? sender, EventArgs e) => Fit();

    private static IEnumerable<T> Descendants<T>(Android.Views.View view) where T : Android.Views.View
    {
        if (view is T match) yield return match;
        if (view is not ViewGroup group) yield break;
        for (var i = 0; i < group.ChildCount; i++)
            if (group.GetChildAt(i) is { } child)
                foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }

    private void Fit()
    {
        if (_root is null) return;
        foreach (var layout in Descendants<TabLayout>(_root))
        {
            if (layout.GetChildAt(0) is not ViewGroup strip) continue;
            var labels = new List<(TextView Label, float Original, float Ratio)>();
            for (var i = 0; i < strip.ChildCount; i++)
            {
                if (strip.GetChildAt(i) is not ViewGroup tab || tab.Width <= 0) continue;
                var label = Descendants<TextView>(tab).FirstOrDefault(t =>
                    t.Visibility == ViewStates.Visible && !string.IsNullOrWhiteSpace(t.Text));
                if (label is null || label.TextSize <= 0) continue;
                var original = _sizes.GetValue(label, t =>
                {
                    var size = t.TextSize;
                    t.SetSingleLine(true);
                    t.SetHorizontallyScrolling(false);
                    return new OriginalSize(size);
                }).Pixels;
                var available = tab.Width - tab.PaddingLeft - tab.PaddingRight
                    - label.PaddingLeft - label.PaddingRight - 2;
                if (available <= 0) continue;
                var text = label.Text ?? "";
                // Native tab styles may transform text to uppercase.
                var width = Math.Max(label.Paint.MeasureText(text),
                    label.Paint.MeasureText(text.ToUpperInvariant())) * original / label.TextSize;
                if (width <= 0) continue;
                labels.Add((label, original, Math.Min(1f, available / width)));
            }
            if (labels.Count == 0) continue;
            var ratio = labels.Min(x => x.Ratio);
            foreach (var (label, original, _) in labels)
            {
                var size = Math.Max(1f, MathF.Floor(original * ratio));
                if (Math.Abs(label.TextSize - size) >= 0.5f)
                    label.SetTextSize(ComplexUnitType.Px, size);
            }
        }
    }
}
#endif
