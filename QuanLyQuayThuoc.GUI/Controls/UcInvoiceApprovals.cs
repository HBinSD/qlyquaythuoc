using System.Drawing;
using System.Windows.Forms;
using QuanLyQuayThuoc.BLL.Models;
using QuanLyQuayThuoc.BLL.Services;
using QuanLyQuayThuoc.DAL.Repositories;
using QuanLyQuayThuoc.GUI.Session;

namespace QuanLyQuayThuoc.GUI.Controls;

public sealed class UcInvoiceApprovals : UserControl
{
    private readonly AdminOperationsService _service = new();
    private readonly DataGridView _stocktakeGrid = CreateGrid();
    private readonly DataGridView _stocktakeDetailGrid = CreateGrid();
    private readonly DataGridView _disposalGrid = CreateGrid();
    private readonly DataGridView _disposalDetailGrid = CreateGrid();
    private readonly DataGridView _invoiceGrid = CreateGrid();
    private readonly DataGridView _invoiceDetailGrid = CreateGrid();
    private readonly TextBox _invoiceSearch = new();
    private readonly TextBox _cancelReason = new();
    private readonly Label _invoiceSummary = new();
    private readonly Label _paymentSummary = new();
    private readonly CheckBox _restockCheck = new();
    private readonly CheckBox _pointsCheck = new();
    private readonly TabControl _approvalTabs = new();
    private PendingStocktakeRow? _selectedStocktake;
    private PendingDisposalRow? _selectedDisposal;
    private InvoiceAdminRow? _selectedInvoice;
    private bool _loading;

