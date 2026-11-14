using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyQuayThuoc.DAL.Database;

namespace QuanLyQuayThuoc.DAL.Repositories;

public sealed record EmployeeListRow(
    string EmployeeId,
    string FullName,
    string Username,
    string RoleId,
    string RoleName,
    bool IsActive);

public sealed record LoyaltySettingsRow(decimal EarnPercent, decimal MoneyPerPoint);

public sealed record VoucherRow(
    string VoucherId,
    string VoucherCode,
    string? Description,
    string DiscountType,
    decimal DiscountValue,
    decimal MinimumOrder,
    decimal? MaximumDiscount,
    int? UsageLimit,
    int UsedCount,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive);

public sealed class EmployeeRepository
{
    public async Task<List<EmployeeListRow>> GetEmployeesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT e.EmployeeId, e.FullName, e.Username, e.RoleId, r.RoleName, e.IsActive
            FROM dbo.Employee e
            INNER JOIN dbo.Role r ON r.RoleId = e.RoleId
            ORDER BY e.IsActive DESC, e.EmployeeId;
            """;

        var rows = new List<EmployeeListRow>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new EmployeeListRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetBoolean(5)));
        }

        return rows;
    }

    public async Task SaveEmployeeAsync(
        string employeeId,
        string fullName,
        string username,
        string roleId,
        bool isActive,
        string? passwordHash,
        string actorId,
        bool isNew,
        CancellationToken ct = default)
    {
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        try
        {
            if (isNew)
            {
                if (passwordHash is null)
                    throw new ArgumentException("Mật khẩu ban đầu không được để trống.");

                const string insertSql = """
                    INSERT dbo.Employee
                        (EmployeeId, RoleId, FullName, Username, PasswordHash, IsActive)
                    VALUES
                        (@EmployeeId, @RoleId, @FullName, @Username, @PasswordHash, @IsActive);
                    """;
                await using var command = new SqlCommand(insertSql, connection, transaction);
                AddEmployeeParameters(command, employeeId, fullName, username, roleId, isActive);
                command.Parameters.Add("@PasswordHash", SqlDbType.VarChar, 255).Value = passwordHash;
                await command.ExecuteNonQueryAsync(ct);

                await WriteAuditAsync(
                    connection,
                    transaction,
                    actorId,
                    "CREATE_EMPLOYEE",
                    employeeId,
                    null,
                    JsonSerializer.Serialize(new { employeeId, fullName, username, roleId, isActive }),
                    ct);
            }
            else
            {
                const string selectSql = """
                    SELECT RoleId, FullName, Username, IsActive
                    FROM dbo.Employee WITH (UPDLOCK, HOLDLOCK)
                    WHERE EmployeeId = @EmployeeId;
                    """;
                string? oldValue;
                string oldRoleId;
                bool oldIsActive;

                await using (var command = new SqlCommand(selectSql, connection, transaction))
                {
                    command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
                    await using var reader = await command.ExecuteReaderAsync(ct);
                    if (!await reader.ReadAsync(ct))
                        throw new InvalidOperationException("Không tìm thấy tài khoản nhân viên.");

                    oldRoleId = reader.GetString(0);
                    oldValue = JsonSerializer.Serialize(new
                    {
                        employeeId,
                        roleId = oldRoleId,
                        fullName = reader.GetString(1),
                        username = reader.GetString(2),
                        isActive = reader.GetBoolean(3)
                    });
                    oldIsActive = reader.GetBoolean(3);
                }

                if (oldIsActive && oldRoleId == "ADMIN" && (!isActive || roleId != "ADMIN"))
                {
                    const string adminsSql = """
                        SELECT COUNT(1)
                        FROM dbo.Employee WITH (UPDLOCK, HOLDLOCK)
                        WHERE RoleId = 'ADMIN' AND IsActive = 1 AND EmployeeId <> @EmployeeId;
                        """;
                    await using var command = new SqlCommand(adminsSql, connection, transaction);
                    command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
                    if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 0)
                        throw new InvalidOperationException("Không thể khóa hoặc đổi vai trò của quản lý cuối cùng.");
                }

                const string updateSql = """
                    UPDATE dbo.Employee
                    SET RoleId = @RoleId,
                        FullName = @FullName,
                        Username = @Username,
                        IsActive = @IsActive,
                        PasswordHash = COALESCE(@PasswordHash, PasswordHash)
                    WHERE EmployeeId = @EmployeeId;
                    """;
                await using (var command = new SqlCommand(updateSql, connection, transaction))
                {
                    AddEmployeeParameters(command, employeeId, fullName, username, roleId, isActive);
                    command.Parameters.Add("@PasswordHash", SqlDbType.VarChar, 255).Value =
                        (object?)passwordHash ?? DBNull.Value;
                    await command.ExecuteNonQueryAsync(ct);
                }

                await WriteAuditAsync(
                    connection,
                    transaction,
                    actorId,
                    "UPDATE_EMPLOYEE",
                    employeeId,
                    oldValue,
                    JsonSerializer.Serialize(new { employeeId, fullName, username, roleId, isActive }),
                    ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<LoyaltySettingsRow> GetLoyaltySettingsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT EarnPercent, MoneyPerPoint
            FROM dbo.LoyaltySettings
            WHERE SettingsId = 1;
            """;

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("Chưa có cấu hình tích điểm. Hãy chạy migration 02.");

