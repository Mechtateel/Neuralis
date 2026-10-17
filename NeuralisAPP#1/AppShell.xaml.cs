using Microsoft.Maui.Storage;
using NeuralisAPP_1.Models.Auth;
using NeuralisAPP_1.Services;
using NeuralisAPP_1.Views.Auth;
using NeuralisAPP_1.Views.Main;
using NeuralisAPP_1.Models.Doctor;
using NeuralisAPP_1.Views.Doctor;
using NeuralisAPP_1.Views.Admin;
using NeuralisAPP_1.Views.Profile;

namespace NeuralisAPP_1;

public partial class AppShell : Shell
{
    private bool _checked;

    public AppShell()
    {
        InitializeComponent();

        // 🔹 AUTH / COMMON
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));

        // 🔹 USER
        Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
        Routing.RegisterRoute(nameof(LatestUpdatesPage), typeof(LatestUpdatesPage));
        Routing.RegisterRoute(nameof(PatientServicesPage), typeof(PatientServicesPage));
        Routing.RegisterRoute(nameof(MedicalServicesPage), typeof(MedicalServicesPage));
        Routing.RegisterRoute(nameof(AIAnalysisPage), typeof(AIAnalysisPage));

        // 🔹 DOCTOR
        Routing.RegisterRoute(nameof(DoctorPendingPage), typeof(DoctorPendingPage));
        Routing.RegisterRoute(nameof(LabDashboardPage), typeof(LabDashboardPage));
        Routing.RegisterRoute(nameof(DoctorLabResultEntryPage), typeof(DoctorLabResultEntryPage));

        // 🔹 ADMIN
       

        Routing.RegisterRoute(nameof(AdminDashboardPage), typeof(AdminDashboardPage));

        // 🔹 PROFILE (shared routes across roles)
        Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
        Routing.RegisterRoute(nameof(EditProfilePage), typeof(EditProfilePage));
        Routing.RegisterRoute(nameof(ChangePasswordPage), typeof(ChangePasswordPage));
        Routing.RegisterRoute(nameof(NotificationPreferencesPage), typeof(NotificationPreferencesPage));
        Routing.RegisterRoute(nameof(PrivacySettingsPage), typeof(PrivacySettingsPage));
        Routing.RegisterRoute(nameof(LanguagePickerPage), typeof(LanguagePickerPage));
    }

    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        var target = args.Target.Location.OriginalString;

        if (target.Contains("AdminDashboard"))
        {
            if (App.CurrentUser?.Role != (int)UserRole.Admin)
            {
                args.Cancel();
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Access denied", "Admin only", "OK");
                    await GoToAsync("//Login");
                });
            }
        }

        if (target.Contains("LabDashboard") || target.Contains("DoctorLabResultEntry"))
        {
            var u = App.CurrentUser;
            if (u?.Role != (int)UserRole.Doctor || !u.IsVerified
                || !DoctorSpecializationUi.IsLabTechnician(u.DoctorSpecialization))
            {
                args.Cancel();
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Access denied", "Lab portal is for lab technicians only.", "OK");
                    await GoToAsync("//Login");
                });
            }
        }

        if (target.Contains("DoctorDashboard"))
        {
            var u = App.CurrentUser;
            if (u?.Role == (int)UserRole.Doctor && u.IsVerified
                && DoctorSpecializationUi.IsLabTechnician(u.DoctorSpecialization))
            {
                args.Cancel();
                MainThread.BeginInvokeOnMainThread(async () =>
                    await GoToAsync("//LabDashboard"));
            }
        }

        base.OnNavigating(args);
    }


    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_checked) return;
        _checked = true;

        try
        {
            var token = await SecureStorage.GetAsync("access_token");

            if (string.IsNullOrWhiteSpace(token))
            {
                await GoToAsync("//Login");
                return;
            }

            var authService = new AuthService();
            var me = await authService.GetMe(token);

            if (me == null)
            {
                await LogoutAsync();
                return;
            }

            App.CurrentUser = me;

            switch ((UserRole)me.Role)
            {
                case UserRole.Admin:
                    await GoToAsync("//AdminDashboard");
                    break;

                case UserRole.Doctor:
                    if (!me.IsVerified)
                    {
                        await GoToAsync("//DoctorPending");
                        break;
                    }

                    if (DoctorSpecializationUi.IsLabTechnician(me.DoctorSpecialization))
                        await GoToAsync("//LabDashboard");
                    else
                        await GoToAsync("//DoctorDashboard");
                    break;

                case UserRole.User:
                default:
                    await GoToAsync("//Main");
                    break;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Startup error", ex.Message, "OK");
            await GoToAsync("//Login");
        }
    }

    public static async Task LogoutAsync()
    {
        SecureStorage.Remove("access_token");
        App.CurrentUser = null;
        await Current.GoToAsync("//Login");
    }
}
