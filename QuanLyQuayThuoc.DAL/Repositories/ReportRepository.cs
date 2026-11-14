using Microsoft.Data.SqlClient;
using System.Data;
using QuanLyQuayThuoc.DAL.Database;

namespace QuanLyQuayThuoc.DAL.Repositories;

public class ReportRepository
{
    // =========================
    // MODELS
    // =========================

    public class DailySummary
    {
        public int InvoiceCount { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal Subtotal { get; set; }
        public decimal PointDiscount { get; set; }
        public decimal VoucherDiscount { get; set; }
        public decimal TotalDiscount => PointDiscount + VoucherDiscount;

        public decimal CashAmount { get; set; }
        public decimal TransferAmount { get; set; }
        public decimal QRAmount { get; set; }

        public decimal TotalPaid =>
            CashAmount + TransferAmount + QRAmount;

        public decimal TotalRefund { get; set; }

        public decimal NetCashFlow =>
            TotalPaid - TotalRefund;
    }

    public class PaymentReport
    {
        public string PaymentMethod { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public int PaymentCount { get; set; }
    }

    public class InvoiceReport
    {
        public string InvoiceId { get; set; } = "";
        public DateTime? PaidAt { get; set; }
        public string EmployeeName { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public decimal Subtotal { get; set; }
        public decimal PointDiscount { get; set; }
        public decimal VoucherDiscount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public string Status { get; set; } = "";
    }

