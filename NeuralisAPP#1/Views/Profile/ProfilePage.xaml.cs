using NeuralisAPP_1.Localization;
using NeuralisAPP_1.Models.Appointments;
using NeuralisAPP_1.Models.Auth;
using NeuralisAPP_1.Models.Doctor;
using NeuralisAPP_1.Models.Profile;
using NeuralisAPP_1.Services;
using NeuralisAPP_1.ViewModels.Profile;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NeuralisAPP_1.Views.Profile;

public partial class ProfilePage : ContentPage
{
    private const double CompactBreakpoint = 900;
    private const double NarrowSidebarBreakpoint = 720;

    private readonly ProfileViewModel _vm = new();

    private readonly UserRole _role;
    private readonly string? _specialization;
    private readonly bool _isLabTechnician;
    private readonly bool _isVerified;
    private bool _modalOpen;
    private Border? _activeActionRow;

    // Footer action handlers for the currently open modal.
    private Func<Task>? _modalPrimaryHandler;
    private Func<Task>? _modalSecondaryHandler;

    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public ProfilePage()
    {
        InitializeComponent();

        var user = App.CurrentUser;
        _role = user == null ? UserRole.User : (UserRole)user.Role;
        _specialization = user?.DoctorSpecialization;
        _isLabTechnician = DoctorSpecializationUi.IsLabTechnician(_specialization);
        _isVerified = user?.IsVerified ?? false;

        ApplyIdentity(user);
        ApplyRoleStrings();
        BuildInfoSection(user);
        BuildStatsTiles();
        BuildStatusCard();
        UpdateLanguageLabel();

        SizeChanged += (_, _) => ApplyResponsiveLayout();
        Loaded += (_, _) => ApplyResponsiveLayout();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Refresh VM + UI in case the user came back from an Edit / Language page.
        _vm.Reload();
        var user = App.CurrentUser;
        ApplyIdentity(user);
        BuildInfoSection(user);
        BuildStatusCard();
        UpdateLanguageLabel();
        UpdateHeroBio();

        await LoadRoleActivityAsync();
    }

    // =====================================================
    // Identity / role strings
    // =====================================================

    private void ApplyIdentity(MeResponse? user)
    {
        var displayName = string.IsNullOrWhiteSpace(user?.FullName)
            ? (user?.Email ?? "User")
            : user.FullName!;
        var email = string.IsNullOrWhiteSpace(user?.Email) ? "—" : user!.Email!;

        HeroNameLabel.Text = displayName;
        HeroEmailLabel.Text = email;
        SidebarUserNameLabel.Text = displayName;
        AvatarInitialsLabel.Text = ResolveInitials(displayName);
        AccountIdLabel.Text = user?.Id == Guid.Empty || user == null
            ? "ID —"
            : $"ID · {user.Id.ToString("N").Substring(0, 8).ToUpperInvariant()}";

        // Account-creation date isn't returned by /auth/me yet; show a graceful "Active session" placeholder.
        MemberSinceLabel.Text = DateTime.Now.ToString("MMM yyyy");

        VerifiedBadge.IsVisible = _isVerified && _role == UserRole.Doctor;
    }

    private void ApplyRoleStrings()
    {
        var roleText = _role switch
        {
            UserRole.Admin => "ADMINISTRATOR",
            UserRole.Doctor when _isLabTechnician => "LAB TECHNICIAN",
            UserRole.Doctor => "DOCTOR",
            _ => "PATIENT"
        };
        var roleSub = _role switch
        {
            UserRole.Admin => "System Administrator",
            UserRole.Doctor when _isLabTechnician => DoctorSpecializationUi.Format(_specialization) is { Length: > 0 } s
                ? $"Lab · {s}"
                : "Lab Technician",
            UserRole.Doctor => string.IsNullOrWhiteSpace(_specialization)
                ? "Clinician"
                : DoctorSpecializationUi.Format(_specialization),
            _ => "Patient"
        };

        RoleBadgeLabel.Text = roleText;
        SidebarRoleLabel.Text = roleSub;

        ProfileSubtitleLabel.Text = _role switch
        {
            UserRole.Admin => "Platform identity, system health, and moderation overview.",
            UserRole.Doctor when _isLabTechnician => "Lab identity, verification, and recent specimen activity.",
            UserRole.Doctor => "Clinician identity, schedule, and consultation history.",
            _ => "Account overview, identity, and clinical activity."
        };

        HeroBioLabel.Text = _role switch
        {
            UserRole.Admin => "Administrative access — manages users, doctor approvals and platform content.",
            UserRole.Doctor when _isLabTechnician => "Clinical lab specialist — uploads and verifies laboratory results.",
            UserRole.Doctor => "Verified clinician — receives bookings and reviews patient analyses.",
            _ => "Patient on the Neuralis platform — books visits and reviews AI-assisted analyses."
        };

        BackToDashboardLabel.Text = _role switch
        {
            UserRole.Admin => "Admin dashboard",
            UserRole.Doctor when _isLabTechnician => "Lab dashboard",
            UserRole.Doctor => "Doctor dashboard",
            _ => "Patient dashboard"
        };

        SidebarSubtitleLabel.Text = _role switch
        {
            UserRole.Admin => "ADMIN PROFILE",
            UserRole.Doctor when _isLabTechnician => "LAB PROFILE",
            UserRole.Doctor => "DOCTOR PROFILE",
            _ => "PATIENT PROFILE"
        };
    }

