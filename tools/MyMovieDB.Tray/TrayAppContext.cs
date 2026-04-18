using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Forms;

namespace MyMovieDB.Tray;

internal sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly string _webDllPath;
    private readonly string _listenUrl;
    private readonly string _localUrl;
    private Process? _webProcess;
    private QrCodeForm? _qrForm;
    private bool _isExiting;

    public TrayAppContext(string[] args)
    {
        _webDllPath = GetArg(args, "--web-dll") ?? throw new InvalidOperationException("Missing --web-dll");
        _listenUrl = GetArg(args, "--listen-url") ?? "http://0.0.0.0:5057";
        _localUrl = GetArg(args, "--local-url") ?? "http://localhost:5057";

        TrayLog.Write($"Tray context init. webDll={_webDllPath}");

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "mymoviedb.ico");
        var icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
        TrayLog.Write($"Icon path: {iconPath}; exists={File.Exists(iconPath)}");

        var menu = new ContextMenuStrip();
        menu.Items.Add("QR code", null, (_, _) => ShowQr());
        menu.Items.Add("Открыть", null, (_, _) => OpenLocal());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выключить", null, (_, _) => ExitApplication());

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "MyMovieDB",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => ShowQr();

        StartWebHost();
        ShowBalloon("MyMovieDB", "Запущено в трее. QR code — в меню значка.");
        TrayLog.Write("Tray icon visible and web host start requested");
    }

    private void StartWebHost()
    {
        if (!File.Exists(_webDllPath))
        {
            ShowBalloon("MyMovieDB", "Не найден web host DLL.");
            TrayLog.Write("Web DLL not found");
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{_webDllPath}\"",
            WorkingDirectory = Path.GetDirectoryName(_webDllPath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.Environment["AppHost__AutoLaunchBrowser"] = "false";
        startInfo.Environment["AppHost__Url"] = _listenUrl;
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";

        TrayLog.Write($"Starting web host: {startInfo.FileName} {startInfo.Arguments}");
        _webProcess = Process.Start(startInfo);

        if (_webProcess is null)
        {
            TrayLog.Write("Process.Start returned null for web host");
            ShowBalloon("MyMovieDB", "Не удалось запустить веб-процесс.");
            return;
        }

        TrayLog.Write($"Web host PID={_webProcess.Id}");
        _webProcess.EnableRaisingEvents = true;
        _webProcess.Exited += (_, _) =>
        {
            TrayLog.Write("Web host exited");
            if (!_isExiting && _notifyIcon.Visible)
            {
                ShowBalloon("MyMovieDB", "Веб-процесс остановился.");
            }
        };
    }

    private void OpenLocal()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _localUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            TrayLog.Write("OpenLocal failed: " + ex);
            ShowBalloon("MyMovieDB", "Не удалось открыть браузер.");
        }
    }

    private void ShowQr()
    {
        var publicUrls = BuildPublicUrls();
        var primaryUrl = publicUrls.FirstOrDefault() ?? _localUrl;
        TrayLog.Write($"Show QR for {primaryUrl}");
        TrayLog.Write("Candidate URLs: " + string.Join(" | ", publicUrls));

        if (_qrForm is null || _qrForm.IsDisposed)
        {
            _qrForm = new QrCodeForm(primaryUrl, publicUrls);
        }
        else
        {
            _qrForm.SetUrls(primaryUrl, publicUrls);
        }

        _qrForm.StartPosition = FormStartPosition.CenterScreen;
        _qrForm.Show();
        _qrForm.BringToFront();
        _qrForm.Activate();
    }

    private IReadOnlyList<string> BuildPublicUrls()
    {
        if (!Uri.TryCreate(_localUrl, UriKind.Absolute, out var uri))
        {
            uri = new Uri("http://127.0.0.1:5057");
        }

        var port = uri.Port;
        var scheme = uri.Scheme;
        var ips = ResolveCandidateIpv4Addresses();
        if (ips.Count == 0)
        {
            return new[] { _localUrl };
        }

        var bestIp = ips[0];
        return new[] { $"{scheme}://{bestIp}:{port}/" };
    }

    private static List<string> ResolveCandidateIpv4Addresses()
    {
        var ranked = new List<(int Score, string Ip)>();

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            var adapterName = $"{ni.Name} {ni.Description}";
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel
                || adapterName.Contains("virtual", StringComparison.OrdinalIgnoreCase)
                || adapterName.Contains("hyper-v", StringComparison.OrdinalIgnoreCase)
                || adapterName.Contains("vmware", StringComparison.OrdinalIgnoreCase)
                || adapterName.Contains("vbox", StringComparison.OrdinalIgnoreCase)
                || adapterName.Contains("docker", StringComparison.OrdinalIgnoreCase)
                || adapterName.Contains("wsl", StringComparison.OrdinalIgnoreCase)
                || adapterName.Contains("tailscale", StringComparison.OrdinalIgnoreCase)
                || adapterName.Contains("zerotier", StringComparison.OrdinalIgnoreCase)
                || adapterName.Contains("vpn", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var properties = ni.GetIPProperties();
            var hasGateway = properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Any.Equals(g.Address) && !IPAddress.None.Equals(g.Address));

            foreach (var address in properties.UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address.Address))
                {
                    continue;
                }

                var value = address.Address.ToString();
                if (value.StartsWith("169.254.", StringComparison.Ordinal))
                {
                    continue;
                }

                var score = 0;
                if (IsPrivateIpv4(value)) score += 100;
                if (hasGateway) score += 50;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) score += 30;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) score += 20;

                ranked.Add((score, value));
            }
        }

        return ranked
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Ip, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Ip)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsPrivateIpv4(string ip)
    {
        return ip.StartsWith("10.", StringComparison.Ordinal)
            || ip.StartsWith("192.168.", StringComparison.Ordinal)
            || (ip.StartsWith("172.", StringComparison.Ordinal) && int.TryParse(ip.Split('.')[1], out var second) && second >= 16 && second <= 31);
    }

    private void ShowBalloon(string title, string text)
    {
        TrayLog.Write($"Balloon: {title} | {text}");
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.ShowBalloonTip(2500);
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        TrayLog.Write("Exit requested");

        try
        {
            _qrForm?.Close();
            _qrForm?.Dispose();
        }
        catch (Exception ex)
        {
            TrayLog.Write("QR form dispose failed: " + ex);
        }

        try
        {
            if (_webProcess is { HasExited: false })
            {
                _webProcess.Kill(entireProcessTree: true);
                _webProcess.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            TrayLog.Write("Web process stop failed: " + ex);
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isExiting)
        {
            ExitApplication();
        }

        base.Dispose(disposing);
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
