using Microsoft.Maui.Storage;
using NeuralisAPP_1.Models.Auth;
using NeuralisAPP_1.Services;
using NeuralisAPP_1.Views.Auth;
using NeuralisAPP_1.Views.Main;
using NeuralisAPP_1.Models.Doctor;
using NeuralisAPP_1.Views.Doctor;

namespace NeuralisAPP_1;

public partial class App : Application
{
    public static MeResponse? CurrentUser { get; set; }
    private static bool _initialized;

    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();
        LoadToken();

        Dispatcher.Dispatch(async () =>
        {
            await CheckAuthAsync();
        });
    }


    private async void LoadToken()
    {
        var token = await SecureStorage.GetAsync("access_token");
        AuthService.SetToken(token);
    }


    private async Task CheckAuthAsync()
    {
        if (_initialized) return;
        _initialized = true;

        var token = await SecureStorage.GetAsync("access_token");

        if (string.IsNullOrWhiteSpace(token))
        {
            await Shell.Current.GoToAsync("//Login");
            return;
        }

        var authService = new AuthService();
        var me = await authService.GetMe(token);

        if (me == null)
        {
            await LogoutAsync();
            return;
        }

        // ✅ источник истины
        CurrentUser = me;

        // ❗ НЕ ШЛЁМ AUTH_CHANGED ТУТ

        await NavigateByRole(me);
    }

    private async Task NavigateByRole(MeResponse me)
    {
        switch ((UserRole)me.Role)
        {
            case UserRole.Admin:
                await Shell.Current.GoToAsync("//AdminDashboard");
                break;

            case UserRole.Doctor:
                if (!me.IsVerified)
                {
                    await Shell.Current.GoToAsync("//DoctorPending");
                    break;
                }

                if (DoctorSpecializationUi.IsLabTechnician(me.DoctorSpecialization))
                    await Shell.Current.GoToAsync("//LabDashboard");
                else
                    await Shell.Current.GoToAsync("//DoctorDashboard");
                break;

            default:
                await Shell.Current.GoToAsync("//Main");
                break;
        }
    }

    public static async Task LogoutAsync()
    {
        SecureStorage.Remove("access_token");
        CurrentUser = null;

        // 🔔 уведомляем UI
        MessagingCenter.Send(Current, "AUTH_CHANGED");

        await Shell.Current.GoToAsync("//Login");
    }
}