    public class MedicineSalesReport
    {
        public string MedicineId { get; set; } = "";
        public string MedicineName { get; set; } = "";
        public decimal QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class ShiftReport
    {
        public string ShiftId { get; set; } = "";
        public string EmployeeId { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public decimal OpeningCash { get; set; }
        public decimal CashSales { get; set; }
        public decimal TransferSales { get; set; }
        public decimal CashRefunds { get; set; }
        public decimal? ActualCash { get; set; }
        public decimal? DifferenceAmount { get; set; }
        public string Status { get; set; } = "";
    }

    public class GoodsReceiptReport
    {
        public string ReceiptId { get; set; } = "";
        public string SupplierName { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public DateTime? ConfirmedAt { get; set; }
        public decimal TotalAmount { get; set; }
    }

    // =========================
    // HELPER
    // =========================

    private static void AddDateRange(
        SqlCommand command,
        DateTime date)
    {
        DateTime fromDate = date.Date;
        DateTime toDate = fromDate.AddDays(1);

        command.Parameters.Add("@FromDate", SqlDbType.DateTime2).Value =
            fromDate;

        command.Parameters.Add("@ToDate", SqlDbType.DateTime2).Value =
            toDate;
    }

    private static void AddEmployeeFilter(
        SqlCommand command,
        string? employeeId)
    {
        command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value =
            string.IsNullOrWhiteSpace(employeeId)
                ? DBNull.Value
                : employeeId;
    }

    // =========================
    // 1. TỔNG QUAN DOANH THU
    // =========================

    public async Task<DailySummary> GetDailySummaryAsync(
        DateTime date,
        string? employeeId = null,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                COUNT(*) AS InvoiceCount,
                ISNULL(SUM(i.Subtotal), 0) AS Subtotal,
                ISNULL(SUM(i.PointDiscount), 0) AS PointDiscount,
                ISNULL(SUM(i.VoucherDiscount), 0) AS VoucherDiscount,
                ISNULL(SUM(i.TotalAmount), 0) AS GrossRevenue
            FROM dbo.Invoice i
            WHERE i.PaidAt >= @FromDate
              AND i.PaidAt < @ToDate
              AND i.Status IN ('PAID', 'RETURNED_PARTIAL', 'RETURNED_FULL')
              AND (@EmployeeId IS NULL OR i.EmployeeId = @EmployeeId);

            SELECT
                ISNULL(SUM(CASE
                    WHEN p.PaymentMethod = 'CASH' THEN p.Amount
                    ELSE 0 END), 0) AS CashAmount,

                ISNULL(SUM(CASE
                    WHEN p.PaymentMethod = 'TRANSFER' THEN p.Amount
                    ELSE 0 END), 0) AS TransferAmount,

                ISNULL(SUM(CASE
                    WHEN p.PaymentMethod = 'QR' THEN p.Amount
                    ELSE 0 END), 0) AS QRAmount
            FROM dbo.InvoicePayment p
            INNER JOIN dbo.Invoice i ON i.InvoiceId = p.InvoiceId
            WHERE p.PaidAt >= @FromDate
              AND p.PaidAt < @ToDate
              AND i.Status IN ('PAID', 'RETURNED_PARTIAL', 'RETURNED_FULL')
              AND (@EmployeeId IS NULL OR i.EmployeeId = @EmployeeId);

            SELECT ISNULL(SUM(r.TotalRefund), 0)
            FROM dbo.ReturnReceipt r
            WHERE r.ReturnDate >= @FromDate
              AND r.ReturnDate < @ToDate
              AND r.Status = 'COMPLETED'
              AND (@EmployeeId IS NULL OR r.EmployeeId = @EmployeeId);
            """;

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);

        AddDateRange(command, date);
        AddEmployeeFilter(command, employeeId);

        var result = new DailySummary();

        await using var reader = await command.ExecuteReaderAsync(ct);

        // Result 1: doanh thu và hóa đơn
        if (await reader.ReadAsync(ct))
        {
            result.InvoiceCount = Convert.ToInt32(reader["InvoiceCount"]);
            result.Subtotal = Convert.ToDecimal(reader["Subtotal"]);
            result.PointDiscount = Convert.ToDecimal(reader["PointDiscount"]);
            result.VoucherDiscount = Convert.ToDecimal(reader["VoucherDiscount"]);
            result.GrossRevenue = Convert.ToDecimal(reader["GrossRevenue"]);
        }

        // Result 2: phương thức thanh toán
        if (await reader.NextResultAsync(ct) &&
            await reader.ReadAsync(ct))
        {
            result.CashAmount = Convert.ToDecimal(reader["CashAmount"]);
            result.TransferAmount = Convert.ToDecimal(reader["TransferAmount"]);
            result.QRAmount = Convert.ToDecimal(reader["QRAmount"]);
        }

        // Result 3: tiền hoàn trả
        if (await reader.NextResultAsync(ct) &&
            await reader.ReadAsync(ct))
        {
            result.TotalRefund = Convert.ToDecimal(reader[0]);
        }

        return result;
    }

    // =========================
    // 2. CHI TIẾT HÓA ĐƠN
    // =========================

    public async Task<List<InvoiceReport>> GetInvoicesAsync(
        DateTime date,
        string? employeeId = null,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                i.InvoiceId,
                i.PaidAt,
                e.FullName AS EmployeeName,
                ISNULL(c.FullName, N'Khách lẻ') AS CustomerName,
                i.Subtotal,
                i.PointDiscount,
                i.VoucherDiscount,
                i.TotalAmount,
                i.AmountPaid,
                i.Status
            FROM dbo.Invoice i
            INNER JOIN dbo.Employee e ON e.EmployeeId = i.EmployeeId
            LEFT JOIN dbo.Customer c ON c.CustomerId = i.CustomerId
            WHERE i.PaidAt >= @FromDate
              AND i.PaidAt < @ToDate
              AND i.Status IN ('PAID', 'RETURNED_PARTIAL', 'RETURNED_FULL')
              AND (@EmployeeId IS NULL OR i.EmployeeId = @EmployeeId)
            ORDER BY i.PaidAt DESC;
            """;

        var list = new List<InvoiceReport>();

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);

        AddDateRange(command, date);
        AddEmployeeFilter(command, employeeId);

        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            list.Add(new InvoiceReport
            {
                InvoiceId = reader["InvoiceId"].ToString()!,
                PaidAt = reader["PaidAt"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["PaidAt"]),
                EmployeeName = reader["EmployeeName"].ToString()!,
                CustomerName = reader["CustomerName"].ToString()!,
                Subtotal = Convert.ToDecimal(reader["Subtotal"]),
                PointDiscount = Convert.ToDecimal(reader["PointDiscount"]),
                VoucherDiscount = Convert.ToDecimal(reader["VoucherDiscount"]),
                TotalAmount = Convert.ToDecimal(reader["TotalAmount"]),
                AmountPaid = Convert.ToDecimal(reader["AmountPaid"]),
                Status = reader["Status"].ToString()!
            });
        }

        return list;
    }

