namespace NeuralisAPP_1.Views.Main.Controls;

internal static class UserSidebarCoordinator
{
    internal static void Wire(
        UserAppSidebarView sidebar,
        UserSidebarActiveItem activeItem,
        Func<Task> onDashboardTap,
        Func<Task> onBookVisitTap,
        Func<Task> onAppointmentsTap,
        Func<Task> onLatestNewsTap,
        Func<Task> onLogoutTap,
        Action? closeSidebar = null)
    {
        sidebar.ActiveItem = activeItem;

        sidebar.DashboardTapped += async (_, _) =>
        {
            closeSidebar?.Invoke();
            await onDashboardTap();
        };
        sidebar.BookVisitTapped += async (_, _) =>
        {
            closeSidebar?.Invoke();
            await onBookVisitTap();
        };
        sidebar.AppointmentsTapped += async (_, _) =>
        {
            closeSidebar?.Invoke();
            await onAppointmentsTap();
        };
        sidebar.LatestNewsTapped += async (_, _) =>
        {
            closeSidebar?.Invoke();
            await onLatestNewsTap();
        };
        sidebar.LogoutTapped += async (_, _) =>
        {
            closeSidebar?.Invoke();
            await onLogoutTap();
        };
    }
}
