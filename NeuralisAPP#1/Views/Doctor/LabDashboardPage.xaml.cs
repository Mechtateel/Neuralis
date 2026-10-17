using NeuralisAPP_1.Services;
using NeuralisAPP_1.ViewModels.Lab;
using NeuralisAPP_1.Views.Doctor.Controls;

namespace NeuralisAPP_1.Views.Doctor;

public partial class LabDashboardPage : ContentPage
{
    private LabDashboardViewModel? _vm;

    public LabDashboardPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (string.IsNullOrWhiteSpace(AuthService.CurrentToken))
        {
            await App.LogoutAsync();
            return;
        }

        _vm ??= new LabDashboardViewModel(AuthService.CurrentToken!);
        BindingContext = _vm;
        LabNav.ActiveItem = LabNavItem.PatientRecords;
        LabNav.ApplyProfileFromSession();
        await _vm.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        LabNav.CollapseProfileMenu();
        base.OnDisappearing();
    }

    private async void OnLabSidebarInvoked(object? sender, LabNavItem e)
    {
        if (e == LabNavItem.PatientRecords)
        {
            LabNav.ActiveItem = LabNavItem.PatientRecords;
            return;
        }

        if (e == LabNavItem.ResultEntry)
        {
            LabNav.ActiveItem = LabNavItem.ResultEntry;
            await Navigation.PushAsync(new DoctorLabResultEntryPage());
            return;
        }

        await DisplayAlert(
            "Neuralis",
            "This section is not available yet.",
            "OK");
    }

    private async void OnOpenResultEntry(object? sender, EventArgs e)
    {
        if (sender is not BindableObject bo || bo.BindingContext is not LabPatientRowViewModel row)
            return;
        await Navigation.PushAsync(new DoctorLabResultEntryPage(row.PatientUserId));
    }
}
