namespace QuanLyQuayThuoc.GUI.Controls;

public sealed class UcComingSoon : UserControl
{
    public UcComingSoon(string title)
    {
        BackColor = Color.FromArgb(245, 247, 251);
        var panel = new Panel
        {
            BackColor = Color.White,
            Size = new Size(650, 220),
            Anchor = AnchorStyles.None
        };
        var heading = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 48, 70),
            AutoSize = true,
            Location = new Point(30, 35)
        };
        var message = new Label
        {
            Text = "Giao diện khung đã được tạo. Chức năng nghiệp vụ sẽ được triển khai ở các phần tiếp theo.",
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.Gray,
            Location = new Point(33, 90),
            Size = new Size(580, 55)
        };
        panel.Controls.Add(heading);
        panel.Controls.Add(message);
        Controls.Add(panel);
        Resize += (_, _) => panel.Location = new Point((ClientSize.Width - panel.Width) / 2, (ClientSize.Height - panel.Height) / 2);
        OnResize(EventArgs.Empty);
    }
}
