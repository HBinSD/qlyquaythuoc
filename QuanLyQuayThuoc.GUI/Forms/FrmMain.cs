
using QuanLyQuayThuoc.GUI.Session;
using QuanLyQuayThuoc.GUI.Controls;

namespace QuanLyQuayThuoc.GUI.Forms;

public partial class FrmMain : Form
{
    private readonly Dictionary<string, Button> _menuButtons = new();

    public FrmMain()
    {
        InitializeComponent();

        // Đăng ký các nút menu trước khi điều hướng.
        RegisterMenuButtons();

        lblUserName.Text = SessionManager.CurrentUser?.FullName ?? "Người dùng";
        lblRole.Text = SessionManager.CurrentUser?.RoleName ?? "";

        ApplyRolePermissions();
        Navigate("dashboard");
    }

    private void ApplyRolePermissions()
    {
        var role = SessionManager.CurrentUser?.RoleId;

        btnMedicine.Visible = role is "ADMIN" or "PHARMACIST";
        btnWarehouse.Visible = role is "ADMIN" or "WAREHOUSE";
        btnSuppliers.Visible = role is "ADMIN" or "WAREHOUSE";
        btnSales.Visible = role is "ADMIN" or "PHARMACIST";
        btnInvoices.Visible = role is "ADMIN" or "PHARMACIST";
        btnEmployees.Visible = role == "ADMIN";
        btnReports.Visible = role is "ADMIN" or "PHARMACIST";
    }

    private void Navigate(string page)
    {
        foreach (var button in _menuButtons.Values)
            button.BackColor = Color.FromArgb(29, 43, 91);

        if (_menuButtons.TryGetValue(page, out var selected))
            selected.BackColor = Color.FromArgb(48, 70, 133);

        pnlContent.SuspendLayout();

        try
        {
            // Giải phóng UserControl cũ trước khi hiển thị trang mới.
            foreach (Control control in pnlContent.Controls)
                control.Dispose();

            pnlContent.Controls.Clear();

            UserControl view = page switch
            {
                "dashboard" => new UcDashboard(),
                "medicine" => new UcMedicine(),
                "warehouse" => new UcWarehouse(),
                _ => new UcComingSoon(GetPageTitle(page))
            };

            view.Dock = DockStyle.Fill;
            pnlContent.Controls.Add(view);

            lblPageTitle.Text = GetPageTitle(page);
        }
        finally
        {
            pnlContent.ResumeLayout(true);
        }
    }

    private static string GetPageTitle(string page) => page switch
    {
        "dashboard" => "Tổng quan",
        "medicine" => "Quản lý thuốc",
        "warehouse" => "Quản lý kho",
        "suppliers" => "Nhà cung cấp",
        "sales" => "Bán thuốc",
        "invoices" => "Hóa đơn",
        "employees" => "Nhân viên",
        "reports" => "Báo cáo",
        _ => "Chức năng"
    };

    private void MenuButton_Click(object? sender, EventArgs e)
    {
        if (sender is Button button && button.Tag is string page)
        {
            // Không điều hướng đến chức năng người dùng không có quyền truy cập.
            if (_menuButtons.TryGetValue(page, out var menuButton) &&
                menuButton.Visible)
            {
                Navigate(page);
            }
        }
    }

    private void btnLogout_Click(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Bạn có chắc chắn muốn đăng xuất?",
            "Đăng xuất",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
            Close();
    }

    private void RegisterMenuButtons()
    {
        _menuButtons["dashboard"] = btnDashboard;
        _menuButtons["medicine"] = btnMedicine;
        _menuButtons["warehouse"] = btnWarehouse;
        _menuButtons["suppliers"] = btnSuppliers;
        _menuButtons["sales"] = btnSales;
        _menuButtons["invoices"] = btnInvoices;
        _menuButtons["employees"] = btnEmployees;
        _menuButtons["reports"] = btnReports;
    }
}
