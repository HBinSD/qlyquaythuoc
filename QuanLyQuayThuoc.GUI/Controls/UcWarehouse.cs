
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuanLyQuayThuoc.BLL.Services;
using QuanLyQuayThuoc.DAL.Repositories;
using QuanLyQuayThuoc.GUI.Forms;
using QuanLyQuayThuoc.GUI.Session;

namespace QuanLyQuayThuoc.GUI.Controls;

public sealed class UcWarehouse : UserControl
{
    private readonly WarehouseService _service;

    // =====================================================
    // CONTROLS
    // =====================================================

    private readonly TabControl tabMain = new();

    private readonly DataGridView dgvInventory = new();
    private readonly DataGridView dgvBatches = new();
    private readonly DataGridView dgvExpiring = new();
    private readonly DataGridView dgvTransactions = new();
    private readonly DataGridView dgvReceipts = new();

    private readonly TextBox txtSearch = new();
    private readonly NumericUpDown numDays = new();

    private readonly Button btnSearch = new();
    private readonly Button btnRefresh = new();
    private readonly Button btnStocktakeProposal = new();
    private readonly Button btnDisposalProposal = new();

    private readonly Label lblTitle = new();
    private readonly Label lblStatus = new();

    private bool _isLoading;

    // =====================================================
    // CONSTRUCTOR
    // =====================================================

    public UcWarehouse()
    {
        _service = new WarehouseService();

        InitializeComponent();

        Load += UcWarehouse_Load;
    }

    public UcWarehouse(WarehouseService service)
    {
        _service = service
            ?? throw new ArgumentNullException(nameof(service));

        InitializeComponent();

        Load += UcWarehouse_Load;
    }

    // =====================================================
    // INITIALIZE UI
    // =====================================================

    private void InitializeComponent()
    {
        SuspendLayout();

        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 9F);

