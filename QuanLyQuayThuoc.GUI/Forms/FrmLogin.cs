using QuanLyQuayThuoc.BLL.Services;
using QuanLyQuayThuoc.DAL.Database;
using QuanLyQuayThuoc.GUI.Session;

namespace QuanLyQuayThuoc.GUI.Forms;

public partial class FrmLogin : Form
{
    private readonly AuthService _authService = new();

    public FrmLogin()
    {
        InitializeComponent();
        AcceptButton = btnLogin;
    }

    private async void btnLogin_Click(object? sender, EventArgs e)
    {
        var username = txtUsername.Text.Trim();
        var password = txtPassword.Text;

        if (username.Length == 0 || password.Length == 0)
        {
            ShowError("Vui lòng nhập tên đăng nhập và mật khẩu.");
            return;
        }

        SetBusy(true);
        try
        {
            var user = await _authService.LoginAsync(username, password);
            if (user is null)
            {
                ShowError("Tên đăng nhập hoặc mật khẩu không đúng, hoặc tài khoản đã bị khóa.");
                txtPassword.SelectAll();
                txtPassword.Focus();
                return;
            }

            SessionManager.SignIn(user);
            Hide();
            using var main = new FrmMain();
            main.ShowDialog();
            SessionManager.SignOut();
            txtPassword.Clear();
            Show();
        }
        catch (Exception ex)
        {
            ShowError("Không thể đăng nhập. Hãy kiểm tra SQL Server và chuỗi kết nối.\n\n" + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void lnkTestDb_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        SetBusy(true);
        try
        {
            var ok = await DbConnectionFactory.TestConnectionAsync();
            MessageBox.Show(ok ? "Kết nối SQL Server thành công." : "Không thể kết nối SQL Server.",
                "Kiểm tra database", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            ShowError("Kết nối thất bại: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        btnLogin.Enabled = !busy;
        lnkTestDb.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void ShowError(string message) =>
        MessageBox.Show(message, "Quản lý quầy thuốc", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
