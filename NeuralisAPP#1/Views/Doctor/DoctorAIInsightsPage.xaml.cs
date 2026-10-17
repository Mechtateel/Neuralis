namespace NeuralisAPP_1.Views.Doctor;

public partial class DoctorAIInsightsPage : ContentPage
{
    public DoctorAIInsightsPage()
    {
        InitializeComponent();
    }

    Border? _activeNav;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SetActive(NavAI); // 👈 ВАЖНО: активный пункт
    }

    // ================= HOVER =================

    void OnNavPointerEntered(object sender, PointerEventArgs e)
    {
        if (sender is not Border border || border == _activeNav)
            return;

        border.BackgroundColor = Color.FromArgb("#0f172a");
        SetNavTextColor(border, "#e5e7eb");
    }

    void OnNavPointerExited(object sender, PointerEventArgs e)
    {
        if (sender is not Border border || border == _activeNav)
            return;

        border.BackgroundColor = Colors.Transparent;
        SetNavTextColor(border, "#9ca3af");
    }

    // ================= ACTIVE STATE =================

    void SetActive(Border active)
    {
        ResetNav(NavDashboard);
        ResetNav(NavPatients);
        ResetNav(NavLab);
        ResetNav(NavAI);
        ResetNav(NavAppointments);

        _activeNav = active;

        active.BackgroundColor = Color.FromArgb("#111827");
        SetNavTextColor(active, "#2dd4bf");
    }

    void ResetNav(Border nav)
    {
        nav.BackgroundColor = Colors.Transparent;
        SetNavTextColor(nav, "#9ca3af");
    }

    void SetNavTextColor(Border nav, string colorHex)
    {
        if (nav.Content is HorizontalStackLayout layout)
        {
            foreach (var label in layout.Children.OfType<Label>())
                label.TextColor = Color.FromArgb(colorHex);
        }
    }

    async void OnDashboardTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DoctorDashboardPage());
    }

    async void OnPatientsTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DoctorPatientsPage());
    }

    async void OnLabTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DoctorLabResultsPage());
    }

    void OnAITapped(object sender, EventArgs e)
    {
        // мы уже здесь
    }

    async void OnAppointmentsTapped(object sender, EventArgs e)
    {
        SetActive(NavAppointments);
        await Navigation.PushAsync(new DoctorAppointmentsPage());
    }

    bool _isProfileOpen;

    async void OnProfileTapped(object sender, EventArgs e)
    {
        if (_isProfileOpen)
        {
            await CloseProfileMenu();
            return;
        }

        ProfileMenu.IsVisible = true;
        await ProfileMenu.FadeTo(1, 120);
        await ProfileMenu.ScaleTo(1, 120, Easing.CubicOut);

        _isProfileOpen = true;
    }

    async Task CloseProfileMenu()
    {
        await ProfileMenu.FadeTo(0, 100);
        await ProfileMenu.ScaleTo(0.96, 100);

        ProfileMenu.IsVisible = false;
        _isProfileOpen = false;
    }

    async void OnLogoutClicked(object sender, EventArgs e)
    {
        await CloseProfileMenu();
        await App.LogoutAsync();
    }

}
