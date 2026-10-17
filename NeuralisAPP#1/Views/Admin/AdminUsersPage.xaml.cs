using NeuralisAPP_1.Models.Admin;
using NeuralisAPP_1.Services;
using NeuralisAPP_1.Views.Admin.Controls;
using System.Collections.ObjectModel;

namespace NeuralisAPP_1.Views.Admin;

public partial class AdminUsersPage : ContentPage
{
    private AdminService _adminService = null!;
    private Button? _activeRoleButton;

    public ObservableCollection<AdminUserModel> AllUsers { get; } = new();
    public ObservableCollection<AdminUserModel> FilteredUsers { get; } = new();
    public ObservableCollection<int> Pages { get; } = new();

    private string _searchText = string.Empty;
    private string _selectedRole = "All";

    private int _currentPage = 1;
    private const int PageSize = 6;
    private int _totalItems;

    public AdminUsersPage()
    {
        InitializeComponent();

        BindingContext = this;
        UsersList.ItemsSource = FilteredUsers;

        AdminNavigationHelper.WireSidebar(AdminSidebar, this, AdminSidebarItem.Users);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        AdminSidebar.ActiveItem = AdminSidebarItem.Users;
        await AdminSidebar.RefreshProfileAsync();
        InitializeAdminService();
        LoadUsers();
    }

    private void InitializeAdminService()
    {
        if (string.IsNullOrWhiteSpace(AuthService.CurrentToken))
            return;

        _adminService = new AdminService(AuthService.CurrentToken);
    }

    private async void LoadUsers()
    {
        try
        {
            var users = await _adminService.GetUsers();

            AllUsers.Clear();
            foreach (var user in users)
                AllUsers.Add(user);

            _currentPage = 1;
            ApplyFilters();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = e.NewTextValue ?? "";
        _currentPage = 1;
        ApplyFilters();
    }

    private void OnRoleFilterClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn)
            return;

        if (_activeRoleButton != null)
            SetFilterButtonActive(_activeRoleButton, false);

        SetFilterButtonActive(btn, true);
        _activeRoleButton = btn;

        _selectedRole = btn.CommandParameter?.ToString() ?? "All";
        _currentPage = 1;

        ApplyFilters();
    }

    private void SetFilterButtonActive(Button button, bool isActive)
    {
        button.BackgroundColor = isActive
            ? Color.FromArgb("#38bdf8")
            : Color.FromArgb("#0b2533");

        button.TextColor = isActive
            ? Colors.White
            : Color.FromArgb("#94a3b8");
    }

    private void ApplyFilters()
    {
        IEnumerable<AdminUserModel> query = AllUsers;

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            query = query.Where(u =>
                u.DisplayName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }

        if (_selectedRole != "All")
        {
            query = query.Where(u =>
                u.Role.Equals(_selectedRole, StringComparison.OrdinalIgnoreCase));
        }

        _totalItems = query.Count();

        UpdatePagination(_totalItems);

        var paged = query
            .Skip((_currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        FilteredUsers.Clear();
        foreach (var user in paged)
            FilteredUsers.Add(user);

        PaginationText =
            $"Showing {((_currentPage - 1) * PageSize) + 1}–{((_currentPage - 1) * PageSize) + paged.Count} of {_totalItems}";

        OnPropertyChanged(nameof(PaginationText));
    }

    private void UpdatePagination(int totalItems)
    {
        Pages.Clear();

        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / PageSize));

        for (int i = 1; i <= totalPages; i++)
            Pages.Add(i);

        OnPropertyChanged(nameof(CurrentPage));
    }

    public string PaginationText { get; set; } = "";

    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            if (_currentPage == value) return;
            _currentPage = value;
            OnPropertyChanged();
        }
    }

    void OnPageClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Text, out int page))
        {
            CurrentPage = page;
            ApplyFilters();
        }
    }

    void OnNextPage(object sender, EventArgs e)
    {
        if (CurrentPage < Pages.Count)
        {
            CurrentPage++;
            ApplyFilters();
        }
    }

    void OnPrevPage(object sender, EventArgs e)
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            ApplyFilters();
        }
    }

    async void OnAddUserClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Add User", "Add user clicked", "OK");
    }

    async void OnBlockToggleClicked(object sender, EventArgs e)
    {
        var button = (Button)sender;
        var user = (AdminUserModel)button.CommandParameter;

        try
        {
            if (user.IsBlocked)
                await _adminService.UnblockUser(user.Id);
            else
                await _adminService.BlockUser(user.Id);

            LoadUsers();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
