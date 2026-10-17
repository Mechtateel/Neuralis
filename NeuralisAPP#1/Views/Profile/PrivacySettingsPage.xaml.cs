using NeuralisAPP_1.ViewModels.Profile;

namespace NeuralisAPP_1.Views.Profile;

public partial class PrivacySettingsPage : ContentPage
{
    private readonly ProfileViewModel _vm;
    private bool _populating;

    public PrivacySettingsPage(ProfileViewModel vm)
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
            ProfileVisibleSwitch.IsToggled = _vm.ProfileVisible;
            ShareAnalyticsSwitch.IsToggled = _vm.ShareAnalytics;
            SaveAiHistorySwitch.IsToggled = _vm.SaveAiHistory;
            ActivityTrackingSwitch.IsToggled = _vm.ActivityTracking;
        }
        finally
        {
            _populating = false;
        }
    }

    private async void OnBackTapped(object? sender, EventArgs e) =>
        await Navigation.PopAsync();

    private void OnProfileVisibleToggled(object? sender, ToggledEventArgs e)
    {
        if (_populating) return;
        _vm.ProfileVisible = e.Value;
    }

    private void OnShareAnalyticsToggled(object? sender, ToggledEventArgs e)
    {
        if (_populating) return;
        _vm.ShareAnalytics = e.Value;
    }

    private void OnSaveAiHistoryToggled(object? sender, ToggledEventArgs e)
    {
        if (_populating) return;
        _vm.SaveAiHistory = e.Value;
    }

    private void OnActivityTrackingToggled(object? sender, ToggledEventArgs e)
    {
        if (_populating) return;
        _vm.ActivityTracking = e.Value;
    }
}
