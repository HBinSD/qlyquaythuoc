using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyQuayThuoc.DAL.Database;

namespace QuanLyQuayThuoc.DAL.Repositories;

public sealed record OpenShiftRow(
    string ShiftId,
    string EmployeeId,
    string EmployeeName,
    decimal OpeningCash,
    DateTime OpenedAt,
    string? OpeningNote);

public sealed record ShiftCloseSummaryRow(
    string ShiftId,
    string EmployeeId,
    string EmployeeName,
    DateTime OpenedAt,
    decimal OpeningCash,
    decimal CashSales,
    decimal TransferSales,
    decimal CashRefunds,
    decimal TheoreticalCash,
    int InvoiceCount,
    int PendingInvoiceCount);

public sealed record ClosedShiftRow(
    string ShiftId,
    string EmployeeId,
    string EmployeeName,
    DateTime OpenedAt,
    DateTime ClosedAt,
    decimal OpeningCash,
    decimal CashSales,
    decimal TransferSales,
    decimal CashRefunds,
    decimal TheoreticalCash,
    decimal ActualCash,
    decimal DifferenceAmount,
    string? DifferenceNote,
    int InvoiceCount);

public sealed class ShiftRepository
{
    public async Task<OpenShiftRow?> GetOpenShiftAsync(
        string employeeId,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (1) ws.ShiftId, ws.EmployeeId, e.FullName,
                   ws.OpeningCash, ws.OpenedAt, ws.OpeningNote
            FROM dbo.WorkShift ws
            INNER JOIN dbo.Employee e ON e.EmployeeId = ws.EmployeeId
            WHERE ws.EmployeeId = @EmployeeId
              AND ws.Status = 'OPEN'
              AND e.IsActive = 1
            ORDER BY ws.OpenedAt DESC;
            """;

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new OpenShiftRow(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.GetDateTime(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));
    }

    public async Task<decimal> GetSuggestedOpeningCashAsync(
        string employeeId,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (1) COALESCE(CashHandover, ActualCash, 0)
            FROM dbo.WorkShift
            WHERE EmployeeId = @EmployeeId AND Status = 'CLOSED'
            ORDER BY ClosedAt DESC;
            """;

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
        object? value = await command.ExecuteScalarAsync(ct);
        return value is null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
    }

