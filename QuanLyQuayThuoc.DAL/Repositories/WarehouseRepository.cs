
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;
using QuanLyQuayThuoc.DAL.Database;

namespace QuanLyQuayThuoc.DAL.Repositories;

/// <summary>
/// Repository quản lý kho thuốc.
/// Sử dụng các bảng:
/// Medicine, MedicineBatch, InventoryReservation,
/// StockTransaction, GoodsReceipt, GoodsReceiptDetail,
/// Supplier, Employee.
/// </summary>
public sealed class WarehouseRepository
{
    // =====================================================
    // MODELS
    // =====================================================

    public sealed record InventoryRow(
        string MedicineId,
        string MedicineName,
        string BaseUnit,
        decimal MinimumStock,
        decimal PhysicalStock,
        decimal ReservedStock,
        decimal AvailableStock,
        int BatchCount,
        string StockStatus
    );

    public sealed record BatchRow(
        string BatchId,
        string MedicineId,
        string MedicineName,
        string BatchNumber,
        DateTime? ManufactureDate,
        DateTime ExpiryDate,
        decimal PurchasePriceBase,
        decimal InitialQuantity,
        decimal PhysicalStock,
        string BatchStatus
    );

    public sealed record ExpiringBatchRow(
        string BatchId,
        string MedicineId,
        string MedicineName,
        string BatchNumber,
        DateTime ExpiryDate,
        decimal PhysicalStock,
        int DaysRemaining
    );

    public sealed record StockTransactionRow(
        long StockTransactionId,
        string BatchId,
        string MedicineId,
        string MedicineName,
        string BatchNumber,
        string TransactionType,
        decimal QuantityChange,
        string ReferenceType,
        string ReferenceCode,
        string EmployeeId,
        string EmployeeName,
        DateTime CreatedAt,
        string? Note
    );

    public sealed record GoodsReceiptRow(
        string ReceiptId,
        string SupplierId,
        string SupplierName,
        string CreatedBy,
        string EmployeeName,
        DateTime ReceiptDate,
        string Status,
        string? Note,
        string? ConfirmedBy,
        DateTime? ConfirmedAt,
        int TotalItems
    );

    public sealed record GoodsReceiptDetailRow(
        long ReceiptDetailId,
        string ReceiptId,
        string MedicineId,
        string MedicineName,
        string UnitName,
        decimal Quantity,
        decimal ConversionFactor,
        decimal QuantityBase,
        decimal PurchaseUnitPrice,
        string BatchNumber,
        DateTime? ManufactureDate,
        DateTime ExpiryDate
    );

    public sealed record ProposalResult(string ProposalId);


    // =====================================================
    // 1. LẤY DANH SÁCH TỒN KHO
    // =====================================================

