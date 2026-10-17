using NeuralisAPP_1.Models.Admin;
using NeuralisAPP_1.Services;
using NeuralisAPP_1.Views.Admin.Controls;

namespace NeuralisAPP_1.Views.Admin;

public partial class AdminDoctorsPage : ContentPage
{
    private readonly AdminService _adminService;

    private string _currentStatus = "Pending";
    private string? _search;
    private int _page = 1;

    public AdminDoctorsPage(AdminService adminService)
    {
        InitializeComponent();
        _adminService = adminService;

        AdminNavigationHelper.WireSidebar(AdminSidebar, this, AdminSidebarItem.Applications, _adminService);
        SetActiveTab(PendingTab);
        LoadApplications();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        AdminSidebar.ActiveItem = AdminSidebarItem.Applications;
        await AdminSidebar.RefreshProfileAsync();
    }

    private async void LoadApplications()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        EmptyLabel.IsVisible = false;

        try
        {
            var result = await _adminService.GetDoctorApplications(
                _currentStatus,
                _page,
                _search
            );

            ApplicationsList.ItemsSource = result.Items;
            EmptyLabel.IsVisible = result.Items.Count == 0;
        }
        catch
        {
            EmptyLabel.Text = "Failed to load doctor applications";
            EmptyLabel.IsVisible = true;
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    void OnPendingClicked(object sender, EventArgs e)
    {
        SetActiveTab(PendingTab);
        _currentStatus = "Pending";
        _page = 1;
        LoadApplications();
    }

    void OnApprovedClicked(object sender, EventArgs e)
    {
        SetActiveTab(ApprovedTab);
        _currentStatus = "Approved";
        _page = 1;
        LoadApplications();
    }

    void OnRejectedClicked(object sender, EventArgs e)
    {
        SetActiveTab(RejectedTab);
        _currentStatus = "Rejected";
        _page = 1;
        LoadApplications();
    }

    private void SetActiveTab(Button active)
    {
        ResetTab(PendingTab);
        ResetTab(ApprovedTab);
        ResetTab(RejectedTab);

        active.BackgroundColor = Color.FromArgb("#164e63");
        active.TextColor = Colors.White;
    }

    private void ResetTab(Button tab)
    {
        tab.BackgroundColor = Color.FromArgb("#0f1c2d");
        tab.TextColor = Color.FromArgb("#94a3b8");
    }

    void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _search = e.NewTextValue;
        _page = 1;
        LoadApplications();
    }

    async void OnFilterClicked(object sender, EventArgs e)
    {
        await DisplayAlert(
            "Filters",
            "Advanced filters will be available soon.",
            "OK"
        );
    }

    async void OnApproveClicked(object sender, EventArgs e)
    {
        var model = (DoctorApplicationModel)((Button)sender).CommandParameter;
        await _adminService.ApproveDoctor(model.DoctorId);
        LoadApplications();
    }

    async void OnRejectClicked(object sender, EventArgs e)
    {
        var model = (DoctorApplicationModel)((Button)sender).CommandParameter;
        await _adminService.RejectDoctor(model.DoctorId, "Rejected by admin");
        LoadApplications();
    }

    async void OnCardPressed(object sender, EventArgs e)
    {
        if (sender is Border card)
            await card.ScaleTo(0.98, 80);
    }

    async void OnCardReleased(object sender, EventArgs e)
    {
        if (sender is Border card)
            await card.ScaleTo(1, 80);
    }
}
