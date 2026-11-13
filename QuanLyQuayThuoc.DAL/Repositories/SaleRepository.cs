
using Microsoft.Data.SqlClient;
using System.Data;
using QuanLyQuayThuoc.DAL.Database;

namespace QuanLyQuayThuoc.DAL.Repositories;

public sealed class SaleRepository
{
    public sealed record OpenShiftRow(
        string ShiftId,
        string EmployeeId,
        string EmployeeName,
        decimal OpeningCash,
        DateTime OpenedAt);

    public sealed record SaleMedicineRow(
        string MedicineId,
        string MedicineName,
        bool RequiresPrescription,
        int MedicineUnitId,
        string UnitName,
        decimal ConversionFactor,
        decimal SalePrice,
        decimal AvailableStock);

    public sealed record CustomerRow(
        string CustomerId,
        string FullName,
        string Phone);

    public sealed class SaleLine
    {
        public string MedicineId { get; set; } = "";
        public int MedicineUnitId { get; set; }
        public string UnitName { get; set; } = "";
        public decimal ConversionFactor { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        public decimal QuantityBase => Quantity * ConversionFactor;
        public decimal LineTotal => Quantity * UnitPrice;
    }

    public sealed class CheckoutRequest
    {
        public string EmployeeId { get; set; } = "";
        public string ShiftId { get; set; } = "";
        public string? CustomerId { get; set; }
        public string? PrescriptionId { get; set; }
        public string PaymentMethod { get; set; } = "CASH";
        public string? TransactionReference { get; set; }
        public List<SaleLine> Lines { get; set; } = new();
    }

    // ==========================================
    // CA LÀM VIỆC ĐANG MỞ
    // ==========================================

    public async Task<OpenShiftRow?> GetOpenShiftAsync(
        string employeeId,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (1)
                ws.ShiftId,
                ws.EmployeeId,
                e.FullName,
                ws.OpeningCash,
                ws.OpenedAt
            FROM dbo.WorkShift ws
            INNER JOIN dbo.Employee e ON e.EmployeeId = ws.EmployeeId
            WHERE ws.EmployeeId = @EmployeeId
              AND ws.Status = 'OPEN'
              AND e.IsActive = 1
            ORDER BY ws.OpenedAt DESC;
            """;

        await using var cn = DbConnectionFactory.CreateConnection();
        await cn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20)
            .Value = employeeId;

        await using var rd = await cmd.ExecuteReaderAsync(ct);

        if (!await rd.ReadAsync(ct))
            return null;

        return new OpenShiftRow(
            rd.GetString(0),
            rd.GetString(1),
            rd.GetString(2),
            rd.GetDecimal(3),
            rd.GetDateTime(4));
    }

    // ==========================================
    // TÌM THUỐC VÀ ĐƠN VỊ BÁN
    // ==========================================

    public async Task<List<SaleMedicineRow>> SearchMedicinesAsync(
        string? keyword = null,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                m.MedicineId,
                m.MedicineName,
                m.RequiresPrescription,
                u.MedicineUnitId,
                u.UnitName,
                u.ConversionFactor,
                u.SalePrice,
                COALESCE(stock.AvailableStock, 0) AS AvailableStock
            FROM dbo.Medicine m
            INNER JOIN dbo.MedicineUnit u
                ON u.MedicineId = m.MedicineId
               AND u.IsActive = 1
            OUTER APPLY
            (
                SELECT SUM(
                    CASE
                        WHEN b.PhysicalStock - COALESCE(r.ReservedQuantity, 0) > 0
                        THEN b.PhysicalStock - COALESCE(r.ReservedQuantity, 0)
                        ELSE 0
                    END
                ) AS AvailableStock
                FROM dbo.MedicineBatch b
                OUTER APPLY
                (
                    SELECT SUM(ir.ReservedQuantity) AS ReservedQuantity
                    FROM dbo.InventoryReservation ir
                    WHERE ir.BatchId = b.BatchId
                      AND ir.Status = 'ACTIVE'
                      AND ir.ExpiresAt > SYSDATETIME()
                ) r
                WHERE b.MedicineId = m.MedicineId
                  AND b.BatchStatus = 'ACTIVE'
                  AND b.ExpiryDate >= CONVERT(date, SYSDATETIME())
            ) stock
            WHERE m.BusinessStatus = 'ACTIVE'
              AND (
                    @Keyword = N''
                    OR m.MedicineId LIKE @Pattern
                    OR m.MedicineName LIKE @Pattern
                    OR m.ActiveIngredient LIKE @Pattern
                    OR m.Barcode LIKE @Pattern
                  )
            ORDER BY m.MedicineName, u.ConversionFactor DESC;
            """;

        var rows = new List<SaleMedicineRow>();

        string key = (keyword ?? "").Trim();

        await using var cn = DbConnectionFactory.CreateConnection();
        await cn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@Keyword", SqlDbType.NVarChar, 200).Value = key;
        cmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 205).Value = $"%{key}%";

