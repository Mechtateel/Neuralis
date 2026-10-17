using NeuralisAPP_1.Models.Doctor;
using NeuralisAPP_1.Localization;
using NeuralisAPP_1.Services;

namespace NeuralisAPP_1.Views.Doctor;

public partial class DoctorDashboardPage : ContentPage
{
    private DoctorService? _doctorService;
    private Border? _activeNav;

    public DoctorDashboardPage()
    {
        InitializeComponent();
        ApplyLanguage();
    }

    // ✅ ЕДИНСТВЕННЫЙ OnAppearing
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // sidebar: активный пункт
        SetActive(NavDashboard);
        ApplyLanguage();

        // бизнес-логика
        await InitializeDoctorAsync();
    }

    protected override void OnDisappearing()
    {
        SidebarProfile.Collapse();
        base.OnDisappearing();
    }

    // ==========================
    // 🔐 DOCTOR INIT
    // ==========================
    private async Task InitializeDoctorAsync()
    {
        if (string.IsNullOrWhiteSpace(AuthService.CurrentToken))
        {
            await App.LogoutAsync();
            return;
        }

        _doctorService = new DoctorService(AuthService.CurrentToken);

        var hasAccess = await _doctorService.CheckDashboardAccess();
        if (!hasAccess)
        {
            await DisplayAlert(
                "Access denied",
                "Doctor profile is not verified yet",
                "OK");

            await App.LogoutAsync();
            return;
        }

        DoctorMeResponse? doctor = await _doctorService.GetMe();
        if (doctor == null)
        {
            await App.LogoutAsync();
            return;
        }

        ApplyDoctorProfile(doctor);
    }

    private void ApplyDoctorProfile(DoctorMeResponse doctor)
    {
        SidebarProfile.ApplyDoctor(doctor.Email, doctor.Specialization);
        VerificationBadge.IsVisible = doctor.IsVerified;
    }

    private void ApplyLanguage()
    {
        WelcomeLabel.Text = T.Get("Welcome back", "С возвращением", "Қайта келдіңіз");
        AiOnlineLabel.Text = T.Get("AI Diagnostics System Online", "Система AI диагностики онлайн", "AI диагностика жүйесі онлайн");
        PatientsTodayLabel.Text = T.Get("Patients Today", "Пациентов сегодня", "Бүгінгі пациенттер");
        CriticalAlertsTitleLabel.Text = T.Get("Critical Alerts", "Критические сигналы", "Сыни ескертулер");
        PendingReviewsTitleLabel.Text = T.Get("Pending Reviews", "Ожидают проверки", "Тексеруді күтуде");
        AiAccuracyLabel.Text = T.Get("AI Accuracy", "Точность AI", "AI дәлдігі");
    }

    private void OnLangEnClicked(object sender, EventArgs e)
    {
        Lang.Set("en");
        ApplyLanguage();
    }

    private void OnLangRuClicked(object sender, EventArgs e)
    {
        Lang.Set("ru");
        ApplyLanguage();
    }

    private void OnLangKzClicked(object sender, EventArgs e)
    {
        Lang.Set("kz");
        ApplyLanguage();
    }

    // ==========================
    // 🧭 NAVIGATION
    // ==========================
    private async void OnPatientsTapped(object sender, EventArgs e)
    {
        SetActive(NavPatients);
        await Navigation.PushAsync(new DoctorPatientsPage());
    }

    private void OnDashboardTapped(object sender, EventArgs e)
    {
        SetActive(NavDashboard);
        // мы уже тут — ничего не делаем
    }

    // ==========================
    // 🖱 HOVER / ACTIVE EFFECTS
    // ==========================
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
            {
                label.TextColor = Color.FromArgb(colorHex);
            }
        }
    }

    async void OnLabTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DoctorLabResultsPage());
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


    async void OnDashboardPlaceholderAction(object sender, EventArgs e)
    {
        var label = sender is Button b ? b.Text : "Action";
        await DisplayAlert(
            "Neuralis",
            $"{label} is not connected to live clinical data yet.",
            "OK");
    }

}
