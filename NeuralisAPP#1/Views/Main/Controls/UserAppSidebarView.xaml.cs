namespace NeuralisAPP_1.Views.Main.Controls;

public enum UserSidebarActiveItem
{
    Dashboard,
    BookVisit,
    Appointments,
    LatestNews,
    Profile
}

public partial class UserAppSidebarView : ContentView
{
    public static readonly BindableProperty ActiveItemProperty = BindableProperty.Create(
        nameof(ActiveItem),
        typeof(UserSidebarActiveItem),
        typeof(UserAppSidebarView),
        UserSidebarActiveItem.Dashboard,
        propertyChanged: OnActiveItemChanged);

    private bool _profileOpened;

    public UserAppSidebarView()
    {
        InitializeComponent();
    }

    public UserSidebarActiveItem ActiveItem
    {
        get => (UserSidebarActiveItem)GetValue(ActiveItemProperty);
        set => SetValue(ActiveItemProperty, value);
    }

    public event EventHandler? DashboardTapped;
    public event EventHandler? BookVisitTapped;
    public event EventHandler? AppointmentsTapped;
    public event EventHandler? LatestNewsTapped;
    public event EventHandler? LogoutTapped;

    private static void OnActiveItemChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is UserAppSidebarView view)
            view.ApplyActiveColors();
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        ApplyActiveColors();
    }

    public void SetUserDisplayName(string name)
    {
        UserNameLabel.Text = string.IsNullOrWhiteSpace(name) ? "User" : name;
    }

    private void ApplyActiveColors()
    {
        var accent = Color.FromArgb("#3FE0C5");
        var muted = Color.FromArgb("#9FBFC4");

        LabelDashboard.TextColor = ActiveItem == UserSidebarActiveItem.Dashboard ? accent : muted;
        LabelBookVisit.TextColor = ActiveItem == UserSidebarActiveItem.BookVisit ? accent : muted;
        LabelAppointments.TextColor = ActiveItem == UserSidebarActiveItem.Appointments ? accent : muted;
        LabelLatestNews.TextColor = ActiveItem == UserSidebarActiveItem.LatestNews ? accent : muted;
    }

    private void OnDashboardTapped(object? sender, EventArgs e) => DashboardTapped?.Invoke(this, EventArgs.Empty);

    private void OnBookVisitTapped(object? sender, EventArgs e) => BookVisitTapped?.Invoke(this, EventArgs.Empty);

    private void OnAppointmentsTapped(object? sender, EventArgs e) => AppointmentsTapped?.Invoke(this, EventArgs.Empty);

    private void OnLatestNewsTapped(object? sender, EventArgs e) => LatestNewsTapped?.Invoke(this, EventArgs.Empty);

    private void OnProfileHeaderTapped(object? sender, EventArgs e)
    {
        _profileOpened = !_profileOpened;
        SidebarProfileMenu.IsVisible = _profileOpened;
    }

    private void OnLogoutTapped(object? sender, EventArgs e)
    {
        SidebarProfileMenu.IsVisible = false;
        LogoutTapped?.Invoke(this, EventArgs.Empty);
    }

    private async void OnProfileMenuTapped(object? sender, EventArgs e)
    {
        SidebarProfileMenu.IsVisible = false;
        _profileOpened = false;
        var nav = Shell.Current?.Navigation;
        if (nav == null)
            return;
        await nav.PushAsync(new NeuralisAPP_1.Views.Profile.ProfilePage());
    }

    private async void OnSettingsMenuTapped(object? sender, EventArgs e)
    {
        SidebarProfileMenu.IsVisible = false;
        _profileOpened = false;
        var page = Shell.Current?.CurrentPage;
        if (page != null)
            await page.DisplayAlert("Settings", "Settings are not available yet.", "OK");
    }
}
