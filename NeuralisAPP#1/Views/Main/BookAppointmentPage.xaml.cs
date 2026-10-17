using System.Diagnostics;
using NeuralisAPP_1.Models.Appointments;
using NeuralisAPP_1.Models.Doctor;
using NeuralisAPP_1.Services;
using NeuralisAPP_1.Views.Main.Controls;

namespace NeuralisAPP_1.Views.Main;

public partial class BookAppointmentPage : ContentPage
{
    private static readonly TimeSpan[] SlotTimes =
    {
        new(9, 0, 0),
        new(10, 0, 0),
        new(11, 30, 0),
        new(13, 0, 0),
        new(14, 15, 0),
        new(15, 0, 0),
        new(16, 30, 0),
        new(17, 0, 0)
    };

    /// <summary>Main content area (excluding 260px sidebar) below this width stacks booking under the doctor list.</summary>
    private const double LayoutBreakpoint = 560;

    private readonly Guid? _preselectedDoctorProfileId;
    private List<AvailableDoctorDto> _doctors = new();
    private List<AvailableDoctorDto> _filtered = new();
    private AvailableDoctorDto? _selectedDoctor;
    private TimeSpan? _selectedSlotTime;
    private bool _narrowLayout;
    private double _lastAllocatedWidth = -1;
    private readonly List<string?> _specApiByPickerIndex = new();
    private bool _specializationPickerReady;