        await using var rd = await cmd.ExecuteReaderAsync(ct);

        while (await rd.ReadAsync(ct))
        {
            rows.Add(new SaleMedicineRow(
                rd.GetString(0),
                rd.GetString(1),
                rd.GetBoolean(2),
                rd.GetInt32(3),
                rd.GetString(4),
                rd.GetDecimal(5),
                rd.GetDecimal(6),
                rd.GetDecimal(7)));
        }

        return rows;
    }

    // ==========================================
    // TÌM KHÁCH HÀNG
    // ==========================================

    public async Task<List<CustomerRow>> SearchCustomersAsync(
        string? keyword = null,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (50)
                CustomerId, FullName, Phone
            FROM dbo.Customer
            WHERE IsActive = 1
              AND (
                    @Keyword = N''
                    OR CustomerId LIKE @Pattern
                    OR FullName LIKE @Pattern
                    OR Phone LIKE @Pattern
                  )
            ORDER BY FullName;
            """;

        var rows = new List<CustomerRow>();

        string key = (keyword ?? "").Trim();

        await using var cn = DbConnectionFactory.CreateConnection();
        await cn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@Keyword", SqlDbType.NVarChar, 200).Value = key;
        cmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 205).Value = $"%{key}%";

        await using var rd = await cmd.ExecuteReaderAsync(ct);

        while (await rd.ReadAsync(ct))
        {
            rows.Add(new CustomerRow(
                rd.GetString(0),
                rd.GetString(1),
                rd.GetString(2)));
        }

        return rows;
    }

    // ==========================================
    // THANH TOÁN VÀ HOÀN TẤT HÓA ĐƠN
    // ==========================================

    public async Task<string> CheckoutAsync(
        CheckoutRequest request,
        CancellationToken ct = default)
    {
        string invoiceId = "INV" + Guid.NewGuid().ToString("N")[..17].ToUpperInvariant();

        await using var cn = DbConnectionFactory.CreateConnection();
        await cn.OpenAsync(ct);

        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        try
        {
            // Kiểm tra nhân viên và ca làm việc.
            const string shiftSql = """
                SELECT COUNT(1)
                FROM dbo.WorkShift ws
                INNER JOIN dbo.Employee e ON e.EmployeeId = ws.EmployeeId
                WHERE ws.ShiftId = @ShiftId
                  AND ws.EmployeeId = @EmployeeId
                  AND ws.Status = 'OPEN'
                  AND e.IsActive = 1;
                """;

            await using (var cmd = new SqlCommand(shiftSql, cn, tx))
            {
                cmd.Parameters.Add("@ShiftId", SqlDbType.VarChar, 20)
                    .Value = request.ShiftId;
                cmd.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20)
                    .Value = request.EmployeeId;

                if (Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) != 1)
                    throw new InvalidOperationException(
                        "Ca làm việc không tồn tại, đã đóng hoặc không thuộc dược sĩ này.");
            }

            // Kiểm tra khách hàng nếu có.
            if (!string.IsNullOrWhiteSpace(request.CustomerId))
            {
                const string customerSql = """
                    SELECT COUNT(1)
                    FROM dbo.Customer
                    WHERE CustomerId = @CustomerId AND IsActive = 1;
                    """;

                await using var cmd = new SqlCommand(customerSql, cn, tx);
                cmd.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20)
                    .Value = request.CustomerId.Trim();

                if (Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) != 1)
                    throw new InvalidOperationException("Khách hàng không tồn tại hoặc đã ngừng hoạt động.");
            }

            // Kiểm tra phương thức thanh toán.
            string paymentMethod = request.PaymentMethod.Trim().ToUpperInvariant();

            if (paymentMethod is not ("CASH" or "TRANSFER" or "QR"))
                throw new InvalidOperationException("Phương thức thanh toán không hợp lệ.");

            if (request.Lines.Count == 0)
                throw new InvalidOperationException("Hóa đơn chưa có thuốc.");

            // Gộp các dòng cùng thuốc và cùng đơn vị để tránh kiểm tra trùng.
            var lines = request.Lines
                .GroupBy(x => new { x.MedicineId, x.MedicineUnitId })
                .Select(g => new SaleLine
                {
                    MedicineId = g.Key.MedicineId,
                    MedicineUnitId = g.Key.MedicineUnitId,
                    UnitName = g.First().UnitName,
                    ConversionFactor = g.First().ConversionFactor,
                    UnitPrice = g.First().UnitPrice,
                    Quantity = g.Sum(x => x.Quantity)
                })
                .ToList();

            decimal subtotal = lines.Sum(x => x.LineTotal);

            if (subtotal <= 0)
                throw new InvalidOperationException("Tổng tiền hóa đơn không hợp lệ.");

            // Kiểm tra đơn thuốc và số lượng được phép bán.
            if (lines.Any(x => IsPrescriptionRequiredAsyncPlaceholder(x)))
            {
                if (string.IsNullOrWhiteSpace(request.PrescriptionId))
                    throw new InvalidOperationException(
                        "Giỏ hàng có thuốc kê đơn. Vui lòng chọn đơn thuốc hợp lệ.");
            }

            if (!string.IsNullOrWhiteSpace(request.PrescriptionId))
            {
                await ValidatePrescriptionAsync(
                    cn, tx, request.PrescriptionId.Trim(),
                    request.EmployeeId, lines, ct);
            }

            // Tạo hóa đơn ở trạng thái PENDING trong transaction.
            const string invoiceSql = """
                INSERT dbo.Invoice
                (
                    InvoiceId, ShiftId, EmployeeId, CustomerId,
                    PrescriptionId, Subtotal, PointDiscount,
                    VoucherDiscount, TotalAmount, AmountPaid, Status
                )
                VALUES
                (
                    @InvoiceId, @ShiftId, @EmployeeId, @CustomerId,
                    @PrescriptionId, @Subtotal, 0,
                    0, @Subtotal, @Subtotal, 'PENDING'
                );
                """;

            await using (var cmd = new SqlCommand(invoiceSql, cn, tx))
            {
                cmd.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                cmd.Parameters.Add("@ShiftId", SqlDbType.VarChar, 20).Value = request.ShiftId;
                cmd.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = request.EmployeeId;
                cmd.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value =
                    Db(request.CustomerId);
                cmd.Parameters.Add("@PrescriptionId", SqlDbType.VarChar, 20).Value =
                    Db(request.PrescriptionId);

                AddMoneyParameter(cmd, "@Subtotal", subtotal);

                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Tạo chi tiết và trừ kho theo FEFO.
            foreach (var line in lines)
            {
                await ProcessSaleLineAsync(
                    cn, tx, invoiceId, request.EmployeeId, line, ct);
            }

            // Ghi nhận thanh toán.
            const string paymentSql = """
                INSERT dbo.InvoicePayment
                    (InvoiceId, PaymentMethod, Amount, TransactionReference)
                VALUES
                    (@InvoiceId, @PaymentMethod, @Amount, @Reference);

                UPDATE dbo.Invoice
                SET Status = 'PAID',
                    PaidAt = SYSDATETIME()
                WHERE InvoiceId = @InvoiceId;
                """;

            await using (var cmd = new SqlCommand(paymentSql, cn, tx))
            {
                cmd.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                cmd.Parameters.Add("@PaymentMethod", SqlDbType.VarChar, 20).Value = paymentMethod;
                cmd.Parameters.Add("@Reference", SqlDbType.VarChar, 100).Value =
                    Db(request.TransactionReference);

                AddMoneyParameter(cmd, "@Amount", subtotal);

                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Cập nhật doanh thu ca làm việc.
            string shiftAmountColumn = paymentMethod == "CASH"
                ? "CashSales"
                : "TransferSales";

            string updateShiftSql = $"""
                UPDATE dbo.WorkShift
                SET {shiftAmountColumn} = {shiftAmountColumn} + @Amount
                WHERE ShiftId = @ShiftId AND Status = 'OPEN';
                """;

            await using (var cmd = new SqlCommand(updateShiftSql, cn, tx))
            {
                AddMoneyParameter(cmd, "@Amount", subtotal);
                cmd.Parameters.Add("@ShiftId", SqlDbType.VarChar, 20)
                    .Value = request.ShiftId;

                if (await cmd.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("Không thể cập nhật doanh thu ca làm việc.");
            }

            // Cập nhật trạng thái đơn thuốc.
            if (!string.IsNullOrWhiteSpace(request.PrescriptionId))
            {
                await UpdatePrescriptionStatusAsync(
                    cn, tx, request.PrescriptionId.Trim(), ct);
            }

            await tx.CommitAsync(ct);
            return invoiceId;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ==========================================
    // XỬ LÝ MỘT DÒNG THUỐC
    // ==========================================

    private static async Task ProcessSaleLineAsync(
        SqlConnection cn,
        SqlTransaction tx,
        string invoiceId,
        string employeeId,
        SaleLine line,
        CancellationToken ct)
    {
        // Đọc lại thông tin đơn vị và giá từ database.
        const string unitSql = """
            SELECT u.UnitName, u.ConversionFactor, u.SalePrice,
                   m.BusinessStatus
            FROM dbo.MedicineUnit u
            INNER JOIN dbo.Medicine m ON m.MedicineId = u.MedicineId
            WHERE u.MedicineUnitId = @UnitId
              AND u.MedicineId = @MedicineId
              AND u.IsActive = 1;
            """;

        string unitName;
        decimal factor;
        decimal price;

        await using (var cmd = new SqlCommand(unitSql, cn, tx))
        {
            cmd.Parameters.Add("@UnitId", SqlDbType.Int).Value = line.MedicineUnitId;
            cmd.Parameters.Add("@MedicineId", SqlDbType.VarChar, 20).Value = line.MedicineId;

            await using var rd = await cmd.ExecuteReaderAsync(ct);

            if (!await rd.ReadAsync(ct))
                throw new InvalidOperationException("Đơn vị bán không còn hợp lệ.");

            unitName = rd.GetString(0);
            factor = rd.GetDecimal(1);
            price = rd.GetDecimal(2);

            if (rd.GetString(3) != "ACTIVE")
                throw new InvalidOperationException("Thuốc đã ngừng kinh doanh.");
        }

        if (line.Quantity <= 0)
            throw new InvalidOperationException("Số lượng bán phải lớn hơn 0.");

        decimal quantityBase = line.Quantity * factor;

        // Lấy các lô theo hạn sử dụng tăng dần.
        const string batchSql = """
            SELECT
                b.BatchId,
                b.PhysicalStock,
                COALESCE(r.ReservedQuantity, 0) AS ReservedQuantity
            FROM dbo.MedicineBatch b WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
            OUTER APPLY
            (
                SELECT SUM(ir.ReservedQuantity) AS ReservedQuantity
                FROM dbo.InventoryReservation ir WITH (UPDLOCK, HOLDLOCK)
                WHERE ir.BatchId = b.BatchId
                  AND ir.Status = 'ACTIVE'
                  AND ir.ExpiresAt > SYSDATETIME()
            ) r
            WHERE b.MedicineId = @MedicineId
              AND b.BatchStatus = 'ACTIVE'
              AND b.ExpiryDate >= CONVERT(date, SYSDATETIME())
              AND b.PhysicalStock > COALESCE(r.ReservedQuantity, 0)
            ORDER BY b.ExpiryDate ASC, b.CreatedAt ASC;
            """;

        var batches = new List<(string Id, decimal Available)>();

        await using (var cmd = new SqlCommand(batchSql, cn, tx))
        {
            cmd.Parameters.Add("@MedicineId", SqlDbType.VarChar, 20)
                .Value = line.MedicineId;

            await using var rd = await cmd.ExecuteReaderAsync(ct);

            while (await rd.ReadAsync(ct))
            {
                string id = rd.GetString(0);
                decimal physical = rd.GetDecimal(1);
                decimal reserved = rd.GetDecimal(2);
                decimal available = physical - reserved;

                if (available > 0)
                    batches.Add((id, available));
            }
        }

        decimal totalAvailable = batches.Sum(x => x.Available);

        if (totalAvailable < quantityBase)
        {
            throw new InvalidOperationException(
                $"Thuốc {line.MedicineId} không đủ tồn kho. " +
                $"Cần {quantityBase:N3}, còn {totalAvailable:N3} đơn vị cơ bản.");
        }

        decimal remaining = quantityBase;

        foreach (var batch in batches)
        {
            if (remaining <= 0)
                break;

            decimal take = Math.Min(remaining, batch.Available);
            decimal saleQuantity = take / factor;
            decimal lineTotal = saleQuantity * price;

            // Trừ tồn kho có điều kiện để không âm.
            const string updateStockSql = """
                UPDATE dbo.MedicineBatch
                SET PhysicalStock = PhysicalStock - @Quantity
                WHERE BatchId = @BatchId
                  AND PhysicalStock >= @Quantity;
                """;

            await using (var cmd = new SqlCommand(updateStockSql, cn, tx))
            {
                AddQuantityParameter(cmd, "@Quantity", take);
                cmd.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = batch.Id;

                if (await cmd.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("Tồn kho đã thay đổi. Vui lòng thử lại.");
            }

            // Lưu chi tiết hóa đơn theo từng lô.
            const string detailSql = """
                INSERT dbo.InvoiceDetail
                (
                    InvoiceId, MedicineId, MedicineUnitId, BatchId,
                    UnitNameSnapshot, ConversionFactorSnapshot,
                    Quantity, QuantityBase, UnitPrice,
                    DiscountAllocated, LineTotal
                )
                VALUES
                (
                    @InvoiceId, @MedicineId, @UnitId, @BatchId,
                    @UnitName, @Factor, @Quantity,
                    @QuantityBase, @Price, 0, @LineTotal
                );
                """;

            await using (var cmd = new SqlCommand(detailSql, cn, tx))
            {
                cmd.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                cmd.Parameters.Add("@MedicineId", SqlDbType.VarChar, 20).Value = line.MedicineId;
                cmd.Parameters.Add("@UnitId", SqlDbType.Int).Value = line.MedicineUnitId;
                cmd.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = batch.Id;
                cmd.Parameters.Add("@UnitName", SqlDbType.NVarChar, 30).Value = unitName;

                AddQuantityParameter(cmd, "@Factor", factor);
                AddQuantityParameter(cmd, "@Quantity", saleQuantity);
                AddQuantityParameter(cmd, "@QuantityBase", take);
                AddMoneyParameter(cmd, "@Price", price);
                AddMoneyParameter(cmd, "@LineTotal", lineTotal);

                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Ghi lịch sử xuất kho.
            const string transactionSql = """
                INSERT dbo.StockTransaction
                (
                    BatchId, TransactionType, QuantityChange,
                    ReferenceType, ReferenceCode, CreatedBy, Note
                )
                VALUES
                (
                    @BatchId, 'SALE', @QuantityChange,
                    'INVOICE', @InvoiceId, @EmployeeId, N'Xuất kho bán thuốc'
                );
                """;

            await using (var cmd = new SqlCommand(transactionSql, cn, tx))
            {
                cmd.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = batch.Id;
                AddQuantityParameter(cmd, "@QuantityChange", -take);
                cmd.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                cmd.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;

                await cmd.ExecuteNonQueryAsync(ct);
            }

            remaining -= take;
        }
    }

    // ==========================================
    // KIỂM TRA ĐƠN THUỐC
    // ==========================================

    private static async Task ValidatePrescriptionAsync(
        SqlConnection cn,
        SqlTransaction tx,
        string prescriptionId,
        string employeeId,
        List<SaleLine> lines,
        CancellationToken ct)
    {
        const string headerSql = """
            SELECT Status
            FROM dbo.Prescription WITH (UPDLOCK, HOLDLOCK)
            WHERE PrescriptionId = @PrescriptionId;
            """;

        string? status;

        await using (var cmd = new SqlCommand(headerSql, cn, tx))
        {
            cmd.Parameters.Add("@PrescriptionId", SqlDbType.VarChar, 20)
                .Value = prescriptionId;

            status = await cmd.ExecuteScalarAsync(ct) as string;
        }

        if (status is not ("UNSOLD" or "PARTIAL"))
            throw new InvalidOperationException("Đơn thuốc không tồn tại hoặc đã bán hết.");

        foreach (var line in lines)
        {
            const string prescriptionLineSql = """
                SELECT TOP (1)
                    pd.PrescribedQuantity,
                    pd.PrescribedUnit,
                    m.RequiresPrescription
                FROM dbo.PrescriptionDetail pd
                INNER JOIN dbo.Medicine m ON m.MedicineId = pd.MedicineId
                WHERE pd.PrescriptionId = @PrescriptionId
                  AND pd.MedicineId = @MedicineId
                  AND pd.PrescribedUnit = @UnitName;
                """;

            decimal prescribedQuantity;
            bool requiresPrescription;

            await using (var cmd = new SqlCommand(prescriptionLineSql, cn, tx))
            {
                cmd.Parameters.Add("@PrescriptionId", SqlDbType.VarChar, 20).Value = prescriptionId;
                cmd.Parameters.Add("@MedicineId", SqlDbType.VarChar, 20).Value = line.MedicineId;
                cmd.Parameters.Add("@UnitName", SqlDbType.NVarChar, 30).Value = line.UnitName;

                await using var rd = await cmd.ExecuteReaderAsync(ct);

                if (!await rd.ReadAsync(ct))
                {
                    if (await IsPrescriptionMedicineAsync(cn, tx, line.MedicineId, ct))
                        throw new InvalidOperationException(
                            $"Thuốc {line.MedicineId} không có trong đơn thuốc.");

                    continue;
                }

                prescribedQuantity = rd.GetDecimal(0);
                requiresPrescription = rd.GetBoolean(2);
            }

            if (!requiresPrescription)
                continue;

            const string soldSql = """
                SELECT COALESCE(SUM(d.Quantity), 0)
                FROM dbo.Invoice i
                INNER JOIN dbo.InvoiceDetail d ON d.InvoiceId = i.InvoiceId
                WHERE i.PrescriptionId = @PrescriptionId
                  AND i.Status IN ('PAID','RETURNED_PARTIAL','RETURNED_FULL')
                  AND d.MedicineId = @MedicineId
                  AND d.UnitNameSnapshot = @UnitName;
                """;

            decimal alreadySold;

            await using (var cmd = new SqlCommand(soldSql, cn, tx))
            {
                cmd.Parameters.Add("@PrescriptionId", SqlDbType.VarChar, 20).Value = prescriptionId;
                cmd.Parameters.Add("@MedicineId", SqlDbType.VarChar, 20).Value = line.MedicineId;
                cmd.Parameters.Add("@UnitName", SqlDbType.NVarChar, 30).Value = line.UnitName;

                alreadySold = Convert.ToDecimal(await cmd.ExecuteScalarAsync(ct));
            }

            if (alreadySold + line.Quantity > prescribedQuantity)
            {
                throw new InvalidOperationException(
                    $"Số lượng {line.UnitName} vượt quá số lượng được kê trong đơn thuốc.");
            }
        }
    }

    private static async Task<bool> IsPrescriptionMedicineAsync(
        SqlConnection cn,
        SqlTransaction tx,
        string medicineId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT RequiresPrescription
            FROM dbo.Medicine
            WHERE MedicineId = @MedicineId;
            """;

        await using var cmd = new SqlCommand(sql, cn, tx);
        cmd.Parameters.Add("@MedicineId", SqlDbType.VarChar, 20).Value = medicineId;

        object? value = await cmd.ExecuteScalarAsync(ct);
        return value != null && value != DBNull.Value && Convert.ToBoolean(value);
    }

    private static async Task UpdatePrescriptionStatusAsync(
        SqlConnection cn,
        SqlTransaction tx,
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
                    )
                    THEN 'SOLD'
                    ELSE 'PARTIAL'
                END
            FROM dbo.Prescription p
            WHERE p.PrescriptionId = @PrescriptionId;
            """;

        await using var cmd = new SqlCommand(sql, cn, tx);
        cmd.Parameters.Add("@PrescriptionId", SqlDbType.VarChar, 20)
            .Value = prescriptionId;

        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ==========================================
    // HÀM HỖ TRỢ
    // ==========================================

    private static bool IsPrescriptionRequiredAsyncPlaceholder(SaleLine line)
        => false;

    private static object Db(string? value)
        => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static void AddMoneyParameter(
        SqlCommand cmd, string name, decimal value)
    {
        var p = cmd.Parameters.Add(name, SqlDbType.Decimal);
        p.Precision = 18;
        p.Scale = 2;
        p.Value = value;
    }

    private static void AddQuantityParameter(
        SqlCommand cmd, string name, decimal value)
    {
        var p = cmd.Parameters.Add(name, SqlDbType.Decimal);
        p.Precision = 18;
        p.Scale = 3;
        p.Value = value;
    }
}