    public async Task<List<InventoryRow>> GetInventoryAsync(
        string? keyword = null,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                m.MedicineId,
                m.MedicineName,
                m.BaseUnit,
                m.MinimumStock,

                ISNULL(SUM(b.PhysicalStock), 0) AS PhysicalStock,

                ISNULL(SUM(r.ReservedStock), 0) AS ReservedStock,

                ISNULL(SUM(b.PhysicalStock), 0)
                - ISNULL(SUM(r.ReservedStock), 0) AS AvailableStock,

                COUNT(DISTINCT b.BatchId) AS BatchCount

            FROM dbo.Medicine m

            LEFT JOIN dbo.MedicineBatch b
                ON b.MedicineId = m.MedicineId
                AND b.BatchStatus = 'ACTIVE'
                AND b.ExpiryDate >= CONVERT(date, GETDATE())

            LEFT JOIN
            (
                SELECT
                    BatchId,
                    SUM(ReservedQuantity) AS ReservedStock
                FROM dbo.InventoryReservation
                WHERE Status = 'ACTIVE'
                    AND ExpiresAt > SYSDATETIME()
                GROUP BY BatchId
            ) r ON r.BatchId = b.BatchId

            WHERE
                (@Keyword = N''
                 OR m.MedicineId LIKE @Pattern
                 OR m.MedicineName LIKE @Pattern)

            GROUP BY
                m.MedicineId,
                m.MedicineName,
                m.BaseUnit,
                m.MinimumStock

            ORDER BY m.MedicineName;
            """;

        var rows = new List<InventoryRow>();

        await using var cn = DbConnectionFactory.CreateConnection();
        await cn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, cn);

        string key = (keyword ?? "").Trim();

        cmd.Parameters.Add("@Keyword", SqlDbType.NVarChar, 200).Value = key;
        cmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 205).Value =
            $"%{key}%";

        await using var rd = await cmd.ExecuteReaderAsync(ct);

        while (await rd.ReadAsync(ct))
        {
            decimal physical = rd.GetDecimal(4);
            decimal reserved = rd.GetDecimal(5);
            decimal available = rd.GetDecimal(6);
            decimal minimum = rd.GetDecimal(3);

            string status;

            if (available <= 0)
                status = "Hết hàng";
            else if (available <= minimum)
                status = "Sắp hết hàng";
            else
                status = "Còn hàng";

            rows.Add(new InventoryRow(
                rd.GetString(0),
                rd.GetString(1),
                rd.GetString(2),
                minimum,
                physical,
                reserved,
                available,
                rd.GetInt32(7),
                status
            ));
        }

        return rows;
    }


    // =====================================================
    // 2. LẤY DANH SÁCH LÔ THUỐC
    // =====================================================

    public async Task<List<BatchRow>> GetBatchesAsync(
        string? medicineId = null,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                b.BatchId,
                b.MedicineId,
                m.MedicineName,
                b.BatchNumber,
                b.ManufactureDate,
                b.ExpiryDate,
                b.PurchasePriceBase,
                b.InitialQuantity,
                b.PhysicalStock,
                b.BatchStatus

            FROM dbo.MedicineBatch b

            INNER JOIN dbo.Medicine m
                ON m.MedicineId = b.MedicineId

            WHERE
                (@MedicineId IS NULL
                 OR b.MedicineId = @MedicineId)

            ORDER BY
                b.ExpiryDate ASC,
                b.BatchId ASC;
            """;

        var rows = new List<BatchRow>();

        await using var cn = DbConnectionFactory.CreateConnection();
        await cn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, cn);

        cmd.Parameters.Add("@MedicineId", SqlDbType.VarChar, 20).Value =
            string.IsNullOrWhiteSpace(medicineId)
                ? DBNull.Value
                : medicineId.Trim();

        await using var rd = await cmd.ExecuteReaderAsync(ct);

        while (await rd.ReadAsync(ct))
        {
            rows.Add(new BatchRow(
                rd.GetString(0),
                rd.GetString(1),
                rd.GetString(2),
                rd.GetString(3),
                rd.IsDBNull(4) ? null : rd.GetDateTime(4),
                rd.GetDateTime(5),
                rd.GetDecimal(6),
                rd.GetDecimal(7),
                rd.GetDecimal(8),
                rd.GetString(9)
            ));
        }

