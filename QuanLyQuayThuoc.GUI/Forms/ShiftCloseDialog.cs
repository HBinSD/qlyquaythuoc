using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using QuanLyQuayThuoc.DAL.Repositories;

namespace QuanLyQuayThuoc.GUI.Forms;

internal sealed class ShiftCloseDialog : Form
{
    private readonly ShiftCloseSummaryRow _summary;
    private readonly NumericUpDown _actualCash = new();
    private readonly Label _difference = new();
    private readonly TextBox _differenceNote = new();

    public decimal ActualCash => _actualCash.Value;

    public string? DifferenceNote =>
        string.IsNullOrWhiteSpace(_differenceNote.Text) ? null : _differenceNote.Text.Trim();

    public ShiftCloseDialog(ShiftCloseSummaryRow summary)
    {
        _summary = summary ?? throw new ArgumentNullException(nameof(summary));

        Text = "Chốt ca / Kết ca";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 500);
        Font = new Font("Segoe UI", 9F);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 14, 18, 8),
            ColumnCount = 2,
            RowCount = 10
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 7; row++)
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        AddField(fields, 0, "Mã ca / nhân viên", ReadOnlyText($"{summary.ShiftId} | {summary.EmployeeName}"));
        AddField(fields, 1, "Thời gian mở ca", ReadOnlyText(summary.OpenedAt.ToString("dd/MM/yyyy HH:mm:ss")));
        AddField(fields, 2, "Tiền mặt đầu ca", ReadOnlyText($"{summary.OpeningCash:N0} đ"));
        AddField(fields, 3, "Doanh thu tiền mặt", ReadOnlyText($"{summary.CashSales:N0} đ"));
        AddField(fields, 4, "Chuyển khoản / QR", ReadOnlyText($"{summary.TransferSales:N0} đ"));
        AddField(fields, 5, "Tiền mặt hoàn trả", ReadOnlyText($"{summary.CashRefunds:N0} đ"));
        AddField(fields, 6, "Tiền lý thuyết trong két", ReadOnlyText($"{summary.TheoreticalCash:N0} đ"));

        _actualCash.Minimum = 0;
        _actualCash.Maximum = 9999999999999999.99M;
        _actualCash.DecimalPlaces = 2;
        _actualCash.ThousandsSeparator = true;
        _actualCash.Dock = DockStyle.Top;
        _actualCash.ValueChanged += (_, _) => UpdateDifference();
        AddField(fields, 7, "Tiền thực tế kiểm đếm", _actualCash);

        _difference.Dock = DockStyle.Fill;
        _difference.TextAlign = ContentAlignment.MiddleLeft;
        _difference.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        AddField(fields, 8, "Chênh lệch", _difference);

        var noteGroup = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 5, 0, 0)
        };
        noteGroup.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        noteGroup.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        noteGroup.Controls.Add(new Label
        {
            Text = "Ghi chú chênh lệch (bắt buộc nếu lệch)",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(65, 75, 90)
        }, 0, 0);
        _differenceNote.Multiline = true;
        _differenceNote.MaxLength = 500;
        _differenceNote.ScrollBars = ScrollBars.Vertical;
        _differenceNote.Dock = DockStyle.Fill;
        noteGroup.Controls.Add(_differenceNote, 0, 1);

        noteGroup.Controls[0].Text =
            $"{summary.InvoiceCount} hóa đơn đã bán | Ghi chú chênh lệch (bắt buộc nếu lệch)";
        fields.Controls.Add(noteGroup, 0, 9);
        fields.SetColumnSpan(noteGroup, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10, 7, 16, 7),
            WrapContents = false
        };
        var confirm = CreateButton("Xác nhận chốt ca", Color.FromArgb(30, 145, 95));
        confirm.Click += (_, _) =>
        {
            if (ActualCash != summary.TheoreticalCash && string.IsNullOrWhiteSpace(DifferenceNote))
            {
                MessageBox.Show(this, "Vui lòng nhập lý do chênh lệch tiền.", "Thiếu ghi chú",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _differenceNote.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
        var cancel = CreateButton("Hủy", Color.FromArgb(100, 110, 125));
        cancel.DialogResult = DialogResult.Cancel;
        actions.Controls.Add(confirm);
        actions.Controls.Add(cancel);

        AcceptButton = confirm;
        CancelButton = cancel;
        Controls.Add(fields);
        Controls.Add(actions);
        UpdateDifference();
    }

    public static void ShowPrintPreview(IWin32Window owner, ClosedShiftRow shift)
    {
        using var document = new PrintDocument();
        document.DocumentName = $"Ket toan ca {shift.ShiftId}";
        document.PrintPage += (_, e) =>
        {
            using var titleFont = new Font("Segoe UI", 16F, FontStyle.Bold);
            using var bodyFont = new Font("Segoe UI", 10F);
            float y = 55;
            e.Graphics.DrawString("PHIẾU KẾT TOÁN CA LÀM VIỆC", titleFont, Brushes.Black, 55, y);
            y += 48;

            string[] lines =
            [
                $"Mã ca: {shift.ShiftId}",
                $"Nhân viên: {shift.EmployeeName} ({shift.EmployeeId})",
                $"Mở ca: {shift.OpenedAt:dd/MM/yyyy HH:mm:ss}",
                $"Chốt ca: {shift.ClosedAt:dd/MM/yyyy HH:mm:ss}",
                $"Tiền mặt đầu ca: {shift.OpeningCash:N0} đ",
                $"Doanh thu tiền mặt: {shift.CashSales:N0} đ",
                $"Doanh thu chuyển khoản / QR: {shift.TransferSales:N0} đ",
                $"Tiền mặt hoàn trả: {shift.CashRefunds:N0} đ",
                $"Tiền lý thuyết: {shift.TheoreticalCash:N0} đ",
                $"Tiền thực tế: {shift.ActualCash:N0} đ",
                $"Chênh lệch: {shift.DifferenceAmount:N0} đ",
                $"Số hóa đơn đã bán: {shift.InvoiceCount}",
                $"Ghi chú: {shift.DifferenceNote ?? "Không có"}"
            ];
            foreach (string line in lines)
            {
                e.Graphics.DrawString(line, bodyFont, Brushes.Black, 65, y);
                y += 27;
            }
        };

        using var preview = new PrintPreviewDialog
        {
            Document = document,
            StartPosition = FormStartPosition.CenterParent,
            Width = 900,
            Height = 700
        };
        preview.ShowDialog(owner);
    }

    private void UpdateDifference()
    {
        decimal difference = _actualCash.Value - _summary.TheoreticalCash;
        _difference.Text = $"{difference:+#,##0.00;-#,##0.00;0.00} đ";
        _difference.ForeColor = difference < 0
            ? Color.Firebrick
            : Color.FromArgb(30, 115, 70);
    }

    private static void AddField(TableLayoutPanel layout, int row, string label, Control input)
    {
        layout.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(65, 75, 90)
        }, 0, row);
        layout.Controls.Add(input, 1, row);
    }

    private static TextBox ReadOnlyText(string value) => new()
    {
        Text = value,
        ReadOnly = true,
        Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(245, 247, 250),
        BorderStyle = BorderStyle.FixedSingle
    };

    private static Button CreateButton(string text, Color color) => new()
    {
        Text = text,
        BackColor = color,
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 9F, FontStyle.Bold),
        Size = new Size(155, 36),
        Cursor = Cursors.Hand
    };
}
