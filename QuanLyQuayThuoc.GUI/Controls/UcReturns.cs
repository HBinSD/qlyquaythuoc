using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using QuanLyQuayThuoc.BLL.Services;
using QuanLyQuayThuoc.DAL.Repositories;
using QuanLyQuayThuoc.GUI.Session;

namespace QuanLyQuayThuoc.GUI.Controls;

public sealed class UcReturns : UserControl
{
    private readonly ReturnService _service = new();
    private readonly DataGridView _invoiceGrid = CreateGrid();
    private readonly DataGridView _lineGrid = CreateGrid();
    private readonly TextBox _searchBox = new();
    private readonly TextBox _reasonBox = new();
    private readonly ComboBox _refundMethod = new();
    private readonly CheckBox _sealedCheck = new();
    private readonly CheckBox _undamagedCheck = new();
    private readonly CheckBox _expiryCheck = new();
    private readonly Label _selectionSummary = new();
    private readonly Label _statusLabel = new();
    private readonly Button _processButton = new();
    private ReturnInvoiceRow? _selectedInvoice;
    private bool _loadingInvoices;
    private bool _loadingLines;
    private bool _processing;

    public UcReturns()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 9F);
        BuildLayout();
        Load += async (_, _) => await LoadInvoicesAsync();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12),
            BackColor = BackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 218));

        var heading = new Label
        {
            Text = "TRẢ HÀNG / HOÀN TIỀN",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = Color.FromArgb(16, 63, 117),
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(heading, 0, 0);

        var searchPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        _searchBox.Dock = DockStyle.Fill;
        _searchBox.PlaceholderText = "Mã hóa đơn / khách hàng / số điện thoại";
        _searchBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await LoadInvoicesAsync();
            }
        };
        var searchButton = MakeButton("Tra cứu", Color.FromArgb(16, 63, 117));
        searchButton.Dock = DockStyle.Fill;
        searchButton.Click += async (_, _) => await LoadInvoicesAsync();
        searchPanel.Controls.Add(_searchBox, 0, 0);
        searchPanel.Controls.Add(searchButton, 1, 0);
        layout.Controls.Add(searchPanel, 0, 1);

        ConfigureInvoiceGrid();
        ConfigureLineGrid();
        layout.Controls.Add(_invoiceGrid, 0, 2);
        layout.Controls.Add(_lineGrid, 0, 3);
        layout.Controls.Add(BuildReturnPanel(), 0, 4);
        Controls.Add(layout);
    }

    private void ConfigureInvoiceGrid()
    {
        _invoiceGrid.AutoGenerateColumns = false;
        _invoiceGrid.Columns.Add(TextColumn(nameof(ReturnInvoiceRow.InvoiceId), "Mã hóa đơn", 110));
        _invoiceGrid.Columns.Add(TextColumn(nameof(ReturnInvoiceRow.PaidAt), "Thanh toán", 130, "dd/MM/yyyy HH:mm"));
        _invoiceGrid.Columns.Add(TextColumn(nameof(ReturnInvoiceRow.CustomerName), "Khách hàng", 150));
        _invoiceGrid.Columns.Add(TextColumn(nameof(ReturnInvoiceRow.CustomerPhone), "SĐT", 100));
        _invoiceGrid.Columns.Add(TextColumn(nameof(ReturnInvoiceRow.Status), "Trạng thái", 120));
        _invoiceGrid.Columns.Add(TextColumn(nameof(ReturnInvoiceRow.AmountPaid), "Đã thanh toán", 120, "N0"));
        _invoiceGrid.SelectionChanged += async (_, _) => await SelectInvoiceAsync();
    }

    private void ConfigureLineGrid()
    {
        _lineGrid.AutoGenerateColumns = false;
        _lineGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Selected",
            HeaderText = "Trả",
            Width = 45,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        });
        _lineGrid.Columns.Add(TextColumn("MedicineName", "Thuốc", 150));
        _lineGrid.Columns.Add(TextColumn("BatchNumber", "Lô gốc", 85));
        _lineGrid.Columns.Add(TextColumn("ExpiryDate", "Hạn dùng", 85, "dd/MM/yyyy"));
        _lineGrid.Columns.Add(TextColumn("BatchStatus", "Tình trạng lô", 90));
        _lineGrid.Columns.Add(TextColumn("RemainingQuantity", "Còn được trả", 90, "0.###"));
        _lineGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ReturnQuantity",
            HeaderText = "SL trả",
            Width = 75,
            ValueType = typeof(decimal),
            DefaultCellStyle = new DataGridViewCellStyle { Format = "0.###" }
        });
        _lineGrid.Columns.Add(TextColumn("UnitName", "Đơn vị", 65));
        _lineGrid.Columns.Add(TextColumn("RemainingRefund", "Tiền còn lại", 105, "N0"));
        _lineGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Estimate",
            HeaderText = "Hoàn dự kiến",
            Width = 110,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" }
        });
        _lineGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "InvoiceDetailId",
            Visible = false
        });

        _lineGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_lineGrid.IsCurrentCellDirty)
                _lineGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _lineGrid.CellValueChanged += (_, e) =>
        {
            if (!_loadingLines && e.RowIndex >= 0)
                UpdateEstimate(_lineGrid.Rows[e.RowIndex]);
        };
        _lineGrid.CellEndEdit += (_, e) =>
        {
            if (e.RowIndex >= 0)
                UpdateEstimate(_lineGrid.Rows[e.RowIndex]);
        };
        _lineGrid.DataError += (_, e) =>
        {
            e.ThrowException = false;
            _statusLabel.Text = "Số lượng trả cần là số hợp lệ (tối đa 3 chữ số thập phân).";
        };
    }

    private Control BuildReturnPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(8),
            BackColor = Color.White
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        _sealedCheck.Text = "Đã kiểm tra: niêm phong còn nguyên";
        _undamagedCheck.Text = "Đã kiểm tra: thuốc không hư hỏng";
        _expiryCheck.Text = "Đã kiểm tra: thuốc còn trong hạn dùng";
        var confirmations = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true
        };
        confirmations.Controls.Add(_sealedCheck);
        confirmations.Controls.Add(_undamagedCheck);
        confirmations.Controls.Add(_expiryCheck);
        panel.Controls.Add(confirmations, 0, 0);
        panel.SetColumnSpan(confirmations, 2);

        var reasonHeading = new Label
        {
            Text = "Lý do trả hàng (bắt buộc, tối đa 500 ký tự):",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
        };
        panel.Controls.Add(reasonHeading, 0, 1);

        _refundMethod.DropDownStyle = ComboBoxStyle.DropDownList;
        _refundMethod.Items.AddRange(new object[] { "CASH - Tiền mặt", "TRANSFER - Chuyển khoản" });
        _refundMethod.SelectedIndex = 0;
        var methodPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        methodPanel.Controls.Add(new Label
        {
            Text = "Phương thức hoàn:",
            AutoSize = true,
            Padding = new Padding(0, 5, 6, 0),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
        });
        _refundMethod.Width = 220;
        methodPanel.Controls.Add(_refundMethod);
        panel.Controls.Add(methodPanel, 1, 1);

        _reasonBox.Dock = DockStyle.Fill;
        _reasonBox.Multiline = true;
        _reasonBox.MaxLength = 500;
        _reasonBox.ScrollBars = ScrollBars.Vertical;
        _reasonBox.PlaceholderText = "Nhập lý do khách trả hàng...";
        panel.Controls.Add(_reasonBox, 0, 2);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10, 0, 0, 0)
        };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        _selectionSummary.Dock = DockStyle.Fill;
        _selectionSummary.ForeColor = Color.FromArgb(35, 75, 120);
        _selectionSummary.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _selectionSummary.Text = "Chọn hóa đơn và nhập số lượng cần trả.";
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Color.FromArgb(110, 75, 35);
        _statusLabel.Text = "Chỉ nhận hóa đơn đã thanh toán trong vòng 7 ngày.";
        right.Controls.Add(_selectionSummary, 0, 0);
        right.Controls.Add(_statusLabel, 0, 1);
        panel.Controls.Add(right, 1, 2);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        _processButton.Text = "XÁC NHẬN TRẢ HÀNG & HOÀN TIỀN";
        _processButton.Width = 285;
        _processButton.Height = 36;
        _processButton.BackColor = Color.FromArgb(28, 135, 91);
        _processButton.ForeColor = Color.White;
        _processButton.FlatStyle = FlatStyle.Flat;
        _processButton.FlatAppearance.BorderSize = 0;
        _processButton.Click += async (_, _) => await ProcessReturnAsync();
        footer.Controls.Add(_processButton);
        panel.Controls.Add(footer, 0, 3);
        panel.SetColumnSpan(footer, 2);
        return panel;
    }

    private async Task LoadInvoicesAsync()
    {
        try
        {
            _loadingInvoices = true;
            _selectedInvoice = null;
            _lineGrid.Rows.Clear();
            _invoiceGrid.DataSource = await _service.SearchInvoicesAsync(_searchBox.Text);
            _statusLabel.Text = _invoiceGrid.Rows.Count == 0
                ? "Không tìm thấy hóa đơn đủ điều kiện."
                : "Chọn hóa đơn đã thanh toán trong 7 ngày gần nhất.";
            _selectionSummary.Text = "Chọn hóa đơn và nhập số lượng cần trả.";
            _reasonBox.Clear();
        }
        catch (ArgumentException ex)
        {
            ShowError(ex.Message);
        }
        catch (SqlException ex)
        {
            ShowError($"Không thể tra cứu hóa đơn: {ex.Message}");
        }
        finally
        {
            _loadingInvoices = false;
        }

        if (_invoiceGrid.CurrentRow?.DataBoundItem is ReturnInvoiceRow invoice)
            await SelectInvoiceAsync(invoice);
    }

    private async Task SelectInvoiceAsync()
    {
        if (_loadingInvoices || _invoiceGrid.CurrentRow?.DataBoundItem is not ReturnInvoiceRow invoice)
            return;
        await SelectInvoiceAsync(invoice);
    }

    private async Task SelectInvoiceAsync(ReturnInvoiceRow invoice)
    {
        _selectedInvoice = invoice;
        try
        {
            _loadingLines = true;
            _lineGrid.Rows.Clear();
            List<ReturnLineRow> lines = await _service.GetRemainingLinesAsync(invoice.InvoiceId);
            if (_selectedInvoice?.InvoiceId != invoice.InvoiceId)
                return;

            foreach (ReturnLineRow line in lines)
            {
                int index = _lineGrid.Rows.Add(
                    false,
                    line.MedicineName,
                    line.BatchNumber,
                    line.ExpiryDate,
                    line.BatchStatus,
                    line.RemainingQuantity,
                    0m,
                    line.UnitName,
                    line.RemainingRefund,
                    0m,
                    line.InvoiceDetailId);
                _lineGrid.Rows[index].Tag = line;
                bool eligible = line.BatchStatus == "ACTIVE" && line.IsWithinExpiry;
                if (!eligible)
                    _lineGrid.Rows[index].DefaultCellStyle.ForeColor = Color.FromArgb(155, 75, 75);
            }
            _selectionSummary.Text =
                $"{invoice.InvoiceId} | {invoice.Status} | Thanh toán {invoice.PaidAt:dd/MM/yyyy HH:mm}\r\n" +
                $"Còn {lines.Count} dòng có thể xem; chỉ nhận hàng từ đúng lô hóa đơn gốc.";
            _statusLabel.Text = "Dòng màu đỏ là lô hết hạn/ngừng hoạt động; hệ thống sẽ từ chối nhập lại kho.";
        }
        catch (ArgumentException ex)
        {
            ShowError(ex.Message);
        }
        catch (SqlException ex)
        {
            ShowError($"Không thể tải chi tiết trả hàng: {ex.Message}");
        }
        finally
        {
            _loadingLines = false;
        }
        UpdateSummary();
    }

    private void UpdateEstimate(DataGridViewRow row)
    {
        if (row.Tag is not ReturnLineRow line)
            return;

        decimal quantity = 0;
        object? value = row.Cells["ReturnQuantity"].Value;
        if (value is not null && decimal.TryParse(
                Convert.ToString(value, CultureInfo.CurrentCulture),
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out decimal parsed))
        {
            quantity = parsed;
        }

        decimal estimate = quantity <= 0 || quantity > line.RemainingQuantity
            ? 0
            : quantity == line.RemainingQuantity
                ? line.RemainingRefund
                : decimal.Round(
                    line.RemainingRefund * quantity / line.RemainingQuantity,
                    2,
                    MidpointRounding.AwayFromZero);
        row.Cells["Estimate"].Value = estimate;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        int selectedLines = 0;
        decimal estimatedRefund = 0;
        foreach (DataGridViewRow row in _lineGrid.Rows)
        {
            if (row.IsNewRow || row.Cells["Selected"].Value is not true)
                continue;
            selectedLines++;
            if (row.Cells["Estimate"].Value is not null)
                estimatedRefund += Convert.ToDecimal(row.Cells["Estimate"].Value);
        }

        _selectionSummary.Text = _selectedInvoice is null
            ? "Chọn hóa đơn và nhập số lượng cần trả."
            : $"{_selectedInvoice.InvoiceId} | Đã chọn {selectedLines} dòng | Hoàn dự kiến {estimatedRefund:N0} đ";
    }

    private async Task ProcessReturnAsync()
    {
        if (_processing)
            return;
        if (_selectedInvoice is null)
        {
            ShowError("Hãy chọn hóa đơn cần trả hàng.");
            return;
        }
        if (string.IsNullOrWhiteSpace(_reasonBox.Text))
        {
            ShowError("Vui lòng nhập lý do trả hàng.");
            _reasonBox.Focus();
            return;
        }
        if (!_sealedCheck.Checked || !_undamagedCheck.Checked || !_expiryCheck.Checked)
        {
            ShowError("Cần xác nhận thuốc còn niêm phong, nguyên vẹn và trong hạn dùng.");
            return;
        }

        var lines = new List<ReturnLineRequest>();
        foreach (DataGridViewRow row in _lineGrid.Rows)
        {
            if (row.IsNewRow || row.Cells["Selected"].Value is not true)
                continue;
            if (row.Tag is not ReturnLineRow line
                || !decimal.TryParse(
                    Convert.ToString(row.Cells["ReturnQuantity"].Value, CultureInfo.CurrentCulture),
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out decimal quantity)
                || quantity <= 0
                || quantity > line.RemainingQuantity
                || quantity != decimal.Round(quantity, 3))
            {
                ShowError("Mỗi dòng được chọn cần có số lượng trả hợp lệ, không vượt số lượng còn lại.");
                return;
            }
            if (!line.IsWithinExpiry || line.BatchStatus != "ACTIVE")
            {
                ShowError($"Lô {line.BatchNumber} đã hết hạn hoặc ngừng hoạt động.");
                return;
            }
            lines.Add(new ReturnLineRequest(line.InvoiceDetailId, quantity));
        }
        if (lines.Count == 0)
        {
            ShowError("Hãy chọn ít nhất một dòng thuốc và nhập số lượng trả.");
            return;
        }

        var user = SessionManager.CurrentUser;
        if (user is null)
        {
            ShowError("Phiên đăng nhập đã hết; vui lòng đăng nhập lại.");
            return;
        }
        string refundMethod = _refundMethod.SelectedIndex == 1 ? "TRANSFER" : "CASH";
        decimal estimate = lines.Sum(line =>
        {
            ReturnLineRow source = _lineGrid.Rows
                .Cast<DataGridViewRow>()
                .Select(row => row.Tag as ReturnLineRow)
                .First(x => x?.InvoiceDetailId == line.InvoiceDetailId)!;
            return line.Quantity == source.RemainingQuantity
                ? source.RemainingRefund
                : decimal.Round(
                    source.RemainingRefund * line.Quantity / source.RemainingQuantity,
                    2,
                    MidpointRounding.AwayFromZero);
        });
        if (MessageBox.Show(
                $"Xác nhận trả hàng hóa đơn {_selectedInvoice.InvoiceId}?\r\n" +
                $"Hoàn dự kiến: {estimate:N0} đ qua {refundMethod}.\r\n" +
                "Khoản hoàn sẽ được tính lại theo số tiền thực trả trên từng dòng.",
                "Xác nhận trả hàng",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            _processing = true;
            _processButton.Enabled = false;
            var result = await _service.ProcessReturnAsync(new ReturnProcessRequest
            {
                InvoiceId = _selectedInvoice.InvoiceId,
                EmployeeId = user.EmployeeId,
                RefundMethod = refundMethod,
                Reason = _reasonBox.Text,
                SealedConfirmed = _sealedCheck.Checked,
                UndamagedConfirmed = _undamagedCheck.Checked,
                WithinExpiryConfirmed = _expiryCheck.Checked,
                Lines = lines
            });
            MessageBox.Show(
                $"Đã xử lý phiếu trả {result.ReturnId}.\r\n" +
                $"Hoàn {result.TotalRefund:N0} đ qua {result.RefundMethod}.\r\n" +
                $"Hoàn điểm: {result.PointsRestored:N0}; thu hồi điểm: {result.PointsRecovered:N0}.",
                "Trả hàng thành công",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            _sealedCheck.Checked = false;
            _undamagedCheck.Checked = false;
            _expiryCheck.Checked = false;
            _reasonBox.Clear();
            await LoadInvoicesAsync();
        }
        catch (ArgumentException ex)
        {
            ShowError(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            ShowError(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowError(ex.Message);
        }
        catch (SqlException ex)
        {
            ShowError($"Không thể hoàn tất trả hàng: {ex.Message}");
        }
        finally
        {
            _processing = false;
            _processButton.Enabled = true;
        }
    }

    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        EnableHeadersVisualStyles = false,
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(229, 236, 245),
            ForeColor = Color.FromArgb(35, 50, 70),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
        },
        DefaultCellStyle = new DataGridViewCellStyle
        {
            Font = new Font("Segoe UI", 8.5F),
            SelectionBackColor = Color.FromArgb(215, 231, 248),
            SelectionForeColor = Color.FromArgb(25, 45, 70)
        }
    };

    private static DataGridViewTextBoxColumn TextColumn(
        string propertyName,
        string header,
        int minimumWidth,
        string? format = null) => new()
    {
        DataPropertyName = propertyName,
        Name = propertyName,
        HeaderText = header,
        MinimumWidth = minimumWidth,
        FillWeight = minimumWidth,
        ReadOnly = true,
        DefaultCellStyle = new DataGridViewCellStyle { Format = format ?? "" }
    };

    private static Button MakeButton(string text, Color color) => new()
    {
        Text = text,
        BackColor = color,
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        FlatAppearance = { BorderSize = 0 },
        Font = new Font("Segoe UI", 9F, FontStyle.Bold)
    };

    private void ShowError(string message)
    {
        _statusLabel.ForeColor = Color.FromArgb(175, 45, 50);
        _statusLabel.Text = message;
        MessageBox.Show(message, "Trả hàng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
