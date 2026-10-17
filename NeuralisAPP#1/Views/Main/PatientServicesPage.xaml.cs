using NeuralisAPP_1.Views.Main.Controls;

namespace NeuralisAPP_1.Views.Main;

public partial class PatientServicesPage : ContentPage
{
    private const double CardsStackBreakpoint = 920;

    private bool _cardsAreStacked;

    public PatientServicesPage()
    {
        InitializeComponent();

        UserSidebarCoordinator.Wire(
            AppSidebar,
            UserSidebarActiveItem.Dashboard,
            onDashboardTap: () => Shell.Current.GoToAsync("//Main"),
            onBookVisitTap: async () => await Navigation.PushAsync(new BookAppointmentPage()),
            onAppointmentsTap: async () => await Navigation.PushAsync(new UserAppointmentsPage()),
            onLatestNewsTap: () => Shell.Current.GoToAsync("//LatestUpdates"),
            onLogoutTap: () => App.LogoutAsync(),
            closeSidebar: () => GlobalLayout.CloseSidebar());

        PatientMainColumn.SizeChanged += (_, _) => ApplyResponsiveLayout();
        Loaded += (_, _) => ApplyResponsiveLayout();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        MessagingCenter.Subscribe<App>(this, "AUTH_CHANGED", __ =>
        {
            MainThread.BeginInvokeOnMainThread(UpdateAuthUi);
        });

        UpdateAuthUi();
        AppointmentDatePicker.MinimumDate = DateTime.Today;
        if (AppointmentDatePicker.Date < DateTime.Today)
            AppointmentDatePicker.Date = DateTime.Today;

        SetSpecialtySelected(0);
        SetTimeSlotActive(Slot1030);
        ApplyResponsiveLayout();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        MessagingCenter.Unsubscribe<App>(this, "AUTH_CHANGED");
    }

    private void UpdateAuthUi()
    {
        var user = App.CurrentUser;
        AppSidebar.SetUserDisplayName(
            user == null || string.IsNullOrWhiteSpace(user.FullName)
                ? "User"
                : user.FullName);
    }

    private void ApplyResponsiveLayout()
    {
        var w = PatientMainColumn.Width;
        if (w <= 0)
            return;

        var stacked = w < CardsStackBreakpoint;
        if (stacked != _cardsAreStacked)
        {
            _cardsAreStacked = stacked;
            ApplyMainCardsLayout(stacked);
        }
    }

    private void ApplyMainCardsLayout(bool stacked)
    {
        MainCardsGrid.RowDefinitions.Clear();
        MainCardsGrid.ColumnDefinitions.Clear();

        if (stacked)
        {
            MainCardsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            MainCardsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            MainCardsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            MainCardsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            MainCardsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            MainCardsGrid.SetRow(BookAppointmentCard, 0);
            MainCardsGrid.SetColumn(BookAppointmentCard, 0);
            MainCardsGrid.SetRow(SymptomCheckerCard, 1);
            MainCardsGrid.SetColumn(SymptomCheckerCard, 0);
            MainCardsGrid.SetRow(AppointmentsSummaryCard, 2);
            MainCardsGrid.SetColumn(AppointmentsSummaryCard, 0);
            MainCardsGrid.SetRow(MedicalHistoryCard, 3);
            MainCardsGrid.SetColumn(MedicalHistoryCard, 0);
        }
        else
        {
            MainCardsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));
            MainCardsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            MainCardsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            MainCardsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            MainCardsGrid.SetRow(BookAppointmentCard, 0);
            MainCardsGrid.SetColumn(BookAppointmentCard, 0);
            MainCardsGrid.SetRow(SymptomCheckerCard, 0);
            MainCardsGrid.SetColumn(SymptomCheckerCard, 1);
            MainCardsGrid.SetRow(AppointmentsSummaryCard, 1);
            MainCardsGrid.SetColumn(AppointmentsSummaryCard, 0);
            MainCardsGrid.SetRow(MedicalHistoryCard, 1);
            MainCardsGrid.SetColumn(MedicalHistoryCard, 1);
        }
    }

    private void SetSpecialtySelected(int index)
    {
        var borders = new[] { BorderSpecNeurology, BorderSpecCardiology, BorderSpecPsychiatry, BorderSpecGenetics };
        var accent = Application.Current?.Resources.TryGetValue("Accent", out var ac) == true && ac is Color a
            ? a
            : Color.FromArgb("#2FE3C9");
        var cardBg = Application.Current?.Resources.TryGetValue("CardBg", out var cb) == true && cb is Color cbg
            ? cbg
            : Color.FromArgb("#0B2430");
        var primary = Application.Current?.Resources.TryGetValue("TextPrimary", out var tp) == true && tp is Color pr
            ? pr
            : Colors.White;
        var secondary = Application.Current?.Resources.TryGetValue("TextSecondary", out var ts) == true && ts is Color se
            ? se
            : Color.FromArgb("#9DBBC3");
        var muted = Application.Current?.Resources.TryGetValue("TextMuted", out var tm) == true && tm is Color mu
            ? mu
            : Color.FromArgb("#9FBFC4");
        var borderMuted = Color.FromArgb("#1B6270");
        var selectedBg = Color.FromArgb("#0F3138");

        for (var i = 0; i < borders.Length; i++)
        {
            var sel = i == index;
            var b = borders[i];
            b.Stroke = sel ? accent : borderMuted;
            b.StrokeThickness = sel ? 2 : 1;
            b.BackgroundColor = sel ? selectedBg : cardBg;

            if (b.Content is HorizontalStackLayout h && h.Count >= 2
                && h[0] is Label icon
                && h[1] is Label title)
            {
                icon.TextColor = sel ? accent : muted;
                title.TextColor = sel ? primary : secondary;
            }
        }
    }

    private void OnSpecialtyNeurologyTapped(object? sender, EventArgs e) => SetSpecialtySelected(0);

    private void OnSpecialtyCardiologyTapped(object? sender, EventArgs e) => SetSpecialtySelected(1);

    private void OnSpecialtyPsychiatryTapped(object? sender, EventArgs e) => SetSpecialtySelected(2);

    private void OnSpecialtyGeneticsTapped(object? sender, EventArgs e) => SetSpecialtySelected(3);

    private void OnTimeSlotClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn)
            SetTimeSlotActive(btn);
    }

    private void SetTimeSlotActive(Button active)
    {
        var inactive = Application.Current?.Resources["FilterChipStyle"] as Style;
        var activeStyle = Application.Current?.Resources["FilterChipActiveStyle"] as Style;
        foreach (var b in new[] { Slot0900, Slot1030, Slot1400 })
            b.Style = ReferenceEquals(b, active) ? activeStyle : inactive;
    }

    private async void OnConfirmAppointmentClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(new BookAppointmentPage());

    private async void OnStartCheckClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//Main");

    private async void OnAppointmentsCardTapped(object? sender, EventArgs e) =>
        await Navigation.PushAsync(new UserAppointmentsPage());

    private async void OnRescheduleHintTapped(object? sender, EventArgs e) =>
        await DisplayAlert("Reschedule", "Open My appointments to change a visit.", "OK");

    private async void OnViewVaultsTapped(object? sender, EventArgs e) =>
        await DisplayAlert("Medical History", "Full vault browsing will connect to your records service.", "OK");

    private async void OnAddRecordFabClicked(object? sender, EventArgs e) =>
        await DisplayAlert("Add record", "Document upload will be available in a future update.", "OK");

    private async void OnBreadcrumbDashboardTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//Main");
}
