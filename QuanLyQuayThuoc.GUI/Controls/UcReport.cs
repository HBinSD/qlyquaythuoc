using QuanLyQuayThuoc.BLL.Services;
using QuanLyQuayThuoc.DAL.Repositories;
using System.Drawing;
using System.Windows.Forms;

namespace QuanLyQuayThuoc.GUI.Controls;

public class UcReport : UserControl
{
    private readonly ReportService _service;
    private readonly string _roleId;

    private DateTimePicker dtpReportDate = null!;
    private Button btnLoad = null!;

    private Label lblRevenue = null!;
    private Label lblInvoiceCount = null!;
    private Label lblMedicineCount = null!;
    private Label lblDiscount = null!;
    private Label lblRefund = null!;
    private Label lblNetCash = null!;

    private DataGridView dgvInvoices = null!;
    private DataGridView dgvMedicines = null!;
    private DataGridView dgvPayments = null!;
    private DataGridView dgvShifts = null!;
    private DataGridView dgvGoodsReceipts = null!;

    private TabControl tabReport = null!;

    public UcReport(string employeeId, string roleId)
    {
        _roleId = (roleId ?? "").Trim().ToUpperInvariant();

        _service = new ReportService(employeeId, _roleId);

        InitializeComponent();

        Load += async (_, _) => await LoadReportAsync();
    }

