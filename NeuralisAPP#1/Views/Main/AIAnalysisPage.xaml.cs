using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using NeuralisAPP_1.Models.Ai;
using NeuralisAPP_1.Services;
using NeuralisAPP_1.Views.Main.Controls;

namespace NeuralisAPP_1.Views.Main;

public partial class AIAnalysisPage : ContentPage
{
    private const string InterpretationUnavailable = "AI interpretation unavailable.";
    /// <summary>Local emergency number used when the user confirms an emergency call.</summary>
    private const string EmergencyPhoneNumber = "112";

    private const double MobileModeBreakpoint = 900;
    private const double WideWorkspaceBreakpoint = 960;
    private const double ResultsSideBySideBreakpoint = 640;

    private bool _isMobileMode;
    private bool _workspaceStacked;
    /// <summary>Matches XAML default: condition + confidence side by side.</summary>
    private bool _resultsStacked;

    public AIAnalysisPage()
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

        AnalysisMainColumn.SizeChanged += (_, _) => ApplyResponsiveLayouts();
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
        var w = AnalysisMainColumn.Width;
        if (w <= 0)
            return;

        _isMobileMode = w < MobileModeBreakpoint;
        ApplyHeaderLayout(_isMobileMode);
        ApplyActionButtonsLayout(_isMobileMode);
        ApplyWorkspaceLayout(w < WideWorkspaceBreakpoint);

