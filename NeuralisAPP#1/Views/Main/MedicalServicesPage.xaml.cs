using NeuralisAPP_1.Views.Main.Controls;

namespace NeuralisAPP_1.Views.Main;

public partial class MedicalServicesPage : ContentPage
{
    private const double WideBodyBreakpoint = 960;
    private const double DiagnosticsThreeColBreakpoint = 720;

    /// <summary>Narrow layout: AI, then prescriptions, then lab (single column).</summary>
    private bool _bodyStacked;

    private bool _diagThreeCol = true;

    public MedicalServicesPage()
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

        MedicalMainColumn.SizeChanged += (_, _) => ApplyResponsiveLayouts();
        Loaded += (_, _) => ApplyResponsiveLayouts();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        MessagingCenter.Subscribe<App>(this, "AUTH_CHANGED", __ =>
        {
            MainThread.BeginInvokeOnMainThread(UpdateAuthUi);
        });

        UpdateAuthUi();
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

    private void ApplyResponsiveLayouts()
    {
        var w = MedicalMainColumn.Width;
        if (w <= 0)
            return;

        ApplyBodyLayout(w < WideBodyBreakpoint);
        ApplyDiagnosticsLayout(w >= DiagnosticsThreeColBreakpoint);
    }

    private void ApplyBodyLayout(bool stacked)
    {
        if (stacked == _bodyStacked)
            return;

        _bodyStacked = stacked;

        MedicalBodyGrid.ColumnDefinitions.Clear();
        MedicalBodyGrid.RowDefinitions.Clear();

        if (stacked)
        {
            MedicalBodyGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            MedicalBodyGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            MedicalBodyGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            MedicalBodyGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(AiAnalysisBorder, 0);
            Grid.SetRow(AiAnalysisBorder, 0);
            Grid.SetColumnSpan(AiAnalysisBorder, 1);
            Grid.SetRowSpan(AiAnalysisBorder, 1);

            Grid.SetColumn(PrescriptionsBorder, 0);
            Grid.SetRow(PrescriptionsBorder, 1);
            Grid.SetColumnSpan(PrescriptionsBorder, 1);
            Grid.SetRowSpan(PrescriptionsBorder, 1);

            Grid.SetColumn(LabResultsBorder, 0);
            Grid.SetRow(LabResultsBorder, 2);
            Grid.SetColumnSpan(LabResultsBorder, 1);
            Grid.SetRowSpan(LabResultsBorder, 1);
        }
        else
        {
            MedicalBodyGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(13, GridUnitType.Star)));
            MedicalBodyGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(7, GridUnitType.Star)));
            MedicalBodyGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            MedicalBodyGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(AiAnalysisBorder, 0);
            Grid.SetRow(AiAnalysisBorder, 0);
            Grid.SetColumnSpan(AiAnalysisBorder, 1);
            Grid.SetRowSpan(AiAnalysisBorder, 1);

            Grid.SetColumn(LabResultsBorder, 0);
            Grid.SetRow(LabResultsBorder, 1);
            Grid.SetColumnSpan(LabResultsBorder, 1);
            Grid.SetRowSpan(LabResultsBorder, 1);

            Grid.SetColumn(PrescriptionsBorder, 1);
            Grid.SetRow(PrescriptionsBorder, 0);
            Grid.SetColumnSpan(PrescriptionsBorder, 1);
            Grid.SetRowSpan(PrescriptionsBorder, 2);
        }
    }

    private void ApplyDiagnosticsLayout(bool threeColumns)
    {
        if (threeColumns == _diagThreeCol)
            return;

        _diagThreeCol = threeColumns;

        DiagnosticsGrid.ColumnDefinitions.Clear();
        DiagnosticsGrid.RowDefinitions.Clear();

        if (threeColumns)
        {
            DiagnosticsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            DiagnosticsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            DiagnosticsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            DiagnosticsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(DiagEcgCard, 0);
            Grid.SetRow(DiagEcgCard, 0);
            Grid.SetColumn(DiagXrayCard, 1);
            Grid.SetRow(DiagXrayCard, 0);
            Grid.SetColumn(DiagNeuroCard, 2);
            Grid.SetRow(DiagNeuroCard, 0);
        }
        else
        {
            DiagnosticsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            DiagnosticsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            DiagnosticsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            DiagnosticsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(DiagEcgCard, 0);
            Grid.SetRow(DiagEcgCard, 0);
            Grid.SetColumn(DiagXrayCard, 0);
            Grid.SetRow(DiagXrayCard, 1);
            Grid.SetColumn(DiagNeuroCard, 0);
            Grid.SetRow(DiagNeuroCard, 2);
        }
    }

    private async void OnGenerateReportClicked(object? sender, EventArgs e) =>
        await DisplayAlert("Report", "Detailed report generation will connect to your clinical data service.", "OK");

    private async void OnRequestRenewalClicked(object? sender, EventArgs e) =>
        await DisplayAlert("Renewal", "Prescription renewal requests will be routed to your care team.", "OK");

    private async void OnViewScanTapped(object? sender, EventArgs e) =>
        await DisplayAlert("Imaging", "Opening the imaging viewer will be available when linked to your records.", "OK");

    private async void OnBreadcrumbDashboardTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//Main");
}
