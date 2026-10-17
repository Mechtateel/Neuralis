using Microsoft.Maui.Controls.Shapes;
using NeuralisAPP_1.Models.Appointments;
using NeuralisAPP_1.Services;
using NeuralisAPP_1.Views.Main.Controls;

namespace NeuralisAPP_1.Views.Main;

public partial class UserAppointmentsPage : ContentPage
{
    private List<MyAppointmentDto> _loaded = new();

    public UserAppointmentsPage()
    {
        InitializeComponent();

        UserSidebarCoordinator.Wire(
            AppSidebar,
            UserSidebarActiveItem.Appointments,
            onDashboardTap: async () => await Navigation.PopAsync(),
            onBookVisitTap: async () => await Navigation.PushAsync(new BookAppointmentPage()),
            onAppointmentsTap: () => Task.CompletedTask,
            onLatestNewsTap: () => Shell.Current.GoToAsync("//LatestUpdates"),
            onLogoutTap: () => App.LogoutAsync(),
            closeSidebar: () => GlobalLayout.CloseSidebar());
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        MessagingCenter.Subscribe<App>(this, "AUTH_CHANGED", __ =>
        {
            MainThread.BeginInvokeOnMainThread(UpdateAuthUi);
        });

        UpdateAuthUi();
        _ = LoadAsync();
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

    private async void OnBookNewTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new BookAppointmentPage());
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            await LoadAsync();
        }
        finally
        {
            RefreshHost.IsRefreshing = false;
        }
    }

    private async Task LoadAsync()
    {
        var token = AuthService.CurrentToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlert("Session", "Please sign in again.", "OK");
            await Navigation.PopAsync();
            return;
        }

        try
        {
            var svc = new AppointmentService(token);
            var list = await svc.GetMyAppointmentsAsync();
            _loaded = list.ToList();
            RebuildAppointmentCards();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private void RebuildAppointmentCards()
    {
        AppointmentsStack.Children.Clear();
        var hasItems = _loaded.Count > 0;
        EmptyStateCard.IsVisible = !hasItems;
        BookNewHeaderButton.IsVisible = hasItems;

        if (!hasItems)
            return;

        var upcoming = _loaded.Where(IsUpcomingSection).OrderBy(x => x.ScheduledAtUtc).ToList();
        var past = _loaded.Where(a => !IsUpcomingSection(a)).OrderByDescending(x => x.ScheduledAtUtc).ToList();

        AppointmentsStack.Children.Add(CreateSummaryCard(upcoming.Count, past.Count));

        AppointmentsStack.Children.Add(CreateSectionTitle("Upcoming appointments"));
        if (upcoming.Count == 0)
            AppointmentsStack.Children.Add(CreateEmptySectionHint("No upcoming visits scheduled."));
        else
            AddCardsWithDividers(upcoming);

        AppointmentsStack.Children.Add(CreateSectionTitle("Past appointments"));
        if (past.Count == 0)
            AppointmentsStack.Children.Add(CreateEmptySectionHint("No past visits yet."));
        else
            AddCardsWithDividers(past);

        AppointmentsStack.Children.Add(CreateFooterSuggestionCard());
    }

    private static bool IsUpcomingSection(MyAppointmentDto a)
    {
        if (a.ParsedStatus is AppointmentStatusDto.Cancelled or AppointmentStatusDto.Completed)
            return false;
        return a.ScheduledLocal > DateTime.Now;
    }

    private Border CreateSummaryCard(int upcomingCount, int pastCount)
    {
        var hoverStyle = Application.Current?.Resources["HoverCardStyle"] as Style;
        var accent = ResolveColor("Accent", Color.FromArgb("#2FE3C9"));
        var textMuted = ResolveColor("TextMuted", Color.FromArgb("#9FBFC4"));
        var textPrimary = ResolveColor("TextPrimary", Colors.White);

        var row = new HorizontalStackLayout
        {
            Spacing = 20,
            HorizontalOptions = LayoutOptions.Fill
        };
        row.Add(new Label
        {
            Text = "today",
            FontFamily = "MaterialIcons",
            FontSize = 20,
            TextColor = textMuted,
            VerticalOptions = LayoutOptions.Center
        });
        var upcomingBlock = new VerticalStackLayout { Spacing = 2 };
        upcomingBlock.Add(new Label
        {
            Text = "Upcoming",
            FontSize = 11,
            TextColor = textMuted
        });
        upcomingBlock.Add(new Label
        {
            Text = upcomingCount.ToString(),
            FontAttributes = FontAttributes.Bold,
            FontSize = 20,
            TextColor = accent
        });
        row.Add(upcomingBlock);

        row.Add(new BoxView
        {
            WidthRequest = 1,
            VerticalOptions = LayoutOptions.Fill,
            Color = Color.FromArgb("#163F46"),
            Margin = new Thickness(0, 4, 0, 4)
        });

        row.Add(new Label
        {
            Text = "history",
            FontFamily = "MaterialIcons",
            FontSize = 20,
            TextColor = textMuted,
            VerticalOptions = LayoutOptions.Center
        });
        var pastBlock = new VerticalStackLayout { Spacing = 2 };
        pastBlock.Add(new Label
        {
            Text = "Past",
            FontSize = 11,
            TextColor = textMuted
        });
        pastBlock.Add(new Label
        {
            Text = pastCount.ToString(),
            FontAttributes = FontAttributes.Bold,
            FontSize = 20,
            TextColor = textPrimary
        });
        row.Add(pastBlock);

        return new Border
        {
            Style = hoverStyle,
            Padding = new Thickness(18, 14),
            Margin = new Thickness(0, 0, 0, 6),
            Content = row
        };
    }

    private static Label CreateSectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            FontAttributes = FontAttributes.Bold,
            FontSize = 15,
            TextColor = Application.Current?.Resources.TryGetValue("TextPrimary", out var c) == true && c is Color col
                ? col
                : Colors.White,
            Margin = new Thickness(0, 16, 0, 8)
        };
    }

    private static Label CreateEmptySectionHint(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 13,
            TextColor = Application.Current?.Resources.TryGetValue("TextMuted", out var c) == true && c is Color col
                ? col
                : Color.FromArgb("#9FBFC4"),
            Margin = new Thickness(0, 0, 0, 8)
        };
    }

    private void AddCardsWithDividers(IReadOnlyList<MyAppointmentDto> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
                AppointmentsStack.Children.Add(CreateCardDivider());
            AppointmentsStack.Children.Add(CreateAppointmentCard(items[i]));
        }
    }

    private static BoxView CreateCardDivider()
    {
        return new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#163F46"),
            Margin = new Thickness(0, 4, 0, 4),
            HorizontalOptions = LayoutOptions.Fill
        };
    }

    private Border CreateFooterSuggestionCard()
    {
        var hoverStyle = Application.Current?.Resources["HoverCardStyle"] as Style;
        var ghostStyle = Application.Current?.Resources["GhostButton"] as Style;
        var textMuted = ResolveColor("TextMuted", Color.FromArgb("#9FBFC4"));
        var textPrimary = ResolveColor("TextPrimary", Colors.White);

        var inner = new VerticalStackLayout { Spacing = 10 };
        inner.Add(new Label
        {
            Text = "Book your next appointment",
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            TextColor = textPrimary
        });
        inner.Add(new Label
        {
            Text = "Stay on track with follow-ups or new concerns.",
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap,
            TextColor = textMuted
        });
        var row = new HorizontalStackLayout { Spacing = 10 };
        var btn = new Button
        {
            Text = "Find a doctor",
            Style = ghostStyle,
            FontSize = 12,
            Padding = new Thickness(14, 8)
        };
        btn.Clicked += async (_, _) => await Navigation.PushAsync(new BookAppointmentPage());
        row.Add(btn);
        inner.Add(row);

        return new Border
        {
            Style = hoverStyle,
            Padding = new Thickness(18, 16),
            Margin = new Thickness(0, 12, 0, 8),
            Content = inner
        };
    }

    private Border CreateAppointmentCard(MyAppointmentDto a)
    {
        var hoverStyle = Application.Current?.Resources["HoverCardStyle"] as Style;
        var ghostStyle = Application.Current?.Resources["GhostButton"] as Style;
        var accent = ResolveColor("Accent", Color.FromArgb("#2FE3C9"));
        var textPrimary = ResolveColor("TextPrimary", Colors.White);
        var textMuted = ResolveColor("TextMuted", Color.FromArgb("#9FBFC4"));
        var textSecondary = ResolveColor("TextSecondary", Color.FromArgb("#9DBBC3"));

        var root = new VerticalStackLayout { Spacing = 0 };

        root.Add(CreateStatusBadge(a, accent, textMuted, textSecondary));

        var primaryBlock = new VerticalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var doctorRow = new HorizontalStackLayout { Spacing = 10 };
        doctorRow.Add(new Label
        {
            Text = "person",
            FontFamily = "MaterialIcons",
            FontSize = 20,
            TextColor = textMuted,
            VerticalOptions = LayoutOptions.Center
        });
        doctorRow.Add(new Label
        {
            Text = a.DoctorFullName,
            FontAttributes = FontAttributes.Bold,
            FontSize = 17,
            TextColor = textPrimary,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        });
        primaryBlock.Add(doctorRow);

        var dateTimeRow = new HorizontalStackLayout { Spacing = 18 };
        var datePart = new HorizontalStackLayout { Spacing = 6 };
        datePart.Add(new Label
        {
            Text = "event",
            FontFamily = "MaterialIcons",
            FontSize = 16,
            TextColor = textMuted,
            VerticalOptions = LayoutOptions.Center
        });
        datePart.Add(new Label
        {
            Text = a.DateDisplay,
            FontSize = 14,
            TextColor = textPrimary,
            VerticalOptions = LayoutOptions.Center
        });
        var timePart = new HorizontalStackLayout { Spacing = 6 };
        timePart.Add(new Label
        {
            Text = "schedule",
            FontFamily = "MaterialIcons",
            FontSize = 16,
            TextColor = textMuted,
            VerticalOptions = LayoutOptions.Center
        });
        timePart.Add(new Label
        {
            Text = a.TimeDisplay,
            FontSize = 14,
            TextColor = textPrimary,
            VerticalOptions = LayoutOptions.Center
        });
        dateTimeRow.Add(datePart);
        dateTimeRow.Add(timePart);
        primaryBlock.Add(dateTimeRow);

        root.Add(primaryBlock);

        var metaBlock = new VerticalStackLayout
        {
            Spacing = 4,
            Margin = new Thickness(0, 12, 0, 0)
        };
        metaBlock.Add(new Label
        {
            Text = a.DoctorSpecialization,
            FontSize = 13,
            TextColor = accent,
            LineBreakMode = LineBreakMode.WordWrap
        });
        metaBlock.Add(new Label
        {
            Text = a.DoctorEmail,
            FontSize = 11,
            TextColor = textMuted,
            LineBreakMode = LineBreakMode.TailTruncation
        });
        root.Add(metaBlock);

        var actionRow = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 14, 0, 0)
        };

        var detailsBtn = SmallGhostButton(ghostStyle, "Details");
        detailsBtn.Clicked += async (_, _) => await OnDetailsClicked(a);
        actionRow.Add(detailsBtn);

        if (a.IsUpcomingActionable)
        {
            var resBtn = SmallGhostButton(ghostStyle, "Reschedule");
            resBtn.Clicked += async (_, _) => await OnRescheduleClicked(a);
            actionRow.Add(resBtn);

            var cancelBtn = SmallCancelOutlineButton();
            cancelBtn.Clicked += async (_, _) => await OnCancelClicked(a);
            actionRow.Add(cancelBtn);
        }

        root.Add(actionRow);

        var border = new Border
        {
            Style = hoverStyle,
            Padding = new Thickness(18, 16),
            Margin = new Thickness(0, 0, 0, 10),
            Content = root
        };

        if (a.ParsedStatus is AppointmentStatusDto.Completed or AppointmentStatusDto.Cancelled)
            border.Opacity = 0.88;

        return border;
    }

    private static Button SmallGhostButton(Style? ghostStyle, string text)
    {
        return new Button
        {
            Text = text,
            Style = ghostStyle,
            FontSize = 12,
            Padding = new Thickness(10, 8),
            MinimumWidthRequest = 0,
            VerticalOptions = LayoutOptions.Center
        };
    }

    private static Button SmallCancelOutlineButton()
    {
        return new Button
        {
            Text = "Cancel",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#FF6B6B"),
            BorderColor = Color.FromArgb("#FF6B6B"),
            BorderWidth = 1,
            CornerRadius = 14,
            FontSize = 12,
            Padding = new Thickness(10, 8),
            VerticalOptions = LayoutOptions.Center
        };
    }

    private static View CreateStatusBadge(
        MyAppointmentDto a,
        Color accent,
        Color textMuted,
        Color textSecondary)
    {
        var status = a.ParsedStatus;
        var icon = status switch
        {
            AppointmentStatusDto.Confirmed => "check_circle",
            AppointmentStatusDto.Pending => "pending_actions",
            AppointmentStatusDto.Completed => "task_alt",
            AppointmentStatusDto.Cancelled => "cancel",
            _ => "info"
        };

        var labelColor = status == AppointmentStatusDto.Confirmed
            ? accent
            : status is AppointmentStatusDto.Completed or AppointmentStatusDto.Cancelled
                ? textMuted
                : textSecondary;

        var row = new HorizontalStackLayout { Spacing = 6 };
        row.Add(new Label
        {
            Text = icon,
            FontFamily = "MaterialIcons",
            FontSize = 15,
            TextColor = labelColor,
            VerticalOptions = LayoutOptions.Center
        });
        row.Add(new Label
        {
            Text = a.Status,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = labelColor,
            VerticalOptions = LayoutOptions.Center
        });

        var border = new Border
        {
            Padding = new Thickness(10, 5),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = row,
            HorizontalOptions = LayoutOptions.Start
        };

        switch (status)
        {
            case AppointmentStatusDto.Confirmed:
                border.Stroke = accent;
                border.StrokeThickness = 1;
                border.BackgroundColor = Color.FromArgb("#0D2B31");
                break;
            case AppointmentStatusDto.Pending:
                border.Stroke = Color.FromArgb("#163B41");
                border.StrokeThickness = 1;
                border.BackgroundColor = Color.FromArgb("#102F36");
                break;
            default:
                border.StrokeThickness = 0;
                border.BackgroundColor = Color.FromArgb("#102F36");
                break;
        }

        return border;
    }

    private static Color ResolveColor(string key, Color fallback)
    {
        return Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c
            ? c
            : fallback;
    }

    private async Task OnDetailsClicked(MyAppointmentDto a)
    {
        var msg =
            $"{a.DoctorFullName}\n{a.DoctorSpecialization}\n{a.DoctorEmail}\n\n{a.DateDisplay}\n{a.TimeDisplay}\n\nStatus: {a.Status}";
        await DisplayAlert("Appointment details", msg, "OK");
    }

    private async Task OnRescheduleClicked(MyAppointmentDto a)
    {
        await Navigation.PushAsync(new BookAppointmentPage(a.DoctorProfileId));
    }

    private async Task OnCancelClicked(MyAppointmentDto a)
    {
        var confirm = await DisplayAlert(
            "Cancel appointment",
            $"Cancel your visit with {a.DoctorFullName} on {a.DateDisplay}?",
            "Yes, cancel",
            "No");
        if (!confirm)
            return;

        var token = AuthService.CurrentToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlert("Session", "Please sign in again.", "OK");
            return;
        }

        try
        {
            var svc = new AppointmentService(token);
            await svc.CancelAppointmentAsync(a.AppointmentId);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Could not cancel", ex.Message, "OK");
        }
    }
}
