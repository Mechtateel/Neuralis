using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using NeuralisAPP_1.Localization;
using NeuralisAPP_1.Models.Patients;
using NeuralisAPP_1.Services;
using NeuralisAPP_1.ViewModels.Lab;

namespace NeuralisAPP_1.Views.Doctor;

public partial class DoctorPatientsPage : ContentPage
{
    private readonly ObservableCollection<PatientDirectoryRowViewModel> _patients = new();
    private readonly List<PatientDirectoryDto> _allPatients = new();
    private List<PatientDirectoryDto> _filteredPatients = new();

    private Border? _activeNav;
    private bool _isProfileOpen;
    private CancellationTokenSource? _searchDebounceCts;

    private string _searchTerm = string.Empty;
    private string? _statusFilter;
    private LastSyncFilter _lastSyncFilter = LastSyncFilter.All;
    private int _currentPage = 1;
    private const int PageSize = 10;

    public DoctorPatientsPage()
    {
        InitializeComponent();
        PatientsList.ItemsSource = _patients;
        ApplyLanguage();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ApplyLanguage();
        SetActive(NavPatients);
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        try
        {
            await LoadAsync();
        }
        finally
        {
            PatientsRefreshView.IsRefreshing = false;
        }
    }

    private async Task LoadAsync()
    {
        var token = AuthService.CurrentToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlert("Session", "Please sign in again.", "OK");
            return;
        }

