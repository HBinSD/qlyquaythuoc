namespace QuanLyQuayThuoc.BLL.Models;

public sealed record LoggedInUser(
    string EmployeeId,
    string Username,
    string FullName,
    string RoleId,
    string RoleName);
