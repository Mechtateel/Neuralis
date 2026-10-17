using NeuralisAPP_1.ViewModels.Profile;

namespace NeuralisAPP_1.Views.Profile;

public partial class LanguagePickerPage : ContentPage
{
    private readonly ProfileViewModel _vm;

    public LanguagePickerPage(ProfileViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        ApplyChecks(_vm.LanguageCode);
    }

    private void ApplyChecks(string code)
    {
        EnCheck.IsVisible = string.Equals(code, "en", StringComparison.OrdinalIgnoreCase);
        RuCheck.IsVisible = string.Equals(code, "ru", StringComparison.OrdinalIgnoreCase);
        KzCheck.IsVisible = string.Equals(code, "kz", StringComparison.OrdinalIgnoreCase);

        EnRow.Stroke = EnCheck.IsVisible ? GetAccent() : Colors.Transparent;
        EnRow.StrokeThickness = EnCheck.IsVisible ? 1 : 0;
        RuRow.Stroke = RuCheck.IsVisible ? GetAccent() : Colors.Transparent;
        RuRow.StrokeThickness = RuCheck.IsVisible ? 1 : 0;
        KzRow.Stroke = KzCheck.IsVisible ? GetAccent() : Colors.Transparent;
        KzRow.StrokeThickness = KzCheck.IsVisible ? 1 : 0;
    }

    private Color GetAccent()
    {
        if (Application.Current?.Resources.TryGetValue("Accent", out var v) == true && v is Color c)
            return c;
        return Color.FromArgb("#2FE3C9");
    }

    private async void OnBackTapped(object? sender, EventArgs e) =>
        await Navigation.PopAsync();

    private void OnEnTapped(object? sender, EventArgs e) => Pick("en");
    private void OnRuTapped(object? sender, EventArgs e) => Pick("ru");
    private void OnKzTapped(object? sender, EventArgs e) => Pick("kz");

    private void Pick(string code)
    {
        _vm.LanguageCode = code;
        ApplyChecks(code);
    }
}
