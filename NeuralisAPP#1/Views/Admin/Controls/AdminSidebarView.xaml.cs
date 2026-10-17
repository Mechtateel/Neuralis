using NeuralisAPP_1.Models.Auth;
using NeuralisAPP_1.Services;

namespace NeuralisAPP_1.Views.Admin.Controls;

public enum AdminSidebarItem
{
    Dashboard,
    Users,
    Applications,
    News
}

public partial class AdminSidebarView : ContentView
{
    private const int ExpandMs = 220;
    private const int CollapseMs = 180;

    private static readonly Color NavHoverBg = Color.FromArgb("#131f2e");
    private static readonly Color MenuHoverBg = Color.FromArgb("#1a2332");
    private static readonly Color LogoutHoverBg = Color.FromArgb("#2a1518");
    private static readonly Color HeaderHoverBg = Color.FromArgb("#162032");

    private bool _isProfileExpanded;
    private bool _isProfileAnimating;

    public static readonly BindableProperty ActiveItemProperty = BindableProperty.Create(
        nameof(ActiveItem),
        typeof(AdminSidebarItem),
        typeof(AdminSidebarView),
        AdminSidebarItem.Dashboard,
        propertyChanged: OnActiveItemChanged);

    public AdminSidebarItem ActiveItem
    {
        get => (AdminSidebarItem)GetValue(ActiveItemProperty);
        set => SetValue(ActiveItemProperty, value);
    }

    public event EventHandler? DashboardTapped;
    public event EventHandler? UsersTapped;
    public event EventHandler? ApplicationsTapped;
    public event EventHandler? NewsTapped;

    public AdminSidebarView()
    {
        InitializeComponent();
        ApplyActiveState();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        await RefreshProfileAsync();
    }

