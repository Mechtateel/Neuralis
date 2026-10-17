using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using NeuralisAPP_1.Localization;
using NeuralisAPP_1.Models.Appointments;
using NeuralisAPP_1.Services;

namespace NeuralisAPP_1.Views.Doctor;

public partial class DoctorAppointmentsPage : ContentPage
{
    private readonly ObservableCollection<AppointmentTimelineItem> _items = new();
    private readonly List<DoctorAppointmentDto> _allAppointments = new();
    private readonly List<DateTime> _calendarDates = new();

    private Border? _activeNav;
    private bool _isProfileOpen;
    private AppointmentRange _activeRange = AppointmentRange.Today;
    private DateTime? _selectedCalendarDate;
    private string _searchQuery = string.Empty;
    private string? _statusFilter;
    private CancellationTokenSource? _searchDebounceCts;

    private readonly Label[] _calendarTopLabels;
    private readonly Border[] _calendarDayBorders;
    private readonly Label[] _calendarDayLabels;

    public DoctorAppointmentsPage()
    {
        InitializeComponent();
        AppointmentsList.ItemsSource = _items;
        ApplyLanguage();

        _calendarTopLabels =
        [
            CalendarTop0, CalendarTop1, CalendarTop2, CalendarTop3, CalendarTop4, CalendarTop5, CalendarTop6
        ];
        _calendarDayBorders =
        [
            CalendarDayBorder0, CalendarDayBorder1, CalendarDayBorder2, CalendarDayBorder3, CalendarDayBorder4, CalendarDayBorder5, CalendarDayBorder6
        ];
        _calendarDayLabels =
        [
            CalendarDayLabel0, CalendarDayLabel1, CalendarDayLabel2, CalendarDayLabel3, CalendarDayLabel4, CalendarDayLabel5, CalendarDayLabel6
        ];
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ApplyLanguage();
        SetActive(NavAppointments);
        UpdateTabVisuals();
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
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

        AppointmentsBusy.IsVisible = true;
        AppointmentsBusy.IsRunning = true;
        ErrorLabel.IsVisible = false;

        try
        {
            var svc = new AppointmentService(token);
            var list = await svc.GetAppointmentsAsync();

            _allAppointments.Clear();
            _allAppointments.AddRange(list);

            UpdateCalendar();
            UpdateStats();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Unable to load appointments. {ex.Message}";
            ErrorLabel.IsVisible = true;
            _items.Clear();
            SummaryLabel.Text = "0 appointments scheduled";
        }
        finally
        {
            AppointmentsBusy.IsRunning = false;
            AppointmentsBusy.IsVisible = false;
        }
    }

