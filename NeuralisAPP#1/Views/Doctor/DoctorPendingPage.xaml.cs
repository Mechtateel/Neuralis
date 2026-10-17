using Microsoft.Maui.Storage;
using NeuralisAPP_1.Services;

namespace NeuralisAPP_1.Views.Doctor;

public partial class DoctorPendingPage : ContentPage
{
    public DoctorPendingPage()
    {
        InitializeComponent();
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        try
        {
            var token = await SecureStorage.GetAsync("access_token");

            if (string.IsNullOrWhiteSpace(token))
            {
                await LogoutAndGoLogin();
                return;
            }

            var doctorService = new DoctorService(token);
            var doctor = await doctorService.GetMe();

            if (doctor == null)
            {
                await LogoutAndGoLogin();
                return;
            }

            // ✅ ВОТ ЕДИНСТВЕННО ПРАВИЛЬНАЯ ПРОВЕРКА
            if (doctor.IsVerified)
            {
                await Shell.Current.GoToAsync("//DoctorDashboard");
                return;
            }

            await DisplayAlert(
                "Still pending",
                "Your profile is not verified yet",
                "OK"
            );
        }
        catch
        {
            await DisplayAlert(
                "Error",
                "Failed to refresh status",
                "OK"
            );
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        await LogoutAndGoLogin();
    }

    private static async Task LogoutAndGoLogin()
    {
        SecureStorage.Remove("access_token");
        App.CurrentUser = null;
        await Shell.Current.GoToAsync("//Login");
    }
}
