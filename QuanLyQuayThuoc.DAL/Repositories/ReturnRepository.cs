using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyQuayThuoc.DAL.Database;

namespace QuanLyQuayThuoc.DAL.Repositories;

public sealed record ReturnInvoiceRow(
    string InvoiceId,
    DateTime PaidAt,
    string? CustomerId,
    string CustomerName,
    string? CustomerPhone,
    string Status,
    decimal AmountPaid);

public sealed record ReturnLineRow(
    long InvoiceDetailId,
    string MedicineName,
    string BatchId,
    string BatchNumber,
    string UnitName,
    decimal OriginalQuantity,
    decimal ReturnedQuantity,
    decimal RemainingQuantity,
    DateTime ExpiryDate,
    bool IsWithinExpiry,
    string BatchStatus,
    decimal RemainingRefund);

public sealed record ReturnLineRequest(long InvoiceDetailId, decimal Quantity);

public sealed class ReturnProcessRequest
{
    public string InvoiceId { get; set; } = "";
    public string EmployeeId { get; set; } = "";
    public string RefundMethod { get; set; } = "";
    public string Reason { get; set; } = "";
    public bool SealedConfirmed { get; set; }
    public bool UndamagedConfirmed { get; set; }
    public bool WithinExpiryConfirmed { get; set; }
    public List<ReturnLineRequest> Lines { get; set; } = new();
}

public sealed record ReturnProcessResult(
    string ReturnId,
    string InvoiceId,
    string RefundMethod,
    decimal TotalRefund,
    int PointsRestored,
    int PointsRecovered);