    private void ApplyFilters()
    {
        IEnumerable<DoctorAppointmentDto> filtered = _allAppointments;

        if (_selectedCalendarDate.HasValue)
        {
            var target = _selectedCalendarDate.Value.Date;
            filtered = filtered.Where(a => a.ScheduledAtUtc.ToLocalTime().Date == target);
        }
        else
        {
            var now = DateTime.Now;
            filtered = _activeRange switch
            {
                AppointmentRange.Today => filtered.Where(a => a.ScheduledAtUtc.ToLocalTime().Date == now.Date),
                AppointmentRange.Week => FilterToWeek(filtered, now),
                AppointmentRange.Month => filtered.Where(a => a.ScheduledAtUtc.ToLocalTime().Year == now.Year &&
                                                              a.ScheduledAtUtc.ToLocalTime().Month == now.Month),
                _ => filtered
            };
        }

        if (!string.IsNullOrWhiteSpace(_statusFilter))
        {
            filtered = filtered.Where(a =>
                string.Equals(NormalizeStatus(a.Status), _statusFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(_searchQuery))
        {
            var q = _searchQuery.Trim();
            filtered = filtered.Where(a =>
                ContainsIgnoreCase(a.PatientDisplay, q) ||
                ContainsIgnoreCase(a.PatientEmail, q) ||
                ContainsIgnoreCase(a.Type, q));
        }

        var sorted = filtered
            .OrderBy(a => a.ScheduledAtUtc.ToLocalTime())
            .ToList();

        BuildTimeline(sorted);
        UpdateSummary(sorted.Count);
        UpdateInsight(sorted);
        EmptyStateLabel.Text = "No appointments found";
    }

    private static IEnumerable<DoctorAppointmentDto> FilterToWeek(IEnumerable<DoctorAppointmentDto> source, DateTime anchor)
    {
        var start = StartOfWeek(anchor);
        var end = start.AddDays(7);
        return source.Where(a =>
        {
            var dt = a.ScheduledAtUtc.ToLocalTime();
            return dt >= start && dt < end;
        });
    }

    private void BuildTimeline(IReadOnlyList<DoctorAppointmentDto> sorted)
    {
        _items.Clear();
        if (sorted.Count == 0)
            return;

        var now = DateTime.Now;
        var firstFutureIdx = sorted
            .Select((a, idx) => new { a, idx })
            .FirstOrDefault(x => x.a.ScheduledAtUtc.ToLocalTime() >= now)?.idx ?? -1;

        var shouldInsertDivider = firstFutureIdx > 0 && firstFutureIdx < sorted.Count;
        for (var i = 0; i < sorted.Count; i++)
        {
            if (shouldInsertDivider && i == firstFutureIdx)
                _items.Add(AppointmentTimelineItem.CreateDivider($"{now:h:mm tt} — CURRENT"));

            _items.Add(AppointmentTimelineItem.Create(sorted[i]));
        }
    }

    private void UpdateSummary(int count)
    {
        if (_selectedCalendarDate.HasValue)
        {
            SummaryLabel.Text = $"{count} appointments on {_selectedCalendarDate.Value:MMM d}";
            return;
        }

        SummaryLabel.Text = _activeRange switch
        {
            AppointmentRange.Today => $"{count} appointments scheduled for today",
            AppointmentRange.Week => $"{count} appointments scheduled for this week",
            AppointmentRange.Month => $"{count} appointments scheduled for this month",
            _ => $"{count} appointments scheduled"
        };
    }

    private void UpdateStats()
    {
        var now = DateTime.Now;
        var thisWeekStart = StartOfWeek(now);
        var thisWeekEnd = thisWeekStart.AddDays(7);
        var prevWeekStart = thisWeekStart.AddDays(-7);

        var thisWeek = _allAppointments
            .Where(a =>
            {
                var dt = a.ScheduledAtUtc.ToLocalTime();
                return dt >= thisWeekStart && dt < thisWeekEnd;
            })
            .ToList();

        var prevWeekCount = _allAppointments.Count(a =>
        {
            var dt = a.ScheduledAtUtc.ToLocalTime();
            return dt >= prevWeekStart && dt < thisWeekStart;
        });

        var weeklyCount = thisWeek.Count;
        WeeklyLoadValueLabel.Text = weeklyCount.ToString(CultureInfo.InvariantCulture);

        var weekDelta = weeklyCount - prevWeekCount;
        WeeklyLoadTrendLabel.Text = weekDelta == 0
            ? "Same as last week"
            : $"{(weekDelta > 0 ? "+" : string.Empty)}{weekDelta} vs last week";

        var cancelled = thisWeek.Count(a => NormalizeStatus(a.Status) == "CANCELLED");
        var rate = weeklyCount == 0 ? 0 : (double)cancelled / weeklyCount * 100.0;
        CancellationRateValueLabel.Text = $"{rate:0.#}%";
        CancellationRateTrendLabel.Text = $"{cancelled} cancelled this week";
    }

    private void UpdateInsight(IReadOnlyCollection<DoctorAppointmentDto> visibleAppointments)
    {
        var latest = visibleAppointments
            .OrderByDescending(a => a.ScheduledAtUtc)
            .FirstOrDefault();

        if (latest is null)
        {
            InsightTextLabel.Text = "No appointments in the selected range. Try switching filters or selecting another date.";
            return;
        }

        var dt = latest.ScheduledAtUtc.ToLocalTime();
        var type = string.IsNullOrWhiteSpace(latest.Type) ? "consultation" : latest.Type.ToLowerInvariant();
        InsightTextLabel.Text =
            $"Latest appointment: {latest.PatientDisplay} at {dt:h:mm tt} on {dt:MMM d}. Consider preparing notes for this {type}.";
    }

    private void UpdateCalendar()
    {
        var baseDate = _selectedCalendarDate ?? DateTime.Today;
        var weekStart = StartOfWeek(baseDate);
        _calendarDates.Clear();
        for (var i = 0; i < 7; i++)
            _calendarDates.Add(weekStart.AddDays(i));

        var topStart = weekStart.AddDays(-7);
        for (var i = 0; i < 7; i++)
        {
            var topDate = topStart.AddDays(i);
            _calendarTopLabels[i].Text = topDate.Day.ToString(CultureInfo.InvariantCulture);
            _calendarTopLabels[i].TextColor = Color.FromArgb("#6b7280");
        }

        for (var i = 0; i < 7; i++)
            _calendarDayLabels[i].Text = _calendarDates[i].Day.ToString(CultureInfo.InvariantCulture);

        CalendarMonthLabel.Text = baseDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        UpdateCalendarSelectionVisuals();
    }

    private void UpdateCalendarSelectionVisuals()
    {
        var selected = _selectedCalendarDate ?? DateTime.Today;
        for (var i = 0; i < _calendarDayBorders.Length; i++)
        {
            var isSelected = _calendarDates[i].Date == selected.Date;
            _calendarDayBorders[i].BackgroundColor = isSelected
                ? Color.FromArgb("#47d6ec")
                : Colors.Transparent;
            _calendarDayLabels[i].TextColor = isSelected
                ? Color.FromArgb("#073b4c")
                : Color.FromArgb("#d1d5db");
            _calendarDayLabels[i].FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None;
        }
    }

    private void UpdateTabVisuals()
    {
        TodayTabLabel.Text = T.Get("Today", "Сегодня", "Бүгін");
        WeekTabLabel.Text = T.Get("Week", "Неделя", "Апта");
        MonthTabLabel.Text = T.Get("Month", "Месяц", "Ай");
        SetTabStyle(TodayTabBorder, TodayTabLabel, _activeRange == AppointmentRange.Today);
        SetTabStyle(WeekTabBorder, WeekTabLabel, _activeRange == AppointmentRange.Week);
        SetTabStyle(MonthTabBorder, MonthTabLabel, _activeRange == AppointmentRange.Month);
    }

    private void ApplyLanguage()
    {
        AppointmentsTitleLabel.Text = T.Get("Appointments", "Приемы", "Қабылдаулар");
        SearchEntry.Placeholder = T.Get("Search patients, appointments, or IDs...", "Поиск пациентов, приемов или ID...", "Науқастарды, қабылдауларды немесе ID іздеу...");
        ExportLabel.Text = T.Get("Export", "Экспорт", "Экспорт");
        FilterLabel.Text = T.Get("Filter", "Фильтр", "Сүзгі");
        EmptyStateLabel.Text = T.Get("No appointments found", "Приемы не найдены", "Қабылдаулар табылмады");
        UpdateTabVisuals();
    }

    private void OnLangEnClicked(object sender, EventArgs e)
    {
        Lang.Set("en");
        ApplyLanguage();
        ApplyFilters();
    }

    private void OnLangRuClicked(object sender, EventArgs e)
    {
        Lang.Set("ru");
        ApplyLanguage();
        ApplyFilters();
    }

    private void OnLangKzClicked(object sender, EventArgs e)
    {
        Lang.Set("kz");
        ApplyLanguage();
        ApplyFilters();
    }

    private static void SetTabStyle(Border border, Label label, bool isActive)
    {
        border.BackgroundColor = isActive ? Color.FromArgb("#4dd8ee") : Colors.Transparent;
        label.TextColor = isActive ? Color.FromArgb("#082f49") : Color.FromArgb("#94a3b8");
        label.FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, cts.Token);
                if (cts.IsCancellationRequested)
                    return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _searchQuery = e.NewTextValue?.Trim() ?? string.Empty;
                    ApplyFilters();
                });
            }
            catch (TaskCanceledException)
            {
                // noop
            }
        }, cts.Token);
    }

    private void OnTodayTabTapped(object sender, TappedEventArgs e) => SetRange(AppointmentRange.Today);
    private void OnWeekTabTapped(object sender, TappedEventArgs e) => SetRange(AppointmentRange.Week);
    private void OnMonthTabTapped(object sender, TappedEventArgs e) => SetRange(AppointmentRange.Month);

    private void SetRange(AppointmentRange range)
    {
        _activeRange = range;
        _selectedCalendarDate = null;
        UpdateTabVisuals();
        UpdateCalendar();
        ApplyFilters();
    }

    private void OnCalendarDayTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not string idxRaw || !int.TryParse(idxRaw, out var idx))
            return;
        if (idx < 0 || idx >= _calendarDates.Count)
            return;

        _selectedCalendarDate = _calendarDates[idx].Date;
        UpdateCalendarSelectionVisuals();
        ApplyFilters();
    }

    private void OnCalendarPrevTapped(object sender, TappedEventArgs e)
    {
        var pivot = (_selectedCalendarDate ?? DateTime.Today).AddDays(-7);
        _selectedCalendarDate = pivot.Date;
        UpdateCalendar();
        ApplyFilters();
    }

    private void OnCalendarNextTapped(object sender, TappedEventArgs e)
    {
        var pivot = (_selectedCalendarDate ?? DateTime.Today).AddDays(7);
        _selectedCalendarDate = pivot.Date;
        UpdateCalendar();
        ApplyFilters();
    }

    private async void OnAppointmentTapped(object sender, TappedEventArgs e)
    {
        if (sender is not TapGestureRecognizer tap || tap.BindingContext is not AppointmentTimelineItem item || item.IsDivider || item.Appointment is null)
            return;

        var action = await DisplayActionSheet("Appointment Actions", "Cancel", null, "View", "Download", "Analyze");
        switch (action)
        {
            case "View":
                await DisplayAlert("View", $"Open details for {item.PatientName}.", "OK");
                break;
            case "Download":
                await DisplayAlert("Download", "Appointment export is available from Export.", "OK");
                break;
            case "Analyze":
                await DisplayAlert("Analyze", "AI analysis request has been queued (stub).", "OK");
                break;
        }
    }

    private async void OnExportTapped(object sender, EventArgs e)
    {
        if (_items.Count == 0)
        {
            await DisplayAlert("Export", "No appointments to export.", "OK");
            return;
        }

        var csv = new StringBuilder();
        csv.AppendLine("Patient,Email,DateTime,Type,Status");
        foreach (var item in _items.Where(x => !x.IsDivider))
        {
            csv.AppendLine($"\"{item.PatientName}\",\"{item.Email}\",\"{item.LocalDateTime:O}\",\"{item.TypeDisplay}\",\"{item.Status}\"");
        }

        var path = Path.Combine(FileSystem.CacheDirectory, $"appointments-{DateTime.Now:yyyyMMddHHmmss}.csv");
        await File.WriteAllTextAsync(path, csv.ToString());
        await DisplayAlert("Export", $"CSV saved to {path}", "OK");
    }

    private async void OnFilterTapped(object sender, EventArgs e)
    {
        var selected = await DisplayActionSheet("Status Filter", "Cancel", null, "All", "CONFIRMED", "PENDING", "CANCELLED");
        if (string.IsNullOrWhiteSpace(selected) || selected == "Cancel")
            return;

        _statusFilter = selected == "All" ? null : selected;
        ApplyFilters();
    }

    private async void OnInsightUpdateClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Insight", "Appointment insight refresh complete.", "OK");
    }

    private async void OnDashboardTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//DoctorDashboard");
    }

    private async void OnPatientsTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DoctorPatientsPage());
    }

    private async void OnLabTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DoctorLabResultsPage());
    }

    private async void OnAITapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DoctorAIInsightsPage());
    }

    private async void OnProfileTapped(object sender, EventArgs e)
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

    private async Task CloseProfileMenu()
    {
        await ProfileMenu.FadeTo(0, 100);
        await ProfileMenu.ScaleTo(0.96, 100);
        ProfileMenu.IsVisible = false;
        _isProfileOpen = false;
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        await CloseProfileMenu();
        await App.LogoutAsync();
    }

    private void OnNavPointerEntered(object sender, PointerEventArgs e)
    {
        if (sender is not Border border || border == _activeNav)
            return;

        border.BackgroundColor = Color.FromArgb("#0f172a");
        SetNavTextColor(border, "#e5e7eb");
    }

    private void OnNavPointerExited(object sender, PointerEventArgs e)
    {
        if (sender is not Border border || border == _activeNav)
            return;

        border.BackgroundColor = Colors.Transparent;
        SetNavTextColor(border, "#9ca3af");
    }

    private void SetActive(Border active)
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

    private void ResetNav(Border nav)
    {
        nav.BackgroundColor = Colors.Transparent;
        SetNavTextColor(nav, "#9ca3af");
    }

    private void SetNavTextColor(Border nav, string colorHex)
    {
        if (nav.Content is HorizontalStackLayout layout)
        {
            foreach (var label in layout.Children.OfType<Label>())
                label.TextColor = Color.FromArgb(colorHex);
        }
    }

    private static bool ContainsIgnoreCase(string? source, string query) =>
        !string.IsNullOrWhiteSpace(source) && source.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    private static string NormalizeStatus(string? status)
    {
        var normalized = status?.Trim().ToUpperInvariant();
        return normalized switch
        {
            "PENDING" => "PENDING",
            "CANCELLED" => "CANCELLED",
            _ => "CONFIRMED"
        };
    }

    private enum AppointmentRange
    {
        Today,
        Week,
        Month
    }

    private sealed class AppointmentTimelineItem
    {
        public bool IsDivider { get; init; }
        public bool IsAppointment => !IsDivider;
        public string DividerText { get; init; } = string.Empty;
        public DoctorAppointmentDto? Appointment { get; init; }

        public string TimeText => IsDivider || Appointment is null
            ? string.Empty
            : Appointment.ScheduledAtUtc.ToLocalTime().ToString("h:mm", CultureInfo.InvariantCulture);

        public string MeridiemText => IsDivider || Appointment is null
            ? string.Empty
            : Appointment.ScheduledAtUtc.ToLocalTime().ToString("tt", CultureInfo.InvariantCulture).ToUpperInvariant();

        public string PatientName => IsDivider ? string.Empty : Appointment?.PatientDisplay ?? string.Empty;
        public string Email => IsDivider ? string.Empty : Appointment?.PatientEmail ?? string.Empty;

        public string TypeDisplay
        {
            get
            {
                if (IsDivider || Appointment is null)
                    return string.Empty;

                var type = string.IsNullOrWhiteSpace(Appointment.Type)
                    ? "General Consultation"
                    : Appointment.Type;
                return $"{type} · {Appointment.ScheduledAtUtc.ToLocalTime():ddd, MMM d}";
            }
        }

        public string Status => IsDivider ? string.Empty : NormalizeStatus(Appointment?.Status);

        public DateTime LocalDateTime => Appointment?.ScheduledAtUtc.ToLocalTime() ?? DateTime.MinValue;

        public static AppointmentTimelineItem CreateDivider(string text) => new()
        {
            IsDivider = true,
            DividerText = text
        };

        public static AppointmentTimelineItem Create(DoctorAppointmentDto appointment) => new()
        {
            IsDivider = false,
            Appointment = appointment
        };
    }
}
