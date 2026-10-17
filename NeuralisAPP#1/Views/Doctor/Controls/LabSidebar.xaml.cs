using System.Linq;
using Microsoft.Maui.Controls;

namespace NeuralisAPP_1.Views.Doctor.Controls;

public partial class LabSidebar : ContentView
{
    private LabNavItem _activeItem = LabNavItem.PatientRecords;
    private Border? _activeNavBorder;

    public LabSidebar()
    {
        InitializeComponent();
        _activeNavBorder = NavPatientRecords;
        ApplyActiveVisuals();
    }

    public LabNavItem ActiveItem
    {
        get => _activeItem;
        set
        {
            if (_activeItem == value)
                return;
            _activeItem = value;
            ApplyActiveVisuals();
        }
    }

    public event EventHandler<LabNavItem>? Invoked;

    public void ApplyProfileFromSession() => SidebarProfile.ApplyLabFromSession();

    /// <summary>Collapse the profile card menu when leaving the page (optional UX).</summary>
    public void CollapseProfileMenu() => SidebarProfile.Collapse();

    private void ApplyActiveVisuals()
    {
        ResetNav(NavPatientRecords);
        ResetNav(NavSpecimenQueue);
        ResetNav(NavResultEntry);
        ResetNav(NavInventory);
        ResetNav(NavSystemHealth);

        _activeNavBorder = _activeItem switch
        {
            LabNavItem.PatientRecords => NavPatientRecords,
            LabNavItem.SpecimenQueue => NavSpecimenQueue,
            LabNavItem.ResultEntry => NavResultEntry,
            LabNavItem.Inventory => NavInventory,
            LabNavItem.SystemHealth => NavSystemHealth,
            _ => NavPatientRecords
        };

        SetActive(_activeNavBorder);
    }

    private static void ResetNav(Border nav)
    {
        nav.BackgroundColor = Colors.Transparent;
        SetNavTextColor(nav, "#9ca3af");
    }

    private static void SetActive(Border active)
    {
        active.BackgroundColor = Color.FromArgb("#111827");
        SetNavTextColor(active, "#2dd4bf");
    }

    private static void SetNavTextColor(Border nav, string colorHex)
    {
        if (nav.Content is not HorizontalStackLayout layout)
            return;
        var c = Color.FromArgb(colorHex);
        foreach (var label in layout.Children.OfType<Label>())
            label.TextColor = c;
    }

    private void OnNavPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Border border || border == _activeNavBorder)
            return;

        border.BackgroundColor = Color.FromArgb("#0f172a");
        SetNavTextColor(border, "#e5e7eb");
    }

    private void OnNavPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is not Border border || border == _activeNavBorder)
            return;

        border.BackgroundColor = Colors.Transparent;
        SetNavTextColor(border, "#9ca3af");
    }

    private void OnPatientRecordsTapped(object? sender, EventArgs e)
    {
        ActiveItem = LabNavItem.PatientRecords;
        Invoked?.Invoke(this, LabNavItem.PatientRecords);
    }

    private void OnSpecimenQueueTapped(object? sender, EventArgs e) =>
        Invoked?.Invoke(this, LabNavItem.SpecimenQueue);

    private void OnResultEntryTapped(object? sender, EventArgs e) =>
        Invoked?.Invoke(this, LabNavItem.ResultEntry);

    private void OnInventoryTapped(object? sender, EventArgs e) =>
        Invoked?.Invoke(this, LabNavItem.Inventory);

    private void OnSystemHealthTapped(object? sender, EventArgs e) =>
        Invoked?.Invoke(this, LabNavItem.SystemHealth);
}