public sealed class ReturnRepository
{
    public async Task<List<ReturnInvoiceRow>> SearchInvoicesAsync(
        string? keyword = null,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (100)
                i.InvoiceId, i.PaidAt, i.CustomerId,
                COALESCE(c.FullName, N'Khách lẻ'), c.Phone,
                i.Status, i.AmountPaid
            FROM dbo.Invoice i
            LEFT JOIN dbo.Customer c ON c.CustomerId = i.CustomerId
            WHERE i.Status IN ('PAID', 'RETURNED_PARTIAL')
              AND i.PaidAt >= DATEADD(day, -7, SYSDATETIME())
              AND i.PaidAt <= SYSDATETIME()
              AND
              (
                    @Keyword = N''
                    OR i.InvoiceId LIKE @Pattern
                    OR i.CustomerId LIKE @Pattern
                    OR c.FullName LIKE @Pattern
                    OR c.Phone LIKE @Pattern
              )
            ORDER BY i.PaidAt DESC, i.InvoiceId DESC;
            """;

        string value = (keyword ?? "").Trim();
        var rows = new List<ReturnInvoiceRow>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Keyword", SqlDbType.NVarChar, 100).Value = value;
        command.Parameters.Add("@Pattern", SqlDbType.NVarChar, 205).Value = $"%{value}%";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new ReturnInvoiceRow(
                reader.GetString(0),
                reader.GetDateTime(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetDecimal(6)));
        }

        return rows;
    }

    public async Task<List<ReturnLineRow>> GetRemainingLinesAsync(
        string invoiceId,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                d.InvoiceDetailId, m.MedicineName, d.BatchId, b.BatchNumber,
                d.UnitNameSnapshot, d.Quantity, d.ReturnedQuantity,
                d.Quantity - d.ReturnedQuantity, b.ExpiryDate,
                CONVERT(bit, CASE WHEN b.ExpiryDate >= CONVERT(date, SYSDATETIME())
                    THEN 1 ELSE 0 END),
                b.BatchStatus,
                CASE
                    WHEN d.LineTotal - d.DiscountAllocated
                         - COALESCE(returned.RefundedAmount, 0) < 0 THEN 0
                    ELSE d.LineTotal - d.DiscountAllocated
                         - COALESCE(returned.RefundedAmount, 0)
                END AS RemainingRefund
            FROM dbo.InvoiceDetail d
            INNER JOIN dbo.Medicine m ON m.MedicineId = d.MedicineId
            INNER JOIN dbo.MedicineBatch b ON b.BatchId = d.BatchId
            OUTER APPLY
            (
                SELECT SUM(rd.RefundAmount) AS RefundedAmount
                FROM dbo.ReturnDetail rd
                INNER JOIN dbo.ReturnReceipt rr ON rr.ReturnId = rd.ReturnId
                WHERE rd.InvoiceDetailId = d.InvoiceDetailId
                  AND rr.Status = 'COMPLETED'
            ) returned
            WHERE d.InvoiceId = @InvoiceId
              AND d.ReturnedQuantity < d.Quantity
            ORDER BY d.InvoiceDetailId;
            """;

        var rows = new List<ReturnLineRow>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new ReturnLineRow(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetDecimal(5),
                reader.GetDecimal(6),
                reader.GetDecimal(7),
                reader.GetDateTime(8),
                reader.GetBoolean(9),
                reader.GetString(10),
                reader.GetDecimal(11)));
        }

        return rows;
    }

    public async Task<ReturnProcessResult> ProcessReturnAsync(
        ReturnProcessRequest request,
        CancellationToken ct = default)
    {
        string returnId = $"TR{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        try
        {
            string? customerId;
            decimal amountPaid;
            const string invoiceSql = """
                SELECT CustomerId, AmountPaid
                FROM dbo.Invoice WITH (UPDLOCK, HOLDLOCK)
                WHERE InvoiceId = @InvoiceId
                  AND Status IN ('PAID', 'RETURNED_PARTIAL')
                  AND PaidAt >= DATEADD(day, -7, SYSDATETIME())
                  AND PaidAt <= SYSDATETIME();
                """;
            await using (var command = new SqlCommand(invoiceSql, connection, transaction))
            {
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = request.InvoiceId;
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException(
                        "Hóa đơn không còn đủ điều kiện trả hàng: chỉ nhận hóa đơn đã thanh toán trong 7 ngày.");
                customerId = reader.IsDBNull(0) ? null : reader.GetString(0);
                amountPaid = reader.GetDecimal(1);
            }

            string shiftId = await GetOpenShiftIdAsync(
                connection, transaction, request.EmployeeId, ct);

            int currentPoints = 0;
            long originalRedeemedPoints = 0;
            long originalEarnedPoints = 0;
            long previouslyRestoredPoints = 0;
            long previouslyRecoveredPoints = 0;
            decimal previouslyRefunded = 0;
            if (customerId is not null)
            {
                const string customerSql = """
                    SELECT LoyaltyPoints
                    FROM dbo.Customer WITH (UPDLOCK, HOLDLOCK)
                    WHERE CustomerId = @CustomerId;
                    """;
                await using (var command = new SqlCommand(customerSql, connection, transaction))
                {
                    command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = customerId;
                    object? value = await command.ExecuteScalarAsync(ct);
                    if (value is null or DBNull)
                        throw new InvalidOperationException("Không tìm thấy khách hàng của hóa đơn.");
                    currentPoints = Convert.ToInt32(value);
                }

                const string pointsSql = """
                    SELECT
                        COALESCE(SUM(CASE WHEN TransactionType = 'REDEEM'
                            THEN CONVERT(bigint, Points) ELSE 0 END), 0),
                        COALESCE(SUM(CASE WHEN TransactionType = 'EARN'
                            THEN CONVERT(bigint, Points) ELSE 0 END), 0)
                    FROM dbo.LoyaltyTransaction WITH (UPDLOCK, HOLDLOCK)
                    WHERE CustomerId = @CustomerId AND ReferenceCode = @InvoiceId;

                    SELECT
                        COALESCE(SUM(CASE WHEN lt.TransactionType = 'REFUND'
                            THEN CONVERT(bigint, lt.Points) ELSE 0 END), 0),
                        COALESCE(SUM(CASE WHEN lt.TransactionType = 'RECOVER'
                            THEN CONVERT(bigint, lt.Points) ELSE 0 END), 0)
                    FROM dbo.LoyaltyTransaction lt WITH (UPDLOCK, HOLDLOCK)
                    INNER JOIN dbo.ReturnReceipt rr ON rr.ReturnId = lt.ReferenceCode
                    WHERE rr.OriginalInvoiceId = @InvoiceId
                      AND rr.Status = 'COMPLETED'
                      AND lt.CustomerId = @CustomerId;

                    SELECT COALESCE(SUM(TotalRefund), 0)
                    FROM dbo.ReturnReceipt WITH (UPDLOCK, HOLDLOCK)
                    WHERE OriginalInvoiceId = @InvoiceId AND Status = 'COMPLETED';
                    """;
                await using var pointsCommand = new SqlCommand(pointsSql, connection, transaction);
                pointsCommand.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = customerId;
                pointsCommand.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = request.InvoiceId;
                await using var reader = await pointsCommand.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    originalRedeemedPoints = reader.GetInt64(0);
                    originalEarnedPoints = reader.GetInt64(1);
                }
                if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct))
                {
                    previouslyRestoredPoints = reader.GetInt64(0);
                    previouslyRecoveredPoints = reader.GetInt64(1);
                }
                if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct))
                    previouslyRefunded = reader.GetDecimal(0);
            }
            else
            {
                const string previousRefundSql = """
                    SELECT COALESCE(SUM(TotalRefund), 0)
                    FROM dbo.ReturnReceipt WITH (UPDLOCK, HOLDLOCK)
                    WHERE OriginalInvoiceId = @InvoiceId AND Status = 'COMPLETED';
                    """;
                await using var command = new SqlCommand(previousRefundSql, connection, transaction);
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = request.InvoiceId;
                previouslyRefunded = Convert.ToDecimal(await command.ExecuteScalarAsync(ct));
            }

            var processedLines = new List<ProcessedLine>(request.Lines.Count);
            decimal totalRefund = 0;
            foreach (ReturnLineRequest requested in request.Lines)
            {
                const string lineSql = """
                    SELECT d.Quantity, d.ReturnedQuantity, d.BatchId,
                           d.ConversionFactorSnapshot, d.LineTotal,
                           d.DiscountAllocated,
                           CONVERT(bit, CASE WHEN b.ExpiryDate >= CONVERT(date, SYSDATETIME())
                               THEN 1 ELSE 0 END),
                           b.BatchStatus,
                           COALESCE((
                               SELECT SUM(rd.RefundAmount)
                               FROM dbo.ReturnDetail rd
                               INNER JOIN dbo.ReturnReceipt rr ON rr.ReturnId = rd.ReturnId
                               WHERE rd.InvoiceDetailId = d.InvoiceDetailId
                                 AND rr.Status = 'COMPLETED'
                           ), 0)
                    FROM dbo.InvoiceDetail d WITH (UPDLOCK, HOLDLOCK)
                    INNER JOIN dbo.MedicineBatch b WITH (UPDLOCK, HOLDLOCK)
                        ON b.BatchId = d.BatchId
                    WHERE d.InvoiceDetailId = @InvoiceDetailId
                      AND d.InvoiceId = @InvoiceId;
                    """;

                decimal originalQuantity;
                decimal returnedQuantity;
                string batchId;
                decimal conversionFactor;
                decimal lineTotal;
                decimal discountAllocated;
                bool isWithinExpiry;
                string batchStatus;
                decimal previouslyRefundedLine;
                await using (var command = new SqlCommand(lineSql, connection, transaction))
                {
                    command.Parameters.Add("@InvoiceDetailId", SqlDbType.BigInt).Value =
                        requested.InvoiceDetailId;
                    command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = request.InvoiceId;
                    await using var reader = await command.ExecuteReaderAsync(ct);
                    if (!await reader.ReadAsync(ct))
                        throw new InvalidOperationException("Một trong các dòng trả hàng không thuộc hóa đơn đã chọn.");
                    originalQuantity = reader.GetDecimal(0);
                    returnedQuantity = reader.GetDecimal(1);
                    batchId = reader.GetString(2);
                    conversionFactor = reader.GetDecimal(3);
                    lineTotal = reader.GetDecimal(4);
                    discountAllocated = reader.GetDecimal(5);
                    isWithinExpiry = reader.GetBoolean(6);
                    batchStatus = reader.GetString(7);
                    previouslyRefundedLine = reader.GetDecimal(8);
                }

                decimal remainingQuantity = originalQuantity - returnedQuantity;
                if (requested.Quantity <= 0 || requested.Quantity > remainingQuantity)
                    throw new InvalidOperationException(
                        $"Số lượng trả vượt số lượng còn lại ({remainingQuantity:0.###}).");
                if (batchStatus != "ACTIVE" || !isWithinExpiry)
                    throw new InvalidOperationException(
                        $"Lô {batchId} đã hết hạn hoặc không còn hoạt động, không thể nhập lại kho.");

                decimal paidLineAmount = Math.Max(0, lineTotal - discountAllocated);
                decimal remainingLineRefund = Math.Max(0, paidLineAmount - previouslyRefundedLine);
                decimal lineRefund = requested.Quantity == remainingQuantity
                    ? remainingLineRefund
                    : Math.Min(
                        remainingLineRefund,
                        decimal.Round(
                            paidLineAmount * requested.Quantity / originalQuantity,
                            2,
                            MidpointRounding.AwayFromZero));

                totalRefund += lineRefund;
                processedLines.Add(new ProcessedLine(
                    requested.InvoiceDetailId,
                    batchId,
                    requested.Quantity,
                    conversionFactor,
                    paidLineAmount,
                    lineRefund));
            }

            int pointsRestored = 0;
            int pointsRecovered = 0;
            if (customerId is not null && amountPaid > 0)
            {
                decimal cumulativeShare = Math.Min(1m, (previouslyRefunded + totalRefund) / amountPaid);
                long targetRestored = decimal.ToInt64(decimal.Round(
                    originalRedeemedPoints * cumulativeShare, 0, MidpointRounding.AwayFromZero));
                long targetRecovered = decimal.ToInt64(decimal.Round(
                    originalEarnedPoints * cumulativeShare, 0, MidpointRounding.AwayFromZero));
                long restoreDue = Math.Max(0, targetRestored - previouslyRestoredPoints);
                long recoverDue = Math.Max(0, targetRecovered - previouslyRecoveredPoints);
                pointsRestored = checked((int)restoreDue);
                pointsRecovered = checked((int)Math.Min(recoverDue, (long)currentPoints + pointsRestored));
            }

            const string insertReturnSql = """
                INSERT dbo.ReturnReceipt
                    (ReturnId, OriginalInvoiceId, ShiftId, EmployeeId,
                     ReturnDate, Reason, RefundMethod, TotalRefund, PointsRecovered)
                VALUES
                    (@ReturnId, @InvoiceId, @ShiftId, @EmployeeId,
                     SYSDATETIME(), @Reason, @RefundMethod, @TotalRefund, @PointsRecovered);
                """;
            await using (var command = new SqlCommand(insertReturnSql, connection, transaction))
            {
                command.Parameters.Add("@ReturnId", SqlDbType.VarChar, 20).Value = returnId;
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = request.InvoiceId;
                command.Parameters.Add("@ShiftId", SqlDbType.VarChar, 20).Value = shiftId;
                command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = request.EmployeeId;
                command.Parameters.Add("@Reason", SqlDbType.NVarChar, 500).Value = request.Reason;
                command.Parameters.Add("@RefundMethod", SqlDbType.VarChar, 20).Value = request.RefundMethod;
                AddMoney(command, "@TotalRefund", totalRefund);
                command.Parameters.Add("@PointsRecovered", SqlDbType.Int).Value = pointsRecovered;
                await command.ExecuteNonQueryAsync(ct);
            }

            foreach (ProcessedLine line in processedLines)
            {
                const string insertDetailSql = """
                    INSERT dbo.ReturnDetail
                        (ReturnId, InvoiceDetailId, BatchId, Quantity, RefundUnitPrice, RefundAmount)
                    VALUES
                        (@ReturnId, @InvoiceDetailId, @BatchId, @Quantity, @RefundUnitPrice, @RefundAmount);
                    """;
                await using (var command = new SqlCommand(insertDetailSql, connection, transaction))
                {
                    command.Parameters.Add("@ReturnId", SqlDbType.VarChar, 20).Value = returnId;
                    command.Parameters.Add("@InvoiceDetailId", SqlDbType.BigInt).Value = line.InvoiceDetailId;
                    command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = line.BatchId;
                    AddQuantity(command, "@Quantity", line.Quantity);
                    AddMoney(command, "@RefundUnitPrice", line.Quantity == 0 ? 0 : line.RefundAmount / line.Quantity);
                    AddMoney(command, "@RefundAmount", line.RefundAmount);
                    await command.ExecuteNonQueryAsync(ct);
                }

                const string updateInvoiceLineSql = """
                    UPDATE dbo.InvoiceDetail
                    SET ReturnedQuantity = ReturnedQuantity + @Quantity
                    WHERE InvoiceDetailId = @InvoiceDetailId
                      AND InvoiceId = @InvoiceId
                      AND ReturnedQuantity + @Quantity <= Quantity;
                    """;
                await using (var command = new SqlCommand(updateInvoiceLineSql, connection, transaction))
                {
                    command.Parameters.Add("@InvoiceDetailId", SqlDbType.BigInt).Value = line.InvoiceDetailId;
                    command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = request.InvoiceId;
                    AddQuantity(command, "@Quantity", line.Quantity);
                    if (await command.ExecuteNonQueryAsync(ct) != 1)
                        throw new InvalidOperationException("Không thể cập nhật số lượng đã trả của hóa đơn.");
                }

                const string updateStockSql = """
                    UPDATE dbo.MedicineBatch
                    SET PhysicalStock = PhysicalStock + @QuantityBase
                    WHERE BatchId = @BatchId
                      AND BatchStatus = 'ACTIVE'
                      AND ExpiryDate >= CONVERT(date, SYSDATETIME());
                    """;
                await using (var command = new SqlCommand(updateStockSql, connection, transaction))
                {
                    AddQuantity(command, "@QuantityBase", line.Quantity * line.ConversionFactor);
                    command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = line.BatchId;
                    if (await command.ExecuteNonQueryAsync(ct) != 1)
                        throw new InvalidOperationException("Không thể hoàn tồn theo đúng lô thuốc.");
                }

                const string stockTransactionSql = """
                    INSERT dbo.StockTransaction
                        (BatchId, TransactionType, QuantityChange, ReferenceType,
                         ReferenceCode, CreatedBy, Note)
                    VALUES
                        (@BatchId, 'RETURN', @QuantityBase, 'RETURN', @ReturnId,
                         @EmployeeId, N'Hoàn tồn theo đúng lô của hóa đơn gốc');
                    """;
                await using (var command = new SqlCommand(stockTransactionSql, connection, transaction))
                {
                    AddQuantity(command, "@QuantityBase", line.Quantity * line.ConversionFactor);
                    command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = line.BatchId;
                    command.Parameters.Add("@ReturnId", SqlDbType.VarChar, 20).Value = returnId;
                    command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = request.EmployeeId;
                    await command.ExecuteNonQueryAsync(ct);
                }
            }

            if (customerId is not null && (pointsRestored > 0 || pointsRecovered > 0))
            {
                const string updateCustomerSql = """
                    UPDATE dbo.Customer
                    SET LoyaltyPoints = LoyaltyPoints + @PointsRestored - @PointsRecovered
                    WHERE CustomerId = @CustomerId
                      AND LoyaltyPoints + @PointsRestored >= @PointsRecovered;
                    """;
                await using (var command = new SqlCommand(updateCustomerSql, connection, transaction))
                {
                    command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = customerId;
                    command.Parameters.Add("@PointsRestored", SqlDbType.Int).Value = pointsRestored;
                    command.Parameters.Add("@PointsRecovered", SqlDbType.Int).Value = pointsRecovered;
                    if (await command.ExecuteNonQueryAsync(ct) != 1)
                        throw new InvalidOperationException("Không thể cập nhật điểm khách hàng an toàn.");
                }

                await AddLoyaltyTransactionAsync(
                    connection, transaction, customerId, "REFUND", pointsRestored, returnId,
                    $"Hoàn điểm đã dùng do trả hàng hóa đơn {request.InvoiceId}", ct);
                await AddLoyaltyTransactionAsync(
                    connection, transaction, customerId, "RECOVER", pointsRecovered, returnId,
                    $"Thu hồi điểm thưởng do trả hàng hóa đơn {request.InvoiceId}", ct);
            }

            if (request.RefundMethod == "CASH" && totalRefund > 0)
            {
                const string cashSql = """
                    UPDATE dbo.WorkShift
                    SET CashRefunds = CashRefunds + @Refund
                    WHERE ShiftId = @ShiftId AND EmployeeId = @EmployeeId AND Status = 'OPEN';
                    """;
                await using var command = new SqlCommand(cashSql, connection, transaction);
                AddMoney(command, "@Refund", totalRefund);
                command.Parameters.Add("@ShiftId", SqlDbType.VarChar, 20).Value = shiftId;
                command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = request.EmployeeId;
                if (await command.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("Không thể ghi nhận hoàn tiền mặt vào ca đang mở.");
            }

            const string invoiceStatusSql = """
                UPDATE dbo.Invoice
                SET Status = CASE WHEN NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.InvoiceDetail
                    WHERE InvoiceId = @InvoiceId AND ReturnedQuantity < Quantity
                )
                THEN 'RETURNED_FULL' ELSE 'RETURNED_PARTIAL' END
                WHERE InvoiceId = @InvoiceId;
                """;
            await using (var command = new SqlCommand(invoiceStatusSql, connection, transaction))
            {
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = request.InvoiceId;
                await command.ExecuteNonQueryAsync(ct);
            }

            string auditValue = JsonSerializer.Serialize(new
            {
                request.InvoiceId,
                request.RefundMethod,
                totalRefund,
                lines = processedLines.Select(x => new
                {
                    x.InvoiceDetailId,
                    x.BatchId,
                    x.Quantity,
                    x.RefundAmount
                }),
                sealedConfirmed = request.SealedConfirmed,
                undamagedConfirmed = request.UndamagedConfirmed,
                withinExpiryConfirmed = request.WithinExpiryConfirmed,
                pointsRestored,
                pointsRecovered
            });
            const string auditSql = """
                INSERT dbo.AuditLog
                    (EmployeeId, ActionName, EntityName, EntityId, NewValue, Reason)
                VALUES
                    (@EmployeeId, 'PROCESS_RETURN', 'ReturnReceipt', @ReturnId, @NewValue, @Reason);
                """;
            await using (var command = new SqlCommand(auditSql, connection, transaction))
            {
                command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = request.EmployeeId;
                command.Parameters.Add("@ReturnId", SqlDbType.VarChar, 50).Value = returnId;
                command.Parameters.Add("@NewValue", SqlDbType.NVarChar, -1).Value = auditValue;
                command.Parameters.Add("@Reason", SqlDbType.NVarChar, 500).Value = request.Reason;
                await command.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return new ReturnProcessResult(
                returnId, request.InvoiceId, request.RefundMethod,
                totalRefund, pointsRestored, pointsRecovered);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task<string> GetOpenShiftIdAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string employeeId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT TOP (1) ws.ShiftId
            FROM dbo.WorkShift ws WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.Employee e WITH (UPDLOCK, HOLDLOCK) ON e.EmployeeId = ws.EmployeeId
            WHERE ws.EmployeeId = @EmployeeId
              AND ws.Status = 'OPEN'
              AND e.IsActive = 1
              AND e.RoleId IN ('ADMIN', 'PHARMACIST')
            ORDER BY ws.OpenedAt DESC;
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
        object? value = await command.ExecuteScalarAsync(ct);
        return value is null or DBNull
            ? throw new InvalidOperationException("Nhân viên không có ca làm việc đang mở để xử lý trả hàng.")
            : Convert.ToString(value)!;
    }

    private static async Task AddLoyaltyTransactionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string customerId,
        string type,
        int points,
        string returnId,
        string note,
        CancellationToken ct)
    {
        if (points == 0)
            return;

        const string sql = """
            INSERT dbo.LoyaltyTransaction
                (CustomerId, TransactionType, Points, ReferenceCode, Note)
            VALUES
                (@CustomerId, @Type, @Points, @ReturnId, @Note);
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = customerId;
        command.Parameters.Add("@Type", SqlDbType.VarChar, 20).Value = type;
        command.Parameters.Add("@Points", SqlDbType.Int).Value = points;
        command.Parameters.Add("@ReturnId", SqlDbType.VarChar, 20).Value = returnId;
        command.Parameters.Add("@Note", SqlDbType.NVarChar, 300).Value = note;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static void AddQuantity(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 3;
        parameter.Value = value;
    }

    private static void AddMoney(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 2;
        parameter.Value = value;
    }

    private sealed record ProcessedLine(
        long InvoiceDetailId,
        string BatchId,
        decimal Quantity,
        decimal ConversionFactor,
        decimal PaidLineAmount,
        decimal RefundAmount);
}
