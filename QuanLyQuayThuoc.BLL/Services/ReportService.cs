using QuanLyQuayThuoc.DAL.Repositories;

namespace QuanLyQuayThuoc.BLL.Services;

public class ReportService
{
    private readonly ReportRepository _repository;

    private readonly string _employeeId;
    private readonly string _roleId;

    public ReportService(string employeeId, string roleId)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("Mã nhân viên không hợp lệ.");

        _employeeId = employeeId;
        _roleId = (roleId ?? "").Trim().ToUpperInvariant();

        _repository = new ReportRepository();
    }

    // =========================
    // MODEL BÁO CÁO TỔNG HỢP
    // =========================

    public class DailySalesReport
    {
        public DateTime ReportDate { get; set; }

        public ReportRepository.DailySummary Summary { get; set; } = new();

        public List<ReportRepository.PaymentReport> Payments { get; set; } = [];

        public List<ReportRepository.InvoiceReport> Invoices { get; set; } = [];

        public List<ReportRepository.MedicineSalesReport> TopMedicines { get; set; } = [];

        public List<ReportRepository.ShiftReport> Shifts { get; set; } = [];
    }

    // =========================
    // PHÂN QUYỀN
    // =========================

    private void EnsureSalesReportPermission()
    {
        // tat ca deu xem dc
    }

    private void EnsureWarehouseReportPermission()
    {
        // tat ca deu xem dc
    }

    private string? GetEmployeeFilter()
    {
        return null;
    }

    private static void ValidateDate(DateTime date)
    {
        if (date == DateTime.MinValue || date == DateTime.MaxValue)
        {
            throw new ArgumentException("Ngày báo cáo không hợp lệ.");
        }
    }

    // =========================
    // 1. BÁO CÁO BÁN HÀNG THEO NGÀY
    // =========================

    public async Task<DailySalesReport> GetDailySalesReportAsync(
        DateTime date,
        CancellationToken ct = default)
    {
        EnsureSalesReportPermission();
        ValidateDate(date);

        string? employeeFilter = GetEmployeeFilter();

        var report = new DailySalesReport
        {
            ReportDate = date.Date
        };

        report.Summary = await _repository.GetDailySummaryAsync(
            date,
            employeeFilter,
            ct);

        report.Payments = await _repository.GetPaymentReportAsync(
            date,
            employeeFilter,
            ct);

        report.Invoices = await _repository.GetInvoicesAsync(
            date,
            employeeFilter,
            ct);

        report.TopMedicines = await _repository.GetTopMedicinesAsync(
            date,
            employeeFilter,
            20,
            ct);

        report.Shifts = await _repository.GetShiftReportsAsync(
            date,
            employeeFilter,
            ct);

        return report;
    }

    // =========================
    // 2. BÁO CÁO NHẬP KHO
    // =========================

    public async Task<List<ReportRepository.GoodsReceiptReport>>
        GetDailyGoodsReceiptsAsync(
            DateTime date,
            CancellationToken ct = default)
    {
        EnsureWarehouseReportPermission();
        ValidateDate(date);

        return await _repository.GetGoodsReceiptsAsync(
            date,
            GetEmployeeFilter(),
            ct);
    }

    // =========================
    // 3. CÁC BÁO CÁO RIÊNG
    // =========================

    public async Task<List<ReportRepository.InvoiceReport>>
        GetDailyInvoicesAsync(
            DateTime date,
            CancellationToken ct = default)
    {
        EnsureSalesReportPermission();
        ValidateDate(date);

        return await _repository.GetInvoicesAsync(
            date,
            GetEmployeeFilter(),
            ct);
    }

    public async Task<List<ReportRepository.MedicineSalesReport>>
        GetDailyTopMedicinesAsync(
            DateTime date,
            int top = 20,
            CancellationToken ct = default)
    {
        EnsureSalesReportPermission();
        ValidateDate(date);

        return await _repository.GetTopMedicinesAsync(
            date,
            GetEmployeeFilter(),
            top,
            ct);
    }

    public async Task<List<ReportRepository.ShiftReport>>
        GetDailyShiftsAsync(
            DateTime date,
            CancellationToken ct = default)
    {
        EnsureSalesReportPermission();
        ValidateDate(date);

        return await _repository.GetShiftReportsAsync(
            date,
            GetEmployeeFilter(),
            ct);
    }
}