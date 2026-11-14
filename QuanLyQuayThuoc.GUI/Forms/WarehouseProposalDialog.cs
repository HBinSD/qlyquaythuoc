using System.Drawing;
using System.Windows.Forms;
using QuanLyQuayThuoc.BLL.Services;
using QuanLyQuayThuoc.DAL.Repositories;

namespace QuanLyQuayThuoc.GUI.Forms;

public sealed class WarehouseProposalDialog : Form
{
    private const decimal MaximumQuantity = 999999999999999.999M;

    private sealed record BatchOption(WarehouseRepository.BatchRow Batch, string Label);

    private readonly WarehouseService _service;
    private readonly bool _isDisposal;
    private readonly string _actorId;
    private readonly string _actorRole;
    private readonly ComboBox _batch = new();
    private readonly NumericUpDown _quantity = new();
    private readonly Label _stockInfo = new();
    private readonly TextBox _reason = new();
    private readonly Button _submit = new();
    private readonly Button _cancel = new();

    public string? ProposalId { get; private set; }

    public WarehouseProposalDialog(
        WarehouseService service,
        bool isDisposal,
        string actorId,
        string actorRole)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _isDisposal = isDisposal;
        _actorId = actorId;
        _actorRole = actorRole;

        InitializeComponent();
        Load += LoadBatchesAsync;
    }

    private void InitializeComponent()
    {
        Text = _isDisposal ? "Đề xuất xuất hủy" : "Đề xuất kiểm kê";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(680, 390);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
            ColumnCount = 2,
            RowCount = 6,
            BackColor = Color.White
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var title = new Label
        {
            Text = _isDisposal ? "TẠO ĐỀ XUẤT XUẤT HỦY" : "TẠO ĐỀ XUẤT KIỂM KÊ",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 55, 80),
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 2);

        AddLabel(layout, "Lô thuốc:", 1);
        _batch.Dock = DockStyle.Fill;
        _batch.DropDownStyle = ComboBoxStyle.DropDownList;
        layout.Controls.Add(_batch, 1, 1);
        _batch.SelectedIndexChanged += (_, _) => UpdateSelectedBatch();

        AddLabel(layout, _isDisposal ? "Số lượng đề xuất:" : "Số lượng thực đếm:", 2);
        _quantity.Dock = DockStyle.Left;
        _quantity.Width = 180;
        _quantity.DecimalPlaces = 3;
        _quantity.Increment = 0.001M;
        _quantity.Maximum = MaximumQuantity;
        _quantity.Minimum = _isDisposal ? 0.001M : 0M;
        layout.Controls.Add(_quantity, 1, 2);

        AddLabel(layout, "Thông tin tồn:", 3);
        _stockInfo.Dock = DockStyle.Fill;
        _stockInfo.ForeColor = Color.FromArgb(70, 85, 105);
        _stockInfo.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(_stockInfo, 1, 3);

        AddLabel(layout, _isDisposal ? "Lý do xuất hủy (bắt buộc):" : "Lý do chênh lệch:", 4);
        _reason.Dock = DockStyle.Fill;
        _reason.Multiline = true;
        _reason.ScrollBars = ScrollBars.Vertical;
        _reason.MaxLength = _isDisposal ? 500 : 300;
        _reason.PlaceholderText = _isDisposal
            ? "Ghi rõ lý do (hết hạn, hư hỏng, ...)"
            : "Bắt buộc nhập nếu số lượng thực đếm khác tồn hệ thống.";
        layout.Controls.Add(_reason, 1, 4);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        _submit.Text = "Gửi đề xuất";
        _submit.Size = new Size(120, 34);
        _submit.BackColor = Color.FromArgb(37, 99, 235);
        _submit.ForeColor = Color.White;
        _submit.FlatStyle = FlatStyle.Flat;
        _submit.FlatAppearance.BorderSize = 0;
        _submit.Click += async (_, _) => await SubmitAsync();
        _cancel.Text = "Hủy";
        _cancel.Size = new Size(90, 34);
        _cancel.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(_submit);
        buttons.Controls.Add(_cancel);
        layout.Controls.Add(buttons, 0, 5);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);
        AcceptButton = _submit;
        CancelButton = _cancel;
    }

    private static void AddLabel(TableLayoutPanel layout, string text, int row)
    {
        layout.Controls.Add(new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(55, 70, 90)
        }, 0, row);
    }

    private async void LoadBatchesAsync(object? sender, EventArgs e)
    {
        _submit.Enabled = false;
        try
        {
            var batches = await _service.GetBatchesAsync();
            var options = batches
                .Where(batch => !_isDisposal || batch.PhysicalStock > 0)
                .Select(batch => new BatchOption(
                    batch,
                    $"{batch.MedicineName} — lô {batch.BatchNumber} ({batch.BatchId}), HSD {batch.ExpiryDate:dd/MM/yyyy}, tồn {batch.PhysicalStock:N3}"))
                .ToList();

            _batch.DataSource = options;
            _batch.DisplayMember = nameof(BatchOption.Label);
            _batch.Enabled = options.Count > 0;
            _submit.Enabled = options.Count > 0;

            if (options.Count == 0)
            {
                MessageBox.Show(
                    _isDisposal
                        ? "Không có lô nào còn tồn để đề xuất xuất hủy."
                        : "Không có lô thuốc để kiểm kê.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            UpdateSelectedBatch();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Không thể tải danh sách lô thuốc.\n\n" + ex.Message,
                "Lỗi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private void UpdateSelectedBatch()
    {
        if (_batch.SelectedItem is not BatchOption option)
            return;

        var batch = option.Batch;
        _stockInfo.Text = _isDisposal
            ? $"Tồn: {batch.PhysicalStock:N3}    |    Giá nhập: {batch.PurchasePriceBase:N4}"
            : $"Tồn hệ thống hiện tại: {batch.PhysicalStock:N3} (đọc lại khi gửi)";

        if (_isDisposal)
        {
            _quantity.Maximum = Math.Max(0.001M, Math.Min(MaximumQuantity, batch.PhysicalStock));
            _quantity.Value = Math.Min(batch.PhysicalStock, _quantity.Maximum);
        }
        else
        {
            _quantity.Value = Math.Min(batch.PhysicalStock, _quantity.Maximum);
        }
    }

    private async Task SubmitAsync()
    {
        if (_batch.SelectedItem is not BatchOption option)
            return;

        _submit.Enabled = false;
        try
        {
            WarehouseRepository.ProposalResult result;
            if (_isDisposal)
            {
                result = await _service.SubmitDisposalProposalAsync(
                    option.Batch.BatchId,
                    _quantity.Value,
                    _reason.Text,
                    _actorId,
                    _actorRole);
            }
            else
            {
                result = await _service.SubmitStocktakeProposalAsync(
                    option.Batch.BatchId,
                    _quantity.Value,
                    _reason.Text,
                    _actorId,
                    _actorRole);
            }

            ProposalId = result.ProposalId;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Không thể gửi đề xuất",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _submit.Enabled = true;
        }
    }
}
