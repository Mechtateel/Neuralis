using Microsoft.Maui.Controls;
using NeuralisAPP_1.Models.Doctor;

namespace NeuralisAPP_1.Views.Doctor.Controls;

public partial class SidebarProfileView : ContentView
{
    private const int ExpandMs = 220;
    private const int CollapseMs = 180;

    private static readonly Color MenuHoverBg = Color.FromArgb("#1a2332");
    private static readonly Color LogoutHoverBg = Color.FromArgb("#2a1518");
    private static readonly Color HeaderHoverBg = Color.FromArgb("#162032");

    private bool _isProfileExpanded;
    private bool _isAnimating;

    public SidebarProfileView()
    {
        InitializeComponent();
        ApplyChrome();
    }

    public bool IsProfileExpanded => _isProfileExpanded;

    /// <summary>Collapse the profile menu (e.g. when navigating away). Uses a short animation.</summary>
    public void Collapse()
    {
        if (!_isProfileExpanded || _isAnimating)
            return;
        _ = CollapseAnimatedAsync();
    }

    /// <summary>Binds lab technician identity from <see cref="App.CurrentUser"/>.</summary>
    public void ApplyLabFromSession()
    {
        var u = App.CurrentUser;
        if (u == null)
        {
            PrimaryLineLabel.Text = "—";
            SecondaryLineLabel.Text = "Lab Technician";
            return;
        }

        PrimaryLineLabel.Text = !string.IsNullOrWhiteSpace(u.Email)
            ? u.Email
            : (!string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : "User");

        var roleLine = "Lab Technician";
        if (!string.IsNullOrWhiteSpace(u.DoctorSpecialization))
            roleLine += " · " + DoctorSpecializationUi.Format(u.DoctorSpecialization);
        SecondaryLineLabel.Text = roleLine;
    }

    /// <summary>Binds doctor email and specialization (matches doctor dashboard sidebar).</summary>
    public void ApplyDoctor(string email, string? specializationCode)
    {
        PrimaryLineLabel.Text = string.IsNullOrWhiteSpace(email) ? "—" : email;
        var spec = DoctorSpecializationUi.Format(specializationCode);
        SecondaryLineLabel.Text = string.IsNullOrWhiteSpace(spec) ? (specializationCode ?? "") : spec;
    }

    private void ApplyChrome()
    {
        if (_isProfileExpanded)
        {
            ProfileCard.Padding = new Thickness(14);
            RootStack.Spacing = 12;
        }
        else
        {
            ProfileCard.Padding = new Thickness(10, 8, 10, 10);
            RootStack.Spacing = 6;
        }
    }

    private async void OnProfileHeaderTapped(object? sender, EventArgs e)
    {
        if (_isAnimating)
            return;

        if (_isProfileExpanded)
            await CollapseAnimatedAsync();
        else
            await ExpandAnimatedAsync();
    }

    private async Task ExpandAnimatedAsync()
    {
        _isAnimating = true;
        try
        {
            _isProfileExpanded = true;
            ChevronLabel.Text = "keyboard_arrow_up";
            ApplyChrome();

            MenuSection.IsVisible = true;
            MenuSection.Opacity = 0;
            MenuSection.TranslationY = -6;

            await Task.WhenAll(
                MenuSection.FadeTo(1, ExpandMs),
                MenuSection.TranslateTo(0, 0, ExpandMs, Easing.CubicOut));
        }
        finally
        {
            MenuSection.TranslationY = 0;
            _isAnimating = false;
        }
    }

    private async Task CollapseAnimatedAsync()
    {
        _isAnimating = true;
        try
        {
            _isProfileExpanded = false;
            ChevronLabel.Text = "keyboard_arrow_down";
            ApplyChrome();

            await Task.WhenAll(
                MenuSection.FadeTo(0, CollapseMs),
                MenuSection.TranslateTo(0, -4, CollapseMs, Easing.CubicIn));

            MenuSection.IsVisible = false;
            MenuSection.TranslationY = 0;
        }
        finally
        {
            _isAnimating = false;
        }
    }

    private void OnHeaderPointerEntered(object? sender, PointerEventArgs e)
    {
        HeaderHitArea.BackgroundColor = HeaderHoverBg;
    }

    private void OnHeaderPointerExited(object? sender, PointerEventArgs e)
    {
        HeaderHitArea.BackgroundColor = Colors.Transparent;
    }

    private void OnMenuRowPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border b)
            b.BackgroundColor = MenuHoverBg;
    }

    private void OnMenuRowPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border b)
            b.BackgroundColor = Colors.Transparent;
    }

    private void OnLogoutRowPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border b)
            b.BackgroundColor = LogoutHoverBg;
    }

    private void OnLogoutRowPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border b)
            b.BackgroundColor = Colors.Transparent;
    }

    private async void OnProfileTapped(object? sender, EventArgs e)
    {
        await CollapseAnimatedAsync();
        var nav = Shell.Current?.Navigation;
        if (nav == null)
            return;
        await nav.PushAsync(new NeuralisAPP_1.Views.Profile.ProfilePage());
    }

    private async void OnSettingsTapped(object? sender, EventArgs e)
    {
        var page = Shell.Current?.CurrentPage;
        if (page != null)
            await page.DisplayAlert("Settings", "Settings are not available yet.", "OK");
    }

    private async void OnPersonalizationTapped(object? sender, EventArgs e)
    {
        var page = Shell.Current?.CurrentPage;
        if (page != null)
            await page.DisplayAlert("Personalization", "Personalization options are not available yet.", "OK");
    }

    private async void OnHelpTapped(object? sender, EventArgs e)
    {
        var page = Shell.Current?.CurrentPage;
        if (page != null)
            await page.DisplayAlert("Help", "For assistance, contact your system administrator.", "OK");
    }

    private async void OnLogoutTapped(object? sender, EventArgs e)
    {
        await CollapseAnimatedAsync();
        await App.LogoutAsync();
    }
}
