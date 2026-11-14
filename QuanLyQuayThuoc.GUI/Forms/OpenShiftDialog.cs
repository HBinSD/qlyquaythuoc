using System.Drawing;
using System.Windows.Forms;
using QuanLyQuayThuoc.GUI.Session;

namespace QuanLyQuayThuoc.GUI.Forms;

internal sealed class OpenShiftDialog : Form
{
    private readonly NumericUpDown _openingCash = new();
    private readonly TextBox _openingNote = new();

    public decimal OpeningCash => _openingCash.Value;

    public string? OpeningNote =>
        string.IsNullOrWhiteSpace(_openingNote.Text) ? null : _openingNote.Text.Trim();

    public OpenShiftDialog(string employeeId, decimal suggestedOpeningCash = 0)
    {
        var currentUser = SessionManager.CurrentUser;
        if (currentUser is null
            || !currentUser.EmployeeId.Equals(employeeId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Không xác định được nhân viên đang đăng nhập.");
        }

        Text = "Mở ca làm việc";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(480, 350);
        Font = new Font("Segoe UI", 9F);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 18, 18, 8),
            ColumnCount = 2,
            RowCount = 4
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        AddField(fields, 0, "Nhân viên", ReadOnlyText(currentUser.FullName));
        AddField(fields, 1, "Thời gian bắt đầu", ReadOnlyText(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")));

        _openingCash.Minimum = 0;
        _openingCash.Maximum = 9999999999999999.99M;
        _openingCash.DecimalPlaces = 2;
        _openingCash.Increment = 10000;
        _openingCash.ThousandsSeparator = true;
        _openingCash.Dock = DockStyle.Top;
        _openingCash.Font = new Font("Segoe UI", 10F);
        _openingCash.Value = Math.Clamp(suggestedOpeningCash, _openingCash.Minimum, _openingCash.Maximum);
        var openingCashPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        openingCashPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        openingCashPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        openingCashPanel.Controls.Add(_openingCash, 0, 0);
        openingCashPanel.Controls.Add(new Label
        {
            Text = $"Gợi ý từ ca trước: {suggestedOpeningCash:N0} đ",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);
        AddField(fields, 2, "Tiền mặt đầu ca (VND)", openingCashPanel);

        _openingNote.Multiline = true;
        _openingNote.MaxLength = 500;
        _openingNote.ScrollBars = ScrollBars.Vertical;
        _openingNote.PlaceholderText = "Ví dụ: Nhận bàn giao từ ca sáng";
        _openingNote.Dock = DockStyle.Fill;
        AddField(fields, 3, "Ghi chú đầu ca", _openingNote);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10, 7, 16, 7),
            WrapContents = false
        };
        var confirm = CreateButton("Xác nhận mở ca", Color.FromArgb(30, 145, 95));
        confirm.DialogResult = DialogResult.OK;
        var cancel = CreateButton("Hủy", Color.FromArgb(100, 110, 125));
        cancel.DialogResult = DialogResult.Cancel;
        actions.Controls.Add(confirm);
        actions.Controls.Add(cancel);

        AcceptButton = confirm;
        CancelButton = cancel;
        Controls.Add(fields);
        Controls.Add(actions);
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
        Dock = DockStyle.Top,
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
        Size = new Size(150, 36),
        Cursor = Cursors.Hand
    };
}