    private static string ResolveInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "N";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "N";
        if (parts.Length == 1)
            return parts[0].Length == 1 ? parts[0].ToUpperInvariant() : parts[0].Substring(0, 2).ToUpperInvariant();
        return ($"{parts[0][0]}{parts[1][0]}").ToUpperInvariant();
    }

    // =====================================================
    // Info section (role-specific fields)
    // =====================================================

    private void BuildInfoSection(MeResponse? user)
    {
        InfoGrid.RowDefinitions.Clear();
        InfoGrid.Children.Clear();

        var pairs = new List<(string Label, string Value)>
        {
            ("EMAIL", string.IsNullOrWhiteSpace(user?.Email) ? "—" : user!.Email!),
            ("FULL NAME", string.IsNullOrWhiteSpace(user?.FullName) ? "Not provided" : user!.FullName!),
        };

        var lang = ResolveLanguageDisplay(_vm.LanguageCode);

        switch (_role)
        {
            case UserRole.User:
                pairs.Add(("PHONE", string.IsNullOrWhiteSpace(_vm.Phone) ? "Not provided" : _vm.Phone));
                pairs.Add(("DATE OF BIRTH", string.IsNullOrWhiteSpace(_vm.DateOfBirth) ? "Not provided" : _vm.DateOfBirth));
                pairs.Add(("EMERGENCY CONTACT", string.IsNullOrWhiteSpace(_vm.EmergencyContact) ? "Not provided" : _vm.EmergencyContact));
                pairs.Add(("PREFERRED LANGUAGE", lang));
                break;
            case UserRole.Doctor when _isLabTechnician:
                pairs.Add(("SPECIALIZATION", "Lab Technician"));
                pairs.Add(("FACILITY",
                    string.IsNullOrWhiteSpace(_vm.DoctorClinic) ? "Neuralis Clinical Lab" : _vm.DoctorClinic));
                pairs.Add(("VERIFICATION", _isVerified ? "Approved" : "Pending"));
                pairs.Add(("STAFF ID", $"LAB-{(user?.Id ?? Guid.Empty).ToString("N").Substring(0, 6).ToUpperInvariant()}"));
                pairs.Add(("PHONE", string.IsNullOrWhiteSpace(_vm.Phone) ? "Not provided" : _vm.Phone));
                pairs.Add(("PREFERRED LANGUAGE", lang));
                break;
            case UserRole.Doctor:
                pairs.Add(("SPECIALIZATION",
                    string.IsNullOrWhiteSpace(_specialization)
                        ? "Not assigned"
                        : DoctorSpecializationUi.Format(_specialization)));
                pairs.Add(("CLINIC",
                    string.IsNullOrWhiteSpace(_vm.DoctorClinic) ? "Neuralis Medical Center" : _vm.DoctorClinic));
                pairs.Add(("VERIFICATION", _isVerified ? "Approved" : "Pending review"));
                pairs.Add(("LICENSE", "On file (not displayed)"));
                pairs.Add(("PHONE", string.IsNullOrWhiteSpace(_vm.Phone) ? "Not provided" : _vm.Phone));
                pairs.Add(("PREFERRED LANGUAGE", lang));
                break;
            case UserRole.Admin:
                pairs.Add(("ROLE", "System Administrator"));
                pairs.Add(("ACCESS LEVEL", "Full platform"));
                pairs.Add(("REGION", "Global"));
                pairs.Add(("MFA", "Not configured"));
                pairs.Add(("PHONE", string.IsNullOrWhiteSpace(_vm.Phone) ? "Not provided" : _vm.Phone));
                pairs.Add(("PREFERRED LANGUAGE", lang));
                break;
        }

        for (var i = 0; i < pairs.Count; i++)
        {
            var col = i % 2;
            var row = i / 2;
            if (row >= InfoGrid.RowDefinitions.Count)
                InfoGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var cell = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label
                    {
                        Text = pairs[i].Label,
                        Style = (Style?)Resources["InfoLabelKey"]
                    },
                    new Label
                    {
                        Text = pairs[i].Value,
                        Style = (Style?)Resources["InfoValueKey"]
                    }
                }
            };
            Grid.SetColumn(cell, col);
            Grid.SetRow(cell, row);
            InfoGrid.Children.Add(cell);
        }
    }

    // =====================================================
    // Stats tiles (role-specific quick KPIs)
    // =====================================================

    private void BuildStatsTiles()
    {
        StatsGrid.Children.Clear();
        StatsGrid.ColumnDefinitions.Clear();

        var tiles = _role switch
        {
            UserRole.Admin => new (string, string, string, string)[]
            {
                ("Total users", "—", "people", "Pending live integration"),
                ("Active doctors", "—", "verified_user", "Pending live integration"),
                ("AI service", "Online", "psychology", "Heuristic + GPT layer"),
                ("Platform uptime", "Stable", "monitor_heart", "Last 30 days")
            },
            UserRole.Doctor when _isLabTechnician => new (string, string, string, string)[]
            {
                ("Verification", _isVerified ? "Approved" : "Pending", "verified", "Lab credential"),
                ("Specimens today", "0", "science", "Live count pending"),
                ("Uploaded reports", "—", "upload_file", "Last 7 days"),
                ("Availability", "Active", "schedule", "Toggle in dashboard")
            },
            UserRole.Doctor => new (string, string, string, string)[]
            {
                ("Verification", _isVerified ? "Approved" : "Pending", "verified", "Clinician status"),
                ("Appointments today", "—", "event_available", "Loaded from schedule"),
                ("Patient panel", "—", "people", "Live integration pending"),
                ("Availability", "Online", "schedule", "Toggle from dashboard")
            },
            _ => new (string, string, string, string)[]
            {
                ("Upcoming visits", "—", "event", "Booked appointments"),
                ("AI analyses", "—", "psychology", "Session history (local)"),
                ("Risk reviews", "—", "monitor_heart", "Last 30 days"),
                ("Saved guidance", "—", "bookmark", "Pinned recommendations")
            }
        };

        for (var i = 0; i < tiles.Length; i++)
            StatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        for (var i = 0; i < tiles.Length; i++)
        {
            var (title, value, icon, sub) = tiles[i];
            var tile = BuildStatTile(title, value, icon, sub);
            Grid.SetColumn(tile, i);
            StatsGrid.Children.Add(tile);
        }
    }

    private Border BuildStatTile(string title, string value, string materialIcon, string sub)
    {
        return new Border
        {
            Style = (Style?)Resources["StatTileStyle"],
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new HorizontalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            new Label
                            {
                                Text = materialIcon,
                                FontFamily = "MaterialIcons",
                                FontSize = 18,
                                TextColor = ResolveColor("Accent", "#2FE3C9"),
                                VerticalOptions = LayoutOptions.Center
                            },
                            new Label
                            {
                                Text = title.ToUpperInvariant(),
                                FontSize = 10,
                                FontAttributes = FontAttributes.Bold,
                                CharacterSpacing = 1,
                                TextColor = ResolveColor("Muted", "#6E8C96"),
                                VerticalOptions = LayoutOptions.Center
                            }
                        }
                    },
                    new Label
                    {
                        Text = value,
                        FontSize = 22,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = ResolveColor("TextPrimary", "#E6F7FA")
                    },
                    new Label
                    {
                        Text = sub,
                        FontSize = 11,
                        TextColor = ResolveColor("Muted", "#6E8C96"),
                        LineBreakMode = LineBreakMode.WordWrap
                    }
                }
            }
        };
    }

    // =====================================================
    // Status card (role-specific)
    // =====================================================

    private void BuildStatusCard()
    {
        StatusCardBody.Children.Clear();

        switch (_role)
        {
            case UserRole.Admin:
                StatusCardTitle.Text = "System overview";
                StatusCardBody.Children.Add(BuildStatusRow("dns", "API service", "Operational", "#4ADE80"));
                StatusCardBody.Children.Add(BuildStatusRow("psychology", "AI engine", "Online", "#4ADE80"));
                StatusCardBody.Children.Add(BuildStatusRow("storage", "Database", "Connected", "#4ADE80"));
                StatusCardBody.Children.Add(BuildStatusRow("gavel", "Moderation tools", "Available", null));
                break;

            case UserRole.Doctor when _isLabTechnician:
                StatusCardTitle.Text = "Lab status";
                StatusCardBody.Children.Add(BuildStatusRow(
                    "verified", "Verification",
                    _isVerified ? "Approved" : "Pending review",
                    _isVerified ? "#4ADE80" : "#FACC15"));
                StatusCardBody.Children.Add(BuildStatusRow("biotech", "Workstation", "Active", "#4ADE80"));
                StatusCardBody.Children.Add(BuildAvailabilityRow());
                break;

            case UserRole.Doctor:
                StatusCardTitle.Text = "Clinician status";
                StatusCardBody.Children.Add(BuildStatusRow(
                    "verified", "Verification",
                    _isVerified ? "Approved" : "Pending review",
                    _isVerified ? "#4ADE80" : "#FACC15"));
                StatusCardBody.Children.Add(BuildAvailabilityRow());
                StatusCardBody.Children.Add(BuildStatusRow("schedule", "Schedule", "Synced", null));
                break;

            default:
                StatusCardTitle.Text = "Care status";
                StatusCardBody.Children.Add(BuildStatusRow("favorite", "Wellness check", "Healthy", "#4ADE80"));
                StatusCardBody.Children.Add(BuildStatusRow("medical_information", "Records", "Linked", null));
                StatusCardBody.Children.Add(BuildStatusRow("priority_high", "Alerts", "None", null));
                break;
        }
    }

    private View BuildAvailabilityRow()
    {
        var availSwitch = new Switch
        {
            IsToggled = _vm.IsAvailable,
            OnColor = ResolveColor("Accent", "#2FE3C9"),
            VerticalOptions = LayoutOptions.Center
        };
        availSwitch.Toggled += (_, args) => _vm.IsAvailable = args.Value;

        return new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            Children =
            {
                new Label
                {
                    Text = "event_available",
                    FontFamily = "MaterialIcons",
                    FontSize = 18,
                    TextColor = ResolveColor("Accent", "#2FE3C9"),
                    VerticalOptions = LayoutOptions.Center
                },
                new VerticalStackLayout
                {
                    Spacing = 2,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label
                        {
                            Text = "Accepting bookings",
                            FontSize = 13,
                            TextColor = ResolveColor("TextPrimary", "#E6F7FA")
                        },
                        new Label
                        {
                            Text = "Toggle availability for new appointments.",
                            FontSize = 11,
                            TextColor = ResolveColor("Muted", "#6E8C96"),
                            LineBreakMode = LineBreakMode.WordWrap
                        }
                    }
                }.Set(Grid.ColumnProperty, 1),
                availSwitch.Set(Grid.ColumnProperty, 2)
            }
        };
    }

    private void UpdateLanguageLabel()
    {
        LanguageCurrentLabel.Text = ResolveLanguageDisplay(_vm.LanguageCode) + " (in-app)";
    }

    private void UpdateHeroBio()
    {
        if (!string.IsNullOrWhiteSpace(_vm.Bio))
        {
            HeroBioLabel.Text = _vm.Bio;
            return;
        }
        HeroBioLabel.Text = _role switch
        {
            UserRole.Admin => "Administrative access — manages users, doctor approvals and platform content.",
            UserRole.Doctor when _isLabTechnician => "Clinical lab specialist — uploads and verifies laboratory results.",
            UserRole.Doctor => "Verified clinician — receives bookings and reviews patient analyses.",
            _ => "Patient on the Neuralis platform — books visits and reviews AI-assisted analyses."
        };
    }

    private static string ResolveLanguageDisplay(string code) => code?.ToLowerInvariant() switch
    {
        "ru" => "Russian",
        "kz" => "Kazakh",
        _ => "English"
    };

    private View BuildStatusRow(string icon, string label, string status, string? statusHex)
    {
        var statusColor = string.IsNullOrWhiteSpace(statusHex)
            ? ResolveColor("TextPrimary", "#E6F7FA")
            : Color.FromArgb(statusHex);

        return new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            Children =
            {
                new Label
                {
                    Text = icon,
                    FontFamily = "MaterialIcons",
                    FontSize = 18,
                    TextColor = ResolveColor("Accent", "#2FE3C9"),
                    VerticalOptions = LayoutOptions.Center
                },
                new Label
                {
                    Text = label,
                    FontSize = 13,
                    TextColor = ResolveColor("TextPrimary", "#E6F7FA"),
                    VerticalOptions = LayoutOptions.Center
                }.Set(Grid.ColumnProperty, 1),
                new Label
                {
                    Text = status,
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = statusColor,
                    VerticalOptions = LayoutOptions.Center
                }.Set(Grid.ColumnProperty, 2),
            }
        };
    }

    // =====================================================
    // Timeline / role activity loading
    // =====================================================

    private async Task LoadRoleActivityAsync()
    {
        TimelineStack.Children.Clear();
        TimelineEmptyLabel.IsVisible = false;
        TimelineDescriptionLabel.Text = "";

        switch (_role)
        {
            case UserRole.User:
                TimelineSectionTitle.Text = "Upcoming visits";
                TimelineDescriptionLabel.Text = "Appointments synced from your bookings.";
                await LoadPatientAppointmentsAsync();
                break;

            case UserRole.Doctor when _isLabTechnician:
                TimelineSectionTitle.Text = "Recent lab activity";
                TimelineDescriptionLabel.Text = "Latest specimen uploads will appear here.";
                ShowTimelinePlaceholder("Specimen log will sync from the lab dashboard.");
                break;

            case UserRole.Doctor:
                TimelineSectionTitle.Text = "Schedule overview";
                TimelineDescriptionLabel.Text = "Upcoming appointments from your clinic queue.";
                await LoadDoctorAppointmentsAsync();
                break;

            case UserRole.Admin:
                TimelineSectionTitle.Text = "Moderation queue";
                TimelineDescriptionLabel.Text = "Live moderation events will appear here once wired.";
                ShowTimelinePlaceholder("Moderation tools are available from the Admin dashboard.");
                break;
        }
    }

    private async Task LoadPatientAppointmentsAsync()
    {
        var token = AuthService.CurrentToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            ShowTimelinePlaceholder("Sign in to view your upcoming visits.");
            return;
        }

        TimelineLoading.IsVisible = true;
        TimelineLoading.IsRunning = true;
        try
        {
            var svc = new AppointmentService(token);
            var list = await svc.GetMyAppointmentsAsync();
            var upcoming = list
                .Where(a => a.ScheduledAtUtc >= DateTime.UtcNow.AddMinutes(-15)
                            && !string.Equals(a.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.ScheduledAtUtc)
                .Take(4)
                .ToList();

            if (upcoming.Count == 0)
            {
                ShowTimelinePlaceholder("No upcoming visits booked.");
                return;
            }

            foreach (var item in upcoming)
                TimelineStack.Children.Add(BuildPatientAppointmentRow(item));
        }
        catch (Exception)
        {
            ShowTimelinePlaceholder("Unable to load upcoming visits at this time.");
        }
        finally
        {
            TimelineLoading.IsVisible = false;
            TimelineLoading.IsRunning = false;
        }
    }

    private async Task LoadDoctorAppointmentsAsync()
    {
        var token = AuthService.CurrentToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            ShowTimelinePlaceholder("Sign in to view your schedule.");
            return;
        }

        TimelineLoading.IsVisible = true;
        TimelineLoading.IsRunning = true;
        try
        {
            var svc = new AppointmentService(token);
            var list = await svc.GetAppointmentsAsync();
            var upcoming = list
                .Where(a => a.ScheduledAtUtc >= DateTime.UtcNow.AddMinutes(-30))
                .OrderBy(a => a.ScheduledAtUtc)
                .Take(4)
                .ToList();

            if (upcoming.Count == 0)
            {
                ShowTimelinePlaceholder("No upcoming appointments in your queue.");
                return;
            }

            foreach (var item in upcoming)
                TimelineStack.Children.Add(BuildDoctorAppointmentRow(item));
        }
        catch (Exception)
        {
            ShowTimelinePlaceholder("Unable to load your schedule at this time.");
        }
        finally
        {
            TimelineLoading.IsVisible = false;
            TimelineLoading.IsRunning = false;
        }
    }

    private View BuildPatientAppointmentRow(MyAppointmentDto item)
    {
        var local = item.ScheduledAtUtc.ToLocalTime();
        var doctor = string.IsNullOrWhiteSpace(item.DoctorFullName)
            ? (item.DoctorEmail ?? "Doctor")
            : item.DoctorFullName!;
        var spec = string.IsNullOrWhiteSpace(item.DoctorSpecialization)
            ? "Clinician"
            : DoctorSpecializationUi.Format(item.DoctorSpecialization);

        return BuildTimelineRow(
            "event",
            $"{doctor} · {spec}",
            $"{local:ddd, MMM d} · {local:HH:mm}",
            item.Status ?? "Scheduled");
    }

    private View BuildDoctorAppointmentRow(DoctorAppointmentDto item)
    {
        var local = item.ScheduledAtUtc.ToLocalTime();
        var patient = string.IsNullOrWhiteSpace(item.PatientFullName)
            ? "Patient"
            : item.PatientFullName!;
        return BuildTimelineRow(
            "person",
            patient,
            $"{local:ddd, MMM d} · {local:HH:mm}",
            item.Status ?? "Scheduled");
    }

    private Border BuildTimelineRow(string icon, string title, string subtitle, string status)
    {
        var statusColor = status?.ToLowerInvariant() switch
        {
            "cancelled" => Color.FromArgb("#F87171"),
            "completed" => Color.FromArgb("#4ADE80"),
            _ => ResolveColor("Accent", "#2FE3C9")
        };

        return new Border
        {
            BackgroundColor = Color.FromArgb("#081820"),
            Stroke = Color.FromArgb("#163B41"),
            StrokeThickness = 1,
            Padding = new Thickness(14),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 12,
                Children =
                {
                    new Label
                    {
                        Text = icon,
                        FontFamily = "MaterialIcons",
                        FontSize = 20,
                        TextColor = ResolveColor("Accent", "#2FE3C9"),
                        VerticalOptions = LayoutOptions.Center
                    },
                    new VerticalStackLayout
                    {
                        Spacing = 2,
                        Children =
                        {
                            new Label
                            {
                                Text = title,
                                FontAttributes = FontAttributes.Bold,
                                FontSize = 13,
                                TextColor = ResolveColor("TextPrimary", "#E6F7FA"),
                                LineBreakMode = LineBreakMode.TailTruncation,
                                MaxLines = 1
                            },
                            new Label
                            {
                                Text = subtitle,
                                FontSize = 11,
                                TextColor = ResolveColor("Muted", "#6E8C96"),
                                LineBreakMode = LineBreakMode.TailTruncation,
                                MaxLines = 1
                            }
                        }
                    }.Set(Grid.ColumnProperty, 1),
                    new Border
                    {
                        Padding = new Thickness(8, 3),
                        BackgroundColor = Color.FromArgb("#0D2B31"),
                        Stroke = statusColor,
                        StrokeThickness = 1,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                        Content = new Label
                        {
                            Text = (status ?? "—").ToUpperInvariant(),
                            FontSize = 9,
                            FontAttributes = FontAttributes.Bold,
                            CharacterSpacing = 1,
                            TextColor = statusColor
                        }
                    }.Set(Grid.ColumnProperty, 2)
                }
            }
        };
    }

    private void ShowTimelinePlaceholder(string message)
    {
        TimelineStack.Children.Clear();
        TimelineEmptyLabel.Text = message;
        TimelineEmptyLabel.IsVisible = true;
    }

    // =====================================================
    // Action handlers
    // =====================================================

    private async void OnBackToDashboardTapped(object? sender, EventArgs e)
    {
        switch (_role)
        {
            case UserRole.Admin:
                await Shell.Current.GoToAsync("//AdminDashboard");
                break;
            case UserRole.Doctor when _isLabTechnician:
                await Shell.Current.GoToAsync("//LabDashboard");
                break;
            case UserRole.Doctor:
                await Shell.Current.GoToAsync(_isVerified ? "//DoctorDashboard" : "//DoctorPending");
                break;
            default:
                await Shell.Current.GoToAsync("//Main");
                break;
        }
    }

    private void OnEditProfileTapped(object? sender, EventArgs e) =>
        ShowEditProfileModal();

    private void OnChangePasswordTapped(object? sender, EventArgs e) =>
        ShowChangePasswordModal();

    private void OnNotificationsTapped(object? sender, EventArgs e) =>
        ShowNotificationsModal();

    private void OnPrivacyTapped(object? sender, EventArgs e) =>
        ShowPrivacyModal();

    private void OnLanguageTapped(object? sender, EventArgs e) =>
        ShowLanguageModal();

    private async void OnLogoutTapped(object? sender, EventArgs e)
    {
        if (_modalOpen)
            await CloseModalAsync();

        var confirm = await DisplayAlert(
            "Sign out",
            "You will be signed out of Neuralis on this device.",
            "Sign out",
            "Cancel");
        if (!confirm)
            return;

        AuthService.Logout();
        await App.LogoutAsync();
    }

    // =====================================================
    // Modal shell (open / close / footer wiring)
    // =====================================================

    private async Task OpenModalAsync(
        string title,
        string subtitle,
        View body,
        string primaryText,
        Func<Task> primaryHandler,
        string secondaryText = "Cancel",
        Func<Task>? secondaryHandler = null,
        bool hidePrimary = false)
    {
        ModalTitleLabel.Text = title;
        ModalSubtitleLabel.Text = subtitle;
        ModalBodyHost.Children.Clear();
        ModalBodyHost.Children.Add(body);

        _modalPrimaryHandler = primaryHandler;
        _modalSecondaryHandler = secondaryHandler ?? (() => CloseModalAsync());

        ModalPrimaryAction.Text = primaryText;
        ModalPrimaryAction.IsVisible = !hidePrimary;
        ModalPrimaryAction.IsEnabled = true;
        ModalSecondaryAction.Text = secondaryText;
        ModalSecondaryAction.IsVisible = true;
        ModalSecondaryAction.IsEnabled = true;

        ModalFooterStatus.Text = "";
        ModalFooterStatus.TextColor = ResolveColor("Muted", "#6E8C96");
        ModalLoadingIndicator.IsVisible = false;
        ModalLoadingIndicator.IsRunning = false;

        ApplyModalSizing();

        ModalCard.Scale = 0.96;
        ModalCard.Opacity = 0;
        ModalOverlay.Opacity = 0;
        ModalOverlay.IsVisible = true;
        _modalOpen = true;

        await Task.WhenAll(
            ModalOverlay.FadeTo(1, 160, Easing.CubicOut),
            ModalCard.FadeTo(1, 180, Easing.CubicOut),
            ModalCard.ScaleTo(1, 200, Easing.CubicOut));
    }

    private async Task CloseModalAsync()
    {
        if (!_modalOpen)
            return;

        await Task.WhenAll(
            ModalOverlay.FadeTo(0, 140, Easing.CubicIn),
            ModalCard.FadeTo(0, 130, Easing.CubicIn),
            ModalCard.ScaleTo(0.96, 140, Easing.CubicIn));

        ModalBodyHost.Children.Clear();
        ModalOverlay.IsVisible = false;
        _modalOpen = false;
        _modalPrimaryHandler = null;
        _modalSecondaryHandler = null;
    }

    private async void OnModalCloseTapped(object? sender, EventArgs e) => await CloseModalAsync();

    private async void OnModalBackdropTapped(object? sender, EventArgs e) => await CloseModalAsync();

    private async void OnModalPrimaryActionClicked(object? sender, EventArgs e)
    {
        if (_modalPrimaryHandler == null)
            return;
        await _modalPrimaryHandler();
    }

    private async void OnModalSecondaryActionClicked(object? sender, EventArgs e)
    {
        if (_modalSecondaryHandler == null)
        {
            await CloseModalAsync();
            return;
        }
        await _modalSecondaryHandler();
    }

    /// <summary>
    /// Size the modal card responsively so it never exceeds the viewport with margins.
    /// </summary>
    private void ApplyModalSizing()
    {
        var w = Width;
        var h = Height;
        if (w <= 0 || h <= 0)
            return;

        // Width: ~560px on wide screens, scales down with viewport
        var targetWidth = Math.Min(640, Math.Max(320, w * 0.7));
        if (w < 720)
            targetWidth = Math.Max(300, w - 48);

        ModalCard.WidthRequest = targetWidth;
        ModalCard.MaximumWidthRequest = targetWidth;
        ModalCard.MaximumHeightRequest = Math.Max(420, h - 80);
    }

    // =====================================================
    // Modal: Edit profile
    // =====================================================

    private async void ShowEditProfileModal()
    {
        // ----- Identity section: read-only name, editable email -----
        var nameValue = string.IsNullOrWhiteSpace(_vm.DisplayName) ? "—" : _vm.DisplayName;
        var nameLocked = BuildReadOnlyField(
            "Full name",
            nameValue,
            "Display name changes require administrator approval.");

        var emailEntry = BuildInputEntry(
            text: !string.IsNullOrWhiteSpace(_vm.PendingEmail) ? _vm.PendingEmail : _vm.Email,
            placeholder: "you@neuralis.ai",
            keyboard: Keyboard.Email);
        var (emailWrap, _) = WrapInput(emailEntry);
        var emailValidation = BuildValidationLabel();
        var emailHelper = BuildHelperLabel(
            _vm.HasPendingEmailChange
                ? $"Pending administrator review · current address {_vm.Email}"
                : "Email changes are validated locally and queued for administrator sync.");
        var emailField = BuildField("Email", emailWrap, helper: emailHelper, validation: emailValidation);

        emailEntry.TextChanged += (_, _) =>
        {
            ClearValidation(emailWrap, emailValidation);
        };

        var identitySection = BuildSection(
            "Identity",
            "Core account information.",
            nameLocked,
            emailField);

        // ----- Contact section -----
        var phoneEntry = BuildInputEntry(_vm.Phone, "+1 555 000 0000", keyboard: Keyboard.Telephone);
        var (phoneWrap, _) = WrapInput(phoneEntry);
        var phoneValidation = BuildValidationLabel();
        phoneEntry.TextChanged += (_, _) => ClearValidation(phoneWrap, phoneValidation);

        var dobPicker = new DatePicker
        {
            Date = DateTime.TryParseExact(_vm.DateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob)
                ? dob
                : DateTime.Today.AddYears(-25),
            TextColor = ResolveColor("TextPrimary", "#E6F7FA"),
            BackgroundColor = Colors.Transparent,
            MinimumHeightRequest = 40,
            VerticalOptions = LayoutOptions.Center
        };
        var (dobWrap, _) = WrapInput(dobPicker);

        var genderPicker = new Picker
        {
            Title = "Select",
            TextColor = ResolveColor("TextPrimary", "#E6F7FA"),
            BackgroundColor = Colors.Transparent,
            TitleColor = ResolveColor("Muted", "#6E8C96"),
            MinimumHeightRequest = 40,
            VerticalOptions = LayoutOptions.Center
        };
        var genders = new[] { "Prefer not to say", "Male", "Female", "Other" };
        foreach (var g in genders)
            genderPicker.Items.Add(g);
        var currentGender = string.IsNullOrWhiteSpace(_vm.Gender) ? "Prefer not to say" : _vm.Gender;
        genderPicker.SelectedIndex = Array.IndexOf(genders, currentGender);
        var (genderWrap, _) = WrapInput(genderPicker);

        var emergencyEntry = BuildInputEntry(_vm.EmergencyContact, "Name and phone");
        var (emergencyWrap, _) = WrapInput(emergencyEntry);

        var contactSection = BuildSection(
            "Contact details",
            "Used by the care team when reaching out.",
            BuildField("Phone", phoneWrap, validation: phoneValidation),
            BuildTwoColumnFields(
                "Date of birth", dobWrap,
                "Gender", genderWrap),
            BuildField("Emergency contact", emergencyWrap));

        // ----- About section -----
        var bioEditor = new Editor
        {
            Text = _vm.Bio,
            Placeholder = "Short note (optional)",
            PlaceholderColor = ResolveColor("Muted", "#6E8C96"),
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 88,
            BackgroundColor = Colors.Transparent,
            TextColor = ResolveColor("TextPrimary", "#E6F7FA")
        };
        var bioWrap = new Border
        {
            Style = (Style?)Resources["ModalInputBorderStyle"],
            Padding = new Thickness(10, 6),
            MinimumHeightRequest = 96,
            Content = bioEditor
        };
        var aboutSection = BuildSection(
            "About",
            "Profile bio shown on your account card.",
            BuildField("Bio / status", bioWrap));

        // ----- Doctor section (optional) -----
        Switch? availabilitySwitch = null;
        Entry? clinicEntry = null;
        Border? clinicWrap = null;
        Border? doctorSectionBorder = null;
        if (_vm.Role == UserRole.Doctor)
        {
            clinicEntry = BuildInputEntry(_vm.DoctorClinic, "Clinic or facility");
            var clinicWrapTuple = WrapInput(clinicEntry);
            clinicWrap = clinicWrapTuple.Wrap;
            availabilitySwitch = BuildToggle(_vm.IsAvailable, (_, args) => _vm.IsAvailable = args.Value);
            doctorSectionBorder = BuildSection(
                _isLabTechnician ? "Lab settings" : "Clinic settings",
                "Visible to patients booking appointments.",
                BuildField("Facility", clinicWrap),
                BuildToggleRow("Accepting bookings", "Allow new appointments from patients.", availabilitySwitch));
        }

        // ----- Assemble body -----
        var body = new VerticalStackLayout { Spacing = 16 };
        body.Children.Add(identitySection);
        body.Children.Add(contactSection);
        body.Children.Add(aboutSection);
        if (doctorSectionBorder != null)
            body.Children.Add(doctorSectionBorder);

        await OpenModalAsync(
            title: "Edit profile",
            subtitle: "Update your identity, contact and availability.",
            body: body,
            primaryText: "Save changes",
            primaryHandler: async () =>
            {
                var newEmail = emailEntry.Text?.Trim() ?? "";
                var phone = phoneEntry.Text?.Trim() ?? "";
                var valid = true;

                if (string.IsNullOrWhiteSpace(newEmail) || !EmailRegex.IsMatch(newEmail))
                {
                    ShowValidation(emailWrap, emailValidation, "Enter a valid email address.");
                    valid = false;
                }
                if (phone.Length > 0 && phone.Length < 5)
                {
                    ShowValidation(phoneWrap, phoneValidation, "Phone number looks too short.");
                    valid = false;
                }
                if (!valid)
                {
                    SetFooterStatus("Fix the highlighted fields to continue.", isError: true);
                    return;
                }

                await RunPrimaryAction(async () =>
                {
                    await Task.Delay(160);
                    var emailChanged = !string.Equals(newEmail, _vm.Email, StringComparison.OrdinalIgnoreCase);
                    _vm.ApplyEdit(new ProfilePreferences
                    {
                        Phone = phone,
                        DateOfBirth = dobPicker.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        Gender = (genderPicker.SelectedItem?.ToString() == "Prefer not to say")
                            ? ""
                            : (genderPicker.SelectedItem?.ToString() ?? ""),
                        EmergencyContact = emergencyEntry.Text?.Trim() ?? "",
                        Bio = bioEditor.Text?.Trim() ?? "",
                        DoctorClinic = clinicEntry?.Text?.Trim() ?? _vm.DoctorClinic,
                        IsAvailable = availabilitySwitch?.IsToggled ?? _vm.IsAvailable,
                        PendingEmail = emailChanged ? newEmail : ""
                    });
                    RefreshProfileSurface();
                    SetFooterStatus("Profile saved.");
                    await Task.Delay(450);
                    await CloseModalAsync();
                });
            });
    }

    // =====================================================
    // Modal: Change password
    // =====================================================

    private async void ShowChangePasswordModal()
    {
        var currentEntry = BuildInputEntry("", "Current password", password: true);
        var (currentWrap, _) = WrapInput(currentEntry);
        var currentValidation = BuildValidationLabel();
        currentEntry.TextChanged += (_, _) => ClearValidation(currentWrap, currentValidation);

        var newEntry = BuildInputEntry("", "New password (min 8 chars)", password: true);
        var (newWrap, _) = WrapInput(newEntry);
        var newValidation = BuildValidationLabel();
        var strengthLabel = new Label
        {
            FontSize = 11,
            TextColor = ResolveColor("Muted", "#6E8C96"),
            Text = ""
        };

        var confirmEntry = BuildInputEntry("", "Confirm new password", password: true);
        var (confirmWrap, _) = WrapInput(confirmEntry);
        var confirmValidation = BuildValidationLabel();

        newEntry.TextChanged += (_, args) =>
        {
            ClearValidation(newWrap, newValidation);
            ClearValidation(confirmWrap, confirmValidation);
            var pw = args.NewTextValue ?? "";
            if (string.IsNullOrEmpty(pw))
            {
                strengthLabel.Text = "";
                return;
            }
            var (label, color) = EvaluateStrength(pw);
            strengthLabel.Text = $"Strength · {label}";
            strengthLabel.TextColor = color;
        };
        confirmEntry.TextChanged += (_, _) => ClearValidation(confirmWrap, confirmValidation);

        var body = new VerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                BuildSection(
                    "Authentication",
                    "Validate your current password to set a new one.",
                    BuildField("Current password", currentWrap, validation: currentValidation),
                    BuildField("New password", newWrap, validation: newValidation),
                    new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children = { strengthLabel }
                    },
                    BuildField("Confirm new password", confirmWrap, validation: confirmValidation))
            }
        };

        await OpenModalAsync(
            title: "Change password",
            subtitle: "Update your sign-in credential.",
            body: body,
            primaryText: "Update password",
            primaryHandler: async () =>
            {
                var currentText = currentEntry.Text ?? "";
                var nextText = newEntry.Text ?? "";
                var confirmText = confirmEntry.Text ?? "";
                var valid = true;

                if (string.IsNullOrWhiteSpace(currentText))
                {
                    ShowValidation(currentWrap, currentValidation, "Enter your current password.");
                    valid = false;
                }
                if (nextText.Length < 8)
                {
                    ShowValidation(newWrap, newValidation, "Use at least 8 characters.");
                    valid = false;
                }
                else if (!nextText.Any(char.IsDigit) || !nextText.Any(char.IsLetter))
                {
                    ShowValidation(newWrap, newValidation, "Mix letters and numbers.");
                    valid = false;
                }
                else if (string.Equals(nextText, currentText, StringComparison.Ordinal))
                {
                    ShowValidation(newWrap, newValidation, "New password must differ from the current one.");
                    valid = false;
                }
                if (!string.Equals(nextText, confirmText, StringComparison.Ordinal))
                {
                    ShowValidation(confirmWrap, confirmValidation, "Passwords do not match.");
                    valid = false;
                }
                if (!valid)
                {
                    SetFooterStatus("Fix the highlighted fields to continue.", isError: true);
                    return;
                }

                await RunPrimaryAction(async () =>
                {
                    await Task.Delay(280);
                    SetFooterStatus("Password validated locally.");
                    await Task.Delay(700);
                    await CloseModalAsync();
                });
            });
    }

    // =====================================================
    // Modal: Notification preferences
    // =====================================================

    private async void ShowNotificationsModal()
    {
        var body = new VerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                BuildSection(
                    "Alerts",
                    "Choose what Neuralis can notify you about.",
                    BuildToggleRow("AI analysis alerts", "Updates when analyses finish processing.", BuildToggle(_vm.NotifyAiAnalysis, (_, e) => _vm.NotifyAiAnalysis = e.Value)),
                    BuildToggleRow("Appointment reminders", "Notifications for scheduled visits.", BuildToggle(_vm.NotifyAppointments, (_, e) => _vm.NotifyAppointments = e.Value)),
                    BuildToggleRow("Security alerts", "Sign-in and account activity warnings.", BuildToggle(_vm.NotifySecurity, (_, e) => _vm.NotifySecurity = e.Value)),
                    BuildToggleRow("System updates", "Product changes and release notices.", BuildToggle(_vm.NotifySystem, (_, e) => _vm.NotifySystem = e.Value)))
            }
        };

        await OpenModalAsync(
            title: "Notification preferences",
            subtitle: "Changes save automatically.",
            body: body,
            primaryText: "Done",
            primaryHandler: () => CloseModalAsync(),
            secondaryText: "Close");
    }

    // =====================================================
    // Modal: Privacy settings
    // =====================================================

    private async void ShowPrivacyModal()
    {
        var body = new VerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                BuildSection(
                    "Privacy",
                    "Control how Neuralis treats your activity and visibility.",
                    BuildToggleRow("Profile visibility", "Allow care-team profile discovery.", BuildToggle(_vm.ProfileVisible, (_, e) => _vm.ProfileVisible = e.Value)),
                    BuildToggleRow("Share analytics", "Share de-identified usage analytics.", BuildToggle(_vm.ShareAnalytics, (_, e) => _vm.ShareAnalytics = e.Value)),
                    BuildToggleRow("Save AI history", "Keep AI analysis history in local profile.", BuildToggle(_vm.SaveAiHistory, (_, e) => _vm.SaveAiHistory = e.Value)),
                    BuildToggleRow("Activity tracking", "Track activity timeline for continuity.", BuildToggle(_vm.ActivityTracking, (_, e) => _vm.ActivityTracking = e.Value)))
            }
        };

        await OpenModalAsync(
            title: "Privacy settings",
            subtitle: "Changes save automatically.",
            body: body,
            primaryText: "Done",
            primaryHandler: () => CloseModalAsync(),
            secondaryText: "Close");
    }

    // =====================================================
    // Modal: Language picker
    // =====================================================

    private async void ShowLanguageModal()
    {
        var options = new (string Label, string Code, string Sub)[]
        {
            ("English", "en", "Default platform language"),
            ("Russian", "ru", "Русский · in-app translations"),
            ("Kazakh", "kz", "Қазақ тілі · in-app translations")
        };

        var stack = new VerticalStackLayout { Spacing = 10 };
        var rows = new List<Border>();
        foreach (var opt in options)
        {
            var row = BuildLanguageOptionRow(opt.Label, opt.Code, opt.Sub, _vm.LanguageCode);
            rows.Add(row);
            row.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    _vm.LanguageCode = opt.Code;
                    foreach (var r in rows)
                        UpdateLanguageOptionState(r, _vm.LanguageCode);
                    UpdateLanguageLabel();
                    BuildInfoSection(App.CurrentUser);
                })
            });
            stack.Children.Add(row);
        }

        var body = new VerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                BuildSection(
                    "In-app language",
                    "Select the language used inside Neuralis. Authentication screens stay in English.",
                    stack)
            }
        };

        await OpenModalAsync(
            title: "Language",
            subtitle: "Choice persists for this account.",
            body: body,
            primaryText: "Done",
            primaryHandler: () => CloseModalAsync(),
            secondaryText: "Close");
    }

    private Border BuildLanguageOptionRow(string label, string code, string subtitle, string currentCode)
    {
        var check = new Label
        {
            Text = "check_circle",
            FontFamily = "MaterialIcons",
            FontSize = 22,
            TextColor = ResolveColor("Accent", "#2FE3C9"),
            VerticalOptions = LayoutOptions.Center,
            IsVisible = string.Equals(code, currentCode, StringComparison.OrdinalIgnoreCase)
        };

        var border = new Border
        {
            Style = (Style?)Resources["ModalToggleRowStyle"],
            AutomationId = code,
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 12,
                Children =
                {
                    new VerticalStackLayout
                    {
                        Spacing = 2,
                        Children =
                        {
                            new Label { Text = label, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = ResolveColor("TextPrimary", "#E6F7FA") },
                            new Label { Text = subtitle, FontSize = 11, TextColor = ResolveColor("Muted", "#6E8C96") }
                        }
                    },
                    check.Set(Grid.ColumnProperty, 1)
                }
            }
        };
        UpdateLanguageOptionState(border, currentCode);
        return border;
    }

    private void UpdateLanguageOptionState(Border row, string currentCode)
    {
        var selected = string.Equals(row.AutomationId, currentCode, StringComparison.OrdinalIgnoreCase);
        row.Stroke = selected
            ? ResolveColor("Accent", "#2FE3C9")
            : Color.FromArgb("#1A4A55");
        row.StrokeThickness = 1;
        row.BackgroundColor = selected
            ? Color.FromArgb("#0F3C44")
            : Color.FromArgb("#0E2E36");

        if (row.Content is Grid g)
        {
            foreach (var child in g.Children)
            {
                if (child is Label l && l.FontFamily == "MaterialIcons")
                    l.IsVisible = selected;
            }
        }
    }

    // =====================================================
    // Modal: helpers (status / loading / validation / footer)
    // =====================================================

    private async Task RunPrimaryAction(Func<Task> work)
    {
        ModalPrimaryAction.IsEnabled = false;
        ModalSecondaryAction.IsEnabled = false;
        ModalLoadingIndicator.IsVisible = true;
        ModalLoadingIndicator.IsRunning = true;
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            SetFooterStatus(ex.Message, isError: true);
        }
        finally
        {
            ModalLoadingIndicator.IsRunning = false;
            ModalLoadingIndicator.IsVisible = false;
            ModalPrimaryAction.IsEnabled = true;
            ModalSecondaryAction.IsEnabled = true;
        }
    }

    private void SetFooterStatus(string message, bool isError = false)
    {
        ModalFooterStatus.Text = message;
        ModalFooterStatus.TextColor = isError
            ? Color.FromArgb("#F87171")
            : ResolveColor("Accent", "#2FE3C9");
    }

    private Label BuildValidationLabel() => new()
    {
        Text = "",
        IsVisible = false,
        Style = (Style?)Resources["ModalValidationLabelStyle"]
    };

    private Label BuildHelperLabel(string text) => new()
    {
        Text = text,
        Style = (Style?)Resources["ModalFieldHelperStyle"]
    };

    private void ShowValidation(Border wrap, Label validation, string message)
    {
        validation.Text = message;
        validation.IsVisible = true;
        wrap.Stroke = Color.FromArgb("#F87171");
        wrap.StrokeThickness = 1;
    }

    private void ClearValidation(Border wrap, Label validation)
    {
        validation.IsVisible = false;
        wrap.Stroke = Color.FromArgb("#214955");
        wrap.StrokeThickness = 1;
    }

    private void RefreshProfileSurface()
    {
        BuildInfoSection(App.CurrentUser);
        BuildStatusCard();
        UpdateLanguageLabel();
        UpdateHeroBio();
    }

    // =====================================================
    // Modal: design-system primitives
    // =====================================================

    private Border BuildSection(string title, string subtitle, params View[] children)
    {
        var stack = new VerticalStackLayout { Spacing = 12 };
        stack.Children.Add(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = title, Style = (Style?)Resources["ModalSectionTitleStyle"] },
                new Label { Text = subtitle, Style = (Style?)Resources["ModalSectionDescStyle"] }
            }
        });
        stack.Children.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#15414B") });
        foreach (var child in children)
            stack.Children.Add(child);

        return new Border
        {
            Style = (Style?)Resources["ModalSectionStyle"],
            Content = stack
        };
    }

    private Entry BuildInputEntry(string text, string placeholder, bool password = false, Keyboard? keyboard = null)
    {
        return new Entry
        {
            Text = text,
            Placeholder = placeholder,
            IsPassword = password,
            Keyboard = keyboard ?? Keyboard.Default,
            BackgroundColor = Colors.Transparent,
            TextColor = ResolveColor("TextPrimary", "#E6F7FA"),
            PlaceholderColor = ResolveColor("Muted", "#6E8C96"),
            MinimumHeightRequest = 40,
            VerticalOptions = LayoutOptions.Center
        };
    }

    private (Border Wrap, View Field) WrapInput(View field)
    {
        var border = new Border
        {
            Style = (Style?)Resources["ModalInputBorderStyle"],
            Content = field
        };

        // Focus glow for Entry-like inputs that support Focused/Unfocused events.
        if (field is InputView iv)
        {
            iv.Focused += (_, _) =>
            {
                border.Stroke = ResolveColor("Accent", "#2FE3C9");
                border.StrokeThickness = 1;
                border.BackgroundColor = Color.FromArgb("#0F3640");
            };
            iv.Unfocused += (_, _) =>
            {
                border.Stroke = Color.FromArgb("#214955");
                border.StrokeThickness = 1;
                border.BackgroundColor = Color.FromArgb("#0E2E36");
            };
        }

        return (border, field);
    }

    private View BuildField(string title, View field, View? helper = null, Label? validation = null)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 6,
            IsVisible = field.IsVisible,
            Children =
            {
                new Label
                {
                    Text = title.ToUpperInvariant(),
                    Style = (Style?)Resources["ModalFieldLabelStyle"]
                },
                field
            }
        };
        if (helper != null)
            stack.Children.Add(helper);
        if (validation != null)
            stack.Children.Add(validation);
        return stack;
    }

    private View BuildTwoColumnFields(string title1, View field1, string title2, View field2)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 12
        };
        var left = BuildField(title1, field1);
        var right = BuildField(title2, field2);
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    private View BuildReadOnlyField(string label, string value, string? helper = null)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = label.ToUpperInvariant(),
                    Style = (Style?)Resources["ModalFieldLabelStyle"]
                },
                new Border
                {
                    Style = (Style?)Resources["ModalInputBorderStyle"],
                    BackgroundColor = Color.FromArgb("#0A2329"),
                    Stroke = Color.FromArgb("#143C44"),
                    Padding = new Thickness(12, 10),
                    Content = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        Children =
                        {
                            new Label
                            {
                                Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
                                Style = (Style?)Resources["ModalReadOnlyValueStyle"],
                                VerticalOptions = LayoutOptions.Center
                            },
                            new Label
                            {
                                Text = "lock",
                                FontFamily = "MaterialIcons",
                                FontSize = 16,
                                TextColor = ResolveColor("Muted", "#6E8C96"),
                                VerticalOptions = LayoutOptions.Center
                            }.Set(Grid.ColumnProperty, 1)
                        }
                    }
                }
            }
        };
        if (!string.IsNullOrWhiteSpace(helper))
            stack.Children.Add(new Label { Text = helper, Style = (Style?)Resources["ModalFieldHelperStyle"] });
        return stack;
    }

    private Switch BuildToggle(bool current, EventHandler<ToggledEventArgs> onToggled)
    {
        var toggle = new Switch
        {
            IsToggled = current,
            OnColor = ResolveColor("Accent", "#2FE3C9"),
            ThumbColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        };
        toggle.Toggled += onToggled;
        return toggle;
    }

    private Border BuildToggleRow(string title, string subtitle, Switch toggle)
    {
        return new Border
        {
            Style = (Style?)Resources["ModalToggleRowStyle"],
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 12,
                Children =
                {
                    new VerticalStackLayout
                    {
                        Spacing = 3,
                        VerticalOptions = LayoutOptions.Center,
                        Children =
                        {
                            new Label { Text = title, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = ResolveColor("TextPrimary", "#E6F7FA") },
                            new Label { Text = subtitle, FontSize = 11, TextColor = ResolveColor("Muted", "#6E8C96"), LineBreakMode = LineBreakMode.WordWrap }
                        }
                    },
                    toggle.Set(Grid.ColumnProperty, 1)
                }
            }
        };
    }

    private static (string Label, Color Color) EvaluateStrength(string pw)
    {
        var score = 0;
        if (pw.Length >= 8) score++;
        if (pw.Any(char.IsDigit)) score++;
        if (pw.Any(char.IsUpper) && pw.Any(char.IsLower)) score++;
        if (pw.Any(c => !char.IsLetterOrDigit(c))) score++;

        return score switch
        {
            <= 1 => ("Weak", Color.FromArgb("#F87171")),
            2 => ("Fair", Color.FromArgb("#FACC15")),
            3 => ("Good", Color.FromArgb("#4ADE80")),
            _ => ("Strong", Color.FromArgb("#4ADE80"))
        };
    }

    // =====================================================
    // Responsive
    // =====================================================

    private void ApplyResponsiveLayout()
    {
        var w = Width;
        if (w <= 0)
            return;

        var compact = w < CompactBreakpoint;
        var hideSidebar = w < NarrowSidebarBreakpoint;

        ShellGrid.ColumnDefinitions.Clear();
        if (hideSidebar)
        {
            ShellGrid.ColumnDefinitions.Add(new ColumnDefinition(0));
            ShellGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            SidebarColumn.IsVisible = false;
        }
        else
        {
            ShellGrid.ColumnDefinitions.Add(new ColumnDefinition(260));
            ShellGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            SidebarColumn.IsVisible = true;
        }

        BodyGrid.ColumnDefinitions.Clear();
        BodyGrid.RowDefinitions.Clear();
        if (compact)
        {
            BodyGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            BodyGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            BodyGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            foreach (var child in BodyGrid.Children)
            {
                if (child is VerticalStackLayout vsl)
                {
                    var col = Grid.GetColumn(vsl);
                    Grid.SetColumn(vsl, 0);
                    Grid.SetRow(vsl, col);
                }
            }
        }
        else
        {
            BodyGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));
            BodyGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            BodyGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        var statTilesCount = StatsGrid.Children.Count;
        if (statTilesCount > 0)
        {
            StatsGrid.ColumnDefinitions.Clear();
            StatsGrid.RowDefinitions.Clear();

            var perRow = w < 700 ? 1 : w < 1100 ? 2 : statTilesCount;
            for (var c = 0; c < perRow; c++)
                StatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var rows = (int)Math.Ceiling(statTilesCount / (double)perRow);
            for (var r = 0; r < rows; r++)
                StatsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            for (var i = 0; i < statTilesCount; i++)
            {
                if (StatsGrid.Children[i] is BindableObject bo)
                {
                    Grid.SetColumn(bo, i % perRow);
                    Grid.SetRow(bo, i / perRow);
                }
            }
        }

        if (_modalOpen)
            ApplyModalSizing();
    }

    private void OnNavPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border b)
            b.BackgroundColor = Color.FromArgb("#0F3138");
    }

    private void OnNavPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border b)
            b.BackgroundColor = Colors.Transparent;
    }

    private void OnActionPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Border row)
            return;

        _activeActionRow = row;
        row.BackgroundColor = Color.FromArgb("#123C45");
        row.Stroke = ResolveColor("Accent", "#2FE3C9");
        row.StrokeThickness = 1;
    }

    private void OnActionPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is not Border row)
            return;

        if (_activeActionRow == row)
            _activeActionRow = null;
        row.BackgroundColor = Colors.Transparent;
        row.Stroke = Colors.Transparent;
        row.StrokeThickness = 0;
    }

    private void OnActionPointerPressed(object? sender, PointerEventArgs e)
    {
        if (sender is not Border row)
            return;

        row.BackgroundColor = Color.FromArgb("#0E353D");
        row.Scale = 0.99;
    }

    private void OnActionPointerReleased(object? sender, PointerEventArgs e)
    {
        if (sender is not Border row)
            return;

        row.Scale = 1;
        row.BackgroundColor = Color.FromArgb("#123C45");
    }

    private void OnModalClosePointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Border b)
            return;
        b.BackgroundColor = Color.FromArgb("#16424B");
        b.Stroke = ResolveColor("Accent", "#2FE3C9");
    }

    private void OnModalClosePointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is not Border b)
            return;
        b.BackgroundColor = Color.FromArgb("#0E333A");
        b.Stroke = Color.FromArgb("#1F5A64");
    }

    private static Color ResolveColor(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c)
            return c;
        return Color.FromArgb(fallbackHex);
    }
}