        var inner = w < WideWorkspaceBreakpoint ? w : w - 48;
        ApplyResultsLayout(inner < ResultsSideBySideBreakpoint);
    }

    private void ApplyHeaderLayout(bool mobile)
    {
        AnalysisDescriptionFullLabel.IsVisible = !mobile;
        AnalysisDescriptionShortLabel.IsVisible = mobile;
        AnalysisDisclaimerFullLabel.IsVisible = !mobile;
        AnalysisDisclaimerShortLabel.IsVisible = mobile;

        AnalysisHeaderGrid.ColumnDefinitions.Clear();
        AnalysisHeaderGrid.RowDefinitions.Clear();

        if (mobile)
        {
            AnalysisHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            AnalysisHeaderGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AnalysisHeaderGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(ExpertConsensusBadge, 0);
            Grid.SetRow(ExpertConsensusBadge, 1);
            ExpertConsensusBadge.HorizontalOptions = LayoutOptions.Start;
            ExpertConsensusBadge.Margin = new Thickness(0, 8, 0, 0);
        }
        else
        {
            AnalysisHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            AnalysisHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            AnalysisHeaderGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(ExpertConsensusBadge, 1);
            Grid.SetRow(ExpertConsensusBadge, 0);
            ExpertConsensusBadge.HorizontalOptions = LayoutOptions.Fill;
            ExpertConsensusBadge.Margin = 0;
        }
    }

    private void ApplyActionButtonsLayout(bool mobile)
    {
        AnalysisActionButtonsGrid.ColumnDefinitions.Clear();
        AnalysisActionButtonsGrid.RowDefinitions.Clear();
        AnalysisActionButtonsGrid.ColumnSpacing = mobile ? 0 : 12;
        AnalysisActionButtonsGrid.RowSpacing = mobile ? 10 : 0;

        if (mobile)
        {
            AnalysisActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            AnalysisActionButtonsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AnalysisActionButtonsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(AiAnalyzingIndicator, 0);
            Grid.SetRow(AiAnalyzingIndicator, 0);
            AiAnalyzingIndicator.HorizontalOptions = LayoutOptions.Center;

            Grid.SetColumn(RunAnalysisButton, 0);
            Grid.SetRow(RunAnalysisButton, 1);
            RunAnalysisButton.HorizontalOptions = LayoutOptions.Fill;
        }
        else
        {
            AnalysisActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            AnalysisActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            AnalysisActionButtonsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(AiAnalyzingIndicator, 0);
            Grid.SetRow(AiAnalyzingIndicator, 0);
            AiAnalyzingIndicator.HorizontalOptions = LayoutOptions.Start;

            Grid.SetColumn(RunAnalysisButton, 1);
            Grid.SetRow(RunAnalysisButton, 0);
            RunAnalysisButton.HorizontalOptions = LayoutOptions.End;
        }
    }

    private void ApplyWorkspaceLayout(bool stacked)
    {
        if (stacked == _workspaceStacked)
            return;

        _workspaceStacked = stacked;

        AiWorkspaceGrid.ColumnDefinitions.Clear();
        AiWorkspaceGrid.RowDefinitions.Clear();

        if (stacked)
        {
            AiWorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            AiWorkspaceGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AiWorkspaceGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(LeftWorkspaceStack, 0);
            Grid.SetRow(LeftWorkspaceStack, 0);
            Grid.SetColumn(RightWorkspaceStack, 0);
            Grid.SetRow(RightWorkspaceStack, 1);
        }
        else
        {
            AiWorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(7, GridUnitType.Star)));
            AiWorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(3, GridUnitType.Star)));
            AiWorkspaceGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(LeftWorkspaceStack, 0);
            Grid.SetRow(LeftWorkspaceStack, 0);
            Grid.SetColumn(RightWorkspaceStack, 1);
            Grid.SetRow(RightWorkspaceStack, 0);
        }
    }

    private void ApplyResultsLayout(bool stacked)
    {
        if (stacked == _resultsStacked)
            return;

        _resultsStacked = stacked;

        ResultsRowGrid.ColumnDefinitions.Clear();
        ResultsRowGrid.RowDefinitions.Clear();

        if (stacked)
        {
            ResultsRowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ResultsRowGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ResultsRowGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(ConditionCard, 0);
            Grid.SetRow(ConditionCard, 0);
            Grid.SetColumn(ConfidenceCard, 0);
            Grid.SetRow(ConfidenceCard, 1);
        }
        else
        {
            ResultsRowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ResultsRowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ResultsRowGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(ConditionCard, 0);
            Grid.SetRow(ConditionCard, 0);
            Grid.SetColumn(ConfidenceCard, 1);
            Grid.SetRow(ConfidenceCard, 0);
        }
    }

    private async void OnBreadcrumbDashboardTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//Main");

    private async void OnViewFullArchiveClicked(object? sender, EventArgs e) =>
        await DisplayAlert(
            "Analysis archive",
            "Full case history and export will be available when linked to your workspace.",
            "OK");

    private async void OnRunAnalysisClicked(object? sender, EventArgs e)
    {
        var token = AuthService.CurrentToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlert("Sign in required", "Please sign in to run AI analysis.", "OK");
            return;
        }

        var text = ClinicalInputEditor.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            await DisplayAlert("Input required", "Enter clinical observations or symptoms to analyze.", "OK");
            return;
        }

        AiErrorLabel.IsVisible = false;
        AiAnalyzingIndicator.IsVisible = true;
        AiAnalyzingIndicator.IsRunning = true;
        RunAnalysisButton.IsEnabled = false;
        var previousRunLabel = RunAnalysisButton.Text;
        RunAnalysisButton.Text = "Analyzing…";

        try
        {
            var svc = new AiService(token);
            var (ok, data, err) = await svc.AnalyzeTextAsync(text);

            if (!ok)
            {
                ShowErrorState(err ?? "Analysis could not be completed. This is not a diagnosis — please consult a clinician.");
                return;
            }

            await ShowSuccessStateAsync(data!);
        }
        catch (Exception ex)
        {
            ShowErrorState(ex.Message);
        }
        finally
        {
            AiAnalyzingIndicator.IsVisible = false;
            AiAnalyzingIndicator.IsRunning = false;
            RunAnalysisButton.IsEnabled = true;
            RunAnalysisButton.Text = previousRunLabel;
        }
    }

    private void ShowErrorState(string message)
    {
        AiErrorLabel.Text = message;
        AiErrorLabel.IsVisible = true;
        ApplyClinicalInterpretation(null);
        ConditionEmptyState.IsVisible = true;
        ConditionResultContent.IsVisible = false;
        ConfidenceEmptyState.IsVisible = true;
        ConfidenceResultContent.IsVisible = false;
        AiConfidenceProgressBar.Progress = 0;
        ConditionResultContent.Opacity = 0;
        ConfidenceResultContent.Opacity = 0;
        HideRecommendedAction();
        ClearResultStacks();
    }

    private async Task ShowSuccessStateAsync(AiAnalyzeResponse data)
    {
        AiErrorLabel.IsVisible = false;
        ConditionEmptyState.IsVisible = false;
        ConfidenceEmptyState.IsVisible = false;

        ClearResultStacks();
        AddBulletLabels(AiSymptomsStack, data.Symptoms, "No symptoms detected in this text.");
        AddBulletLabels(AiConditionsStack, data.PossibleConditions, "No conditions detected.");

        var pct = GetConfidencePercent(data);
        var riskColor = ResolveRiskColor(data.RiskLevel);

        AiRiskLabel.Text = FormatRiskDisplay(data.RiskLevel);
        AiRiskLabel.TextColor = riskColor;
        AiConfidenceProgressBar.Progress = pct / 100.0;
        AiConfidenceProgressBar.ProgressColor = riskColor;
        AiConfidencePercentLabel.Text = $"{pct}%";

        PopulateConditionChips(data.PossibleConditions);

        AiRecommendationLabel.Text = string.IsNullOrWhiteSpace(data.Recommendation)
            ? "—"
            : data.Recommendation;
        ApplyClinicalInterpretation(data.AiInterpretation);

        FillNextSteps(AiNextStepsStack, data.Recommendation);

        ApplyRecommendedAction(data.RiskLevel);

        ConditionResultContent.IsVisible = true;
        ConfidenceResultContent.IsVisible = true;
        ConditionResultContent.Opacity = 0;
        ConfidenceResultContent.Opacity = 0;
        RecommendedActionCard.IsVisible = true;
        RecommendedActionCard.Opacity = 0;

        await Task.WhenAll(
            ConditionResultContent.FadeTo(1, 300, Easing.CubicOut),
            ConfidenceResultContent.FadeTo(1, 300, Easing.CubicOut),
            RecommendedActionCard.FadeTo(1, 300, Easing.CubicOut));
    }

    /// <summary>
    /// Maps the NLP risk level to the extensible action profile and renders the status card.
    /// Copy is educational only — never states a diagnosis.
    /// </summary>
    private void ApplyRecommendedAction(string? riskLevel)
    {
        var profile = NlpRiskActionRegistry.Resolve(riskLevel);

        RecommendedActionInnerCard.BackgroundColor = profile.BackgroundColor;
        RecommendedActionInnerCard.Stroke = profile.BorderColor;
        RecommendedActionInnerCard.StrokeThickness = profile.BorderThickness;

        RecommendedActionWarningIcon.IsVisible = profile.ShowWarningIcon;
        RecommendedActionWarningIcon.TextColor = profile.AccentColor;

        RecommendedActionStatusLabel.Text = $"{FormatRiskDisplay(riskLevel).ToUpperInvariant()} RISK";
        RecommendedActionStatusLabel.TextColor = profile.AccentColor;

        RecommendedActionMessage.Text = profile.PrimaryMessage;

        var hasSecondary = !string.IsNullOrWhiteSpace(profile.SecondaryMessage);
        RecommendedActionSecondaryMessage.IsVisible = hasSecondary;
        RecommendedActionSecondaryMessage.Text = hasSecondary ? profile.SecondaryMessage : string.Empty;

        // HIGH risk: stronger visual emphasis (red border + warning icon + shadow).
        if (profile.ShowWarningIcon)
        {
            RecommendedActionInnerCard.Shadow = new Shadow
            {
                Brush = new SolidColorBrush(profile.AccentColor),
                Radius = 16,
                Opacity = 0.45f,
                Offset = new Point(0, 4)
            };
        }
        else
        {
            RecommendedActionInnerCard.Shadow = null;
        }

        RecommendedActionButtons.Children.Clear();
        foreach (var action in profile.Buttons)
            RecommendedActionButtons.Children.Add(CreateRecommendedActionButton(action, profile));
    }

    private void HideRecommendedAction()
    {
        RecommendedActionCard.IsVisible = false;
        RecommendedActionCard.Opacity = 0;
        RecommendedActionButtons.Children.Clear();
        RecommendedActionInnerCard.Shadow = null;
    }

    /// <summary>
    /// Builds a styled button wired to the shared action dispatcher.
    /// </summary>
    private Button CreateRecommendedActionButton(NlpRiskActionButton action, NlpRiskActionProfile profile)
    {
        var button = new Button
        {
            Text = action.Label,
            Margin = new Thickness(0, 0, 12, 10),
            MinimumWidthRequest = 140
        };

        if (action.IsPrimary)
        {
            // App-level styles (Styles.xaml / AuthStyles.xaml) — not on ContentPage.Resources.
            button.Style = ResolveAppStyle("PrimaryButton");
            if (profile.RiskLevelKey.Equals("high", StringComparison.OrdinalIgnoreCase))
                button.BackgroundColor = profile.AccentColor;
        }
        else
        {
            button.Style = ResolveAppStyle("GhostButton");
            button.BorderColor = profile.AccentColor;
            button.TextColor = profile.AccentColor;
        }

        button.Clicked += async (_, _) => await HandleRecommendedActionAsync(action.Kind);
        return button;
    }

    /// <summary>
    /// Routes recommended-action buttons to existing app flows or emergency dialer.
    /// </summary>
    private async Task HandleRecommendedActionAsync(NlpRiskActionKind kind)
    {
        switch (kind)
        {
            case NlpRiskActionKind.LearnMore:
                await Navigation.PushAsync(new PatientServicesPage());
                break;

            case NlpRiskActionKind.BookAppointment:
            case NlpRiskActionKind.ContactDoctor:
                await Navigation.PushAsync(new BookAppointmentPage());
                break;

            case NlpRiskActionKind.EmergencyCall:
                var confirm = await DisplayAlert(
                    "Emergency call",
                    "You are about to dial emergency services. Only proceed if you need urgent medical help.",
                    "Call",
                    "Cancel");
                if (confirm)
                    await DialEmergencyAsync();
                break;
        }
    }

    private static async Task DialEmergencyAsync()
    {
        try
        {
            await Launcher.Default.OpenAsync($"tel:{EmergencyPhoneNumber}");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                "Unable to dial",
                $"Could not open the phone dialer. Please call {EmergencyPhoneNumber} manually.\n\n{ex.Message}",
                "OK");
        }
    }

    /// <summary>
    /// Resolves a <see cref="Style"/> from application resources (merged dictionaries in App.xaml).
    /// Page-scoped <c>Resources[key]</c> does not include app-level styles and will throw.
    /// </summary>
    private static Style? ResolveAppStyle(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Style style)
            return style;

        return null;
    }

    private static Color ResolveRiskColor(string? riskLevel)
    {
        return (riskLevel?.Trim().ToLowerInvariant()) switch
        {
            "low" => Color.FromArgb("#4ADE80"),
            "medium" => Color.FromArgb("#FACC15"),
            "high" => Color.FromArgb("#F87171"),
            _ => Application.Current?.Resources.TryGetValue("Accent", out var ac) == true && ac is Color a
                ? a
                : Color.FromArgb("#2FE3C9")
        };
    }

    private void ClearResultStacks()
    {
        AiSymptomsStack.Children.Clear();
        AiConditionsStack.Children.Clear();
        AiNextStepsStack.Children.Clear();
        ApplyClinicalInterpretation(null);
        ConfidenceConditionChips.Children.Clear();
        AiNoConditionsLabel.IsVisible = false;
        HideRecommendedAction();
    }

    private void ApplyClinicalInterpretation(string? interpretation)
    {
        var text = string.IsNullOrWhiteSpace(interpretation)
            ? InterpretationUnavailable
            : interpretation.Trim();
        var isFallback = text.Equals(InterpretationUnavailable, StringComparison.OrdinalIgnoreCase);
        AiClinicalInterpretationLabel.Text = text;
        AiClinicalInterpretationFallbackHint.IsVisible = isFallback;
    }

    private static string FormatRiskDisplay(string? riskLevel)
    {
        if (string.IsNullOrWhiteSpace(riskLevel))
            return "—";
        var t = riskLevel.Trim();
        if (t.Length == 1)
            return t.ToUpperInvariant();
        return char.ToUpperInvariant(t[0]) + t.Substring(1).ToLowerInvariant();
    }

    private static int GetConfidencePercent(AiAnalyzeResponse data)
    {
        if (data.Confidence is { } c && !double.IsNaN(c) && !double.IsInfinity(c))
        {
            var v = (int)Math.Round(Math.Clamp(c, 0, 100));
            return v;
        }

        return (data.RiskLevel?.Trim().ToLowerInvariant()) switch
        {
            "high" => 85,
            "medium" => 60,
            "low" => 30,
            _ => 50
        };
    }

    private void PopulateConditionChips(IReadOnlyList<string> conditions)
    {
        ConfidenceConditionChips.Children.Clear();
        Resources.TryGetValue("ConditionChipStyle", out var styleObj);
        var chipStyle = styleObj as Style;

        var textPrimary = Application.Current?.Resources.TryGetValue("TextPrimary", out var tp) == true && tp is Color pr
            ? pr
            : Colors.White;

        var any = false;
        foreach (var raw in conditions)
        {
            var c = raw?.Trim();
            if (string.IsNullOrEmpty(c))
                continue;
            any = true;
            var chip = new Border
            {
                Style = chipStyle,
                Content = new Label
                {
                    Text = c,
                    FontSize = 12,
                    TextColor = textPrimary,
                    LineBreakMode = LineBreakMode.WordWrap
                }
            };

            if (chipStyle == null)
            {
                chip.BackgroundColor = Color.FromArgb("#0D2B31");
                chip.Stroke = Application.Current?.Resources.TryGetValue("Accent", out var ac) == true && ac is Color acol
                    ? acol
                    : Color.FromArgb("#2FE3C9");
                chip.StrokeThickness = 1;
                chip.Padding = new Thickness(12, 8);
                chip.Margin = new Thickness(0, 0, 10, 10);
                chip.StrokeShape = new RoundRectangle { CornerRadius = 12 };
            }

            ConfidenceConditionChips.Children.Add(chip);
        }

        AiNoConditionsLabel.IsVisible = !any;
    }

    private static void AddBulletLabels(VerticalStackLayout stack, IEnumerable<string> items, string emptyMessage)
    {
        var muted = Application.Current?.Resources.TryGetValue("TextMuted", out var tm) == true && tm is Color tmc
            ? tmc
            : Color.FromArgb("#9FBFC4");

        foreach (var s in items.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            stack.Children.Add(new Label
            {
                Text = "• " + s.Trim(),
                FontSize = 13,
                TextColor = muted,
                LineBreakMode = LineBreakMode.WordWrap
            });
        }

        if (stack.Children.Count == 0)
            stack.Children.Add(new Label { Text = emptyMessage, FontSize = 13, TextColor = muted, LineBreakMode = LineBreakMode.WordWrap });
    }

    private static void FillNextSteps(VerticalStackLayout stack, string recommendation)
    {
        var muted = Application.Current?.Resources.TryGetValue("TextMuted", out var tm) == true && tm is Color tmc
            ? tmc
            : Color.FromArgb("#9FBFC4");

        if (string.IsNullOrWhiteSpace(recommendation))
        {
            stack.Children.Add(new Label
            {
                Text = "• Review possible conditions with a qualified clinician.",
                FontSize = 12,
                TextColor = muted,
                LineBreakMode = LineBreakMode.WordWrap
            });
            return;
        }

        var parts = recommendation
            .Split(new[] { '.', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 4)
            .Take(5)
            .ToList();

        foreach (var p in parts)
        {
            stack.Children.Add(new Label
            {
                Text = "• " + p + (p.EndsWith('.') ? "" : "."),
                FontSize = 12,
                TextColor = muted,
                LineBreakMode = LineBreakMode.WordWrap
            });
        }

        if (stack.Children.Count == 0)
        {
            stack.Children.Add(new Label
            {
                Text = "• Follow the recommendation above with your care team.",
                FontSize = 12,
                TextColor = muted,
                LineBreakMode = LineBreakMode.WordWrap
            });
        }
    }
}
