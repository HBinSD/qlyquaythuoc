using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyQuayThuoc.DAL.Database;

namespace QuanLyQuayThuoc.DAL.Repositories;

public sealed record InvoiceAdminRow(
    string InvoiceId,
    DateTime CreatedAt,
    DateTime? PaidAt,
    string ShiftId,
    string EmployeeId,
    string EmployeeName,
    string? CustomerId,
    string CustomerName,
    string? CustomerPhone,
    string Status,
    decimal Subtotal,
    decimal PointDiscount,
    decimal VoucherDiscount,
    decimal TotalAmount,
    decimal AmountPaid);

public sealed record InvoicePaymentRow(string PaymentMethod, decimal Amount);

public sealed record InvoiceDetailRow(
    string MedicineName,
    string BatchId,
    string BatchNumber,
    string UnitName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record PendingStocktakeRow(
    string StocktakeId,
    string CreatedBy,
    string EmployeeName,
    DateTime CreatedAt,
    string? Note);

public sealed record StocktakeDetailRow(
    long StocktakeDetailId,
    string BatchId,
    string MedicineId,
    string MedicineName,
    string BatchNumber,
    decimal SystemQuantity,
    decimal ActualQuantity,
    decimal DifferenceQuantity,
    string? DifferenceReason);

public sealed record PendingDisposalRow(
    string DisposalId,
    string CreatedBy,
    string EmployeeName,
    DateTime CreatedAt,
    string Reason,
    decimal TotalLoss);

public sealed record DisposalDetailRow(
    long DisposalDetailId,
    string BatchId,
    string MedicineId,
    string MedicineName,
    string BatchNumber,
    DateTime ExpiryDate,
    decimal PhysicalStock,
    decimal Quantity,
    decimal PurchasePriceBase,
    decimal LossAmount);

public sealed record InvoiceCancellationResult(
    string InvoiceId,
    decimal TotalRefund,
    decimal CashRefund,
    int PointsRestored,
    int PointsRecovered,
    int BatchesRestocked);

public sealed class AdminOperationsRepository
{
    public async Task<List<InvoiceAdminRow>> SearchInvoicesAsync(
        string? keyword = null,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (100)
                i.InvoiceId, i.CreatedAt, i.PaidAt, i.ShiftId, i.EmployeeId,
                e.FullName, i.CustomerId, COALESCE(c.FullName, N'Khách lẻ'),
                c.Phone, i.Status, i.Subtotal, i.PointDiscount,
                i.VoucherDiscount, i.TotalAmount, i.AmountPaid
            FROM dbo.Invoice i
            INNER JOIN dbo.Employee e ON e.EmployeeId = i.EmployeeId
            LEFT JOIN dbo.Customer c ON c.CustomerId = i.CustomerId
            WHERE @Keyword = N''
               OR i.InvoiceId LIKE @Pattern
               OR i.EmployeeId LIKE @Pattern
               OR e.FullName LIKE @Pattern
               OR c.FullName LIKE @Pattern
               OR c.Phone LIKE @Pattern
            ORDER BY i.CreatedAt DESC, i.InvoiceId DESC;
            """;

        string value = (keyword ?? "").Trim();
        var rows = new List<InvoiceAdminRow>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Keyword", SqlDbType.NVarChar, 100).Value = value;
        command.Parameters.Add("@Pattern", SqlDbType.NVarChar, 205).Value = $"%{value}%";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new InvoiceAdminRow(
                reader.GetString(0),
                reader.GetDateTime(1),
                reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetString(9),
                reader.GetDecimal(10),
                reader.GetDecimal(11),
                reader.GetDecimal(12),
                reader.GetDecimal(13),
                reader.GetDecimal(14)));
        }

        return rows;
    }

    public async Task<List<InvoicePaymentRow>> GetInvoicePaymentsAsync(
        string invoiceId,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT PaymentMethod, SUM(Amount) AS Amount
            FROM dbo.InvoicePayment
            WHERE InvoiceId = @InvoiceId
            GROUP BY PaymentMethod
            ORDER BY PaymentMethod;
            """;

        var rows = new List<InvoicePaymentRow>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new InvoicePaymentRow(reader.GetString(0), reader.GetDecimal(1)));
        return rows;
    }

    public async Task<List<InvoiceDetailRow>> GetInvoiceDetailsAsync(
        string invoiceId,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT m.MedicineName, d.BatchId, b.BatchNumber, d.UnitNameSnapshot,
                   d.Quantity, d.UnitPrice, d.LineTotal
            FROM dbo.InvoiceDetail d
            INNER JOIN dbo.Medicine m ON m.MedicineId = d.MedicineId
            INNER JOIN dbo.MedicineBatch b ON b.BatchId = d.BatchId
            WHERE d.InvoiceId = @InvoiceId
            ORDER BY d.InvoiceDetailId;
            """;

        var rows = new List<InvoiceDetailRow>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new InvoiceDetailRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetDecimal(4),
                reader.GetDecimal(5),
                reader.GetDecimal(6)));
        }

        return rows;
    }

    public async Task<InvoiceCancellationResult> CancelPaidInvoiceAsync(
        string invoiceId,
        string reason,
        string actorId,
        CancellationToken ct = default)
    {
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        try
        {
            string status;
            string shiftId;
            string? customerId;
            string? prescriptionId;
            decimal oldAmount;

            const string invoiceSql = """
                SELECT Status, ShiftId, CustomerId, PrescriptionId, AmountPaid
                FROM dbo.Invoice WITH (UPDLOCK, HOLDLOCK)
                WHERE InvoiceId = @InvoiceId;
                """;
            await using (var command = new SqlCommand(invoiceSql, connection, transaction))
            {
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Không tìm thấy hóa đơn.");

                status = reader.GetString(0);
                shiftId = reader.GetString(1);
                customerId = reader.IsDBNull(2) ? null : reader.GetString(2);
                prescriptionId = reader.IsDBNull(3) ? null : reader.GetString(3);
                oldAmount = reader.GetDecimal(4);
            }

            if (status != "PAID")
                throw new InvalidOperationException("Chỉ được hủy hóa đơn đang ở trạng thái Đã thanh toán.");

            decimal cashRefund = 0;
            decimal nonCashRefund = 0;
            const string paymentSql = """
                SELECT
                    COALESCE(SUM(CASE WHEN PaymentMethod = 'CASH' THEN Amount ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN PaymentMethod IN ('TRANSFER','QR') THEN Amount ELSE 0 END), 0)
                FROM dbo.InvoicePayment WITH (UPDLOCK, HOLDLOCK)
                WHERE InvoiceId = @InvoiceId;
                """;
            await using (var command = new SqlCommand(paymentSql, connection, transaction))
            {
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    cashRefund = reader.GetDecimal(0);
                    nonCashRefund = reader.GetDecimal(1);
                }
            }

            if (cashRefund + nonCashRefund != oldAmount)
                throw new InvalidOperationException("Tổng thanh toán không khớp số tiền hóa đơn; không thể hủy an toàn.");

            var batches = new List<(string BatchId, decimal Quantity)>();
            const string detailSql = """
                SELECT BatchId, SUM(QuantityBase)
                FROM dbo.InvoiceDetail WITH (UPDLOCK, HOLDLOCK)
                WHERE InvoiceId = @InvoiceId
                GROUP BY BatchId;
                """;
            await using (var command = new SqlCommand(detailSql, connection, transaction))
            {
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    batches.Add((reader.GetString(0), reader.GetDecimal(1)));
            }

            if (batches.Count == 0)
                throw new InvalidOperationException("Hóa đơn không có chi tiết thuốc để hoàn tồn.");

            foreach (var batch in batches)
            {
                const string restockSql = """
                    UPDATE dbo.MedicineBatch
                    SET PhysicalStock = PhysicalStock + @Quantity
                    WHERE BatchId = @BatchId;
                    """;
                await using (var command = new SqlCommand(restockSql, connection, transaction))
                {
                    AddQuantity(command, "@Quantity", batch.Quantity);
                    command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = batch.BatchId;
                    if (await command.ExecuteNonQueryAsync(ct) != 1)
                        throw new InvalidOperationException($"Không tìm thấy lô {batch.BatchId} để hoàn tồn.");
                }

                const string movementSql = """
                    INSERT dbo.StockTransaction
                        (BatchId, TransactionType, QuantityChange, ReferenceType,
                         ReferenceCode, CreatedBy, Note)
                    VALUES
                        (@BatchId, 'CANCEL_SALE', @Quantity, 'INVOICE', @InvoiceId,
                         @ActorId, N'Hoàn tồn do quản lý hủy hóa đơn đã thanh toán');
                    """;
                await using (var command = new SqlCommand(movementSql, connection, transaction))
                {
                    command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = batch.BatchId;
                    AddQuantity(command, "@Quantity", batch.Quantity);
                    command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                    command.Parameters.Add("@ActorId", SqlDbType.VarChar, 20).Value = actorId;
                    await command.ExecuteNonQueryAsync(ct);
                }
            }

            int pointsRestored = 0;
            int pointsRecovered = 0;
            if (customerId is not null)
            {
                const string pointsSql = """
                    SELECT
                        COALESCE(SUM(CASE WHEN TransactionType = 'REDEEM' THEN Points ELSE 0 END), 0),
                        COALESCE(SUM(CASE WHEN TransactionType = 'EARN' THEN Points ELSE 0 END), 0)
                    FROM dbo.LoyaltyTransaction WITH (UPDLOCK, HOLDLOCK)
                    WHERE CustomerId = @CustomerId AND ReferenceCode = @InvoiceId;
                    """;
                await using (var command = new SqlCommand(pointsSql, connection, transaction))
                {
                    command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = customerId;
                    command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                    await using var reader = await command.ExecuteReaderAsync(ct);
                    if (await reader.ReadAsync(ct))
                    {
                        pointsRestored = reader.GetInt32(0);
                        pointsRecovered = reader.GetInt32(1);
                    }
                }

                if (pointsRestored > 0 || pointsRecovered > 0)
                {
                    const string updatePointsSql = """
                        UPDATE dbo.Customer
                        SET LoyaltyPoints = LoyaltyPoints + @PointsRestored - @PointsRecovered
                        WHERE CustomerId = @CustomerId
                          AND LoyaltyPoints + @PointsRestored >= @PointsRecovered;
                        """;
                    await using (var command = new SqlCommand(updatePointsSql, connection, transaction))
                    {
                        command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = customerId;
                        command.Parameters.Add("@PointsRestored", SqlDbType.Int).Value = pointsRestored;
                        command.Parameters.Add("@PointsRecovered", SqlDbType.Int).Value = pointsRecovered;
                        if (await command.ExecuteNonQueryAsync(ct) != 1)
                            throw new InvalidOperationException("Khách đã sử dụng điểm thưởng từ hóa đơn này; không thể thu hồi điểm an toàn.");
                    }

                    await AddLoyaltyReversalAsync(
                        connection, transaction, customerId, invoiceId, actorId,
                        "REFUND", pointsRestored, "Hoàn điểm đã dùng do hủy hóa đơn", ct);
                    await AddLoyaltyReversalAsync(
                        connection, transaction, customerId, invoiceId, actorId,
                        "RECOVER", pointsRecovered, "Thu hồi điểm thưởng do hủy hóa đơn", ct);
                }
            }

            const string voucherSql = """
                UPDATE vu
                SET CancelledAt = SYSDATETIME(), CancelledBy = @ActorId
                FROM dbo.VoucherUsage vu
                WHERE vu.InvoiceId = @InvoiceId AND vu.CancelledAt IS NULL;

                IF @@ROWCOUNT = 1
                    UPDATE v
                    SET UsedCount = CASE WHEN UsedCount > 0 THEN UsedCount - 1 ELSE 0 END
                    FROM dbo.Voucher v
                    INNER JOIN dbo.VoucherUsage vu ON vu.VoucherId = v.VoucherId
                    WHERE vu.InvoiceId = @InvoiceId;
                """;
            await using (var command = new SqlCommand(voucherSql, connection, transaction))
            {
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                command.Parameters.Add("@ActorId", SqlDbType.VarChar, 20).Value = actorId;
                await command.ExecuteNonQueryAsync(ct);
            }

            const string releaseReservationsSql = """
                UPDATE dbo.InventoryReservation
                SET Status = 'RELEASED', ReleasedAt = SYSDATETIME()
                WHERE InvoiceId = @InvoiceId AND Status = 'ACTIVE';
                """;
            await using (var command = new SqlCommand(releaseReservationsSql, connection, transaction))
            {
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                await command.ExecuteNonQueryAsync(ct);
            }

            const string shiftSql = """
                UPDATE dbo.WorkShift
                SET CashRefunds = CashRefunds + @CashRefund,
                    TransferSales = TransferSales - @NonCashRefund
                WHERE ShiftId = @ShiftId
                  AND TransferSales >= @NonCashRefund;
                """;
            await using (var command = new SqlCommand(shiftSql, connection, transaction))
            {
                AddMoney(command, "@CashRefund", cashRefund);
                AddMoney(command, "@NonCashRefund", nonCashRefund);
                command.Parameters.Add("@ShiftId", SqlDbType.VarChar, 20).Value = shiftId;
                if (await command.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("Không thể cập nhật sổ quỹ của ca; dữ liệu doanh thu ca không khớp.");
            }

            const string cancelSql = """
                UPDATE dbo.Invoice
                SET Status = 'CANCELLED',
                    CancelledBy = @ActorId,
                    CancelledAt = SYSDATETIME(),
                    CancelReason = @Reason
                WHERE InvoiceId = @InvoiceId AND Status = 'PAID';
                """;
            await using (var command = new SqlCommand(cancelSql, connection, transaction))
            {
                command.Parameters.Add("@ActorId", SqlDbType.VarChar, 20).Value = actorId;
                command.Parameters.Add("@Reason", SqlDbType.NVarChar, 500).Value = reason;
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                if (await command.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("Trạng thái hóa đơn đã thay đổi; hãy tải lại dữ liệu.");
            }

            if (prescriptionId is not null)
                await RecalculatePrescriptionStatusAsync(connection, transaction, prescriptionId, ct);

            string newValue = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = "CANCELLED",
                reason,
                cashRefund,
                nonCashRefund,
                pointsRestored,
                pointsRecovered,
                batchesRestocked = batches.Count
            });
            string oldValue = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = "PAID",
                amountPaid = oldAmount
            });
            await WriteAuditAsync(
                connection,
                transaction,
                actorId,
                "CANCEL_PAID_INVOICE",
                "Invoice",
                invoiceId,
                oldValue,
                newValue,
                reason,
                ct);

            await transaction.CommitAsync(ct);
            return new InvoiceCancellationResult(
                invoiceId,
                oldAmount,
                cashRefund,
                pointsRestored,
                pointsRecovered,
                batches.Count);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<List<PendingStocktakeRow>> GetPendingStocktakesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT s.StocktakeId, s.CreatedBy, e.FullName, s.CreatedAt, s.Note
            FROM dbo.Stocktake s
            INNER JOIN dbo.Employee e ON e.EmployeeId = s.CreatedBy
            WHERE s.Status = 'PENDING'
            ORDER BY s.CreatedAt;
            """;

        var rows = new List<PendingStocktakeRow>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new PendingStocktakeRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDateTime(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return rows;
    }

    public async Task<List<StocktakeDetailRow>> GetStocktakeDetailsAsync(
        string stocktakeId,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT d.StocktakeDetailId, d.BatchId, b.MedicineId, m.MedicineName,
                   b.BatchNumber, d.SystemQuantity, d.ActualQuantity,
                   d.DifferenceQuantity, d.DifferenceReason
            FROM dbo.StocktakeDetail d
            INNER JOIN dbo.MedicineBatch b ON b.BatchId = d.BatchId
            INNER JOIN dbo.Medicine m ON m.MedicineId = b.MedicineId
            WHERE d.StocktakeId = @StocktakeId
            ORDER BY d.StocktakeDetailId;
            """;

        var rows = new List<StocktakeDetailRow>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@StocktakeId", SqlDbType.VarChar, 20).Value = stocktakeId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new StocktakeDetailRow(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetDecimal(5),
                reader.GetDecimal(6),
                reader.GetDecimal(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }

        return rows;
    }

    public Task ApproveStocktakeAsync(
        string stocktakeId,
        string actorId,
        CancellationToken ct = default) =>
        DecideStocktakeAsync(stocktakeId, actorId, true, null, ct);

    public Task RejectStocktakeAsync(
        string stocktakeId,
        string actorId,
        string reason,
        CancellationToken ct = default) =>
        DecideStocktakeAsync(stocktakeId, actorId, false, reason, ct);

    private async Task DecideStocktakeAsync(
        string stocktakeId,
        string actorId,
        bool approve,
        string? reason,
        CancellationToken ct)
    {
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        try
        {
            await EnsurePendingAsync(connection, transaction, "Stocktake", "StocktakeId", stocktakeId, ct);
            if (approve)
            {
                var details = new List<(string BatchId, decimal Actual)>();
                const string detailSql = """
                    SELECT BatchId, ActualQuantity
                    FROM dbo.StocktakeDetail WITH (UPDLOCK, HOLDLOCK)
                    WHERE StocktakeId = @Id;
                    """;
                await using (var command = new SqlCommand(detailSql, connection, transaction))
                {
                    command.Parameters.Add("@Id", SqlDbType.VarChar, 20).Value = stocktakeId;
                    await using var reader = await command.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                        details.Add((reader.GetString(0), reader.GetDecimal(1)));
                }

                if (details.Count == 0)
                    throw new InvalidOperationException("Phiếu kiểm kê không có chi tiết để duyệt.");

                foreach (var detail in details)
                {
                    const string currentStockSql = """
                        SELECT PhysicalStock
                        FROM dbo.MedicineBatch WITH (UPDLOCK, HOLDLOCK)
                        WHERE BatchId = @BatchId;
                        """;
                    decimal currentStock;
                    await using (var command = new SqlCommand(currentStockSql, connection, transaction))
                    {
                        command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = detail.BatchId;
                        object? value = await command.ExecuteScalarAsync(ct);
                        if (value is null || value == DBNull.Value)
                            throw new InvalidOperationException($"Không tìm thấy lô {detail.BatchId}.");
                        currentStock = Convert.ToDecimal(value);
                    }

                    decimal reserved = await GetActiveReservedQuantityAsync(
                        connection, transaction, detail.BatchId, ct);
                    if (detail.Actual < reserved)
                        throw new InvalidOperationException(
                            $"Số lượng kiểm kê của lô {detail.BatchId} thấp hơn lượng đang được giữ cho hóa đơn.");

                    const string updateSql = """
                        UPDATE dbo.MedicineBatch
                        SET PhysicalStock = @Actual
                        WHERE BatchId = @BatchId;
                        """;
                    await using (var command = new SqlCommand(updateSql, connection, transaction))
                    {
                        AddQuantity(command, "@Actual", detail.Actual);
                        command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = detail.BatchId;
                        if (await command.ExecuteNonQueryAsync(ct) != 1)
                            throw new InvalidOperationException($"Không tìm thấy lô {detail.BatchId}.");
                    }

                    decimal adjustment = detail.Actual - currentStock;
                    if (adjustment != 0)
                    {
                        await InsertStockTransactionAsync(
                            connection, transaction, detail.BatchId, "STOCKTAKE",
                            adjustment, stocktakeId, actorId,
                            "Điều chỉnh tồn kho sau khi quản lý duyệt kiểm kê",
                            ct);
                    }
                }
            }

            const string updateHeaderSql = """
                UPDATE dbo.Stocktake
                SET Status = @Status, ApprovedBy = @ActorId, ApprovedAt = SYSDATETIME()
                WHERE StocktakeId = @Id AND Status = 'PENDING';
                """;
            await using (var command = new SqlCommand(updateHeaderSql, connection, transaction))
            {
                command.Parameters.Add("@Status", SqlDbType.VarChar, 20).Value = approve ? "APPROVED" : "REJECTED";
                command.Parameters.Add("@ActorId", SqlDbType.VarChar, 20).Value = actorId;
                command.Parameters.Add("@Id", SqlDbType.VarChar, 20).Value = stocktakeId;
                if (await command.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("Trạng thái phiếu kiểm kê đã thay đổi; hãy tải lại dữ liệu.");
            }

            await WriteAuditAsync(
                connection, transaction, actorId,
                approve ? "APPROVE_STOCKTAKE" : "REJECT_STOCKTAKE",
                "Stocktake", stocktakeId,
                "{\"status\":\"PENDING\"}",
                $"{{\"status\":\"{(approve ? "APPROVED" : "REJECTED")}\"}}",
                reason,
                ct);

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<List<PendingDisposalRow>> GetPendingDisposalsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT d.DisposalId, d.CreatedBy, e.FullName, d.CreatedAt, d.Reason, d.TotalLoss
            FROM dbo.Disposal d
            INNER JOIN dbo.Employee e ON e.EmployeeId = d.CreatedBy
            WHERE d.Status = 'PENDING'
            ORDER BY d.CreatedAt;
            """;

        var rows = new List<PendingDisposalRow>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new PendingDisposalRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDateTime(3),
                reader.GetString(4),
                reader.GetDecimal(5)));
        }

        return rows;
    }

    public async Task<List<DisposalDetailRow>> GetDisposalDetailsAsync(
        string disposalId,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT d.DisposalDetailId, d.BatchId, b.MedicineId, m.MedicineName,
                   b.BatchNumber, b.ExpiryDate, b.PhysicalStock,
                   d.Quantity, d.PurchasePriceBase, d.LossAmount
            FROM dbo.DisposalDetail d
            INNER JOIN dbo.MedicineBatch b ON b.BatchId = d.BatchId
            INNER JOIN dbo.Medicine m ON m.MedicineId = b.MedicineId
            WHERE d.DisposalId = @DisposalId
            ORDER BY d.DisposalDetailId;
            """;

        var rows = new List<DisposalDetailRow>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@DisposalId", SqlDbType.VarChar, 20).Value = disposalId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new DisposalDetailRow(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetDateTime(5),
                reader.GetDecimal(6),
                reader.GetDecimal(7),
                reader.GetDecimal(8),
                reader.GetDecimal(9)));
        }

        return rows;
    }

    public Task ApproveDisposalAsync(
        string disposalId,
        string actorId,
        CancellationToken ct = default) =>
        DecideDisposalAsync(disposalId, actorId, true, null, ct);

    public Task RejectDisposalAsync(
        string disposalId,
        string actorId,
        string reason,
        CancellationToken ct = default) =>
        DecideDisposalAsync(disposalId, actorId, false, reason, ct);

    private async Task DecideDisposalAsync(
        string disposalId,
        string actorId,
        bool approve,
        string? decisionReason,
        CancellationToken ct)
    {
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        try
        {
            await EnsurePendingAsync(connection, transaction, "Disposal", "DisposalId", disposalId, ct);
            if (approve)
            {
                var details = new List<(string BatchId, decimal Quantity, decimal Price)>();
                const string detailSql = """
                    SELECT BatchId, Quantity, PurchasePriceBase
                    FROM dbo.DisposalDetail WITH (UPDLOCK, HOLDLOCK)
                    WHERE DisposalId = @Id;
                    """;
                await using (var command = new SqlCommand(detailSql, connection, transaction))
                {
                    command.Parameters.Add("@Id", SqlDbType.VarChar, 20).Value = disposalId;
                    await using var reader = await command.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                        details.Add((reader.GetString(0), reader.GetDecimal(1), reader.GetDecimal(2)));
                }

                if (details.Count == 0)
                    throw new InvalidOperationException("Phiếu xuất hủy không có chi tiết để duyệt.");

                decimal totalLoss = 0;
                foreach (var detail in details)
                {
                    decimal reserved = await GetActiveReservedQuantityAsync(
                        connection, transaction, detail.BatchId, ct);

                    const string updateSql = """
                        UPDATE dbo.MedicineBatch
                        SET PhysicalStock = PhysicalStock - @Quantity,
                            BatchStatus = CASE
                                WHEN PhysicalStock - @Quantity = 0 THEN 'DISPOSED'
                                ELSE BatchStatus
                            END
                        WHERE BatchId = @BatchId
                          AND PhysicalStock - @Quantity >= @Reserved;
                        """;
                    await using (var command = new SqlCommand(updateSql, connection, transaction))
                    {
                        AddQuantity(command, "@Quantity", detail.Quantity);
                        AddQuantity(command, "@Reserved", reserved);
                        command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = detail.BatchId;
                        if (await command.ExecuteNonQueryAsync(ct) != 1)
                            throw new InvalidOperationException(
                                $"Lô {detail.BatchId} không còn đủ tồn khả dụng để xuất hủy.");
                    }

                    await InsertStockTransactionAsync(
                        connection, transaction, detail.BatchId, "DISPOSAL",
                        -detail.Quantity, disposalId, actorId,
                        "Xuất hủy sau khi quản lý phê duyệt",
                        ct);
                    totalLoss += detail.Quantity * detail.Price;
                }

                const string updateTotalSql = "UPDATE dbo.Disposal SET TotalLoss = @TotalLoss WHERE DisposalId = @Id;";
                await using (var command = new SqlCommand(updateTotalSql, connection, transaction))
                {
                    AddMoney(command, "@TotalLoss", totalLoss);
                    command.Parameters.Add("@Id", SqlDbType.VarChar, 20).Value = disposalId;
                    await command.ExecuteNonQueryAsync(ct);
                }
            }

            const string updateHeaderSql = """
                UPDATE dbo.Disposal
                SET Status = @Status, ApprovedBy = @ActorId, ApprovedAt = SYSDATETIME()
                WHERE DisposalId = @Id AND Status = 'PENDING';
                """;
            await using (var command = new SqlCommand(updateHeaderSql, connection, transaction))
            {
                command.Parameters.Add("@Status", SqlDbType.VarChar, 20).Value = approve ? "APPROVED" : "REJECTED";
                command.Parameters.Add("@ActorId", SqlDbType.VarChar, 20).Value = actorId;
                command.Parameters.Add("@Id", SqlDbType.VarChar, 20).Value = disposalId;
                if (await command.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("Trạng thái phiếu xuất hủy đã thay đổi; hãy tải lại dữ liệu.");
            }

            await WriteAuditAsync(
                connection, transaction, actorId,
                approve ? "APPROVE_DISPOSAL" : "REJECT_DISPOSAL",
                "Disposal", disposalId,
                "{\"status\":\"PENDING\"}",
                $"{{\"status\":\"{(approve ? "APPROVED" : "REJECTED")}\"}}",
                decisionReason,
                ct);

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task EnsurePendingAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string table,
        string keyColumn,
        string id,
        CancellationToken ct)
    {
        string sql = $"SELECT Status FROM dbo.{table} WITH (UPDLOCK, HOLDLOCK) WHERE {keyColumn} = @Id;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.VarChar, 20).Value = id;
        object? value = await command.ExecuteScalarAsync(ct);
        if (value is not string status)
            throw new InvalidOperationException("Không tìm thấy phiếu cần xử lý.");
        if (status != "PENDING")
            throw new InvalidOperationException("Phiếu đã được xử lý trước đó; hãy tải lại danh sách.");
    }

    private static async Task<decimal> GetActiveReservedQuantityAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string batchId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT COALESCE(SUM(ReservedQuantity), 0)
            FROM dbo.InventoryReservation WITH (UPDLOCK, HOLDLOCK)
            WHERE BatchId = @BatchId
              AND Status = 'ACTIVE'
              AND ExpiresAt > SYSDATETIME();
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = batchId;
        return Convert.ToDecimal(await command.ExecuteScalarAsync(ct));
    }

    private static async Task InsertStockTransactionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string batchId,
        string type,
        decimal quantity,
        string referenceCode,
        string actorId,
        string note,
        CancellationToken ct)
    {
        if (quantity == 0)
            return;

        const string sql = """
            INSERT dbo.StockTransaction
                (BatchId, TransactionType, QuantityChange, ReferenceType,
                 ReferenceCode, CreatedBy, Note)
            VALUES
                (@BatchId, @Type, @Quantity, @ReferenceType, @ReferenceCode,
                 @ActorId, @Note);
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = batchId;
        command.Parameters.Add("@Type", SqlDbType.VarChar, 20).Value = type;
        AddQuantity(command, "@Quantity", quantity);
        command.Parameters.Add("@ReferenceType", SqlDbType.VarChar, 30).Value = type;
        command.Parameters.Add("@ReferenceCode", SqlDbType.VarChar, 20).Value = referenceCode;
        command.Parameters.Add("@ActorId", SqlDbType.VarChar, 20).Value = actorId;
        command.Parameters.Add("@Note", SqlDbType.NVarChar, 300).Value = note;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task AddLoyaltyReversalAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string customerId,
        string invoiceId,
        string actorId,
        string type,
        int points,
        string note,
        CancellationToken ct)
    {
        if (points == 0)
            return;

        const string sql = """
            INSERT dbo.LoyaltyTransaction
                (CustomerId, TransactionType, Points, ReferenceCode, Note)
            VALUES (@CustomerId, @Type, @Points, @InvoiceId, @Note);

            INSERT dbo.AuditLog(EmployeeId, ActionName, EntityName, EntityId, NewValue)
            VALUES
                (@ActorId, 'REVERSE_LOYALTY', 'LoyaltyTransaction', @InvoiceId,
                 (SELECT @CustomerId AS CustomerId, @Type AS TransactionType,
                         @Points AS Points FOR JSON PATH, WITHOUT_ARRAY_WRAPPER));
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = customerId;
        command.Parameters.Add("@Type", SqlDbType.VarChar, 20).Value = type;
        command.Parameters.Add("@Points", SqlDbType.Int).Value = points;
        command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
        command.Parameters.Add("@Note", SqlDbType.NVarChar, 300).Value = note;
        command.Parameters.Add("@ActorId", SqlDbType.VarChar, 20).Value = actorId;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task RecalculatePrescriptionStatusAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string prescriptionId,
        CancellationToken ct)
    {
        const string sql = """
            UPDATE p
            SET Status =
                CASE
                    WHEN NOT EXISTS
                    (
                        SELECT 1
                        FROM dbo.Invoice i
                        WHERE i.PrescriptionId = p.PrescriptionId AND i.Status = 'PAID'
                    ) THEN 'UNSOLD'
                    WHEN NOT EXISTS
                    (
                        SELECT 1
                        FROM dbo.PrescriptionDetail pd
                        OUTER APPLY
                        (
                            SELECT SUM(d.Quantity) AS SoldQuantity
                            FROM dbo.Invoice i
                            INNER JOIN dbo.InvoiceDetail d ON d.InvoiceId = i.InvoiceId
                            WHERE i.PrescriptionId = p.PrescriptionId
                              AND i.Status = 'PAID'
                              AND d.MedicineId = pd.MedicineId
                              AND d.UnitNameSnapshot = pd.PrescribedUnit
                        ) sold
                        WHERE pd.PrescriptionId = p.PrescriptionId
                          AND COALESCE(sold.SoldQuantity, 0) < pd.PrescribedQuantity
                    ) THEN 'SOLD'
                    ELSE 'PARTIAL'
                END
            FROM dbo.Prescription p
            WHERE p.PrescriptionId = @PrescriptionId;
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@PrescriptionId", SqlDbType.VarChar, 20).Value = prescriptionId;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task WriteAuditAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string actorId,
        string action,
        string entity,
        string entityId,
        string? oldValue,
        string? newValue,
        string? reason,
        CancellationToken ct)
    {
        const string sql = """
            INSERT dbo.AuditLog
                (EmployeeId, ActionName, EntityName, EntityId, OldValue, NewValue, Reason)
            VALUES
                (@ActorId, @Action, @Entity, @EntityId, @OldValue, @NewValue, @Reason);
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@ActorId", SqlDbType.VarChar, 20).Value = actorId;
        command.Parameters.Add("@Action", SqlDbType.VarChar, 100).Value = action;
        command.Parameters.Add("@Entity", SqlDbType.VarChar, 100).Value = entity;
        command.Parameters.Add("@EntityId", SqlDbType.VarChar, 50).Value = entityId;
        command.Parameters.Add("@OldValue", SqlDbType.NVarChar, -1).Value = Db(oldValue);
        command.Parameters.Add("@NewValue", SqlDbType.NVarChar, -1).Value = Db(newValue);
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 500).Value = Db(reason);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static void AddMoney(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 2;
        parameter.Value = value;
    }

    private static void AddQuantity(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 3;
        parameter.Value = value;
    }

    private static object Db(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
}
