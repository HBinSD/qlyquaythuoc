using QuanLyQuayThuoc.BLL.Models;
using QuanLyQuayThuoc.BLL.Security;
using QuanLyQuayThuoc.DAL.Repositories;

namespace QuanLyQuayThuoc.BLL.Services;

public sealed class AuthService
{
    private readonly AuthRepository _repository = new();

    public async Task<LoggedInUser?> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        var employee = await _repository.FindByUsernameAsync(username.Trim(), cancellationToken);
        if (employee is null || !PasswordHasher.Verify(password, employee.PasswordHash))
            return null;

        return new LoggedInUser(
            employee.EmployeeId,
            employee.Username,
            employee.FullName,
            employee.RoleId,
            employee.RoleName);
    }
}
