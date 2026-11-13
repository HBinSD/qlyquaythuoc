using Microsoft.Data.SqlClient;
using QuanLyQuayThuoc.DAL.Database;

namespace QuanLyQuayThuoc.DAL.Repositories;

public sealed record EmployeeRecord(
    string EmployeeId,
    string Username,
    string FullName,
    string RoleId,
    string RoleName,
    string PasswordHash);

public sealed class AuthRepository
{
    public async Task<EmployeeRecord?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP (1)
    e.EmployeeId, e.Username, e.FullName, e.RoleId, r.RoleName, e.PasswordHash
FROM dbo.Employee e
INNER JOIN dbo.Role r ON r.RoleId = e.RoleId
WHERE e.Username = @Username AND e.IsActive = 1;";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new EmployeeRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5));
    }
}