    // =========================
    // 3. DOANH THU THEO PHƯƠNG THỨC THANH TOÁN
    // =========================

    public async Task<List<PaymentReport>> GetPaymentReportAsync(
        DateTime date,
        string? employeeId = null,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                p.PaymentMethod,
                SUM(p.Amount) AS TotalAmount,
                COUNT(*) AS PaymentCount
            FROM dbo.InvoicePayment p
            INNER JOIN dbo.Invoice i ON i.InvoiceId = p.InvoiceId
            WHERE p.PaidAt >= @FromDate
              AND p.PaidAt < @ToDate
              AND i.Status IN ('PAID', 'RETURNED_PARTIAL', 'RETURNED_FULL')
              AND (@EmployeeId IS NULL OR i.EmployeeId = @EmployeeId)
            GROUP BY p.PaymentMethod
            ORDER BY p.PaymentMethod;
            """;

        var list = new List<PaymentReport>();

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);

        AddDateRange(command, date);
        AddEmployeeFilter(command, employeeId);

        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            list.Add(new PaymentReport
            {
                PaymentMethod = reader["PaymentMethod"].ToString()!,
                TotalAmount = Convert.ToDecimal(reader["TotalAmount"]),
                PaymentCount = Convert.ToInt32(reader["PaymentCount"])
            });
        }

        return list;
    }

    // =========================
    // 4. THUỐC BÁN CHẠY TRONG NGÀY
    // =========================

    public async Task<List<MedicineSalesReport>> GetTopMedicinesAsync(
        DateTime date,
        string? employeeId = null,
        int top = 20,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (@Top)
                m.MedicineId,
                m.MedicineName,
                SUM(d.Quantity) AS QuantitySold,
                SUM(d.LineTotal) AS Revenue
            FROM dbo.InvoiceDetail d
            INNER JOIN dbo.Invoice i ON i.InvoiceId = d.InvoiceId
            INNER JOIN dbo.Medicine m ON m.MedicineId = d.MedicineId
            WHERE i.PaidAt >= @FromDate
              AND i.PaidAt < @ToDate
              AND i.Status IN ('PAID', 'RETURNED_PARTIAL', 'RETURNED_FULL')
              AND (@EmployeeId IS NULL OR i.EmployeeId = @EmployeeId)
            GROUP BY m.MedicineId, m.MedicineName
            ORDER BY SUM(d.Quantity) DESC;
            """;

        var list = new List<MedicineSalesReport>();

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);

        AddDateRange(command, date);
        AddEmployeeFilter(command, employeeId);

        command.Parameters.Add("@Top", SqlDbType.Int).Value =
            Math.Clamp(top, 1, 100);

        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            list.Add(new MedicineSalesReport
            {
                MedicineId = reader["MedicineId"].ToString()!,
                MedicineName = reader["MedicineName"].ToString()!,
                QuantitySold = Convert.ToDecimal(reader["QuantitySold"]),
                Revenue = Convert.ToDecimal(reader["Revenue"])
            });
        }

        return list;
    }

    // =========================
    // 5. BÁO CÁO CA LÀM VIỆC
    // =========================