        return rows;
    }


    // =====================================================
    // 3. LẤY DANH SÁCH THUỐC SẮP HẾT HẠN
    // =====================================================

    public async Task<List<ExpiringBatchRow>> GetExpiringBatchesAsync(
        int daysAhead = 30,
        CancellationToken ct = default)
    {
        if (daysAhead < 0)
            throw new ArgumentException(
                "Số ngày kiểm tra không hợp lệ.");

        const string sql = """
            SELECT
                b.BatchId,
                b.MedicineId,
                m.MedicineName,
                b.BatchNumber,
                b.ExpiryDate,
                b.PhysicalStock,
                DATEDIFF(
                    DAY,
                    CONVERT(date, GETDATE()),
                    b.ExpiryDate
                ) AS DaysRemaining

            FROM dbo.MedicineBatch b

            INNER JOIN dbo.Medicine m
                ON m.MedicineId = b.MedicineId

            WHERE
                b.BatchStatus = 'ACTIVE'
                AND b.PhysicalStock > 0
                AND b.ExpiryDate >= CONVERT(date, GETDATE())
                AND b.ExpiryDate <= DATEADD(
                    DAY,
                    @DaysAhead,
                    CONVERT(date, GETDATE())
                )

            ORDER BY b.ExpiryDate ASC;
            """;

        var rows = new List<ExpiringBatchRow>();

        await using var cn = DbConnectionFactory.CreateConnection();
        await cn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, cn);

        cmd.Parameters.Add("@DaysAhead", SqlDbType.Int).Value =
            daysAhead;

        await using var rd = await cmd.ExecuteReaderAsync(ct);

        while (await rd.ReadAsync(ct))
        {
            rows.Add(new ExpiringBatchRow(
                rd.GetString(0),
                rd.GetString(1),
                rd.GetString(2),
                rd.GetString(3),
                rd.GetDateTime(4),
                rd.GetDecimal(5),
                rd.GetInt32(6)
            ));
        }

        return rows;
    }


    // =====================================================
    // 4. LẤY LỊCH SỬ GIAO DỊCH KHO
    // =====================================================

    public async Task<List<StockTransactionRow>> GetStockTransactionsAsync(
        string? keyword = null,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                st.StockTransactionId,
                st.BatchId,
                b.MedicineId,
                m.MedicineName,
                b.BatchNumber,
                st.TransactionType,
                st.QuantityChange,
                st.ReferenceType,
                st.ReferenceCode,
                st.CreatedBy,
                e.FullName,
                st.CreatedAt,
                st.Note

            FROM dbo.StockTransaction st

            INNER JOIN dbo.MedicineBatch b
                ON b.BatchId = st.BatchId

            INNER JOIN dbo.Medicine m
                ON m.MedicineId = b.MedicineId

            INNER JOIN dbo.Employee e
                ON e.EmployeeId = st.CreatedBy

            WHERE
                (@Keyword = N''
                 OR st.BatchId LIKE @Pattern
                 OR b.MedicineId LIKE @Pattern
                 OR m.MedicineName LIKE @Pattern
                 OR st.ReferenceCode LIKE @Pattern
                 OR e.FullName LIKE @Pattern)

            ORDER BY st.CreatedAt DESC,
                     st.StockTransactionId DESC;
            """;

        var rows = new List<StockTransactionRow>();

        await using var cn = DbConnectionFactory.CreateConnection();
        await cn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, cn);

        string key = (keyword ?? "").Trim();

        cmd.Parameters.Add("@Keyword", SqlDbType.NVarChar, 200).Value = key;
        cmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 205).Value =
            $"%{key}%";

        await using var rd = await cmd.ExecuteReaderAsync(ct);

        while (await rd.ReadAsync(ct))
        {
            rows.Add(new StockTransactionRow(
                rd.GetInt64(0),
                rd.GetString(1),
                rd.GetString(2),
                rd.GetString(3),
                rd.GetString(4),
                rd.GetString(5),
                rd.GetDecimal(6),
                rd.GetString(7),
                rd.GetString(8),
                rd.GetString(9),
                rd.GetString(10),
                rd.GetDateTime(11),
                rd.IsDBNull(12) ? null : rd.GetString(12)
            ));
        }

        return rows;
    }


    // =====================================================
    // 5. LẤY DANH SÁCH PHIẾU NHẬP
    // =====================================================

    public async Task<List<GoodsReceiptRow>> GetGoodsReceiptsAsync(
        string? keyword = null,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                gr.ReceiptId,
                gr.SupplierId,
                s.SupplierName,
                gr.CreatedBy,
                e.FullName,
                gr.ReceiptDate,
                gr.Status,
                gr.Note,
                gr.ConfirmedBy,
                gr.ConfirmedAt,
                COUNT(gd.ReceiptDetailId) AS TotalItems

            FROM dbo.GoodsReceipt gr

            INNER JOIN dbo.Supplier s
                ON s.SupplierId = gr.SupplierId

            INNER JOIN dbo.Employee e
                ON e.EmployeeId = gr.CreatedBy

            LEFT JOIN dbo.GoodsReceiptDetail gd
                ON gd.ReceiptId = gr.ReceiptId

            WHERE
                (@Keyword = N''
                 OR gr.ReceiptId LIKE @Pattern
                 OR s.SupplierName LIKE @Pattern
                 OR e.FullName LIKE @Pattern)

            GROUP BY
                gr.ReceiptId,
                gr.SupplierId,
                s.SupplierName,
                gr.CreatedBy,
                e.FullName,
                gr.ReceiptDate,
                gr.Status,
                gr.Note,
                gr.ConfirmedBy,
                gr.ConfirmedAt

            ORDER BY gr.ReceiptDate DESC;
            """;

        var rows = new List<GoodsReceiptRow>();

        await using var cn = DbConnectionFactory.CreateConnection();
        await cn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, cn);

        string key = (keyword ?? "").Trim();

        cmd.Parameters.Add("@Keyword", SqlDbType.NVarChar, 200).Value = key;
        cmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 205).Value =
            $"%{key}%";

        await using var rd = await cmd.ExecuteReaderAsync(ct);

        while (await rd.ReadAsync(ct))
        {
            rows.Add(new GoodsReceiptRow(
                rd.GetString(0),
                rd.GetString(1),
                rd.GetString(2),
                rd.GetString(3),
                rd.GetString(4),
                rd.GetDateTime(5),
                rd.GetString(6),
                rd.IsDBNull(7) ? null : rd.GetString(7),
                rd.IsDBNull(8) ? null : rd.GetString(8),
                rd.IsDBNull(9) ? null : rd.GetDateTime(9),
                rd.GetInt32(10)
            ));
        }

        return rows;
    }


    // =====================================================
    // 6. LẤY CHI TIẾT PHIẾU NHẬP
    // =====================================================

    public async Task<List<GoodsReceiptDetailRow>> GetGoodsReceiptDetailsAsync(
        string receiptId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(receiptId))
            throw new ArgumentException(
                "Mã phiếu nhập không được để trống.");

        const string sql = """
            SELECT
                gd.ReceiptDetailId,
                gd.ReceiptId,
                gd.MedicineId,
                m.MedicineName,
                gd.UnitName,
                gd.Quantity,
                gd.ConversionFactor,
                gd.QuantityBase,
                gd.PurchaseUnitPrice,
                gd.BatchNumber,
                gd.ManufactureDate,
                gd.ExpiryDate

            FROM dbo.GoodsReceiptDetail gd

            INNER JOIN dbo.Medicine m
                ON m.MedicineId = gd.MedicineId

            WHERE gd.ReceiptId = @ReceiptId

            ORDER BY gd.ReceiptDetailId;
            """;

        var rows = new List<GoodsReceiptDetailRow>();

        await using var cn = DbConnectionFactory.CreateConnection();
        await cn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, cn);

        cmd.Parameters.Add("@ReceiptId", SqlDbType.VarChar, 20).Value =
            receiptId.Trim();

        await using var rd = await cmd.ExecuteReaderAsync(ct);

        while (await rd.ReadAsync(ct))
        {
            rows.Add(new GoodsReceiptDetailRow(
                rd.GetInt64(0),
                rd.GetString(1),
                rd.GetString(2),
                rd.GetString(3),
                rd.GetString(4),
                rd.GetDecimal(5),
                rd.GetDecimal(6),
                rd.GetDecimal(7),
                rd.GetDecimal(8),
                rd.GetString(9),
                rd.IsDBNull(10) ? null : rd.GetDateTime(10),
                rd.GetDateTime(11)
            ));
        }

        return rows;
    }

    public async Task<ProposalResult> CreateStocktakeProposalAsync(
        string batchId,
        decimal actualQuantity,
        string? differenceReason,
        string actorId,
        CancellationToken ct = default)
    {
        string stocktakeId = $"ST{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        try
        {
            var (systemQuantity, reservedQuantity, _) = await LockBatchAndReadStockAsync(
                connection, transaction, batchId, ct);

            if (actualQuantity < reservedQuantity)
            {
                throw new InvalidOperationException(
                    "Số lượng kiểm kê không thể thấp hơn số lượng đang được giữ cho hóa đơn.");
            }

            bool hasDifference = actualQuantity != systemQuantity;
            string? reason = string.IsNullOrWhiteSpace(differenceReason)
                ? null
                : differenceReason.Trim();
            if (hasDifference && reason is null)
                throw new ArgumentException("Vui lòng nhập lý do cho số lượng chênh lệch.", nameof(differenceReason));

            const string headerSql = """
                INSERT dbo.Stocktake (StocktakeId, CreatedBy, Status, Note)
                VALUES (@StocktakeId, @ActorId, 'PENDING', @Note);
                """;
            await using (var command = new SqlCommand(headerSql, connection, transaction))
            {
                command.Parameters.Add("@StocktakeId", SqlDbType.VarChar, 20).Value = stocktakeId;
                command.Parameters.Add("@ActorId", SqlDbType.VarChar, 20).Value = actorId;
                command.Parameters.Add("@Note", SqlDbType.NVarChar, 500).Value =
                    (object?)reason ?? DBNull.Value;
                await command.ExecuteNonQueryAsync(ct);
            }

            const string detailSql = """
                INSERT dbo.StocktakeDetail
                    (StocktakeId, BatchId, SystemQuantity, ActualQuantity, DifferenceReason)
                VALUES (@StocktakeId, @BatchId, @SystemQuantity, @ActualQuantity, @Reason);
                """;
            await using (var command = new SqlCommand(detailSql, connection, transaction))
            {
                command.Parameters.Add("@StocktakeId", SqlDbType.VarChar, 20).Value = stocktakeId;
                command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = batchId;
                AddQuantity(command, "@SystemQuantity", systemQuantity);
                AddQuantity(command, "@ActualQuantity", actualQuantity);
                command.Parameters.Add("@Reason", SqlDbType.NVarChar, 300).Value =
                    (object?)reason ?? DBNull.Value;
                await command.ExecuteNonQueryAsync(ct);
            }

            await WriteProposalAuditAsync(
                connection, transaction, actorId, "CREATE_STOCKTAKE_PROPOSAL",
                "Stocktake", stocktakeId,
                new
                {
                    batchId,
                    systemQuantity,
                    actualQuantity,
                    differenceQuantity = actualQuantity - systemQuantity,
                    reason
                },
                reason,
                ct);

            await transaction.CommitAsync(ct);
            return new ProposalResult(stocktakeId);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ProposalResult> CreateDisposalProposalAsync(
        string batchId,
        decimal quantity,
        string reason,
        string actorId,
        CancellationToken ct = default)
    {
        string disposalId = $"DP{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        try
        {
            var (physicalStock, reservedQuantity, purchasePrice) = await LockBatchAndReadStockAsync(
                connection, transaction, batchId, ct);
            decimal availableStock = physicalStock - reservedQuantity;
            if (availableStock < 0)
                availableStock = 0;
            if (quantity > availableStock)
            {
                throw new InvalidOperationException(
                    $"Số lượng đề xuất vượt tồn khả dụng ({availableStock:N3}, đã trừ lượng đang giữ).");
            }

            decimal totalLoss = decimal.Round(quantity * purchasePrice, 2, MidpointRounding.AwayFromZero);
            const string headerSql = """
                INSERT dbo.Disposal (DisposalId, CreatedBy, Status, Reason, TotalLoss)
                VALUES (@DisposalId, @ActorId, 'PENDING', @Reason, @TotalLoss);
                """;
            await using (var command = new SqlCommand(headerSql, connection, transaction))
            {
                command.Parameters.Add("@DisposalId", SqlDbType.VarChar, 20).Value = disposalId;
                command.Parameters.Add("@ActorId", SqlDbType.VarChar, 20).Value = actorId;
                command.Parameters.Add("@Reason", SqlDbType.NVarChar, 500).Value = reason;
                AddMoney(command, "@TotalLoss", totalLoss);
                await command.ExecuteNonQueryAsync(ct);
            }

            const string detailSql = """
                INSERT dbo.DisposalDetail (DisposalId, BatchId, Quantity, PurchasePriceBase)
                VALUES (@DisposalId, @BatchId, @Quantity, @PurchasePriceBase);
                """;
            await using (var command = new SqlCommand(detailSql, connection, transaction))
            {
                command.Parameters.Add("@DisposalId", SqlDbType.VarChar, 20).Value = disposalId;
                command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = batchId;
                AddQuantity(command, "@Quantity", quantity);
                var priceParameter = command.Parameters.Add("@PurchasePriceBase", SqlDbType.Decimal);
                priceParameter.Precision = 18;
                priceParameter.Scale = 4;
                priceParameter.Value = purchasePrice;
                await command.ExecuteNonQueryAsync(ct);
            }

            await WriteProposalAuditAsync(
                connection, transaction, actorId, "CREATE_DISPOSAL_PROPOSAL",
                "Disposal", disposalId,
                new { batchId, quantity, purchasePriceBase = purchasePrice, totalLoss, reason },
                reason,
                ct);

            await transaction.CommitAsync(ct);
            return new ProposalResult(disposalId);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task<(decimal PhysicalStock, decimal ReservedQuantity, decimal PurchasePrice)>
        LockBatchAndReadStockAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string batchId,
            CancellationToken ct)
    {
        const string batchSql = """
            SELECT PhysicalStock, PurchasePriceBase
            FROM dbo.MedicineBatch WITH (UPDLOCK, HOLDLOCK)
            WHERE BatchId = @BatchId;
            """;

        decimal physicalStock;
        decimal purchasePrice;
        await using (var command = new SqlCommand(batchSql, connection, transaction))
        {
            command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = batchId;
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException("Không tìm thấy lô thuốc đã chọn.");
            physicalStock = reader.GetDecimal(0);
            purchasePrice = reader.GetDecimal(1);
        }

        const string reservationSql = """
            SELECT COALESCE(SUM(ReservedQuantity), 0)
            FROM dbo.InventoryReservation WITH (UPDLOCK, HOLDLOCK)
            WHERE BatchId = @BatchId
              AND Status = 'ACTIVE'
              AND ExpiresAt > SYSDATETIME();
            """;
        decimal reservedQuantity;
        await using (var command = new SqlCommand(reservationSql, connection, transaction))
        {
            command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = batchId;
            reservedQuantity = Convert.ToDecimal(await command.ExecuteScalarAsync(ct));
        }

        return (physicalStock, reservedQuantity, purchasePrice);
    }

    private static async Task WriteProposalAuditAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string actorId,
        string action,
        string entity,
        string entityId,
        object newValue,
        string? reason,
        CancellationToken ct)
    {
        const string sql = """
            INSERT dbo.AuditLog
                (EmployeeId, ActionName, EntityName, EntityId, NewValue, Reason)
            VALUES
                (@ActorId, @Action, @Entity, @EntityId, @NewValue, @Reason);
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@ActorId", SqlDbType.VarChar, 20).Value = actorId;
        command.Parameters.Add("@Action", SqlDbType.VarChar, 100).Value = action;
        command.Parameters.Add("@Entity", SqlDbType.VarChar, 100).Value = entity;
        command.Parameters.Add("@EntityId", SqlDbType.VarChar, 50).Value = entityId;
        command.Parameters.Add("@NewValue", SqlDbType.NVarChar, -1).Value =
            JsonSerializer.Serialize(newValue);
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 500).Value =
            (object?)reason ?? DBNull.Value;
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
}
