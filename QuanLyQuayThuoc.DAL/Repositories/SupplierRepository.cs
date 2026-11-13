
using Microsoft.Data.SqlClient;
using System.Data;
using QuanLyQuayThuoc.DAL.Database;

namespace QuanLyQuayThuoc.DAL.Repositories;

public sealed class SupplierRepository
{
    public sealed class SupplierRow
    {
        public string SupplierId { get; set; } = "";
        public string SupplierName { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? TaxCode { get; set; }
        public bool IsActive { get; set; }
    }

    public async Task<List<SupplierRow>> GetAllAsync(
        string? keyword = null,
        bool includeInactive = false,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT SupplierId, SupplierName, Phone, Email,
                   Address, TaxCode, IsActive
            FROM dbo.Supplier
            WHERE
                (@Keyword = N''
                OR SupplierId LIKE N'%' + @Keyword + N'%'
                OR SupplierName LIKE N'%' + @Keyword + N'%'
                OR ISNULL(Phone, '') LIKE N'%' + @Keyword + N'%'
                OR ISNULL(TaxCode, '') LIKE N'%' + @Keyword + N'%')
                AND (@IncludeInactive = 1 OR IsActive = 1)
            ORDER BY IsActive DESC, SupplierName;
            """;

        var result = new List<SupplierRow>();

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Keyword", SqlDbType.NVarChar, 200)
            .Value = (keyword ?? "").Trim();
        command.Parameters.Add("@IncludeInactive", SqlDbType.Bit)
            .Value = includeInactive;

        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            result.Add(new SupplierRow
            {
                SupplierId = reader.GetString(0),
                SupplierName = reader.GetString(1),
                Phone = reader.IsDBNull(2) ? null : reader.GetString(2),
                Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                Address = reader.IsDBNull(4) ? null : reader.GetString(4),
                TaxCode = reader.IsDBNull(5) ? null : reader.GetString(5),
                IsActive = reader.GetBoolean(6)
            });
        }

        return result;
    }

    public async Task<SupplierRow?> GetByIdAsync(
        string supplierId,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT SupplierId, SupplierName, Phone, Email,
                   Address, TaxCode, IsActive
            FROM dbo.Supplier
            WHERE SupplierId = @SupplierId;
            """;

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@SupplierId", SqlDbType.VarChar, 20)
            .Value = supplierId;

        await using var reader = await command.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return null;

        return new SupplierRow
        {
            SupplierId = reader.GetString(0),
            SupplierName = reader.GetString(1),
            Phone = reader.IsDBNull(2) ? null : reader.GetString(2),
            Email = reader.IsDBNull(3) ? null : reader.GetString(3),
            Address = reader.IsDBNull(4) ? null : reader.GetString(4),
            TaxCode = reader.IsDBNull(5) ? null : reader.GetString(5),
            IsActive = reader.GetBoolean(6)
        };
    }

    public async Task<bool> ExistsAsync(
        string supplierId,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM dbo.Supplier WHERE SupplierId = @SupplierId
            ) THEN 1 ELSE 0 END;
            """;

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@SupplierId", SqlDbType.VarChar, 20)
            .Value = supplierId;

        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1;
    }

    public async Task AddAsync(
        SupplierRow supplier,
        CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.Supplier
                (SupplierId, SupplierName, Phone, Email, Address, TaxCode, IsActive)
            VALUES
                (@SupplierId, @SupplierName, @Phone, @Email, @Address, @TaxCode, 1);
            """;

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        AddParameters(command, supplier);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(
        SupplierRow supplier,
        CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.Supplier
            SET SupplierName = @SupplierName,
                Phone = @Phone,
                Email = @Email,
                Address = @Address,
                TaxCode = @TaxCode
            WHERE SupplierId = @SupplierId;
            """;

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        AddParameters(command, supplier);

        int affected = await command.ExecuteNonQueryAsync(ct);

        if (affected == 0)
            throw new InvalidOperationException("Không tìm thấy nhà cung cấp cần cập nhật.");
    }

    public async Task SetActiveAsync(
        string supplierId,
        bool isActive,
        CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.Supplier
            SET IsActive = @IsActive
            WHERE SupplierId = @SupplierId;
            """;

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@SupplierId", SqlDbType.VarChar, 20)
            .Value = supplierId;
        command.Parameters.Add("@IsActive", SqlDbType.Bit)
            .Value = isActive;

        int affected = await command.ExecuteNonQueryAsync(ct);

        if (affected == 0)
            throw new InvalidOperationException("Không tìm thấy nhà cung cấp.");
    }

    private static void AddParameters(
        SqlCommand command,
        SupplierRow supplier)
    {
        command.Parameters.Add("@SupplierId", SqlDbType.VarChar, 20)
            .Value = supplier.SupplierId.Trim();

        command.Parameters.Add("@SupplierName", SqlDbType.NVarChar, 200)
            .Value = supplier.SupplierName.Trim();

        command.Parameters.Add("@Phone", SqlDbType.VarChar, 20)
            .Value = DbValue(supplier.Phone);

        command.Parameters.Add("@Email", SqlDbType.VarChar, 150)
            .Value = DbValue(supplier.Email);

        command.Parameters.Add("@Address", SqlDbType.NVarChar, 300)
            .Value = DbValue(supplier.Address);

        command.Parameters.Add("@TaxCode", SqlDbType.VarChar, 30)
            .Value = DbValue(supplier.TaxCode);
    }

    private static object DbValue(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? DBNull.Value
            : value.Trim();
}
