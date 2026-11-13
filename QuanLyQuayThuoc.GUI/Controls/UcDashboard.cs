using QuanLyQuayThuoc.BLL.Services;

namespace QuanLyQuayThuoc.GUI.Controls;

public sealed class UcDashboard : UserControl
{
    private readonly DashboardService _service = new();
    private readonly Label _lblRevenue = new();
    private readonly Label _lblInvoices = new();
    private readonly Label _lblMedicines = new();
    private readonly Label _lblLowStock = new();
    private readonly Label _lblExpiring = new();
    private readonly Label _lblExpired = new();
    private readonly Label _lblStatus = new();

    public UcDashboard()
    {
        BuildLayout();
        Load += UcDashboard_Load;
    }

    private void BuildLayout()
    {
        BackColor = Color.FromArgb(245, 247, 251);
        Padding = new Padding(24);

        var title = new Label
        {
            Text = "Hoạt động trong ngày",
            Font = new Font("Segoe UI", 17F, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 48, 70),
            AutoSize = true,
            Location = new Point(24, 20)
        };
        var subtitle = new Label
        {
            Text = "Tổng hợp nhanh tình hình kinh doanh và kho thuốc",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Color.Gray,
            AutoSize = true,
            Location = new Point(27, 55)
        };
        Controls.Add(title);
        Controls.Add(subtitle);

        var grid = new TableLayoutPanel
        {
            Location = new Point(24, 100),
            Size = new Size(1050, 310),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ColumnCount = 3,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        for (var i = 0; i < 3; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        for (var i = 0; i < 2; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        grid.Controls.Add(CreateCard("Doanh thu hôm nay", _lblRevenue, "0 ₫", Color.FromArgb(37, 99, 170)), 0, 0);
        grid.Controls.Add(CreateCard("Hóa đơn hôm nay", _lblInvoices, "0", Color.FromArgb(22, 143, 120)), 1, 0);
        grid.Controls.Add(CreateCard("Thuốc đang kinh doanh", _lblMedicines, "0", Color.FromArgb(116, 86, 180)), 2, 0);
        grid.Controls.Add(CreateCard("Thuốc dưới tồn tối thiểu", _lblLowStock, "0", Color.FromArgb(220, 145, 35)), 0, 1);
        grid.Controls.Add(CreateCard("Lô sắp hết hạn (30 ngày)", _lblExpiring, "0", Color.FromArgb(214, 120, 40)), 1, 1);
        grid.Controls.Add(CreateCard("Lô đã hết hạn còn tồn", _lblExpired, "0", Color.FromArgb(190, 65, 65)), 2, 1);
        Controls.Add(grid);

        var panel = new Panel
        {
            Location = new Point(24, 440),
            Size = new Size(1050, 180),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.White,
            Padding = new Padding(20)
        };
        var panelTitle = new Label
        {
            Text = "Tình trạng hệ thống",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 18)
        };
        _lblStatus.Text = "Đang tải dữ liệu...";
        _lblStatus.Font = new Font("Segoe UI", 10F);
        _lblStatus.ForeColor = Color.DimGray;
        _lblStatus.Location = new Point(20, 58);
        _lblStatus.Size = new Size(980, 80);
        panel.Controls.Add(panelTitle);
        panel.Controls.Add(_lblStatus);
        Controls.Add(panel);
    }

    private static Panel CreateCard(string title, Label valueLabel, string initialValue, Color accent)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 14, 14),
            Padding = new Padding(18)
        };
        var stripe = new Panel { Dock = DockStyle.Left, Width = 5, BackColor = accent };
        var heading = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Color.Gray,
            AutoSize = true,
            Location = new Point(22, 22)
        };
        valueLabel.Text = initialValue;
        valueLabel.Font = new Font("Segoe UI", 21F, FontStyle.Bold);
        valueLabel.ForeColor = Color.FromArgb(37, 48, 70);
        valueLabel.AutoSize = true;
        valueLabel.Location = new Point(22, 58);
        card.Controls.Add(stripe);
        card.Controls.Add(heading);
        card.Controls.Add(valueLabel);
        return card;
    }

    private async void UcDashboard_Load(object? sender, EventArgs e)
    {
        try
        {
            var data = await _service.GetSummaryAsync();
            _lblRevenue.Text = data.TodayRevenue.ToString("N0") + " ₫";
            _lblInvoices.Text = data.TodayInvoiceCount.ToString("N0");
            _lblMedicines.Text = data.ActiveMedicineCount.ToString("N0");
            _lblLowStock.Text = data.LowStockMedicineCount.ToString("N0");
            _lblExpiring.Text = data.ExpiringBatchCount.ToString("N0");
            _lblExpired.Text = data.ExpiredBatchCount.ToString("N0");
            _lblStatus.Text = "Đã tải dữ liệu từ SQL Server. Số liệu được tổng hợp trực tiếp từ danh mục thuốc, lô hàng và hóa đơn.";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = "Không tải được dữ liệu dashboard. Hãy kiểm tra database và chuỗi kết nối.\n" + ex.Message;
            _lblStatus.ForeColor = Color.Firebrick;
        }
    }
}
