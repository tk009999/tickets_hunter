using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TicketsHunter.Desktop;

public partial class MainWindow : Window
{
    private readonly string _dataDirectory;
    private readonly string _configPath;
    private readonly string _loginConfigPath;
    private readonly string _browserProfilePath;
    private Process? _engineProcess;
    private bool _isPaused;
    private bool _isLoginMode;

    private string PauseFlagPath => Path.Combine(
        AppContext.BaseDirectory, "_engine", "instances", "desktop", "MAXBOT_INT28_IDLE.txt");

    public MainWindow()
    {
        InitializeComponent();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TicketsHunter");
        _configPath = Path.Combine(_dataDirectory, "desktop.json");
        _loginConfigPath = Path.Combine(_dataDirectory, "ticketplus-login.json");
        _browserProfilePath = Path.Combine(_dataDirectory, "ChromeProfile");
        Directory.CreateDirectory(_dataDirectory);
        LoadSavedSettings();
        Closing += Window_Closing;
    }

    private static string FormatKeyword(string text)
    {
        var items = text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.Replace("\"", string.Empty).Replace("'", string.Empty))
            .Where(item => item.Length > 0);
        return string.Join(",", items.Select(item => $"\"{item}\""));
    }

    private static string DisplayKeyword(string? text) =>
        (text ?? string.Empty).Replace("\",\"", ";").Replace("\"", string.Empty);

    private int SelectedTicketCount() =>
        int.Parse(((ComboBoxItem)TicketCountCombo.SelectedItem).Tag!.ToString()!);

    private string SelectedMode() =>
        ((ComboBoxItem)SelectionModeCombo.SelectedItem).Tag!.ToString()!;

    private JsonObject BuildConfig()
    {
        var config = DefaultConfig.Create();
        config["homepage"] = UrlTextBox.Text.Trim();
        config["ticket_number"] = SelectedTicketCount();
        config["date_auto_select"]!["date_keyword"] = FormatKeyword(DateKeywordsTextBox.Text);
        config["date_auto_select"]!["mode"] = SelectedMode();
        config["area_auto_select"]!["area_keyword"] = FormatKeyword(AreaKeywordsTextBox.Text);
        config["area_auto_select"]!["mode"] = SelectedMode();
        config["keyword_exclude"] = FormatKeyword(ExcludeKeywordsTextBox.Text);
        config["advanced"]!["user_data_dir"] = _browserProfilePath;
        return config;
    }

    private JsonObject BuildLoginConfig()
    {
        var config = BuildConfig();
        config["homepage"] = "https://ticketplus.com.tw/";
        config["date_auto_select"]!["enable"] = false;
        config["area_auto_select"]!["enable"] = false;
        return config;
    }

    private bool ValidateInputs()
    {
        if (!Uri.TryCreate(UrlTextBox.Text.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MainTabs.SelectedIndex = 0;
            MessageBox.Show("請貼上完整的活動售票網址。", "網址格式不正確",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            UrlTextBox.Focus();
            return false;
        }
        return true;
    }

    private bool SaveSettings(bool showConfirmation)
    {
        if (!ValidateInputs()) return false;
        File.WriteAllText(_configPath, BuildConfig().ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }), new UTF8Encoding(false));
        if (showConfirmation)
            MessageBox.Show("設定已儲存在這台電腦。", "儲存完成",
                MessageBoxButton.OK, MessageBoxImage.Information);
        return true;
    }

    private void LoadSavedSettings()
    {
        if (!File.Exists(_configPath)) return;
        try
        {
            var config = JsonNode.Parse(File.ReadAllText(_configPath))!.AsObject();
            UrlTextBox.Text = config["homepage"]?.GetValue<string>() ?? string.Empty;
            var count = Math.Clamp(config["ticket_number"]?.GetValue<int>() ?? 1, 1, 4);
            TicketCountCombo.SelectedIndex = count - 1;
            DateKeywordsTextBox.Text = DisplayKeyword(config["date_auto_select"]?["date_keyword"]?.GetValue<string>());
            AreaKeywordsTextBox.Text = DisplayKeyword(config["area_auto_select"]?["area_keyword"]?.GetValue<string>());
            ExcludeKeywordsTextBox.Text = DisplayKeyword(config["keyword_exclude"]?.GetValue<string>());
            var mode = config["area_auto_select"]?["mode"]?.GetValue<string>() ?? "from top to bottom";
            SelectionModeCombo.SelectedIndex = mode switch
            {
                "random" => 1, "from bottom to top" => 2, "center" => 3, _ => 0
            };
        }
        catch (Exception exception)
        {
            MessageBox.Show($"舊設定檔無法讀取，將使用預設值。\n\n{exception.Message}",
                "設定檔錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e) => SaveSettings(true);

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (_engineProcess is { HasExited: false })
        {
            if (_isLoginMode)
            {
                RequestEngineQuit("login");
                AppendLog("正在儲存 TicketPlus 登入狀態…");
                LoginButton.IsEnabled = false;
                return;
            }

            MessageBox.Show("請先停止目前的搶票程式。", "程式正在執行",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        File.WriteAllText(_loginConfigPath, BuildLoginConfig().ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }), new UTF8Encoding(false));

        LogTextBox.Clear();
        AppendLog("正在開啟 TicketPlus 登入頁…");
        AppendLog("請在 Chrome 完成登入，然後回到這裡按「登入完成」。");
        MainTabs.SelectedIndex = 2;
        LaunchEngine(_loginConfigPath, "login", true);
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_engineProcess is { HasExited: false })
        {
            MessageBox.Show("搶票程式已經在執行。", "正在執行",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!SaveSettings(false)) return;

        LaunchEngine(_configPath, "desktop", false);
    }

    private void LaunchEngine(string configPath, string instance, bool loginMode)
    {
        var enginePath = Path.Combine(AppContext.BaseDirectory, "_engine", "nodriver_tixcraft.exe");
        if (!File.Exists(enginePath))
        {
            MessageBox.Show("找不到內部搶票引擎，請重新解壓縮完整的發行包。",
                "程式不完整", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var staleQuitFlag = Path.Combine(AppContext.BaseDirectory, "_engine", "instances", instance,
            "MAXBOT_INT28_QUIT.txt");
        if (File.Exists(staleQuitFlag)) File.Delete(staleQuitFlag);

        _isLoginMode = loginMode;
        SetPaused(false);
        SetRunningState(true, loginMode ? "請在 Chrome 登入 TicketPlus" : "正在搶票");

        var startInfo = new ProcessStartInfo(enginePath)
        {
            WorkingDirectory = Path.GetDirectoryName(enginePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add($"--input={configPath}");
        startInfo.ArgumentList.Add($"--instance={instance}");

        _engineProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _engineProcess.OutputDataReceived += (_, args) => { if (args.Data != null) AppendLog(args.Data); };
        _engineProcess.ErrorDataReceived += (_, args) => { if (args.Data != null) AppendLog("錯誤：" + args.Data); };
        _engineProcess.Exited += (_, _) => Dispatcher.Invoke(() =>
        {
            AppendLog("搶票程式已停止。");
            SetPaused(false);
            var wasLoginMode = _isLoginMode;
            _isLoginMode = false;
            SetRunningState(false, wasLoginMode ? "已儲存登入狀態" : "已停止");
            if (wasLoginMode)
                AppendLog("登入狀態已保存，現在可以按「開始搶票」。");
        });

        try
        {
            _engineProcess.Start();
            _engineProcess.BeginOutputReadLine();
            _engineProcess.BeginErrorReadLine();
        }
        catch (Exception exception)
        {
            AppendLog("啟動失敗：" + exception.Message);
            SetRunningState(false, "啟動失敗");
            MessageBox.Show(exception.Message, "啟動失敗", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RequestEngineQuit(string instance)
    {
        try
        {
            var quitPath = Path.Combine(AppContext.BaseDirectory, "_engine", "instances", instance,
                "MAXBOT_INT28_QUIT.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(quitPath)!);
            File.WriteAllText(quitPath, "quit");
        }
        catch (Exception exception)
        {
            AppendLog("關閉登入視窗時發生問題：" + exception.Message);
            StopEngine();
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_engineProcess is not { HasExited: false }) return;

        SetPaused(!_isPaused);
        if (_isPaused)
        {
            AppendLog("已暫停自動操作，Chrome 會繼續保留。你現在可以手動登入 TicketPlus。");
            StatusText.Text = "已暫停，可手動操作 Chrome";
        }
        else
        {
            AppendLog("已恢復自動搶票。");
            StatusText.Text = "正在搶票";
        }
    }

    private void SetPaused(bool paused)
    {
        _isPaused = paused;
        try
        {
            if (paused)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PauseFlagPath)!);
                File.WriteAllText(PauseFlagPath, "paused");
            }
            else if (File.Exists(PauseFlagPath))
            {
                File.Delete(PauseFlagPath);
            }
        }
        catch (Exception exception)
        {
            AppendLog("切換暫停狀態時發生問題：" + exception.Message);
        }

        StopButton.Content = paused ? "恢復自動搶票" : "暫停自動操作";
    }

    private void StopEngine()
    {
        try
        {
            if (_engineProcess is { HasExited: false })
            {
                AppendLog("正在停止…");
                _engineProcess.Kill(entireProcessTree: true);
                _engineProcess.WaitForExit(5000);
            }
        }
        catch (Exception exception)
        {
            AppendLog("停止時發生問題：" + exception.Message);
        }
        SetPaused(false);
        SetRunningState(false, "已停止");
    }

    private void AppendLog(string line) => Dispatcher.Invoke(() =>
    {
        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
        LogTextBox.ScrollToEnd();
    });

    private void SetRunningState(bool running, string text)
    {
        StatusText.Text = text;
        StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(running ? "#29A36A" : "#9AA0AA"));
        StartButton.IsEnabled = !running;
        LoginButton.IsEnabled = !running || _isLoginMode;
        LoginButton.Content = _isLoginMode ? "登入完成" : "先登入 TicketPlus";
        StopButton.IsEnabled = running && !_isLoginMode;
    }

    private void Window_Closing(object? sender, CancelEventArgs e) => StopEngine();
}