    public UcInvoiceApprovals()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 9F);
        BuildLayout();
        Load += async (_, _) => await LoadAllAsync();
    }

    private void BuildLayout()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Size = new Size(1050, 700),
            SplitterWidth = 8,
            BackColor = BackColor
        };
        ControlLayout.SetSplitRatio(split, 0.57);
        split.Panel1.Padding = new Padding(12);
        split.Panel2.Padding = new Padding(8, 12, 12, 12);

        _approvalTabs.Dock = DockStyle.Fill;
        _approvalTabs.TabPages.Add(BuildStocktakeTab());
        _approvalTabs.TabPages.Add(BuildDisposalTab());
        split.Panel1.Controls.Add(_approvalTabs);
        split.Panel2.Controls.Add(BuildInvoicePanel());
        Controls.Add(split);
    }

    private TabPage BuildStocktakeTab()
    {
        var page = new TabPage("Kiểm kê chờ duyệt") { BackColor = Color.White };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(8)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.Controls.Add(Heading("PHIẾU KIỂM KÊ CHỜ DUYỆT"), 0, 0);
        _stocktakeGrid.Dock = DockStyle.Fill;
        _stocktakeGrid.SelectionChanged += async (_, _) => await SelectStocktakeAsync();
        _stocktakeDetailGrid.Dock = DockStyle.Fill;
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var approve = Button("DUYỆT & CẬP NHẬT TỒN KHO", Color.FromArgb(35, 112, 190), 220);
        var reject = Button("Từ chối phiếu", Color.FromArgb(100, 110, 125), 145);
        approve.Click += async (_, _) => await DecideStocktakeAsync(true);
        reject.Click += async (_, _) => await DecideStocktakeAsync(false);
        actions.Controls.Add(approve);
        actions.Controls.Add(reject);
        layout.Controls.Add(_stocktakeGrid, 0, 1);
        layout.Controls.Add(_stocktakeDetailGrid, 0, 2);
        layout.Controls.Add(actions, 0, 3);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildDisposalTab()
    {
        var page = new TabPage("Xuất hủy chờ duyệt") { BackColor = Color.White };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(8)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.Controls.Add(Heading("PHIẾU XUẤT HỦY CHỜ DUYỆT"), 0, 0);
        _disposalGrid.Dock = DockStyle.Fill;
        _disposalGrid.SelectionChanged += async (_, _) => await SelectDisposalAsync();
        _disposalDetailGrid.Dock = DockStyle.Fill;
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var approve = Button("PHÊ DUYỆT & CẬP NHẬT TỒN KHO", Color.FromArgb(35, 112, 190), 230);
        var reject = Button("Từ chối phiếu", Color.FromArgb(100, 110, 125), 145);
        approve.Click += async (_, _) => await DecideDisposalAsync(true);
        reject.Click += async (_, _) => await DecideDisposalAsync(false);
        actions.Controls.Add(approve);
        actions.Controls.Add(reject);
        layout.Controls.Add(_disposalGrid, 0, 1);
        layout.Controls.Add(_disposalDetailGrid, 0, 2);
        layout.Controls.Add(actions, 0, 3);
        page.Controls.Add(layout);
        return page;
    }

    private Control BuildInvoicePanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = Color.White,
            Padding = new Padding(10)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 75));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.Controls.Add(Heading("HỦY HÓA ĐƠN ĐÃ THANH TOÁN"), 0, 0);

        var search = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        _invoiceSearch.Dock = DockStyle.Fill;
        _invoiceSearch.PlaceholderText = "Mã hóa đơn / khách hàng / SĐT";
        var searchButton = Button("Tra cứu", Color.FromArgb(16, 63, 117), 96);
        searchButton.Dock = DockStyle.Fill;
        searchButton.Click += async (_, _) => await LoadInvoicesAsync();
        _invoiceSearch.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await LoadInvoicesAsync();
            }
        };
        search.Controls.Add(_invoiceSearch, 0, 0);
        search.Controls.Add(searchButton, 1, 0);
        layout.Controls.Add(search, 0, 1);

        _invoiceGrid.Dock = DockStyle.Fill;
        _invoiceGrid.SelectionChanged += async (_, _) => await SelectInvoiceAsync();
        layout.Controls.Add(_invoiceGrid, 0, 2);

        var summaryPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(255, 244, 244),
            Padding = new Padding(8)
        };
        _invoiceSummary.Dock = DockStyle.Top;
        _invoiceSummary.Height = 40;
        _invoiceSummary.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        _invoiceSummary.ForeColor = Color.FromArgb(150, 45, 50);
        _paymentSummary.Dock = DockStyle.Bottom;
        _paymentSummary.Height = 22;
        _paymentSummary.ForeColor = Color.FromArgb(65, 75, 90);
        summaryPanel.Controls.Add(_invoiceSummary);
        summaryPanel.Controls.Add(_paymentSummary);
        layout.Controls.Add(summaryPanel, 0, 3);

        _invoiceDetailGrid.Dock = DockStyle.Fill;
        layout.Controls.Add(_invoiceDetailGrid, 0, 4);

        var cancellation = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        cancellation.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        cancellation.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        cancellation.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        cancellation.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _restockCheck.Text = "Hoàn tồn theo đúng lô đã xuất";
        _pointsCheck.Text = "Hoàn điểm đã dùng / thu hồi điểm thưởng";
        _restockCheck.Checked = true;
        _pointsCheck.Checked = true;
        _restockCheck.Enabled = false;
        _pointsCheck.Enabled = false;
        _cancelReason.Multiline = true;
        _cancelReason.ScrollBars = ScrollBars.Vertical;
        _cancelReason.PlaceholderText = "Bắt buộc nhập lý do hủy hóa đơn...";
        cancellation.Controls.Add(new Label
        {
            Text = "Lý do hủy (bắt buộc):",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
        }, 0, 0);
        cancellation.Controls.Add(_restockCheck, 0, 1);
        cancellation.Controls.Add(_pointsCheck, 0, 2);
        cancellation.Controls.Add(_cancelReason, 0, 3);
        layout.Controls.Add(cancellation, 0, 5);

        var cancelButton = Button("XÁC NHẬN HỦY HÓA ĐƠN & LƯU NHẬT KÝ", Color.FromArgb(215, 54, 68), 420);
        cancelButton.Dock = DockStyle.Fill;
        cancelButton.Click += async (_, _) => await CancelInvoiceAsync();
        layout.Controls.Add(cancelButton, 0, 6);
        return layout;
    }

    private async Task LoadAllAsync()
    {
        if (_loading)
            return;

        _loading = true;
        try
        {
            _ = RequireManager();
            await LoadStocktakesAsync();
            await LoadDisposalsAsync();
            await LoadInvoicesAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadStocktakesAsync()
    {
        var user = RequireManager();
        var rows = await _service.GetPendingStocktakesAsync(user.EmployeeId, user.RoleId);
        _stocktakeGrid.DataSource = rows;
        SetHeader(_stocktakeGrid, nameof(PendingStocktakeRow.StocktakeId), "Mã phiếu");
        SetHeader(_stocktakeGrid, nameof(PendingStocktakeRow.EmployeeName), "Người lập");
        SetHeader(_stocktakeGrid, nameof(PendingStocktakeRow.CreatedAt), "Ngày lập");
        SetHeader(_stocktakeGrid, nameof(PendingStocktakeRow.Note), "Ghi chú");
        _stocktakeDetailGrid.DataSource = null;
        _selectedStocktake = null;
    }

    private async Task SelectStocktakeAsync()
    {
        if (_loading || _stocktakeGrid.CurrentRow?.DataBoundItem is not PendingStocktakeRow row)
            return;

        _selectedStocktake = row;
        try
        {
            var user = RequireManager();
            var details = await _service.GetStocktakeDetailsAsync(
                row.StocktakeId, user.EmployeeId, user.RoleId);
            if (_selectedStocktake?.StocktakeId != row.StocktakeId)
                return;
            _stocktakeDetailGrid.DataSource = details;
            SetHeader(_stocktakeDetailGrid, nameof(StocktakeDetailRow.MedicineName), "Tên thuốc");
            SetHeader(_stocktakeDetailGrid, nameof(StocktakeDetailRow.BatchNumber), "Mã lô");
            SetHeader(_stocktakeDetailGrid, nameof(StocktakeDetailRow.SystemQuantity), "SL hệ thống");
            SetHeader(_stocktakeDetailGrid, nameof(StocktakeDetailRow.ActualQuantity), "SL thực tế");
            SetHeader(_stocktakeDetailGrid, nameof(StocktakeDetailRow.DifferenceQuantity), "Chênh lệch");
            SetHeader(_stocktakeDetailGrid, nameof(StocktakeDetailRow.DifferenceReason), "Lý do");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task LoadDisposalsAsync()
    {
        var user = RequireManager();
        var rows = await _service.GetPendingDisposalsAsync(user.EmployeeId, user.RoleId);
        _disposalGrid.DataSource = rows;
        SetHeader(_disposalGrid, nameof(PendingDisposalRow.DisposalId), "Mã phiếu");
        SetHeader(_disposalGrid, nameof(PendingDisposalRow.EmployeeName), "Người lập");
        SetHeader(_disposalGrid, nameof(PendingDisposalRow.CreatedAt), "Ngày lập");
        SetHeader(_disposalGrid, nameof(PendingDisposalRow.Reason), "Lý do");
        _disposalDetailGrid.DataSource = null;
        _selectedDisposal = null;
    }

    private async Task SelectDisposalAsync()
    {
        if (_loading || _disposalGrid.CurrentRow?.DataBoundItem is not PendingDisposalRow row)
            return;

        _selectedDisposal = row;
        try
        {
            var user = RequireManager();
            var details = await _service.GetDisposalDetailsAsync(
                row.DisposalId, user.EmployeeId, user.RoleId);
            if (_selectedDisposal?.DisposalId != row.DisposalId)
                return;
            _disposalDetailGrid.DataSource = details;
            SetHeader(_disposalDetailGrid, nameof(DisposalDetailRow.MedicineName), "Tên thuốc");
            SetHeader(_disposalDetailGrid, nameof(DisposalDetailRow.BatchNumber), "Mã lô");
            SetHeader(_disposalDetailGrid, nameof(DisposalDetailRow.ExpiryDate), "HSD");
            SetHeader(_disposalDetailGrid, nameof(DisposalDetailRow.PhysicalStock), "Tồn hiện tại");
            SetHeader(_disposalDetailGrid, nameof(DisposalDetailRow.Quantity), "SL hủy");
            SetHeader(_disposalDetailGrid, nameof(DisposalDetailRow.LossAmount), "Giá trị hủy");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task DecideStocktakeAsync(bool approve)
    {
        if (_selectedStocktake is null)
        {
            MessageBox.Show("Hãy chọn một phiếu kiểm kê chờ duyệt.", "Kiểm kê",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? reason = approve ? null : ReasonDialog.Show(this, "Lý do từ chối phiếu kiểm kê");
        if (!approve && reason is null)
            return;
        if (!ConfirmDecision(
                approve ? "Duyệt phiếu kiểm kê và cập nhật tồn kho?" : "Từ chối phiếu kiểm kê?",
                _selectedStocktake.StocktakeId))
            return;

        try
        {
            var user = RequireManager();
            if (approve)
                await _service.ApproveStocktakeAsync(_selectedStocktake.StocktakeId, user.EmployeeId, user.RoleId);
            else
                await _service.RejectStocktakeAsync(_selectedStocktake.StocktakeId, reason!, user.EmployeeId, user.RoleId);
            MessageBox.Show("Đã xử lý phiếu kiểm kê.", "Kiểm kê",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            await LoadStocktakesAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task DecideDisposalAsync(bool approve)
    {
        if (_selectedDisposal is null)
        {
            MessageBox.Show("Hãy chọn một phiếu xuất hủy chờ duyệt.", "Xuất hủy",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? reason = approve ? null : ReasonDialog.Show(this, "Lý do từ chối phiếu xuất hủy");
        if (!approve && reason is null)
            return;
        if (!ConfirmDecision(
                approve ? "Duyệt xuất hủy và trừ số lượng khỏi các lô?" : "Từ chối phiếu xuất hủy?",
                _selectedDisposal.DisposalId))
            return;

        try
        {
            var user = RequireManager();
            if (approve)
                await _service.ApproveDisposalAsync(_selectedDisposal.DisposalId, user.EmployeeId, user.RoleId);
            else
                await _service.RejectDisposalAsync(_selectedDisposal.DisposalId, reason!, user.EmployeeId, user.RoleId);
            MessageBox.Show("Đã xử lý phiếu xuất hủy.", "Xuất hủy",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            await LoadDisposalsAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task LoadInvoicesAsync()
    {
        try
        {
            var user = RequireManager();
            var rows = await _service.SearchInvoicesAsync(
                _invoiceSearch.Text, user.EmployeeId, user.RoleId);
            _invoiceGrid.DataSource = rows;
            SetHeader(_invoiceGrid, nameof(InvoiceAdminRow.InvoiceId), "Mã HĐ");
            SetHeader(_invoiceGrid, nameof(InvoiceAdminRow.PaidAt), "Thời gian bán");
            SetHeader(_invoiceGrid, nameof(InvoiceAdminRow.EmployeeName), "Dược sĩ");
            SetHeader(_invoiceGrid, nameof(InvoiceAdminRow.CustomerName), "Khách hàng");
            SetHeader(_invoiceGrid, nameof(InvoiceAdminRow.TotalAmount), "Tổng tiền");
            SetHeader(_invoiceGrid, nameof(InvoiceAdminRow.Status), "Trạng thái");
            _invoiceDetailGrid.DataSource = null;
            _selectedInvoice = null;
            _invoiceSummary.Text = rows.Count == 0 ? "Không tìm thấy hóa đơn." : "Chọn hóa đơn để xem chi tiết.";
            _paymentSummary.Text = "";
            _cancelReason.Clear();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task SelectInvoiceAsync()
    {
        if (_loading || _invoiceGrid.CurrentRow?.DataBoundItem is not InvoiceAdminRow invoice)
            return;

        _selectedInvoice = invoice;
        try
        {
            var user = RequireManager();
            var details = await _service.GetInvoiceDetailsAsync(
                invoice.InvoiceId, user.EmployeeId, user.RoleId);
            if (_selectedInvoice?.InvoiceId != invoice.InvoiceId)
                return;
            _invoiceDetailGrid.DataSource = details;
            SetHeader(_invoiceDetailGrid, nameof(InvoiceDetailRow.MedicineName), "Thuốc");
            SetHeader(_invoiceDetailGrid, nameof(InvoiceDetailRow.BatchNumber), "Mã lô");
            SetHeader(_invoiceDetailGrid, nameof(InvoiceDetailRow.UnitName), "Đơn vị");
            SetHeader(_invoiceDetailGrid, nameof(InvoiceDetailRow.Quantity), "SL");
            SetHeader(_invoiceDetailGrid, nameof(InvoiceDetailRow.UnitPrice), "Đơn giá");
            SetHeader(_invoiceDetailGrid, nameof(InvoiceDetailRow.LineTotal), "Thành tiền");

            var payments = await _service.GetInvoicePaymentsAsync(
                invoice.InvoiceId, user.EmployeeId, user.RoleId);
            if (_selectedInvoice?.InvoiceId != invoice.InvoiceId)
                return;
            string customer = invoice.CustomerId is null
                ? "Khách lẻ"
                : $"{invoice.CustomerName} ({invoice.CustomerPhone})";
            _invoiceSummary.Text =
                $"Mã HĐ: {invoice.InvoiceId} | {invoice.Status}\r\n" +
                $"Bán: {invoice.PaidAt:dd/MM/yyyy HH:mm} | {invoice.EmployeeName} ({invoice.ShiftId}) | {customer}\r\n" +
                $"Tổng tiền: {invoice.TotalAmount:N0} đ";
            _paymentSummary.Text = "Thanh toán: " +
                (payments.Count == 0
                    ? "Không có dữ liệu"
                    : string.Join("  •  ", payments.Select(x => $"{PaymentName(x.PaymentMethod)} {x.Amount:N0} đ")));
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task CancelInvoiceAsync()
    {
        if (_selectedInvoice is null)
        {
            MessageBox.Show("Hãy chọn hóa đơn cần hủy.", "Hóa đơn",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_selectedInvoice.Status != "PAID")
        {
            MessageBox.Show("Chỉ hóa đơn Đã thanh toán mới có thể được Quản lý hủy.", "Hóa đơn",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_cancelReason.Text))
        {
            MessageBox.Show("Vui lòng nhập lý do hủy chi tiết.", "Hóa đơn",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _cancelReason.Focus();
            return;
        }

        var confirm = MessageBox.Show(
            $"Xác nhận hủy hóa đơn {_selectedInvoice.InvoiceId} trị giá {_selectedInvoice.AmountPaid:N0} đ?\r\n" +
            "Hệ thống sẽ hoàn tồn theo đúng lô, xử lý điểm và ghi nhật ký.",
            "Xác nhận hủy hóa đơn",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
            return;

        try
        {
            var user = RequireManager();
            var result = await _service.CancelPaidInvoiceAsync(
                _selectedInvoice.InvoiceId,
                _cancelReason.Text,
                user.EmployeeId,
                user.RoleId);
            MessageBox.Show(
                $"Đã hủy hóa đơn {result.InvoiceId}.\r\n" +
                $"Tổng hoàn: {result.TotalRefund:N0} đ (tiền mặt {result.CashRefund:N0} đ).\r\n" +
                $"Hoàn điểm: {result.PointsRestored:N0}; thu hồi điểm: {result.PointsRecovered:N0}.\r\n" +
                $"Số lô đã hoàn tồn: {result.BatchesRestocked}.",
                "Hóa đơn đã hủy",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            await LoadInvoicesAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private static bool ConfirmDecision(string prompt, string id) =>
        MessageBox.Show($"{prompt}\r\nMã phiếu: {id}",
            "Xác nhận thao tác",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) == DialogResult.Yes;

    private static string PaymentName(string method) => method switch
    {
        "CASH" => "Tiền mặt",
        "TRANSFER" => "Chuyển khoản",
        "QR" => "QR",
        _ => method
    };

    private static LoggedInUser RequireManager()
    {
        var user = SessionManager.CurrentUser;
        if (user is null || user.RoleId != "ADMIN")
            throw new UnauthorizedAccessException("Chức năng này chỉ dành cho Quản lý.");
        return user;
    }

    private static DataGridView CreateGrid() => new()
    {
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        Dock = DockStyle.Fill,
        AutoGenerateColumns = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        ReadOnly = true,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        ColumnHeadersHeight = 32,
        RowTemplate = { Height = 28 }
    };

    private static void SetHeader(DataGridView grid, string property, string title)
    {
        if (grid.Columns[property] is { } column)
            column.HeaderText = title;
    }

    private static Label Heading(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
        ForeColor = Color.FromArgb(35, 55, 80),
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static Button Button(string text, Color color, int width) => new()
    {
        Text = text,
        BackColor = color,
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 8F, FontStyle.Bold),
        Size = new Size(width, 34),
        Cursor = Cursors.Hand
    };

    private static void ShowError(Exception ex) =>
        MessageBox.Show(ex.Message, "Không thể hoàn tất thao tác",
            MessageBoxButtons.OK, MessageBoxIcon.Error);

    private sealed class ReasonDialog : Form
    {
        private readonly TextBox _reason = new();

        private ReasonDialog(string title)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(420, 190);
            _reason.Dock = DockStyle.Fill;
            _reason.Multiline = true;
            _reason.ScrollBars = ScrollBars.Vertical;
            _reason.PlaceholderText = "Nhập lý do (bắt buộc)...";

            var save = Button("Xác nhận", Color.FromArgb(16, 63, 117), 120);
            save.DialogResult = DialogResult.OK;
            var cancel = Button("Hủy", Color.FromArgb(100, 110, 125), 90);
            cancel.DialogResult = DialogResult.Cancel;
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8)
            };
            actions.Controls.Add(save);
            actions.Controls.Add(cancel);
            AcceptButton = save;
            CancelButton = cancel;
            Controls.Add(_reason);
            Controls.Add(actions);
        }

        public static string? Show(IWin32Window owner, string title)
        {
            using var dialog = new ReasonDialog(title);
            while (dialog.ShowDialog(owner) == DialogResult.OK)
            {
                string reason = dialog._reason.Text.Trim();
                if (reason.Length is > 0 and <= 500)
                    return reason;
                MessageBox.Show(owner, "Vui lòng nhập lý do tối đa 500 ký tự.",
                    title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return null;
        }
    }
}