    public async Task<List<ShiftReport>> GetShiftReportsAsync(
        DateTime date,
        string? employeeId = null,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                ws.ShiftId,
                ws.EmployeeId,
                e.FullName AS EmployeeName,
                ws.OpenedAt,
                ws.ClosedAt,
                ws.OpeningCash,
                ws.CashSales,
                ws.TransferSales,
                ws.CashRefunds,
                ws.ActualCash,
                ws.DifferenceAmount,
                ws.Status
            FROM dbo.WorkShift ws
            INNER JOIN dbo.Employee e ON e.EmployeeId = ws.EmployeeId
            WHERE
                (
                    (ws.OpenedAt >= @FromDate AND ws.OpenedAt < @ToDate)
                    OR
                    (ws.ClosedAt >= @FromDate AND ws.ClosedAt < @ToDate)
                )
                AND (@EmployeeId IS NULL OR ws.EmployeeId = @EmployeeId)
            ORDER BY ws.OpenedAt DESC;
            """;

        var list = new List<ShiftReport>();

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);

        AddDateRange(command, date);
        AddEmployeeFilter(command, employeeId);

        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            list.Add(new ShiftReport
            {
                ShiftId = reader["ShiftId"].ToString()!,
                EmployeeId = reader["EmployeeId"].ToString()!,
                EmployeeName = reader["EmployeeName"].ToString()!,
                OpenedAt = Convert.ToDateTime(reader["OpenedAt"]),
                ClosedAt = reader["ClosedAt"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["ClosedAt"]),
                OpeningCash = Convert.ToDecimal(reader["OpeningCash"]),
                CashSales = Convert.ToDecimal(reader["CashSales"]),
                TransferSales = Convert.ToDecimal(reader["TransferSales"]),
                CashRefunds = Convert.ToDecimal(reader["CashRefunds"]),
                ActualCash = reader["ActualCash"] == DBNull.Value
                    ? null
                    : Convert.ToDecimal(reader["ActualCash"]),
                DifferenceAmount = reader["DifferenceAmount"] == DBNull.Value
                    ? null
                    : Convert.ToDecimal(reader["DifferenceAmount"]),
                Status = reader["Status"].ToString()!
            });
        }

        return list;
    }

    // =========================
    // 6. BÁO CÁO NHẬP KHO TRONG NGÀY
    // =========================

    public async Task<List<GoodsReceiptReport>> GetGoodsReceiptsAsync(
        DateTime date,
        string? employeeId = null,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                gr.ReceiptId,
                s.SupplierName,
                e.FullName AS EmployeeName,
                gr.ConfirmedAt,
                ISNULL(SUM(
                    gd.Quantity * gd.PurchaseUnitPrice
                ), 0) AS TotalAmount
            FROM dbo.GoodsReceipt gr
            INNER JOIN dbo.Supplier s ON s.SupplierId = gr.SupplierId
            INNER JOIN dbo.Employee e ON e.EmployeeId = gr.CreatedBy
            LEFT JOIN dbo.GoodsReceiptDetail gd ON gd.ReceiptId = gr.ReceiptId
            WHERE gr.Status = 'CONFIRMED'
              AND gr.ConfirmedAt >= @FromDate
              AND gr.ConfirmedAt < @ToDate
              AND (@EmployeeId IS NULL OR gr.CreatedBy = @EmployeeId)
            GROUP BY
                gr.ReceiptId,
                s.SupplierName,
                e.FullName,
                gr.ConfirmedAt
            ORDER BY gr.ConfirmedAt DESC;
            """;

        var list = new List<GoodsReceiptReport>();

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);

        AddDateRange(command, date);
        AddEmployeeFilter(command, employeeId);

        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            list.Add(new GoodsReceiptReport
            {
                ReceiptId = reader["ReceiptId"].ToString()!,
                SupplierName = reader["SupplierName"].ToString()!,
                EmployeeName = reader["EmployeeName"].ToString()!,
                ConfirmedAt = reader["ConfirmedAt"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["ConfirmedAt"]),
                TotalAmount = Convert.ToDecimal(reader["TotalAmount"])
            });
        }

        return list;
    }
}