    // ==========================================
    // KHỞI TẠO GIAO DIỆN
    // ==========================================

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(245, 247, 250);
        Padding = new Padding(15);

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = BackColor
        };

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 125));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Tiêu đề
        var header = new Label
        {
            Text = "BÁO CÁO - THỐNG KÊ",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 55, 80),
            TextAlign = ContentAlignment.MiddleLeft
        };

        mainLayout.Controls.Add(header, 0, 0);

        // Thanh chọn ngày
        var filterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };

        var lblDate = new Label
        {
            Text = "Ngày báo cáo:",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 8, 10, 0)
        };

        dtpReportDate = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd/MM/yyyy",
            Width = 150,
            Font = new Font("Segoe UI", 10),
            Value = DateTime.Today
        };

        btnLoad = new Button
        {
            Text = "Xem báo cáo",
            Width = 130,
            Height = 32,
            BackColor = Color.FromArgb(35, 120, 190),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Margin = new Padding(15, 0, 0, 0)
        };

        btnLoad.FlatAppearance.BorderSize = 0;
        btnLoad.Click += async (_, _) => await LoadReportAsync();

        filterPanel.Controls.Add(lblDate);
        filterPanel.Controls.Add(dtpReportDate);
        filterPanel.Controls.Add(btnLoad);

        mainLayout.Controls.Add(filterPanel, 0, 1);

        // Các thẻ thống kê
        var cardPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(0, 5, 0, 5)
        };

        for (int i = 0; i < 3; i++)
            cardPanel.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 33.33f));

        cardPanel.RowStyles.Add(
            new RowStyle(SizeType.Percent, 50));

        cardPanel.RowStyles.Add(
            new RowStyle(SizeType.Percent, 50));

        lblRevenue = CreateCard(cardPanel, "Doanh thu bán hàng", 0, 0);
        lblInvoiceCount = CreateCard(cardPanel, "Số hóa đơn", 1, 0);
        lblMedicineCount = CreateCard(cardPanel, "Số lượng thuốc bán", 2, 0);

        lblDiscount = CreateCard(cardPanel, "Tổng giảm giá", 0, 1);
        lblRefund = CreateCard(cardPanel, "Tiền hoàn trả", 1, 1);
        lblNetCash = CreateCard(cardPanel, "Tiền thực thu sau hoàn", 2, 1);

        mainLayout.Controls.Add(cardPanel, 0, 2);

        // Các tab báo cáo
        tabReport = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9)
        };

        dgvInvoices = CreateGrid();
        dgvMedicines = CreateGrid();
        dgvPayments = CreateGrid();
        dgvShifts = CreateGrid();
        dgvGoodsReceipts = CreateGrid();

        tabReport.TabPages.Add(
            CreateTab("Hóa đơn", dgvInvoices));

        tabReport.TabPages.Add(
            CreateTab("Thuốc bán chạy", dgvMedicines));

        tabReport.TabPages.Add(
            CreateTab("Thanh toán", dgvPayments));

        tabReport.TabPages.Add(
            CreateTab("Ca làm việc", dgvShifts));

        tabReport.TabPages.Add(
            CreateTab("Nhập kho", dgvGoodsReceipts));

        mainLayout.Controls.Add(tabReport, 0, 3);

        Controls.Add(mainLayout);
    }

    // ==========================================
    // TẠO THẺ THỐNG KÊ
    // ==========================================

    private Label CreateCard(
        TableLayoutPanel parent,
        string title,
        int column,
        int row)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(4),
            Padding = new Padding(10)
        };

        var lblTitle = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 25,
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.Gray
        };

        var lblValue = new Label
        {
            Text = "0",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 65, 100),
            TextAlign = ContentAlignment.MiddleLeft
        };

        card.Controls.Add(lblValue);
        card.Controls.Add(lblTitle);

        parent.Controls.Add(card, column, row);

        return lblValue;
    }

    // ==========================================
    // TẠO DATAGRIDVIEW
    // ==========================================

    private DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            AutoGenerateColumns = true,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 38,
            RowTemplate = { Height = 32 }
        };

        grid.ColumnHeadersDefaultCellStyle.BackColor =
            Color.FromArgb(235, 241, 247);

        grid.ColumnHeadersDefaultCellStyle.ForeColor =
            Color.FromArgb(40, 55, 75);

        grid.ColumnHeadersDefaultCellStyle.Font =
            new Font("Segoe UI", 9, FontStyle.Bold);

        grid.DefaultCellStyle.Font =
            new Font("Segoe UI", 9);

        grid.DefaultCellStyle.SelectionBackColor =
            Color.FromArgb(220, 235, 250);

        grid.DefaultCellStyle.SelectionForeColor =
            Color.Black;

        grid.AlternatingRowsDefaultCellStyle.BackColor =
            Color.FromArgb(248, 250, 252);

        return grid;
    }

    private TabPage CreateTab(
        string title,
        DataGridView grid)
    {
        var page = new TabPage(title)
        {
            BackColor = Color.White,
            Padding = new Padding(5)
        };

        page.Controls.Add(grid);

        return page;
    }

    // ==========================================
    // TẢI BÁO CÁO
    // ==========================================

    private async Task LoadReportAsync()
    {
        btnLoad.Enabled = false;
        btnLoad.Text = "Đang tải...";
        
        try
        {
            DateTime selectedDate = dtpReportDate.Value.Date;
            var report = await _service.GetDailySalesReportAsync(
                selectedDate);

            DisplaySalesSummary(report.Summary);

            dgvInvoices.DataSource = report.Invoices;
            dgvMedicines.DataSource = report.TopMedicines;
            dgvPayments.DataSource = report.Payments;
            dgvShifts.DataSource = report.Shifts;

            FormatGrid(dgvInvoices);
            FormatGrid(dgvMedicines);
            FormatGrid(dgvPayments);
            FormatGrid(dgvShifts);

            var receipts = await _service.GetDailyGoodsReceiptsAsync(
                selectedDate);

            dgvGoodsReceipts.DataSource = receipts;
            FormatGrid(dgvGoodsReceipts);
        }
        catch (UnauthorizedAccessException ex)
        {
            MessageBox.Show(
                ex.Message,
                "Không có quyền truy cập",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Không thể tải báo cáo.\n\nChi tiết: " + ex.Message,
                "Lỗi báo cáo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            btnLoad.Enabled = true;
            btnLoad.Text = "Xem báo cáo";
        }
    }

    // ==========================================
    // HIỂN THỊ THỐNG KÊ
    // ==========================================

    private void DisplaySalesSummary(
        ReportRepository.DailySummary summary)
    {
        lblRevenue.Text =
            summary.GrossRevenue.ToString("N0") + " đ";

        lblInvoiceCount.Text =
            summary.InvoiceCount.ToString("N0");

        lblMedicineCount.Text =
            dgvMedicines.DataSource is
                List<ReportRepository.MedicineSalesReport> medicines
                ? medicines.Sum(x => x.QuantitySold).ToString("N0")
                : "0";

        lblDiscount.Text =
            summary.TotalDiscount.ToString("N0") + " đ";

        lblRefund.Text =
            summary.TotalRefund.ToString("N0") + " đ";

        lblNetCash.Text =
            summary.NetCashFlow.ToString("N0") + " đ";
    }

    // ==========================================
    // ĐỊNH DẠNG CỘT
    // ==========================================

    private void FormatGrid(DataGridView grid)
    {
        if (grid.Columns.Count == 0)
            return;

        foreach (DataGridViewColumn column in grid.Columns)
        {
            string name = column.Name;

            if (name.Contains("Amount", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Revenue", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Discount", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Cash", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Price", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Subtotal", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Total", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Paid", StringComparison.OrdinalIgnoreCase))
            {
                column.DefaultCellStyle.Format = "N0";
                column.DefaultCellStyle.Alignment =
                    DataGridViewContentAlignment.MiddleRight;
            }

            if (name.Contains("Date", StringComparison.OrdinalIgnoreCase)
                || name.Contains("At", StringComparison.OrdinalIgnoreCase))
            {
                column.DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
            }

            if (name == "QuantitySold")
            {
                column.DefaultCellStyle.Format = "N3";
            }
        }

        // Đổi tên cột cho thân thiện hơn.
        SetHeader(grid, "InvoiceId", "Mã hóa đơn");
        SetHeader(grid, "PaidAt", "Thời gian thanh toán");
        SetHeader(grid, "EmployeeName", "Nhân viên");
        SetHeader(grid, "CustomerName", "Khách hàng");
        SetHeader(grid, "Subtotal", "Tiền hàng");
        SetHeader(grid, "PointDiscount", "Giảm điểm");
        SetHeader(grid, "VoucherDiscount", "Giảm voucher");
        SetHeader(grid, "TotalAmount", "Tổng tiền");
        SetHeader(grid, "AmountPaid", "Đã thanh toán");
        SetHeader(grid, "Status", "Trạng thái");

        SetHeader(grid, "MedicineId", "Mã thuốc");
        SetHeader(grid, "MedicineName", "Tên thuốc");
        SetHeader(grid, "QuantitySold", "Số lượng bán");
        SetHeader(grid, "Revenue", "Doanh thu");

        SetHeader(grid, "PaymentMethod", "Phương thức");
        SetHeader(grid, "PaymentCount", "Số lần thanh toán");

        SetHeader(grid, "ShiftId", "Mã ca");
        SetHeader(grid, "EmployeeId", "Mã nhân viên");
        SetHeader(grid, "OpenedAt", "Giờ mở ca");
        SetHeader(grid, "ClosedAt", "Giờ đóng ca");
        SetHeader(grid, "OpeningCash", "Tiền đầu ca");
        SetHeader(grid, "CashSales", "Doanh thu tiền mặt");
        SetHeader(grid, "TransferSales", "Doanh thu chuyển khoản");
        SetHeader(grid, "CashRefunds", "Tiền hoàn trả");
        SetHeader(grid, "ActualCash", "Tiền thực tế");
        SetHeader(grid, "DifferenceAmount", "Chênh lệch");

        SetHeader(grid, "ReceiptId", "Mã phiếu nhập");
        SetHeader(grid, "SupplierName", "Nhà cung cấp");
        SetHeader(grid, "ConfirmedAt", "Thời gian xác nhận");
    }

    private void SetHeader(
        DataGridView grid,
        string columnName,
        string headerText)
    {
        if (grid.Columns.Contains(columnName))
        {
            grid.Columns[columnName].HeaderText = headerText;
        }
    }
}