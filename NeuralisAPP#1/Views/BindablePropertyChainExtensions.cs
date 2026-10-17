namespace NeuralisAPP_1.Views;

/// <summary>
/// Tiny fluent helper that lets us set a BindableProperty (e.g. <c>Grid.ColumnProperty</c>)
/// on a freshly-created element inline inside a collection initializer.
/// Reachable from <c>NeuralisAPP_1.Views.Admin</c>, <c>NeuralisAPP_1.Views.Profile</c>, etc.
/// via C# namespace ancestor lookup, so no <c>using</c> is required.
/// </summary>
internal static class BindablePropertyChainExtensions
{
    public static T Set<T>(this T element, Microsoft.Maui.Controls.BindableProperty property, object value)
        where T : Microsoft.Maui.Controls.BindableObject
    {
        element.SetValue(property, value);
        return element;
    }
}
