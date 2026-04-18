using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using QRCoder;

namespace MyMovieDB.Tray;

internal sealed class QrCodeForm : Form
{
    private readonly PictureBox _pictureBox;
    private readonly TextBox _urlTextBox;
    private readonly ListBox _alternativesList;
    private readonly Label _altLabel;
    private readonly Button _copyButton;
    private readonly Button _openButton;
    private readonly Button _closeButton;
    private string _currentUrl;

    public QrCodeForm(string url, IReadOnlyList<string>? urls = null)
    {
        _currentUrl = url;

        Text = "MyMovieDB QR";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(18, 22, 30);
        ForeColor = Color.FromArgb(242, 244, 248);
        ClientSize = new Size(420, 620);

        var title = new Label
        {
            Text = "Открой с телефона",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = ForeColor,
            Dock = DockStyle.Top,
            Height = 46
        };

        _pictureBox = new PictureBox
        {
            Dock = DockStyle.Top,
            Height = 340,
            Width = 340,
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = Color.White,
            Padding = new Padding(12)
        };

        var help = new Label
        {
            Text = "Если QR не открывается — нажми на адрес ниже и скопируй его вручную.",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 42,
            ForeColor = Color.FromArgb(207, 225, 255),
            Font = new Font("Segoe UI", 9, FontStyle.Regular)
        };

        _urlTextBox = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 32,
            ReadOnly = true,
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            BackColor = Color.FromArgb(28, 34, 43),
            ForeColor = ForeColor,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(16, 6, 16, 6)
        };
        _urlTextBox.Click += (_, _) => _urlTextBox.SelectAll();
        _urlTextBox.DoubleClick += (_, _) => _urlTextBox.SelectAll();

        _altLabel = new Label
        {
            Text = "Другие адреса",
            AutoSize = false,
            TextAlign = ContentAlignment.BottomLeft,
            Dock = DockStyle.Top,
            Height = 24,
            Padding = new Padding(16, 0, 0, 0),
            ForeColor = Color.FromArgb(207, 225, 255),
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };

        _alternativesList = new ListBox
        {
            Dock = DockStyle.Top,
            Height = 92,
            BackColor = Color.FromArgb(28, 34, 43),
            ForeColor = ForeColor,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9, FontStyle.Regular)
        };
        _alternativesList.DoubleClick += (_, _) => SelectAlternative();

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 62,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(16, 10, 16, 10),
            WrapContents = false
        };

        _copyButton = new Button
        {
            Text = "Копировать адрес",
            Width = 125,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            ForeColor = ForeColor,
            BackColor = Color.FromArgb(38, 49, 66)
        };
        _copyButton.FlatAppearance.BorderColor = Color.FromArgb(44, 51, 65);
        _copyButton.Click += (_, _) =>
        {
            try { Clipboard.SetText(_currentUrl); } catch { }
        };

        _openButton = new Button
        {
            Text = "Открыть на этом ПК",
            Width = 145,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            ForeColor = ForeColor,
            BackColor = Color.FromArgb(38, 49, 66)
        };
        _openButton.FlatAppearance.BorderColor = Color.FromArgb(44, 51, 65);
        _openButton.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = _currentUrl, UseShellExecute = true });
            }
            catch { }
        };

        _closeButton = new Button
        {
            Text = "Закрыть",
            Width = 90,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            ForeColor = ForeColor,
            BackColor = Color.FromArgb(38, 49, 66)
        };
        _closeButton.FlatAppearance.BorderColor = Color.FromArgb(44, 51, 65);
        _closeButton.Click += (_, _) => Close();

        buttonPanel.Controls.Add(_copyButton);
        buttonPanel.Controls.Add(_openButton);
        buttonPanel.Controls.Add(_closeButton);

        Controls.Add(buttonPanel);
        Controls.Add(_alternativesList);
        Controls.Add(_altLabel);
        Controls.Add(_urlTextBox);
        Controls.Add(help);
        Controls.Add(_pictureBox);
        Controls.Add(title);

        SetUrls(url, urls);
    }

    public void SetUrls(string url, IReadOnlyList<string>? urls = null)
    {
        _currentUrl = url;
        _urlTextBox.Text = url;
        _pictureBox.Image?.Dispose();
        _pictureBox.Image = BuildQrBitmap(url);

        _alternativesList.Items.Clear();
        if (urls is not null)
        {
            foreach (var item in urls.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                _alternativesList.Items.Add(item);
            }
        }

        var hasAlternatives = _alternativesList.Items.Count > 1;
        _alternativesList.Visible = hasAlternatives;
        _altLabel.Visible = hasAlternatives;
        if (_alternativesList.Items.Count > 0)
        {
            _alternativesList.SelectedIndex = 0;
        }
    }

    private void SelectAlternative()
    {
        if (_alternativesList.SelectedItem is string selected && !string.IsNullOrWhiteSpace(selected))
        {
            SetUrls(selected, _alternativesList.Items.Cast<string>().ToList());
        }
    }

    private static Bitmap BuildQrBitmap(string url)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        using var qr = new QRCode(data);
        return qr.GetGraphic(16, Color.Black, Color.White, drawQuietZones: true);
    }
}
