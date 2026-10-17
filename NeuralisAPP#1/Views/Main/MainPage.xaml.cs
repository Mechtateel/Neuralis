using NeuralisAPP_1.Models.News;
using NeuralisAPP_1.Services;
using NeuralisAPP_1.Views.Main.Controls;

namespace NeuralisAPP_1.Views.Main;

public partial class MainPage : ContentPage
{
    private const double NewsGridNarrowBreakpoint = 640;
    private const double CompactLayoutBreakpoint = 900;
    private const double TwoColumnLayoutBreakpoint = 1200;

    private static readonly string[] NewsPreviewTags = ["Health", "AI", "Tips", "Research"];
    private static readonly string[] NewsPreviewIcons = ["article", "health_and_safety", "smart_toy", "tips_and_updates"];

    public MainPage()
    {
        InitializeComponent();

        UserSidebarCoordinator.Wire(
            AppSidebar,
            UserSidebarActiveItem.Dashboard,
            onDashboardTap: () => Task.CompletedTask,
            onBookVisitTap: async () => await Navigation.PushAsync(new BookAppointmentPage()),
            onAppointmentsTap: async () => await Navigation.PushAsync(new UserAppointmentsPage()),
            onLatestNewsTap: () => Shell.Current.GoToAsync("//LatestUpdates"),
            onLogoutTap: () => App.LogoutAsync(),
            closeSidebar: () => GlobalLayout.CloseSidebar());

        ServicesAndNewsLayoutGrid.SizeChanged += (_, _) => ApplyNewsPreviewGridLayout();
        SizeChanged += (_, _) => ApplyFluidDashboardLayout();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        MessagingCenter.Subscribe<App>(this, "AUTH_CHANGED", __ =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateAuthUI();
                _ = LoadNewsPreviewAsync();
            });
        });

        UpdateAuthUI();
        ApplyFluidDashboardLayout();
        await LoadNewsPreviewAsync();
    }

    private void ApplyFluidDashboardLayout()
    {
        var width = Width;
        if (width <= 0)
            return;

        var compact = width < CompactLayoutBreakpoint;
        var wide = width >= TwoColumnLayoutBreakpoint;

        HeaderHeroImage.IsVisible = !compact;
        HeaderImageColumnDefinition.Width = !compact ? GridLength.Star : 0;
        DashboardTitleLabel.FontSize = wide ? 32 : compact ? 24 : 28;
        DashboardSubtitleLabel.FontSize = wide ? 16 : 13;

        OverviewCol1.Width = GridLength.Star;
        OverviewCol2.Width = compact ? 0 : GridLength.Star;
        OverviewCol3.Width = wide ? GridLength.Star : 0;
        Grid.SetColumn(OverviewCard1, 0);
        Grid.SetRow(OverviewCard1, 0);
        Grid.SetColumn(OverviewCard2, compact ? 0 : 1);
        Grid.SetRow(OverviewCard2, compact ? 1 : 0);
        Grid.SetColumn(OverviewCard3, wide ? 2 : 0);
        Grid.SetRow(OverviewCard3, wide ? 0 : compact ? 2 : 1);

        ServicesColumnDefinition.Width = GridLength.Star;
        NewsColumnDefinition.Width = compact ? 0 : GridLength.Star;
        Grid.SetColumn(NewsColumn, compact ? 0 : 1);
        Grid.SetRow(NewsColumn, compact ? 1 : 0);
    }

    private async Task GoToLatestNewsAsync()
    {
        await Shell.Current.GoToAsync("//LatestUpdates");
    }

    private async Task GoToPatientServicesAsync()
    {
        await Shell.Current.GoToAsync("//PatientServices");
    }

    private async Task GoToMedicalServicesAsync()
    {
        await Shell.Current.GoToAsync("//MedicalServices");
    }

    private async Task GoToAiAnalysisAsync()
    {
        await Shell.Current.GoToAsync("//AIAnalysis");
    }

    private async Task CloseAndNavigateAsync(Func<Task> action)
    {
        GlobalLayout.CloseSidebar();
        await action();
    }

    private async void OnLatestUpdatesTapped(object sender, EventArgs e)
    {
        await CloseAndNavigateAsync(GoToLatestNewsAsync);
    }

    private async void OnPatientServicesTapped(object? sender, EventArgs e) =>
        await CloseAndNavigateAsync(GoToPatientServicesAsync);

    private async void OnMedicalServicesTapped(object? sender, EventArgs e) =>
        await CloseAndNavigateAsync(GoToMedicalServicesAsync);

    private async void OnAiAnalysisTapped(object? sender, EventArgs e) =>
        await CloseAndNavigateAsync(GoToAiAnalysisAsync);

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        MessagingCenter.Unsubscribe<App>(this, "AUTH_CHANGED");
    }

    private void UpdateAuthUI()
    {
        var user = App.CurrentUser;
        var displayName = user == null || string.IsNullOrWhiteSpace(user.FullName)
            ? "User"
            : user.FullName;
        AppSidebar.SetUserDisplayName(displayName);
    }

    private async Task LoadNewsPreviewAsync()
    {
        DashboardNewsGrid.Children.Clear();
        NewsPreviewEmptyLabel.IsVisible = false;
        NewsPreviewLoading.IsVisible = true;
        NewsPreviewLoading.IsRunning = true;

        try
        {
            var svc = new NewsService();
            var list = await svc.GetPublishedNewsAsync();
            var top = list.OrderByDescending(x => x.CreatedAt).Take(4).ToList();

            if (top.Count == 0)
            {
                NewsPreviewEmptyLabel.Text = "No updates yet. Check back soon.";
                NewsPreviewEmptyLabel.IsVisible = true;
                return;
            }

            var accent = ResolveResourceColor("Accent", "#3FE0C5");
            var textMuted = ResolveResourceColor("TextMuted", "#9FBFC4");
            var textPrimary = ResolveResourceColor("TextPrimary", "#FFFFFF");
            var textSecondary = ResolveResourceColor("TextSecondary", "#B6D6DB");
            var hoverStyle = Application.Current?.Resources["HoverCardStyle"] as Style;

            for (var i = 0; i < top.Count; i++)
                DashboardNewsGrid.Children.Add(CreateNewsPreviewCard(top[i], i, hoverStyle, accent, textPrimary, textSecondary, textMuted));

            ApplyNewsPreviewGridLayout();
        }
        catch (Exception ex)
        {
            NewsPreviewEmptyLabel.Text = $"Could not load updates: {ex.Message}";
            NewsPreviewEmptyLabel.IsVisible = true;
        }
        finally
        {
            NewsPreviewLoading.IsVisible = false;
            NewsPreviewLoading.IsRunning = false;
        }
    }

    private static Color ResolveResourceColor(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c)
            return c;
        return Color.FromArgb(fallbackHex);
    }

    private Border CreateNewsPreviewCard(
        NewsItemDto item,
        int index,
        Style? hoverStyle,
        Color accent,
        Color textPrimary,
        Color textSecondary,
        Color textMuted)
    {
        var tag = NewsPreviewTags[index % NewsPreviewTags.Length];
        var icon = NewsPreviewIcons[index % NewsPreviewIcons.Length];

        var topRow = new HorizontalStackLayout { Spacing = 10 };
        topRow.Add(new Label
        {
            Text = icon,
            FontFamily = "MaterialIcons",
            FontSize = 22,
            TextColor = accent,
            VerticalOptions = LayoutOptions.Center
        });
        topRow.Add(new Label
        {
            Text = tag,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = accent,
            VerticalOptions = LayoutOptions.Center
        });

        var inner = new VerticalStackLayout { Spacing = 10 };
        inner.Add(topRow);
        inner.Add(new Label
        {
            Text = item.Title,
            FontAttributes = FontAttributes.Bold,
            FontSize = 15,
            TextColor = textPrimary,
            LineBreakMode = LineBreakMode.WordWrap
        });
        inner.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(item.Content) ? "—" : item.Content.Trim(),
            FontSize = 13,
            TextColor = textSecondary,
            LineBreakMode = LineBreakMode.WordWrap
        });
        inner.Add(new Label
        {
            Text = item.CreatedLocalDisplay,
            FontSize = 11,
            TextColor = textMuted
        });

        var border = new Border
        {
            Style = hoverStyle,
            Padding = new Thickness(20),
            Content = inner
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await Shell.Current.GoToAsync("//LatestUpdates");
        border.GestureRecognizers.Add(tap);

        return border;
    }

    private void ApplyNewsPreviewGridLayout()
    {
        var count = DashboardNewsGrid.Children.Count;
        if (count == 0)
            return;

        var w = DashboardNewsGrid.Width;
        if (w <= 0 && ServicesAndNewsLayoutGrid.Width > 0)
            w = Math.Max(200, ServicesAndNewsLayoutGrid.Width / 2 - 32);
        if (w <= 0)
            w = NewsGridNarrowBreakpoint + 1;

        var narrow = w < NewsGridNarrowBreakpoint;

        DashboardNewsGrid.ColumnDefinitions.Clear();
        DashboardNewsGrid.RowDefinitions.Clear();

        if (narrow)
        {
            DashboardNewsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (var i = 0; i < count; i++)
                DashboardNewsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            for (var i = 0; i < count; i++)
            {
                var child = DashboardNewsGrid.Children[i];
                DashboardNewsGrid.SetColumn(child, 0);
                DashboardNewsGrid.SetRow(child, i);
            }
        }
        else
        {
            DashboardNewsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            DashboardNewsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var rows = (int)Math.Ceiling(count / 2.0);
            for (var r = 0; r < rows; r++)
                DashboardNewsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            for (var i = 0; i < count; i++)
            {
                var child = DashboardNewsGrid.Children[i];
                DashboardNewsGrid.SetColumn(child, i % 2);
                DashboardNewsGrid.SetRow(child, i / 2);
            }
        }
    }

}
