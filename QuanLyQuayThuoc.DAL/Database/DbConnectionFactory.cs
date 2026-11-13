using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace QuanLyQuayThuoc.DAL.Database;

public static class DbConnectionFactory
{
    private static string? _connectionString;

    private static string GetConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
            return _connectionString;

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "QuanLyQuayThuoc.GUI", "appsettings.json")
        };

        var configPath = candidates.FirstOrDefault(File.Exists);
        if (configPath is null)
            throw new FileNotFoundException("Không tìm thấy appsettings.json. Hãy kiểm tra file cấu hình kết nối.");

        using var stream = File.OpenRead(configPath);
        using var document = JsonDocument.Parse(stream);
        _connectionString = document.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("Default")
            .GetString();

        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException("ConnectionStrings:Default chưa được cấu hình.");

        return _connectionString;
    }

    public static SqlConnection CreateConnection() => new(GetConnectionString());

    public static async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return connection.State == System.Data.ConnectionState.Open;
    }
}
