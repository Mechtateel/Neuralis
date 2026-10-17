using NeuralisAPP_1.Services;

namespace NeuralisAPP_1.Views.Admin.Controls;

public static class AdminNavigationHelper
{
    public static void WireSidebar(
        AdminSidebarView sidebar,
        Page hostPage,
        AdminSidebarItem activeItem,
        AdminService? adminService = null)
    {
        sidebar.ActiveItem = activeItem;

        sidebar.DashboardTapped += async (_, _) =>
            await NavigateToDashboardAsync(hostPage);

        sidebar.UsersTapped += async (_, _) =>
            await NavigateIfNotCurrentAsync(hostPage, () => new AdminUsersPage());

        sidebar.ApplicationsTapped += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(AuthService.CurrentToken))
                return;

            var svc = adminService ?? new AdminService(AuthService.CurrentToken!);
            await NavigateIfNotCurrentAsync(hostPage, () => new AdminDoctorsPage(svc));
        };

        sidebar.NewsTapped += async (_, _) =>
            await NavigateIfNotCurrentAsync(hostPage, () => new AdminNewsPage());
    }

    public static async Task NavigateToDashboardAsync(Page hostPage)
    {
        if (hostPage is AdminDashboardPage)
            return;

        var nav = hostPage.Navigation;
        if (nav.NavigationStack.Any(p => p is AdminDashboardPage))
        {
            await nav.PopToRootAsync();
            return;
        }

        await nav.PushAsync(new AdminDashboardPage());
    }

    private static async Task NavigateIfNotCurrentAsync<T>(Page hostPage, Func<T> factory)
        where T : Page
    {
        if (hostPage is T)
            return;

        await hostPage.Navigation.PushAsync(factory());
    }
}