    private static void OnActiveItemChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AdminSidebarView view)
            view.ApplyActiveState();
    }

    public void SetProfile(string name, string role)
    {
        ProfileNameLabel.Text = name;
        ProfileRoleLabel.Text = role;
    }

    public async Task RefreshProfileAsync()
    {
        if (AuthService.CurrentUser != null)
        {
            ApplyProfile(AuthService.CurrentUser);
            return;
        }

        if (string.IsNullOrWhiteSpace(AuthService.CurrentToken))
            return;

        var authService = new AuthService();
        var user = await authService.GetMe(AuthService.CurrentToken);
        if (user == null)
            return;

        AuthService.SetCurrentUser(user);
        ApplyProfile(user);
    }

    private void ApplyProfile(MeResponse user)
    {
        ProfileNameLabel.Text = string.IsNullOrWhiteSpace(user.FullName)
            ? user.Email
            : user.FullName;

        ProfileRoleLabel.Text = user.Role switch
        {
            2 => "System Administrator",
            1 => "Doctor",
            _ => "User"
        };
    }

    public void CollapseProfileMenu()
    {
        if (!_isProfileExpanded || _isProfileAnimating)
            return;

        _ = CollapseProfileAnimatedAsync();
    }

    private void ApplyActiveState()
    {
        ResetItem(DashboardItem);
        ResetItem(UsersItem);
        ResetItem(ApplicationsItem);
        ResetItem(NewsItem);

        var active = ActiveItem switch
        {
            AdminSidebarItem.Users => UsersItem,
            AdminSidebarItem.Applications => ApplicationsItem,
            AdminSidebarItem.News => NewsItem,
            _ => DashboardItem
        };

        active.BackgroundColor = Color.FromArgb("#0f2f3f");
        active.Stroke = Color.FromArgb("#1fb6ff");
        active.StrokeThickness = 1;
        SetNavTextColor(active, "#e5e7eb");
    }

    private static void ResetItem(Border item)
    {
        item.BackgroundColor = Colors.Transparent;
        item.StrokeThickness = 0;
        SetNavTextColor(item, "#94a3b8");
    }

    private static void SetNavTextColor(Border nav, string colorHex)
    {
        if (nav.Content is not HorizontalStackLayout layout)
            return;

        foreach (var label in layout.Children.OfType<Label>())
            label.TextColor = Color.FromArgb(colorHex);
    }

    private bool IsActiveNavItem(Border item) =>
        item.BackgroundColor != Colors.Transparent;

    private void OnNavPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border border && !IsActiveNavItem(border))
            border.BackgroundColor = NavHoverBg;
    }

    private void OnNavPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border border && !IsActiveNavItem(border))
            border.BackgroundColor = Colors.Transparent;
    }

    private async void OnProfileHeaderTapped(object? sender, EventArgs e)
    {
        if (_isProfileAnimating)
            return;

        if (_isProfileExpanded)
            await CollapseProfileAnimatedAsync();
        else
            await ExpandProfileAnimatedAsync();
    }

    private async Task ExpandProfileAnimatedAsync()
    {
        _isProfileAnimating = true;
        try
        {
            _isProfileExpanded = true;
            ProfileChevronLabel.Text = "keyboard_arrow_up";
            ProfileCard.Padding = new Thickness(14);
            ProfileRootStack.Spacing = 12;

            ProfileMenuSection.IsVisible = true;
            ProfileMenuSection.Opacity = 0;
            ProfileMenuSection.TranslationY = -6;

            await Task.WhenAll(
                ProfileMenuSection.FadeTo(1, ExpandMs),
                ProfileMenuSection.TranslateTo(0, 0, ExpandMs, Easing.CubicOut));
        }
        finally
        {
            ProfileMenuSection.TranslationY = 0;
            _isProfileAnimating = false;
        }
    }

    private async Task CollapseProfileAnimatedAsync()
    {
        _isProfileAnimating = true;
        try
        {
            _isProfileExpanded = false;
            ProfileChevronLabel.Text = "keyboard_arrow_down";
            ProfileCard.Padding = new Thickness(10, 8, 10, 10);
            ProfileRootStack.Spacing = 6;

            await Task.WhenAll(
                ProfileMenuSection.FadeTo(0, CollapseMs),
                ProfileMenuSection.TranslateTo(0, -4, CollapseMs, Easing.CubicIn));

            ProfileMenuSection.IsVisible = false;
            ProfileMenuSection.TranslationY = 0;
        }
        finally
        {
            _isProfileAnimating = false;
        }
    }

    private void OnProfileHeaderPointerEntered(object? sender, PointerEventArgs e) =>
        ProfileHeaderHitArea.BackgroundColor = HeaderHoverBg;

    private void OnProfileHeaderPointerExited(object? sender, PointerEventArgs e) =>
        ProfileHeaderHitArea.BackgroundColor = Colors.Transparent;

    private void OnMenuRowPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
            border.BackgroundColor = MenuHoverBg;
    }

    private void OnMenuRowPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
            border.BackgroundColor = Colors.Transparent;
    }

    private void OnLogoutRowPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
            border.BackgroundColor = LogoutHoverBg;
    }

    private void OnLogoutRowPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
            border.BackgroundColor = Colors.Transparent;
    }

    private void OnDashboardTapped(object sender, TappedEventArgs e)
    {
        CollapseProfileMenu();
        DashboardTapped?.Invoke(this, EventArgs.Empty);
    }

    private void OnUsersTapped(object sender, TappedEventArgs e)
    {
        CollapseProfileMenu();
        UsersTapped?.Invoke(this, EventArgs.Empty);
    }

    private void OnApplicationsTapped(object sender, TappedEventArgs e)
    {
        CollapseProfileMenu();
        ApplicationsTapped?.Invoke(this, EventArgs.Empty);
    }

    private void OnNewsTapped(object sender, TappedEventArgs e)
    {
        CollapseProfileMenu();
        NewsTapped?.Invoke(this, EventArgs.Empty);
    }

    private async void OnProfileMenuTapped(object? sender, EventArgs e)
    {
        await CollapseProfileAnimatedAsync();
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
        await CollapseProfileAnimatedAsync();
        await App.LogoutAsync();
    }
}