        return new LoyaltySettingsRow(reader.GetDecimal(0), reader.GetDecimal(1));
    }

    public async Task SaveLoyaltySettingsAsync(
        decimal earnPercent,
        decimal moneyPerPoint,
        string actorId,
        CancellationToken ct = default)
    {
        const string sql = """
            DECLARE @OldValue nvarchar(max);
            SELECT @OldValue = (SELECT EarnPercent, MoneyPerPoint
                                FROM dbo.LoyaltySettings
                                WHERE SettingsId = 1 FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);

            UPDATE dbo.LoyaltySettings
            SET EarnPercent = @EarnPercent,
                MoneyPerPoint = @MoneyPerPoint,
                UpdatedBy = @ActorId,
                UpdatedAt = SYSDATETIME()
            WHERE SettingsId = 1;

            IF @@ROWCOUNT = 0
                THROW 50010, N'Chưa có cấu hình tích điểm. Hãy chạy migration 02.', 1;

            INSERT dbo.AuditLog(EmployeeId, ActionName, EntityName, EntityId, OldValue, NewValue)
            VALUES
                (@ActorId, 'UPDATE_LOYALTY_SETTINGS', 'LoyaltySettings', '1', @OldValue,
                 (SELECT @EarnPercent AS EarnPercent, @MoneyPerPoint AS MoneyPerPoint
                  FOR JSON PATH, WITHOUT_ARRAY_WRAPPER));
            """;

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            await using var command = new SqlCommand(sql, connection, transaction);
            AddDecimal(command, "@EarnPercent", earnPercent, 6, 3);
            AddDecimal(command, "@MoneyPerPoint", moneyPerPoint, 18, 2);
            command.Parameters.Add("@ActorId", SqlDbType.VarChar, 20).Value = actorId;
            await command.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<List<VoucherRow>> GetVouchersAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT VoucherId, VoucherCode, Description, DiscountType, DiscountValue,
                   MinimumOrder, MaximumDiscount, UsageLimit, UsedCount,
                   StartDate, EndDate, IsActive
            FROM dbo.Voucher
            ORDER BY IsActive DESC, EndDate DESC, VoucherCode;
            """;

        var rows = new List<VoucherRow>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new VoucherRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetDecimal(4),
                reader.GetDecimal(5),
                reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7),
                reader.GetInt32(8),
                reader.GetDateTime(9),
                reader.GetDateTime(10),
                reader.GetBoolean(11)));
        }

        return rows;
    }

    public async Task AddVoucherAsync(
        VoucherRow voucher,
        string actorId,
        CancellationToken ct = default)
    {
        const string sql = """
            INSERT dbo.Voucher
                (VoucherId, VoucherCode, Description, DiscountType, DiscountValue,
                 MinimumOrder, MaximumDiscount, UsageLimit, StartDate, EndDate, IsActive)
            VALUES
                (@VoucherId, @VoucherCode, @Description, @DiscountType, @DiscountValue,
                 @MinimumOrder, @MaximumDiscount, @UsageLimit, @StartDate, @EndDate, 1);

            INSERT dbo.AuditLog(EmployeeId, ActionName, EntityName, EntityId, NewValue)
            VALUES
                (@ActorId, 'CREATE_VOUCHER', 'Voucher', @VoucherId,
                 (SELECT @VoucherCode AS VoucherCode, @DiscountType AS DiscountType,
                         @DiscountValue AS DiscountValue, @MinimumOrder AS MinimumOrder,
                         @StartDate AS StartDate, @EndDate AS EndDate
                  FOR JSON PATH, WITHOUT_ARRAY_WRAPPER));
            """;

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.Add("@VoucherId", SqlDbType.VarChar, 20).Value = voucher.VoucherId;
            command.Parameters.Add("@VoucherCode", SqlDbType.VarChar, 40).Value = voucher.VoucherCode;
            command.Parameters.Add("@Description", SqlDbType.NVarChar, 300).Value = Db(voucher.Description);
            command.Parameters.Add("@DiscountType", SqlDbType.VarChar, 10).Value = voucher.DiscountType;
            AddDecimal(command, "@DiscountValue", voucher.DiscountValue, 18, 2);
            AddDecimal(command, "@MinimumOrder", voucher.MinimumOrder, 18, 2);
            AddNullableDecimal(command, "@MaximumDiscount", voucher.MaximumDiscount);
            command.Parameters.Add("@UsageLimit", SqlDbType.Int).Value =
                (object?)voucher.UsageLimit ?? DBNull.Value;
            command.Parameters.Add("@StartDate", SqlDbType.DateTime2).Value = voucher.StartDate;
            command.Parameters.Add("@EndDate", SqlDbType.DateTime2).Value = voucher.EndDate;
            command.Parameters.Add("@ActorId", SqlDbType.VarChar, 20).Value = actorId;
            await command.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task SetVoucherActiveAsync(
        string voucherId,
        bool isActive,
        string actorId,
        CancellationToken ct = default)
    {
        const string sql = """
            DECLARE @OldActive bit;
            SELECT @OldActive = IsActive FROM dbo.Voucher WITH (UPDLOCK, HOLDLOCK)
            WHERE VoucherId = @VoucherId;
            IF @OldActive IS NULL
                THROW 50011, N'Không tìm thấy mã giảm giá.', 1;

            UPDATE dbo.Voucher SET IsActive = @IsActive WHERE VoucherId = @VoucherId;

            INSERT dbo.AuditLog(EmployeeId, ActionName, EntityName, EntityId, OldValue, NewValue)
            VALUES
                (@ActorId, 'UPDATE_VOUCHER_STATUS', 'Voucher', @VoucherId,
                 (SELECT @OldActive AS IsActive FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
                 (SELECT @IsActive AS IsActive FOR JSON PATH, WITHOUT_ARRAY_WRAPPER));
            """;

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.Add("@VoucherId", SqlDbType.VarChar, 20).Value = voucherId;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = isActive;
            command.Parameters.Add("@ActorId", SqlDbType.VarChar, 20).Value = actorId;
            await command.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task WriteAuditAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string actorId,
        string action,
        string entityId,
        string? oldValue,
        string newValue,
        CancellationToken ct)
    {
        const string sql = """
            INSERT dbo.AuditLog(EmployeeId, ActionName, EntityName, EntityId, OldValue, NewValue)
            VALUES (@ActorId, @Action, 'Employee', @EntityId, @OldValue, @NewValue);
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@ActorId", SqlDbType.VarChar, 20).Value = actorId;
        command.Parameters.Add("@Action", SqlDbType.VarChar, 100).Value = action;
        command.Parameters.Add("@EntityId", SqlDbType.VarChar, 50).Value = entityId;
        command.Parameters.Add("@OldValue", SqlDbType.NVarChar, -1).Value = Db(oldValue);
        command.Parameters.Add("@NewValue", SqlDbType.NVarChar, -1).Value = newValue;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static void AddEmployeeParameters(
        SqlCommand command,
        string employeeId,
        string fullName,
        string username,
        string roleId,
        bool isActive)
    {
        command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
        command.Parameters.Add("@FullName", SqlDbType.NVarChar, 150).Value = fullName;
        command.Parameters.Add("@Username", SqlDbType.VarChar, 50).Value = username;
        command.Parameters.Add("@RoleId", SqlDbType.VarChar, 20).Value = roleId;
        command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = isActive;
    }

    private static void AddDecimal(SqlCommand command, string name, decimal value, byte precision, byte scale)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = precision;
        parameter.Scale = scale;
        parameter.Value = value;
    }

    private static void AddNullableDecimal(SqlCommand command, string name, decimal? value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 2;
        parameter.Value = (object?)value ?? DBNull.Value;
    }

    private static object Db(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
}
