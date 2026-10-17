using NeuralisAPP_1.ViewModels.Profile;

namespace NeuralisAPP_1.Views.Profile;

public partial class NotificationPreferencesPage : ContentPage
{
    private readonly ProfileViewModel _vm;
    private bool _populating;

    public NotificationPreferencesPage(ProfileViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        Populate();
    }

    private void Populate()
    {
        _populating = true;
        try
        {
            AiAnalysisSwitch.IsToggled = _vm.NotifyAiAnalysis;
            AppointmentsSwitch.IsToggled = _vm.NotifyAppointments;
            SecuritySwitch.IsToggled = _vm.NotifySecurity;
            SystemSwitch.IsToggled = _vm.NotifySystem;
        }
        finally
        {
            _populating = false;
        }
    }

    private async void OnBackTapped(object? sender, EventArgs e) =>
        await Navigation.PopAsync();

    private void OnAiAnalysisToggled(object? sender, ToggledEventArgs e)
    {
        if (_populating) return;
        _vm.NotifyAiAnalysis = e.Value;
    }

    private void OnAppointmentsToggled(object? sender, ToggledEventArgs e)
    {
        if (_populating) return;
        _vm.NotifyAppointments = e.Value;
    }

    private void OnSecurityToggled(object? sender, ToggledEventArgs e)
    {
        if (_populating) return;
        _vm.NotifySecurity = e.Value;
    }

    private void OnSystemToggled(object? sender, ToggledEventArgs e)
    {
        if (_populating) return;
        _vm.NotifySystem = e.Value;
    }
}