        PatientsErrorLabel.IsVisible = false;
        PatientsBusy.IsVisible = true;
        PatientsBusy.IsRunning = true;
        try
        {
            var service = new PatientService(token);
            var list = await service.GetPatientsAsync();
            System.Diagnostics.Debug.WriteLine($"PATIENTS RESPONSE COUNT: {list.Count}");
            _allPatients.Clear();
            _allPatients.AddRange(list.OrderByDescending(p => p.LastSync));
            _currentPage = 1;
            ApplyFiltersAndPagination();
            UpdateStatCards();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DoctorPatients] {ex}");
            _allPatients.Clear();
            _filteredPatients = new List<PatientDirectoryDto>();
            _patients.Clear();
            PatientsCountLabel.Text = T.Get("No patients found", "Пациенты не найдены", "Науқастар табылмады");
            PatientsErrorLabel.Text = T.Get("Failed to load patients", "Не удалось загрузить пациентов", "Науқастарды жүктеу мүмкін болмады");
            PatientsErrorLabel.IsVisible = true;
            UpdateStatCards();
            UpdatePagerUi();
        }
        finally
        {
            PatientsBusy.IsRunning = false;
            PatientsBusy.IsVisible = false;
        }
    }

    private void ApplyLanguage()
    {
        SearchEntry.Placeholder = T.Get("Search records...", "Поиск записей...", "Жазбаларды іздеу...");
        FilterLabel.Text = T.Get("Filter", "Фильтр", "Сүзгі");
        ExportLabel.Text = T.Get("Export", "Экспорт", "Экспорт");
        DirectoryTitleLabel.Text = T.Get("Patient Directory", "Каталог пациентов", "Науқастар тізімі");
        DirectorySubtitleLabel.Text = T.Get(
            "Manage and view active patient profiles across the neural clinical network.",
            "Управляйте и просматривайте активные профили пациентов клинической сети.",
            "Клиникалық желідегі белсенді пациент профильдерін басқарыңыз және қараңыз.");
        NoPatientsLabel.Text = T.Get("No patients found", "Пациенты не найдены", "Науқастар табылмады");
    }

    private void OnLangEnClicked(object sender, EventArgs e)
    {
        Lang.Set("en");
        ApplyLanguage();
        ApplyFiltersAndPagination();
    }

    private void OnLangRuClicked(object sender, EventArgs e)
    {
        Lang.Set("ru");
        ApplyLanguage();
        ApplyFiltersAndPagination();
    }

    private void OnLangKzClicked(object sender, EventArgs e)
    {
        Lang.Set("kz");
        ApplyLanguage();
        ApplyFiltersAndPagination();
    }

    private void ApplyFiltersAndPagination()
    {
        IEnumerable<PatientDirectoryDto> query = _allPatients;

        if (!string.IsNullOrWhiteSpace(_searchTerm))
        {
            query = query.Where(p =>
                ContainsIgnoreCase(p.FullName, _searchTerm) ||
                ContainsIgnoreCase(p.Email, _searchTerm) ||
                ContainsIgnoreCase(p.PatientCode, _searchTerm));
        }

        if (!string.IsNullOrWhiteSpace(_statusFilter))
        {
            query = query.Where(p =>
                string.Equals(p.Status, _statusFilter, StringComparison.OrdinalIgnoreCase));
        }

        var now = DateTime.UtcNow;
        query = _lastSyncFilter switch
        {
            LastSyncFilter.Today => query.Where(p => p.LastSync.ToLocalTime().Date == DateTime.Now.Date),
            LastSyncFilter.Last7Days => query.Where(p => p.LastSync >= now.AddDays(-7)),
            LastSyncFilter.Last30Days => query.Where(p => p.LastSync >= now.AddDays(-30)),
            _ => query
        };

        _filteredPatients = query
            .OrderByDescending(p => p.LastSync)
            .ToList();
        System.Diagnostics.Debug.WriteLine($"patients state: total={_allPatients.Count}, filtered={_filteredPatients.Count}, page={_currentPage}");

        var totalPages = Math.Max(1, (int)Math.Ceiling(_filteredPatients.Count / (double)PageSize));
        _currentPage = Math.Clamp(_currentPage, 1, totalPages);

        var pageSlice = _filteredPatients
            .Skip((_currentPage - 1) * PageSize)
            .Take(PageSize)
            .Select(MapToRow)
            .ToList();

        _patients.Clear();
        foreach (var row in pageSlice)
            _patients.Add(row);

        var from = _filteredPatients.Count == 0 ? 0 : ((_currentPage - 1) * PageSize) + 1;
        var to = Math.Min(_currentPage * PageSize, _filteredPatients.Count);
        PatientsCountLabel.Text = _filteredPatients.Count == 0
            ? T.Get("No patients found", "Пациенты не найдены", "Науқастар табылмады")
            : T.Get(
                $"Showing {from}-{to} of {_filteredPatients.Count} active patients",
                $"Показано {from}-{to} из {_filteredPatients.Count} активных пациентов",
                $"{_filteredPatients.Count} белсенді науқастың {from}-{to} көрсетілді");

        UpdatePagerUi();
    }

    private void UpdateStatCards()
    {
        var total = _allPatients.Count;
        var critical = _allPatients.Count(p => string.Equals(p.Status, "DISCHARGED", StringComparison.OrdinalIgnoreCase));
        var pending = _allPatients.Count(p => string.Equals(p.Status, "NEW", StringComparison.OrdinalIgnoreCase));
        var ai = _allPatients.Count(p =>
            string.Equals(p.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Status, "RETURNING", StringComparison.OrdinalIgnoreCase));

        TotalPatientsLabel.Text = total.ToString(CultureInfo.InvariantCulture);
        TotalPatientsTrendLabel.Text = $"+{Math.Max(0, total / 10)}%";
        CriticalAlertsLabel.Text = critical.ToString(CultureInfo.InvariantCulture);
        PendingReviewLabel.Text = pending.ToString(CultureInfo.InvariantCulture);
        AiDiagnosticsLabel.Text = ai.ToString(CultureInfo.InvariantCulture);
    }

    private void UpdatePagerUi()
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(_filteredPatients.Count / (double)PageSize));
        var pageBase = ((Math.Max(_currentPage - 1, 0)) / 3) * 3 + 1;
        var pages = new[] { pageBase, pageBase + 1, pageBase + 2 };

        UpdatePageButton(Page1Border, Page1Label, pages[0], totalPages);
        UpdatePageButton(Page2Border, Page2Label, pages[1], totalPages);
        UpdatePageButton(Page3Border, Page3Label, pages[2], totalPages);

        PrevPageBorder.Opacity = _currentPage <= 1 ? 0.45 : 1;
        NextPageBorder.Opacity = _currentPage >= totalPages ? 0.45 : 1;
    }

    private void UpdatePageButton(Border border, Label label, int page, int totalPages)
    {
        label.Text = page <= totalPages ? page.ToString(CultureInfo.InvariantCulture) : string.Empty;
        border.IsVisible = page <= totalPages;

        var isActive = page == _currentPage;
        border.BackgroundColor = isActive ? Color.FromArgb("#22d3ee") : Color.FromArgb("#111827");
        label.TextColor = isActive ? Color.FromArgb("#0f172a") : Color.FromArgb("#9ca3af");
    }

    private PatientDirectoryRowViewModel MapToRow(PatientDirectoryDto dto)
    {
        var status = NormalizeStatus(dto.Status);
        var (bg, fg) = status switch
        {
            "ACTIVE" => ("#155e75", "#67e8f9"),
            "RETURNING" => ("#78350f", "#fbbf24"),
            "NEW" => ("#4c1d95", "#c4b5fd"),
            "DISCHARGED" => ("#374151", "#d1d5db"),
            _ => ("#1f2937", "#cbd5e1")
        };

        return new PatientDirectoryRowViewModel
        {
            Id = dto.Id,
            FullName = string.IsNullOrWhiteSpace(dto.FullName) ? "Unknown Patient" : dto.FullName,
            IdentityLine = $"#{dto.PatientCode} • {dto.Email}",
            Initials = BuildInitials(dto.FullName),
            StatusLabel = status is "NEW" ? "NEW PATIENT" : status,
            StatusBackground = bg,
            StatusForeground = fg,
            LastSyncRelative = ToRelativeTime(dto.LastSync.ToLocalTime())
        };
    }

    private static string ToRelativeTime(DateTime dateTime)
    {
        var delta = DateTime.Now - dateTime;
        if (delta.TotalSeconds < 60)
            return "Just now";
        if (delta.TotalMinutes < 60)
            return $"{Math.Max(1, (int)delta.TotalMinutes)} mins ago";
        if (delta.TotalHours < 24)
            return $"{Math.Max(1, (int)delta.TotalHours)} hours ago";
        return $"{Math.Max(1, (int)delta.TotalDays)} days ago";
    }

    private static string BuildInitials(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "NA";

        var parts = fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(p => p[0].ToString().ToUpperInvariant())
            .ToArray();
        return parts.Length == 0 ? "NA" : string.Join(string.Empty, parts);
    }

    private static string NormalizeStatus(string? status) => status?.Trim().ToUpperInvariant() switch
    {
        "ACTIVE" => "ACTIVE",
        "RETURNING" => "RETURNING",
        "NEW" => "NEW",
        "DISCHARGED" => "DISCHARGED",
        _ => "ACTIVE"
    };

    private static bool ContainsIgnoreCase(string? source, string needle) =>
        !string.IsNullOrWhiteSpace(source) &&
        source.Contains(needle, StringComparison.OrdinalIgnoreCase);

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
                    _searchTerm = e.NewTextValue?.Trim() ?? string.Empty;
                    _currentPage = 1;
                    ApplyFiltersAndPagination();
                });
            }
            catch (TaskCanceledException)
            {
                // noop
            }
        }, cts.Token);
    }

    private async void OnFilterTapped(object sender, TappedEventArgs e)
    {
        var status = await DisplayActionSheet("Status", "Cancel", null, "All", "ACTIVE", "RETURNING", "NEW", "DISCHARGED");
        if (status != "Cancel")
            _statusFilter = status == "All" ? null : status;

        var sync = await DisplayActionSheet("Last Sync", "Cancel", null, "All", "Today", "7 Days", "30 Days");
        _lastSyncFilter = sync switch
        {
            "Today" => LastSyncFilter.Today,
            "7 Days" => LastSyncFilter.Last7Days,
            "30 Days" => LastSyncFilter.Last30Days,
            _ => LastSyncFilter.All
        };

        _currentPage = 1;
        ApplyFiltersAndPagination();
    }

    private async void OnExportTapped(object sender, TappedEventArgs e)
    {
        if (_filteredPatients.Count == 0)
        {
            await DisplayAlert("Export", "No patients to export.", "OK");
            return;
        }

        var csv = new StringBuilder();
        csv.AppendLine("Id,FullName,Email,PatientCode,Status,LastSync,CreatedAt");
        foreach (var p in _filteredPatients)
        {
            csv.AppendLine($"{p.Id},\"{p.FullName}\",\"{p.Email}\",\"{p.PatientCode}\",{NormalizeStatus(p.Status)},{p.LastSync:O},{p.CreatedAt:O}");
        }

        var path = Path.Combine(FileSystem.CacheDirectory, $"patients-{DateTime.Now:yyyyMMddHHmmss}.csv");
        await File.WriteAllTextAsync(path, csv.ToString());
        await DisplayAlert("Export", $"CSV saved to {path}", "OK");
    }

    private void OnPrevPageTapped(object sender, TappedEventArgs e)
    {
        if (_currentPage <= 1)
            return;
        _currentPage--;
        ApplyFiltersAndPagination();
    }

    private void OnNextPageTapped(object sender, TappedEventArgs e)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(_filteredPatients.Count / (double)PageSize));
        if (_currentPage >= totalPages)
            return;
        _currentPage++;
        ApplyFiltersAndPagination();
    }

    private void OnPageTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not string raw || !int.TryParse(raw, out var page))
            return;

        var totalPages = Math.Max(1, (int)Math.Ceiling(_filteredPatients.Count / (double)PageSize));
        if (page < 1 || page > totalPages)
            return;

        _currentPage = page;
        ApplyFiltersAndPagination();
    }

    private async void OnOpenProfileClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not Guid id)
            return;

        try
        {
            await Shell.Current.GoToAsync($"/patients/{id}");
        }
        catch
        {
            await DisplayAlert("Profile", $"Patient ID: {id}", "OK");
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

    private async void OnDashboardTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//DoctorDashboard");
    }

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
                label.TextColor = Color.FromArgb(colorHex);
        }
    }

    private enum LastSyncFilter
    {
        All,
        Today,
        Last7Days,
        Last30Days
    }
}