    public BookAppointmentPage(Guid? preselectedDoctorProfileId = null)
    {
        InitializeComponent();
        _preselectedDoctorProfileId = preselectedDoctorProfileId;

        UserSidebarCoordinator.Wire(
            AppSidebar,
            UserSidebarActiveItem.BookVisit,
            onDashboardTap: async () => await Navigation.PopAsync(),
            onBookVisitTap: () => Task.CompletedTask,
            onAppointmentsTap: async () => await Navigation.PushAsync(new UserAppointmentsPage()),
            onLatestNewsTap: () => Shell.Current.GoToAsync("//LatestUpdates"),
            onLogoutTap: () => App.LogoutAsync(),
            closeSidebar: () => GlobalLayout.CloseSidebar());
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || Math.Abs(width - _lastAllocatedWidth) < 1)
            return;
        _lastAllocatedWidth = width;
        ApplyResponsiveLayout(Math.Max(0, width));
    }

    protected override async void OnAppearing()
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

        RebuildTimeSlots();

        var token = AuthService.CurrentToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlert("Session", "Please sign in again.", "OK");
            await Navigation.PopAsync();
            return;
        }

        BuildSpecializationPickerItems();
        _specializationPickerReady = false;
        SpecializationPicker.SelectedIndex = 0;
        _specializationPickerReady = true;

        LoadingIndicator.IsVisible = true;
        SubmitButton.IsEnabled = false;
        try
        {
            await FetchDoctorsAsync(token);

            if (_preselectedDoctorProfileId is { } id)
            {
                var match = _doctors.FirstOrDefault(d => d.DoctorProfileId == id);
                if (match != null)
                    SelectDoctor(match);
            }

            if (_doctors.Count == 0)
                await DisplayAlert("No doctors", "There are no verified doctors available yet. Try again later.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            RefreshSubmitButtonState();
        }

        if (Width > 0)
            ApplyResponsiveLayout(Math.Max(0, Width));
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

    private async void OnClearFiltersClicked(object sender, EventArgs e)
    {
        NameFilterEntry.Text = string.Empty;
        _specializationPickerReady = false;
        if (SpecializationPicker.Items.Count > 0)
            SpecializationPicker.SelectedIndex = 0;
        _specializationPickerReady = true;

        var token = AuthService.CurrentToken;
        if (string.IsNullOrWhiteSpace(token))
            return;

        LoadingIndicator.IsVisible = true;
        try
        {
            await FetchDoctorsAsync(token);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
        }
    }

    private void ApplyResponsiveLayout(double width)
    {
        var narrow = width < LayoutBreakpoint;
        if (narrow == _narrowLayout && RootSplit.ColumnDefinitions.Count > 0)
            return;

        _narrowLayout = narrow;

        if (narrow)
        {
            RootSplit.ColumnDefinitions.Clear();
            RootSplit.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            RootSplit.RowDefinitions.Clear();
            RootSplit.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RootSplit.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(LeftScroll, 0);
            Grid.SetColumn(LeftScroll, 0);
            Grid.SetRow(RightScroll, 1);
            Grid.SetColumn(RightScroll, 0);
        }
        else
        {
            RootSplit.RowDefinitions.Clear();
            RootSplit.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            RootSplit.ColumnDefinitions.Clear();
            RootSplit.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7, GridUnitType.Star) });
            RootSplit.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });

            Grid.SetRow(LeftScroll, 0);
            Grid.SetColumn(LeftScroll, 0);
            Grid.SetRow(RightScroll, 0);
            Grid.SetColumn(RightScroll, 1);
        }

        RebuildDoctorGrid();
        RebuildTimeSlots();
    }

    private void BuildSpecializationPickerItems()
    {
        SpecializationPicker.Items.Clear();
        _specApiByPickerIndex.Clear();

        SpecializationPicker.Items.Add("All specializations");
        _specApiByPickerIndex.Add(null);

        SpecializationPicker.Items.Add("Therapist");
        _specApiByPickerIndex.Add("Therapist");

        SpecializationPicker.Items.Add("ENT (Otolaryngologist)");
        _specApiByPickerIndex.Add("ENT");

        SpecializationPicker.Items.Add("Dermatologist");
        _specApiByPickerIndex.Add("Dermatologist");

        SpecializationPicker.Items.Add("Urologist (😏 Specialist)");
        _specApiByPickerIndex.Add("Urologist");

        SpecializationPicker.Items.Add("Proctologist (💀 Dangerous Doctor)");
        _specApiByPickerIndex.Add("Proctologist");
    }

    private async Task FetchDoctorsAsync(string token)
    {
        string? spec = null;
        var idx = SpecializationPicker.SelectedIndex;
        if (idx >= 0 && idx < _specApiByPickerIndex.Count)
            spec = _specApiByPickerIndex[idx];

        var svc = new AppointmentService(token);
        _doctors = await svc.GetAvailableDoctorsAsync(spec);
        ApplyFilters();
    }

    private void OnNameFilterChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private async void OnSpecializationChanged(object? sender, EventArgs e)
    {
        if (!_specializationPickerReady)
            return;

        var token = AuthService.CurrentToken;
        if (string.IsNullOrWhiteSpace(token))
            return;

        LoadingIndicator.IsVisible = true;
        try
        {
            await FetchDoctorsAsync(token);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
        }
    }

    private void OnAppointmentDateSelected(object? sender, DateChangedEventArgs e)
    {
        if (e.NewDate < DateTime.Today)
            AppointmentDatePicker.Date = DateTime.Today;
    }

    private void ApplyFilters()
    {
        var q = NameFilterEntry.Text?.Trim() ?? "";

        _filtered = _doctors
            .Where(d =>
                string.IsNullOrEmpty(q) ||
                d.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                d.Email.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();

        EmptyDoctorsLabel.IsVisible = _filtered.Count == 0;
        EmptyDoctorsLabel.Text = _doctors.Count == 0
            ? "No verified doctors available yet."
            : "No doctors match your filters.";
        RebuildDoctorGrid();
    }

    private void RefreshSubmitButtonState()
    {
        SubmitButton.IsEnabled = _selectedDoctor != null && _selectedSlotTime != null;
    }

    private void SelectDoctor(AvailableDoctorDto doctor)
    {
        _selectedDoctor = doctor;
        SelectedDoctorNameLabel.Text = doctor.FullName;
        SelectedDoctorSpecLabel.Text = DoctorSpecializationUi.Format(doctor.Specialization);
        RebuildDoctorGrid();
        RefreshSubmitButtonState();
    }

    private void RebuildDoctorGrid()
    {
        DoctorCardsGrid.Children.Clear();
        DoctorCardsGrid.RowDefinitions.Clear();
        DoctorCardsGrid.ColumnDefinitions.Clear();

        var cols = _narrowLayout ? 1 : 2;
        for (var c = 0; c < cols; c++)
            DoctorCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        if (_filtered.Count == 0)
            return;

        var rows = (int)Math.Ceiling(_filtered.Count / (double)cols);
        for (var r = 0; r < rows; r++)
            DoctorCardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var i = 0; i < _filtered.Count; i++)
        {
            var d = _filtered[i];
            var selected = _selectedDoctor?.DoctorProfileId == d.DoctorProfileId;
            var card = CreateDoctorCard(d, selected);
            var row = i / cols;
            var col = i % cols;
            DoctorCardsGrid.Children.Add(card);
            Grid.SetRow(card, row);
            Grid.SetColumn(card, col);
        }
    }

    private Border CreateDoctorCard(AvailableDoctorDto d, bool selected)
    {
        var accent = ResolveColor("Accent", Color.FromArgb("#2FE3C9"));
        var textPrimary = ResolveColor("TextPrimary", Colors.White);
        var textMuted = ResolveColor("TextMuted", Color.FromArgb("#9FBFC4"));
        var hoverStyle = Application.Current?.Resources["HoverCardStyle"] as Style;
        var ghostStyle = Application.Current?.Resources["GhostButton"] as Style;

        var inner = new VerticalStackLayout { Spacing = 12 };

        var titleRow = new HorizontalStackLayout { Spacing = 10 };
        titleRow.Add(new Label
        {
            Text = "medical_services",
            FontFamily = "MaterialIcons",
            FontSize = 22,
            TextColor = accent,
            VerticalOptions = LayoutOptions.Center
        });
        titleRow.Add(new Label
        {
            Text = d.FullName,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            TextColor = textPrimary,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        });
        inner.Add(titleRow);

        inner.Add(new Label
        {
            Text = DoctorSpecializationUi.Format(d.Specialization),
            FontSize = 14,
            TextColor = accent,
            LineBreakMode = LineBreakMode.WordWrap
        });

        var statsRow = new HorizontalStackLayout { Spacing = 6 };
        statsRow.Add(new Label
        {
            Text = "star",
            FontFamily = "MaterialIcons",
            FontSize = 14,
            TextColor = textMuted,
            VerticalOptions = LayoutOptions.Center
        });
        statsRow.Add(new Label
        {
            Text = "Verified specialist",
            FontSize = 12,
            TextColor = textMuted,
            VerticalOptions = LayoutOptions.Center
        });
        inner.Add(statsRow);

        inner.Add(new Label
        {
            Text = d.Email,
            FontSize = 11,
            TextColor = textMuted,
            LineBreakMode = LineBreakMode.TailTruncation
        });

        var bookBtn = new Button
        {
            Text = "Book Appointment",
            Style = ghostStyle,
            HorizontalOptions = LayoutOptions.Start
        };
        bookBtn.Clicked += (_, _) => SelectDoctor(d);
        inner.Add(bookBtn);

        var border = new Border
        {
            Style = hoverStyle,
            Padding = new Thickness(18, 18, 18, 16),
            MinimumHeightRequest = 188,
            Content = inner
        };

        if (selected)
        {
            border.Stroke = accent;
            border.StrokeThickness = 2;
        }

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => SelectDoctor(d);
        border.GestureRecognizers.Add(tap);

        return border;
    }

    private static Color ResolveColor(string key, Color fallback)
    {
        return Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c
            ? c
            : fallback;
    }

    private void RebuildTimeSlots()
    {
        var primaryStyle = Application.Current?.Resources["PrimaryButton"] as Style;
        var ghostStyle = Application.Current?.Resources["GhostButton"] as Style;

        TimeSlotsGrid.Children.Clear();
        TimeSlotsGrid.RowDefinitions.Clear();

        var cols = 2;
        var n = SlotTimes.Length;
        var rows = (int)Math.Ceiling(n / (double)cols);
        for (var r = 0; r < rows; r++)
            TimeSlotsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var i = 0; i < n; i++)
        {
            var ts = SlotTimes[i];
            var selected = _selectedSlotTime == ts;
            var labelTime = DateTime.Today.Add(ts).ToString("t");
            var btn = new Button
            {
                Text = labelTime,
                Style = selected ? primaryStyle : ghostStyle
            };
            var captured = ts;
            btn.Clicked += (_, _) =>
            {
                _selectedSlotTime = captured;
                RebuildTimeSlots();
            };
            TimeSlotsGrid.Children.Add(btn);
            Grid.SetRow(btn, i / cols);
            Grid.SetColumn(btn, i % cols);
        }

        RefreshSubmitButtonState();
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        if (_selectedDoctor == null)
        {
            await DisplayAlert("Book a visit", "Please select a doctor before booking", "OK");
            return;
        }

        if (_selectedSlotTime == null)
        {
            await DisplayAlert("Book a visit", "Please select a time slot before booking.", "OK");
            return;
        }

        var token = AuthService.CurrentToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlert("Session", "Please sign in again.", "OK");
            return;
        }

        var local = AppointmentDatePicker.Date.Date + _selectedSlotTime.Value;
        var localKind = DateTime.SpecifyKind(local, DateTimeKind.Local);
        if (localKind <= DateTime.Now)
        {
            await DisplayAlert("Invalid time", "Choose a date and time in the future.", "OK");
            return;
        }

        var utc = localKind.ToUniversalTime();

        SubmitButton.IsEnabled = false;
        try
        {
            Debug.WriteLine($"Booking: doctor={_selectedDoctor.DoctorProfileId}, time={utc:O}");
            var svc = new AppointmentService(token);
            await svc.CreateAppointmentAsync(_selectedDoctor.DoctorProfileId, utc);
            await DisplayAlert("Success", "Your appointment was booked.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            var msg = !string.IsNullOrWhiteSpace(ex.Message)
                ? ex.Message
                : "Unable to complete booking. Please try again.";
            await DisplayAlert("Book a visit", msg, "OK");
        }
        finally
        {
            RefreshSubmitButtonState();
        }
    }
}
