using NeuralisAPP_1.Services;
using NeuralisAPP_1.ViewModels.Lab;
using NeuralisAPP_1.Views.Doctor.Controls;

namespace NeuralisAPP_1.Views.Doctor;

public partial class DoctorLabResultEntryPage : ContentPage
{
    private readonly Guid? _preselectPatientId;
    private LabResultEntryViewModel? _vm;

    public DoctorLabResultEntryPage(Guid? preselectPatientId = null)
    {
        InitializeComponent();
        _preselectPatientId = preselectPatientId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (string.IsNullOrWhiteSpace(AuthService.CurrentToken))
        {
            await App.LogoutAsync();
            return;
        }

        _vm ??= new LabResultEntryViewModel(AuthService.CurrentToken!);
        BindingContext = _vm;
        LabNav.ActiveItem = LabNavItem.ResultEntry;
        LabNav.ApplyProfileFromSession();

        await _vm.LoadAsync();
        if (_preselectPatientId is { } id)
            _vm.ApplyPreselectedPatient(id);
    }

    protected override void OnDisappearing()
    {
        LabNav.CollapseProfileMenu();
        base.OnDisappearing();
    }

    private async void OnLabSidebarInvoked(object? sender, LabNavItem e)
    {
        if (e == LabNavItem.ResultEntry)
        {
            LabNav.ActiveItem = LabNavItem.ResultEntry;
            return;
        }

        if (e == LabNavItem.PatientRecords)
        {
            LabNav.ActiveItem = LabNavItem.PatientRecords;
            await Navigation.PopAsync();
            return;
        }

        await DisplayAlert("Neuralis", "This section is not available yet.", "OK");
    }

    private void OnBrowseClicked(object? sender, EventArgs e)
    {
        if (BindingContext is LabResultEntryViewModel vm && vm.PickFileCommand.CanExecute(null))
            vm.PickFileCommand.Execute(null);
    }
}