        // Main layout
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20),
            BackColor = BackColor
        };

        mainLayout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 55));

        mainLayout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 65));

        mainLayout.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100));

        // =================================================
        // TITLE
        // =================================================

        lblTitle.Text = "QUẢN LÝ KHO THUỐC";
        lblTitle.Font = new Font(
            "Segoe UI",
            17F,
            FontStyle.Bold);

        lblTitle.ForeColor = Color.FromArgb(35, 55, 80);
        lblTitle.Dock = DockStyle.Fill;
        lblTitle.TextAlign = ContentAlignment.MiddleLeft;

        mainLayout.Controls.Add(lblTitle, 0, 0);

        // =================================================
        // TOOLBAR
        // =================================================

        var toolbar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(10)
        };

        txtSearch.PlaceholderText = "Nhập mã thuốc, tên thuốc...";
        txtSearch.Font = new Font("Segoe UI", 9F);
        txtSearch.Width = 260;
        txtSearch.Location = new Point(10, 14);

        txtSearch.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await LoadCurrentTabAsync();
            }
        };

        btnSearch.Text = "Tìm kiếm";
        btnSearch.Size = new Size(100, 34);
        btnSearch.Location = new Point(280, 10);
        btnSearch.BackColor = Color.FromArgb(37, 99, 235);
        btnSearch.ForeColor = Color.White;
        btnSearch.FlatStyle = FlatStyle.Flat;
        btnSearch.FlatAppearance.BorderSize = 0;
        btnSearch.Cursor = Cursors.Hand;

        btnSearch.Click += async (_, _) =>
        {
            await LoadCurrentTabAsync();
        };

        btnRefresh.Text = "Làm mới";
        btnRefresh.Size = new Size(90, 34);
        btnRefresh.Location = new Point(390, 10);
        btnRefresh.FlatStyle = FlatStyle.Flat;
        btnRefresh.Cursor = Cursors.Hand;

        btnRefresh.Click += async (_, _) =>
        {
            txtSearch.Clear();
            await LoadCurrentTabAsync();
        };

        lblStatus.Text = "Sẵn sàng";
        lblStatus.AutoSize = true;
        lblStatus.Location = new Point(770, 19);
        lblStatus.ForeColor = Color.Gray;

        ConfigureProposalButton(btnStocktakeProposal, "Đề xuất kiểm kê", 500);
        btnStocktakeProposal.Click += async (_, _) => await OpenProposalDialogAsync(isDisposal: false);
        ConfigureProposalButton(btnDisposalProposal, "Đề xuất xuất hủy", 635);
        btnDisposalProposal.Click += async (_, _) => await OpenProposalDialogAsync(isDisposal: true);

        toolbar.Controls.Add(txtSearch);
        toolbar.Controls.Add(btnSearch);
        toolbar.Controls.Add(btnRefresh);
        toolbar.Controls.Add(btnStocktakeProposal);
        toolbar.Controls.Add(btnDisposalProposal);
        toolbar.Controls.Add(lblStatus);
        ConfigureProposalAccess();

        mainLayout.Controls.Add(toolbar, 0, 1);

        // =================================================
        // TAB CONTROL
        // =================================================

        tabMain.Dock = DockStyle.Fill;
        tabMain.Font = new Font("Segoe UI", 9F);

        tabMain.TabPages.Add(CreateTab(
            "Tồn kho",
            dgvInventory));

        tabMain.TabPages.Add(CreateTab(
            "Lô thuốc",
            dgvBatches));

        tabMain.TabPages.Add(CreateTab(
            "Sắp hết hạn",
            dgvExpiring));

        tabMain.TabPages.Add(CreateTab(
            "Lịch sử kho",
            dgvTransactions));

        tabMain.TabPages.Add(CreateTab(
            "Phiếu nhập",
            dgvReceipts));

        tabMain.SelectedIndexChanged += async (_, _) =>
        {
            UpdateToolbarForCurrentTab();
            await LoadCurrentTabAsync();
        };

        // Double click receipt to see details
        dgvReceipts.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex < 0)
                return;

            await ShowReceiptDetailsAsync();
        };

        mainLayout.Controls.Add(tabMain, 0, 2);

        Controls.Add(mainLayout);

        UpdateToolbarForCurrentTab();

        ResumeLayout(false);
    }

    // =====================================================
    // CREATE TAB
    // =====================================================

    private TabPage CreateTab(
        string title,
        DataGridView grid)
    {
        var page = new TabPage(title)
        {
            BackColor = Color.White,
            Padding = new Padding(8)
        };

        ConfigureGrid(grid);

        grid.Dock = DockStyle.Fill;

        page.Controls.Add(grid);

        return page;
    }

    // =====================================================
    // DATAGRIDVIEW STYLE
    // =====================================================

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.AutoGenerateColumns = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;

        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.SelectionMode =
            DataGridViewSelectionMode.FullRowSelect;

        grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode =
            DataGridViewAutoSizeColumnsMode.Fill;

        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;

        grid.EnableHeadersVisualStyles = false;

        grid.ColumnHeadersDefaultCellStyle.BackColor =
            Color.FromArgb(240, 244, 248);

        grid.ColumnHeadersDefaultCellStyle.ForeColor =
            Color.FromArgb(40, 55, 75);

        grid.ColumnHeadersDefaultCellStyle.Font =
            new Font("Segoe UI", 9F, FontStyle.Bold);

        grid.ColumnHeadersHeight = 42;

        grid.DefaultCellStyle.Font =
            new Font("Segoe UI", 9F);

        grid.DefaultCellStyle.SelectionBackColor =
            Color.FromArgb(219, 234, 254);

        grid.DefaultCellStyle.SelectionForeColor =
            Color.FromArgb(30, 50, 80);

        grid.RowTemplate.Height = 36;

        grid.AlternatingRowsDefaultCellStyle.BackColor =
            Color.FromArgb(249, 251, 253);
    }

    // =====================================================
    // FORM LOAD
    // =====================================================

    private async void UcWarehouse_Load(
        object? sender,
        EventArgs e)
    {
        await LoadCurrentTabAsync();
    }

    private static void ConfigureProposalButton(Button button, string text, int left)
    {
        button.Text = text;
        button.Size = new Size(125, 34);
        button.Location = new Point(left, 10);
        button.BackColor = Color.FromArgb(37, 99, 235);
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Cursor = Cursors.Hand;
    }

    private void ConfigureProposalAccess()
    {
        string? role = SessionManager.CurrentUser?.RoleId;
        bool canCreate = string.Equals(role, "WAREHOUSE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase);
        btnStocktakeProposal.Visible = canCreate;
        btnDisposalProposal.Visible = canCreate;
    }

    private async Task OpenProposalDialogAsync(bool isDisposal)
    {
        var user = SessionManager.CurrentUser;
        if (user is null
            || (!string.Equals(user.RoleId, "WAREHOUSE", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(user.RoleId, "ADMIN", StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(
                "Chỉ nhân viên kho hoặc quản lý mới được tạo đề xuất kho.",
                "Không đủ quyền",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new WarehouseProposalDialog(
            _service,
            isDisposal,
            user.EmployeeId,
            user.RoleId);
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            MessageBox.Show(
                $"Đã gửi đề xuất {dialog.ProposalId} chờ quản lý phê duyệt. Tồn kho chưa thay đổi.",
                "Đã gửi đề xuất",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            await LoadCurrentTabAsync();
        }
    }

    // =====================================================
    // UPDATE TOOLBAR
    // =====================================================

    private void UpdateToolbarForCurrentTab()
    {
        bool isExpiring = tabMain.SelectedIndex == 2;

        numDays.Visible = isExpiring;

        if (isExpiring)
        {
            lblStatus.Text = "Số ngày kiểm tra:";
        }
        else
        {
            lblStatus.Text = "Sẵn sàng";
        }
    }

    // =====================================================
    // LOAD CURRENT TAB
    // =====================================================

    private async Task LoadCurrentTabAsync()
    {
        if (_isLoading || IsDisposed)
            return;

        _isLoading = true;

        SetLoadingState(true);

        try
        {
            string keyword = txtSearch.Text.Trim();

            switch (tabMain.SelectedIndex)
            {
                case 0:
                    await LoadInventoryAsync(keyword);
                    break;

                case 1:
                    await LoadBatchesAsync();
                    break;

                case 2:
                    await LoadExpiringAsync();
                    break;

                case 3:
                    await LoadTransactionsAsync(keyword);
                    break;

                case 4:
                    await LoadReceiptsAsync(keyword);
                    break;
            }

            lblStatus.Text = "Tải dữ liệu thành công";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Không thể tải dữ liệu kho.\n\n" + ex.Message,
                "Lỗi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            lblStatus.Text = "Có lỗi xảy ra";
        }
        finally
        {
            _isLoading = false;
            SetLoadingState(false);
        }
    }

    // =====================================================
    // LOAD INVENTORY
    // =====================================================

    private async Task LoadInventoryAsync(string keyword)
    {
        var data = await _service.GetInventoryAsync(keyword);

        dgvInventory.DataSource = data;

        SetHeader(dgvInventory, "MedicineId", "Mã thuốc");
        SetHeader(dgvInventory, "MedicineName", "Tên thuốc");
        SetHeader(dgvInventory, "BaseUnit", "Đơn vị");
        SetHeader(dgvInventory, "MinimumStock", "Tồn tối thiểu");
        SetHeader(dgvInventory, "PhysicalStock", "Tồn thực tế");
        SetHeader(dgvInventory, "ReservedStock", "Đã giữ");
        SetHeader(dgvInventory, "AvailableStock", "Có thể bán");
        SetHeader(dgvInventory, "BatchCount", "Số lô");
        SetHeader(dgvInventory, "StockStatus", "Trạng thái");

        SetNumberFormat(dgvInventory,
            "MinimumStock",
            "N2");

        SetNumberFormat(dgvInventory,
            "PhysicalStock",
            "N2");

        SetNumberFormat(dgvInventory,
            "ReservedStock",
            "N2");

        SetNumberFormat(dgvInventory,
            "AvailableStock",
            "N2");

        ColorInventoryStatus();
    }

    // =====================================================
    // LOAD BATCHES
    // =====================================================

    private async Task LoadBatchesAsync()
    {
        var data = await _service.GetBatchesAsync();

        dgvBatches.DataSource = data;

        SetHeader(dgvBatches, "BatchId", "Mã lô");
        SetHeader(dgvBatches, "MedicineId", "Mã thuốc");
        SetHeader(dgvBatches, "MedicineName", "Tên thuốc");
        SetHeader(dgvBatches, "BatchNumber", "Số lô nhà sản xuất");
        SetHeader(dgvBatches, "ManufactureDate", "Ngày sản xuất");
        SetHeader(dgvBatches, "ExpiryDate", "Hạn sử dụng");
        SetHeader(dgvBatches, "PurchasePriceBase", "Giá nhập");
        SetHeader(dgvBatches, "InitialQuantity", "Số lượng ban đầu");
        SetHeader(dgvBatches, "PhysicalStock", "Tồn hiện tại");
        SetHeader(dgvBatches, "BatchStatus", "Trạng thái lô");

        SetDateFormat(dgvBatches, "ManufactureDate");
        SetDateFormat(dgvBatches, "ExpiryDate");

        SetNumberFormat(dgvBatches,
            "PurchasePriceBase",
            "N0");

        SetNumberFormat(dgvBatches,
            "PhysicalStock",
            "N2");

        SetNumberFormat(dgvBatches,
            "InitialQuantity",
            "N2");
    }

    // =====================================================
    // LOAD EXPIRING MEDICINES
    // =====================================================

    private async Task LoadExpiringAsync()
    {
        var data = await _service.GetExpiringBatchesAsync(30);

        dgvExpiring.DataSource = data;

        SetHeader(dgvExpiring, "BatchId", "Mã lô");
        SetHeader(dgvExpiring, "MedicineId", "Mã thuốc");
        SetHeader(dgvExpiring, "MedicineName", "Tên thuốc");
        SetHeader(dgvExpiring, "BatchNumber", "Số lô");
        SetHeader(dgvExpiring, "ExpiryDate", "Hạn sử dụng");
        SetHeader(dgvExpiring, "PhysicalStock", "Số lượng tồn");
        SetHeader(dgvExpiring, "DaysRemaining", "Còn lại (ngày)");

        SetDateFormat(dgvExpiring, "ExpiryDate");

        SetNumberFormat(dgvExpiring,
            "PhysicalStock",
            "N2");

        ColorExpiringStatus();
    }

    // =====================================================
    // LOAD TRANSACTIONS
    // =====================================================

    private async Task LoadTransactionsAsync(string keyword)
    {
        var data = await _service.GetStockTransactionsAsync(keyword);

        dgvTransactions.DataSource = data;

        SetHeader(dgvTransactions,
            "StockTransactionId",
            "Mã giao dịch");

        SetHeader(dgvTransactions,
            "BatchId",
            "Mã lô");

        SetHeader(dgvTransactions,
            "MedicineId",
            "Mã thuốc");

        SetHeader(dgvTransactions,
            "MedicineName",
            "Tên thuốc");

        SetHeader(dgvTransactions,
            "BatchNumber",
            "Số lô");

        SetHeader(dgvTransactions,
            "TransactionType",
            "Loại giao dịch");

        SetHeader(dgvTransactions,
            "QuantityChange",
            "Số lượng thay đổi");

        SetHeader(dgvTransactions,
            "ReferenceType",
            "Loại tham chiếu");

        SetHeader(dgvTransactions,
            "ReferenceCode",
            "Mã tham chiếu");

        SetHeader(dgvTransactions,
            "EmployeeId",
            "Mã nhân viên");

        SetHeader(dgvTransactions,
            "EmployeeName",
            "Nhân viên");

        SetHeader(dgvTransactions,
            "CreatedAt",
            "Thời gian");

        SetHeader(dgvTransactions,
            "Note",
            "Ghi chú");

        SetDateTimeFormat(dgvTransactions, "CreatedAt");

        SetNumberFormat(dgvTransactions,
            "QuantityChange",
            "N2");
    }

    // =====================================================
    // LOAD RECEIPTS
    // =====================================================

    private async Task LoadReceiptsAsync(string keyword)
    {
        var data = await _service.GetGoodsReceiptsAsync(keyword);

        dgvReceipts.DataSource = data;

        SetHeader(dgvReceipts, "ReceiptId", "Mã phiếu nhập");
        SetHeader(dgvReceipts, "SupplierId", "Mã nhà cung cấp");
        SetHeader(dgvReceipts, "SupplierName", "Nhà cung cấp");
        SetHeader(dgvReceipts, "CreatedBy", "Mã nhân viên");
        SetHeader(dgvReceipts, "EmployeeName", "Người tạo");
        SetHeader(dgvReceipts, "ReceiptDate", "Ngày nhập");
        SetHeader(dgvReceipts, "Status", "Trạng thái");
        SetHeader(dgvReceipts, "Note", "Ghi chú");
        SetHeader(dgvReceipts, "ConfirmedBy", "Người xác nhận");
        SetHeader(dgvReceipts, "ConfirmedAt", "Ngày xác nhận");
        SetHeader(dgvReceipts, "TotalItems", "Số mặt hàng");

        SetDateTimeFormat(dgvReceipts, "ReceiptDate");
        SetDateTimeFormat(dgvReceipts, "ConfirmedAt");
    }

    // =====================================================
    // SHOW RECEIPT DETAILS
    // =====================================================

    private async Task ShowReceiptDetailsAsync()
    {
        if (dgvReceipts.CurrentRow?.DataBoundItem
            is not WarehouseRepository.GoodsReceiptRow receipt)
        {
            return;
        }

        try
        {
            var details = await _service.GetGoodsReceiptDetailsAsync(
                receipt.ReceiptId);

            using var dialog = new Form
            {
                Text = $"Chi tiết phiếu nhập {receipt.ReceiptId}",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(1050, 520),
                MinimumSize = new Size(800, 400),
                BackColor = Color.White,
                Font = Font
            };

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                DataSource = details
            };

            ConfigureGrid(grid);

            SetHeader(grid, "ReceiptDetailId", "Mã chi tiết");
            SetHeader(grid, "ReceiptId", "Mã phiếu");
            SetHeader(grid, "MedicineId", "Mã thuốc");
            SetHeader(grid, "MedicineName", "Tên thuốc");
            SetHeader(grid, "UnitName", "Đơn vị nhập");
            SetHeader(grid, "Quantity", "Số lượng");
            SetHeader(grid, "ConversionFactor", "Hệ số quy đổi");
            SetHeader(grid, "QuantityBase", "Số lượng quy đổi");
            SetHeader(grid, "PurchaseUnitPrice", "Giá nhập");
            SetHeader(grid, "BatchNumber", "Số lô");
            SetHeader(grid, "ManufactureDate", "Ngày sản xuất");
            SetHeader(grid, "ExpiryDate", "Hạn sử dụng");

            SetDateFormat(grid, "ManufactureDate");
            SetDateFormat(grid, "ExpiryDate");

            dialog.Controls.Add(grid);

            dialog.ShowDialog(FindForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Không thể tải chi tiết phiếu nhập.\n\n" + ex.Message,
                "Lỗi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    // =====================================================
    // GRID HELPERS
    // =====================================================

    private static void SetHeader(
        DataGridView grid,
        string columnName,
        string header)
    {
        if (grid.Columns.Contains(columnName))
        {
            grid.Columns[columnName].HeaderText = header;
        }
    }

    private static void SetNumberFormat(
        DataGridView grid,
        string columnName,
        string format)
    {
        if (grid.Columns.Contains(columnName))
        {
            grid.Columns[columnName].DefaultCellStyle.Format = format;
            grid.Columns[columnName].DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleRight;
        }
    }

    private static void SetDateFormat(
        DataGridView grid,
        string columnName)
    {
        if (grid.Columns.Contains(columnName))
        {
            grid.Columns[columnName].DefaultCellStyle.Format =
                "dd/MM/yyyy";
        }
    }

    private static void SetDateTimeFormat(
        DataGridView grid,
        string columnName)
    {
        if (grid.Columns.Contains(columnName))
        {
            grid.Columns[columnName].DefaultCellStyle.Format =
                "dd/MM/yyyy HH:mm";
        }
    }

    // =====================================================
    // COLOR STATUS
    // =====================================================

    private void ColorInventoryStatus()
    {
        if (!dgvInventory.Columns.Contains("StockStatus"))
            return;

        foreach (DataGridViewRow row in dgvInventory.Rows)
        {
            string status =
                Convert.ToString(
                    row.Cells["StockStatus"].Value) ?? "";

            if (status == "Hết hàng")
            {
                row.Cells["StockStatus"].Style.ForeColor =
                    Color.Red;
            }
            else if (status == "Sắp hết hàng")
            {
                row.Cells["StockStatus"].Style.ForeColor =
                    Color.DarkOrange;
            }
            else
            {
                row.Cells["StockStatus"].Style.ForeColor =
                    Color.Green;
            }

            row.Cells["StockStatus"].Style.Font =
                new Font("Segoe UI", 9F, FontStyle.Bold);
        }
    }

    private void ColorExpiringStatus()
    {
        if (!dgvExpiring.Columns.Contains("DaysRemaining"))
            return;

        foreach (DataGridViewRow row in dgvExpiring.Rows)
        {
            if (int.TryParse(
                Convert.ToString(
                    row.Cells["DaysRemaining"].Value),
                out int days))
            {
                if (days <= 7)
                {
                    row.Cells["DaysRemaining"].Style.ForeColor =
                        Color.Red;
                }
                else if (days <= 15)
                {
                    row.Cells["DaysRemaining"].Style.ForeColor =
                        Color.DarkOrange;
                }
            }
        }
    }

    // =====================================================
    // LOADING STATE
    // =====================================================

    private void SetLoadingState(bool loading)
    {
        btnSearch.Enabled = !loading;
        btnRefresh.Enabled = !loading;
        tabMain.Enabled = !loading;

        if (loading)
        {
            lblStatus.Text = "Đang tải dữ liệu...";
            Cursor = Cursors.WaitCursor;
        }
        else
        {
            Cursor = Cursors.Default;
        }
    }
}
