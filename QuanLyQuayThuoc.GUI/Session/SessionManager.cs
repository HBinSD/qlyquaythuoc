using QuanLyQuayThuoc.BLL.Models;

namespace QuanLyQuayThuoc.GUI.Session;

public static class SessionManager
{
    public static LoggedInUser? CurrentUser { get; private set; }

    public static void SignIn(LoggedInUser user) => CurrentUser = user;
    public static void SignOut() => CurrentUser = null;
}
