using Microsoft.Maui.Storage;
using NeuralisAPP_1.Localization;
using NeuralisAPP_1.Models.Auth;
using NeuralisAPP_1.Models.Doctor;
using NeuralisAPP_1.Services;

namespace NeuralisAPP_1.Views.Auth;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _authService = new();

    public LoginPage()
    {
        InitializeComponent();
        ApplyLanguage();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        WelcomeBackLabel.Text = T.Get("Welcome back", "С возвращением", "Қайта келдіңіз");
        SignInContinueLabel.Text = T.Get("Sign in to continue", "Войдите, чтобы продолжить", "Жалғастыру үшін кіріңіз");
        EmailEntry.Placeholder = T.Get("Email address", "Электронная почта", "Электрондық пошта");
        PasswordEntry.Placeholder = T.Get("Password", "Пароль", "Құпия сөз");
        LoginButton.Text = T.Get("Log in ->", "Войти ->", "Кіру ->");
        NoAccountLabel.Text = T.Get("Don't have an account? ", "Нет аккаунта? ", "Тіркелгіңіз жоқ па? ");
        RegisterLabel.Text = T.Get("Register", "Регистрация", "Тіркелу");
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        try
        {
            var email = EmailEntry.Text?.Trim();
            var password = PasswordEntry.Text;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                await DisplayAlert("Error", "Please enter email and password.", "OK");
                return;
            }

            // 1️⃣ LOGIN → token
            var token = await _authService.Login(email, password);

            if (string.IsNullOrWhiteSpace(token))
            {
                await DisplayAlert("Error", "Invalid email or password.", "OK");
                return;
            }

            // 2️⃣ сохраняем токен
            await SecureStorage.SetAsync("access_token", token);

            // 3️⃣ получаем /auth/me
            var me = await _authService.GetMe(token);

            if (me == null)
            {
                await DisplayAlert("Error", "Unable to load profile.", "OK");
                return;
            }

            App.CurrentUser = me;

            // 🔔 уведомляем UI
            MessagingCenter.Send<App>(Application.Current as App, "AUTH_CHANGED");

            // 4️⃣ НАВИГАЦИЯ
            await RedirectAfterLogin(me, token);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Sign-in error", ex.Message, "OK");
        }
    }

    private async Task RedirectAfterLogin(MeResponse me, string token)
    {
        // ADMIN
        if ((UserRole)me.Role == UserRole.Admin)
        {
            await Shell.Current.GoToAsync("//AdminDashboard");
            return;
        }

        // DOCTOR
        if ((UserRole)me.Role == UserRole.Doctor)
        {
            var doctorService = new DoctorService(token);
            var doctor = await doctorService.GetMe();

            if (doctor == null)
            {
                await DisplayAlert("Error", "Doctor profile not found.", "OK");
                await Logout();
                return;
            }

            if (!doctor.IsVerified)
            {
                await Shell.Current.GoToAsync("//DoctorPending");
                return;
            }

            if (DoctorSpecializationUi.IsLabTechnician(doctor.Specialization))
                await Shell.Current.GoToAsync("//LabDashboard");
            else
                await Shell.Current.GoToAsync("//DoctorDashboard");

            return;
        }
        // USER
        if ((UserRole)me.Role == UserRole.User)
        {
            await Shell.Current.GoToAsync("//Main"); // или //UserDashboard
            return;
        }


        // fallback
        await Shell.Current.GoToAsync("//Login");
    }

    private async Task Logout()
    {
        SecureStorage.Remove("access_token");
        App.CurrentUser = null;
        await Shell.Current.GoToAsync("//Login");
    }

    private async void OnRegisterTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Register");
    }
}
