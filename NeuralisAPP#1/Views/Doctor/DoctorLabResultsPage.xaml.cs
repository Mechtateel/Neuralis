using NeuralisAPP_1.Services;
using NeuralisAPP_1.ViewModels.Doctor;

namespace NeuralisAPP_1.Views.Doctor;

public partial class DoctorLabResultsPage : ContentPage
{
    private readonly DoctorLabResultsViewModel _vm;
    private Border? _activeNav;

    public DoctorLabResultsPage()
    {
        InitializeComponent();
        _vm = new DoctorLabResultsViewModel();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        SetActive(NavLab);

        var token = AuthService.CurrentToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlert("Session", "Please sign in again.", "OK");
            return;
        }

        _vm.AttachToken(token);
        await _vm.LoadResultsAsync();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        try
        {
            await _vm.LoadResultsAsync();
        }
        finally
        {
            LabRefreshView.IsRefreshing = false;
        }
    }

    private async void OnViewSelectedClicked(object? sender, EventArgs e) => await _vm.ViewSelectedAsync();

    private async void OnDownloadSelectedClicked(object? sender, EventArgs e) => await _vm.DownloadSelectedAsync();

    private async void OnAnalyzeSelectedClicked(object? sender, EventArgs e) => await _vm.AnalyzeSelectedAsync();

    private void OnDismissAiClicked(object? sender, EventArgs e) => _vm.DismissAiPanel();

    // ================= NAVIGATION =================

    async void OnDashboardTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DoctorDashboardPage());
    }

    async void OnPatientsTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DoctorPatientsPage());
    }

    void OnLabTapped(object sender, EventArgs e)
    {
        // Already on Lab Results.
    }

    async void OnAITapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DoctorAIInsightsPage());
    }

    async void OnAppointmentsTapped(object sender, EventArgs e)
    {
        SetActive(NavAppointments);
        await Navigation.PushAsync(new DoctorAppointmentsPage());
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

    // ================= PROFILE =================


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