    public async Task<ShiftCloseSummaryRow> GetCloseSummaryAsync(
        string employeeId,
        string shiftId,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT ws.ShiftId, ws.EmployeeId, e.FullName, ws.OpenedAt,
                   ws.OpeningCash, ws.CashSales, ws.TransferSales, ws.CashRefunds,
                   ws.TheoreticalCash,
                   (SELECT COUNT(1)
                    FROM dbo.Invoice i
                    WHERE i.ShiftId = ws.ShiftId
                      AND i.Status IN ('PAID','RETURNED_PARTIAL','RETURNED_FULL')),
                   (SELECT COUNT(1)
                    FROM dbo.Invoice i
                    WHERE i.ShiftId = ws.ShiftId AND i.Status = 'PENDING')
            FROM dbo.WorkShift ws
            INNER JOIN dbo.Employee e ON e.EmployeeId = ws.EmployeeId
            WHERE ws.ShiftId = @ShiftId
              AND ws.EmployeeId = @EmployeeId
              AND ws.Status = 'OPEN'
              AND e.IsActive = 1;
            """;

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@ShiftId", SqlDbType.VarChar, 20).Value = shiftId;
        command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("Không tìm thấy ca đang mở của nhân viên này.");

        return new ShiftCloseSummaryRow(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDateTime(3),
            reader.GetDecimal(4),
            reader.GetDecimal(5),
            reader.GetDecimal(6),
            reader.GetDecimal(7),
            reader.GetDecimal(8),
            reader.GetInt32(9),
            reader.GetInt32(10));
    }

    public async Task<ClosedShiftRow> CloseShiftAsync(
        string employeeId,
        string shiftId,
        decimal actualCash,
        string? differenceNote,
        CancellationToken ct = default)
    {
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        try
        {
            string employeeName;
            DateTime openedAt;
            decimal openingCash;
            decimal cashSales;
            decimal transferSales;
            decimal cashRefunds;
            decimal theoreticalCash;
            int invoiceCount;

            const string shiftSql = """
                SELECT e.FullName, ws.OpenedAt, ws.OpeningCash, ws.CashSales,
                       ws.TransferSales, ws.CashRefunds, ws.TheoreticalCash,
                       (SELECT COUNT(1)
                        FROM dbo.Invoice i
                        WHERE i.ShiftId = ws.ShiftId
                          AND i.Status IN ('PAID','RETURNED_PARTIAL','RETURNED_FULL'))
                FROM dbo.WorkShift ws WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.Employee e ON e.EmployeeId = ws.EmployeeId
                WHERE ws.ShiftId = @ShiftId
                  AND ws.EmployeeId = @EmployeeId
                  AND ws.Status = 'OPEN'
                  AND e.IsActive = 1;
                """;
            await using (var command = new SqlCommand(shiftSql, connection, transaction))
            {
                command.Parameters.Add("@ShiftId", SqlDbType.VarChar, 20).Value = shiftId;
                command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Không tìm thấy ca đang mở của nhân viên này.");

                employeeName = reader.GetString(0);
                openedAt = reader.GetDateTime(1);
                openingCash = reader.GetDecimal(2);
                cashSales = reader.GetDecimal(3);
                transferSales = reader.GetDecimal(4);
                cashRefunds = reader.GetDecimal(5);
                theoreticalCash = reader.GetDecimal(6);
                invoiceCount = reader.GetInt32(7);
            }

            const string pendingSql = """
                SELECT COUNT(1)
                FROM dbo.Invoice WITH (UPDLOCK, HOLDLOCK)
                WHERE ShiftId = @ShiftId AND Status = 'PENDING';
                """;
            int pendingCount;
            await using (var command = new SqlCommand(pendingSql, connection, transaction))
            {
                command.Parameters.Add("@ShiftId", SqlDbType.VarChar, 20).Value = shiftId;
                pendingCount = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
            }

            if (pendingCount > 0)
                throw new InvalidOperationException(
                    $"Không thể chốt ca vì còn {pendingCount} hóa đơn chờ thanh toán. Hãy thanh toán hoặc hủy các hóa đơn này trước.");

            decimal difference = actualCash - theoreticalCash;
            if (difference != 0 && string.IsNullOrWhiteSpace(differenceNote))
                throw new InvalidOperationException("Bắt buộc nhập lý do khi tiền thực tế lệch tiền lý thuyết.");

            const string closeSql = """
                UPDATE dbo.WorkShift
                SET ActualCash = @ActualCash,
                    CashHandover = @ActualCash,
                    DifferenceNote = @DifferenceNote,
                    ClosedAt = SYSDATETIME(),
                    Status = 'CLOSED'
                OUTPUT INSERTED.ClosedAt, INSERTED.DifferenceAmount
                WHERE ShiftId = @ShiftId
                  AND EmployeeId = @EmployeeId
                  AND Status = 'OPEN';
                """;
            DateTime closedAt;
            await using (var command = new SqlCommand(closeSql, connection, transaction))
            {
                AddCashParameter(command, "@ActualCash", actualCash);
                command.Parameters.Add("@DifferenceNote", SqlDbType.NVarChar, 500).Value =
                    string.IsNullOrWhiteSpace(differenceNote) ? DBNull.Value : differenceNote.Trim();
                command.Parameters.Add("@ShiftId", SqlDbType.VarChar, 20).Value = shiftId;
                command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Trạng thái ca đã thay đổi; hãy tải lại màn hình.");

                closedAt = reader.GetDateTime(0);
                difference = reader.GetDecimal(1);
            }

            string newValue = JsonSerializer.Serialize(new
            {
                shiftId,
                openedAt,
                closedAt,
                openingCash,
                cashSales,
                transferSales,
                cashRefunds,
                theoreticalCash,
                actualCash,
                difference,
                differenceNote
            });
            const string auditSql = """
                INSERT dbo.AuditLog(EmployeeId, ActionName, EntityName, EntityId, NewValue)
                VALUES (@EmployeeId, 'CLOSE_SHIFT', 'WorkShift', @ShiftId, @NewValue);
                """;
            await using (var command = new SqlCommand(auditSql, connection, transaction))
            {
                command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
                command.Parameters.Add("@ShiftId", SqlDbType.VarChar, 50).Value = shiftId;
                command.Parameters.Add("@NewValue", SqlDbType.NVarChar, -1).Value = newValue;
                await command.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return new ClosedShiftRow(
                shiftId,
                employeeId,
                employeeName,
                openedAt,
                closedAt,
                openingCash,
                cashSales,
                transferSales,
                cashRefunds,
                theoreticalCash,
                actualCash,
                difference,
                string.IsNullOrWhiteSpace(differenceNote) ? null : differenceNote.Trim(),
                invoiceCount);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<OpenShiftRow> OpenShiftAsync(
        string employeeId,
        decimal openingCash,
        string? openingNote,
        CancellationToken ct = default)
    {
        string shiftId = $"CA{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        try
        {
            string employeeName;
            string roleId;
            bool isActive;
            const string employeeSql = """
                SELECT FullName, RoleId, IsActive
                FROM dbo.Employee WITH (UPDLOCK, HOLDLOCK)
                WHERE EmployeeId = @EmployeeId;
                """;
            await using (var command = new SqlCommand(employeeSql, connection, transaction))
            {
                command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Không tìm thấy nhân viên đang đăng nhập.");

                employeeName = reader.GetString(0);
                roleId = reader.GetString(1);
                isActive = reader.GetBoolean(2);
            }

            if (!isActive)
                throw new InvalidOperationException("Tài khoản nhân viên đã bị khóa, không thể mở ca.");
            if (roleId is not ("ADMIN" or "PHARMACIST"))
                throw new UnauthorizedAccessException("Chỉ Dược sĩ hoặc Quản lý mới được mở ca bán hàng.");

            const string existingSql = """
                SELECT COUNT(1)
                FROM dbo.WorkShift WITH (UPDLOCK, HOLDLOCK)
                WHERE EmployeeId = @EmployeeId AND Status = 'OPEN';
                """;
            await using (var command = new SqlCommand(existingSql, connection, transaction))
            {
                command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
                if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 0)
                    throw new InvalidOperationException("Nhân viên đã có một ca đang mở.");
            }

            const string insertSql = """
                INSERT dbo.WorkShift
                    (ShiftId, EmployeeId, OpenedAt, OpeningCash, OpeningNote, Status)
                OUTPUT INSERTED.OpenedAt
                VALUES
                    (@ShiftId, @EmployeeId, SYSDATETIME(), @OpeningCash, @OpeningNote, 'OPEN');
                """;
            DateTime openedAt;
            await using (var command = new SqlCommand(insertSql, connection, transaction))
            {
                command.Parameters.Add("@ShiftId", SqlDbType.VarChar, 20).Value = shiftId;
                command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
                var cash = command.Parameters.Add("@OpeningCash", SqlDbType.Decimal);
                cash.Precision = 18;
                cash.Scale = 2;
                cash.Value = openingCash;
                command.Parameters.Add("@OpeningNote", SqlDbType.NVarChar, 500).Value =
                    string.IsNullOrWhiteSpace(openingNote) ? DBNull.Value : openingNote.Trim();
                openedAt = Convert.ToDateTime(await command.ExecuteScalarAsync(ct));
            }

            string newValue = JsonSerializer.Serialize(new
            {
                shiftId,
                employeeId,
                openedAt,
                openingCash,
                openingNote
            });
            const string auditSql = """
                INSERT dbo.AuditLog(EmployeeId, ActionName, EntityName, EntityId, NewValue)
                VALUES (@EmployeeId, 'OPEN_SHIFT', 'WorkShift', @ShiftId, @NewValue);
                """;
            await using (var command = new SqlCommand(auditSql, connection, transaction))
            {
                command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
                command.Parameters.Add("@ShiftId", SqlDbType.VarChar, 50).Value = shiftId;
                command.Parameters.Add("@NewValue", SqlDbType.NVarChar, -1).Value = newValue;
                await command.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return new OpenShiftRow(
                shiftId,
                employeeId,
                employeeName,
                openingCash,
                openedAt,
                openingNote);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            await transaction.RollbackAsync(ct);
            throw new InvalidOperationException("Nhân viên đã có một ca đang mở.", ex);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static void AddCashParameter(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 2;
        parameter.Value = value;
    }
}
