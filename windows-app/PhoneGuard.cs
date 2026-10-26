using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace PhoneGuardDesktop
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private const string AndroidPackage = "com.r15008.phoneguard";
        private const string ReportPath = "/sdcard/Android/data/com.r15008.phoneguard/files/report.json";
        private readonly ComboBox devices = new ComboBox();
        private readonly TextBox pairAddress = new TextBox();
        private readonly TextBox pairCode = new TextBox();
        private readonly TextBox connectAddress = new TextBox();
        private readonly DataGridView grid = new DataGridView();
        private readonly Label status = new Label();
        private readonly Button scanButton = new Button();
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();

        public MainForm()
        {
            Text = "PhoneGuard · 电脑端";
            MinimumSize = new Size(900, 550);
            Size = new Size(1120, 720);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(247, 249, 252);
            Font = new Font("Microsoft YaHei UI", 9F);
            json.MaxJsonLength = 16 * 1024 * 1024;
            BuildUi();
            Shown += async (s, e) => await RefreshDevices();
        }

        private void BuildUi()
        {
            var layout = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
                Margin = new Padding(0), Padding = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 129));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            Controls.Add(layout);
            var heading = new Label {
                Text = "PhoneGuard", Font = new Font("Microsoft YaHei UI", 21F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 43, 65), Dock = DockStyle.Top, Height = 50,
                Padding = new Padding(16, 9, 0, 0)
            };
            layout.Controls.Add(heading, 0, 0);

            var intro = new Label {
                Text = "查看手机应用风险线索。通过 USB 调试或 Android 无线调试连接；卸载需在手机上确认。",
                Dock = DockStyle.Top, Height = 34, Padding = new Padding(18, 0, 0, 0)
            };
            layout.Controls.Add(intro, 0, 1);

            var toolbar = new FlowLayoutPanel {
                Dock = DockStyle.Top, Height = 129, AutoScroll = true,
                Padding = new Padding(12, 4, 12, 4), WrapContents = true
            };
            layout.Controls.Add(toolbar, 0, 2);
            toolbar.Controls.Add(Label("设备"));
            devices.DropDownStyle = ComboBoxStyle.DropDownList;
            devices.Width = 230;
            toolbar.Controls.Add(devices);
            toolbar.Controls.Add(Button("刷新设备", async (s, e) => await RefreshDevices()));
            toolbar.Controls.Add(Button("安装手机 App", async (s, e) => await InstallAndroidApp()));
            scanButton.Text = "开始扫描";
            scanButton.AutoSize = true;
            scanButton.Click += async (s, e) => await Scan();
            toolbar.Controls.Add(scanButton);
            toolbar.Controls.Add(Button("在手机上查看选中应用", async (s, e) => await OpenAppInfo()));

            toolbar.SetFlowBreak(toolbar.Controls[toolbar.Controls.Count - 1], true);
            toolbar.Controls.Add(Label("配对地址 IP:端口"));
            pairAddress.Width = 145;
            toolbar.Controls.Add(pairAddress);
            toolbar.Controls.Add(Label("配对码"));
            pairCode.Width = 75;
            toolbar.Controls.Add(pairCode);
            toolbar.Controls.Add(Button("无线配对", async (s, e) => await Pair()));
            toolbar.Controls.Add(Label("连接地址 IP:端口"));
            connectAddress.Width = 145;
            toolbar.Controls.Add(connectAddress);
            toolbar.Controls.Add(Button("无线连接", async (s, e) => await Connect()));

            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Color.White;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.RowHeadersVisible = false;
            grid.Columns.Add("Name", "应用");
            grid.Columns.Add("Package", "包名");
            grid.Columns.Add("Risk", "线索分");
            grid.Columns.Add("Level", "复查建议");
            grid.Columns.Add("Installer", "安装来源");
            grid.Columns.Add("Reasons", "线索");
            grid.Columns[0].FillWeight = 100;
            grid.Columns[1].FillWeight = 160;
            grid.Columns[2].FillWeight = 50;
            grid.Columns[3].FillWeight = 70;
            grid.Columns[4].FillWeight = 125;
            grid.Columns[5].FillWeight = 220;
            layout.Controls.Add(grid, 0, 3);

            status.Text = "正在查找设备…";
            status.Dock = DockStyle.Bottom;
            status.Height = 54;
            status.Padding = new Padding(16, 9, 16, 8);
            status.ForeColor = Color.FromArgb(40, 60, 80);
            layout.Controls.Add(status, 0, 4);
        }

        private Label Label(string text)
        {
            return new Label { Text = text, AutoSize = true, Margin = new Padding(8, 9, 3, 3) };
        }

        private Button Button(string text, EventHandler handler)
        {
            var button = new Button { Text = text, AutoSize = true, Margin = new Padding(4, 3, 4, 3) };
            button.Click += handler;
            return button;
        }

        private string SelectedSerial()
        {
            var device = devices.SelectedItem as Device;
            if (device == null) throw new InvalidOperationException("请先选择已授权的手机。电脑和手机需开启 Android 调试。");
            return device.Serial;
        }

        private async Task RefreshDevices()
        {
            try {
                SetStatus("正在查找设备…");
                string output = await Task.Run(() => Adb("devices", "-l"));
                string selected = (devices.SelectedItem as Device) == null ? null : ((Device)devices.SelectedItem).Serial;
                devices.Items.Clear();
                foreach (string raw in output.Split('\n')) {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("List of devices")) continue;
                    string[] fields = Regex.Split(line, "\\s+");
                    if (fields.Length >= 2 && fields[1] == "device") {
                        var device = new Device { Serial = fields[0] };
                        devices.Items.Add(device);
                        if (fields[0] == selected) devices.SelectedItem = device;
                    }
                }
                if (devices.SelectedIndex < 0 && devices.Items.Count > 0) devices.SelectedIndex = 0;
                SetStatus(devices.Items.Count + " 台已授权设备。若显示 unauthorized，请在手机上同意 USB 调试授权。");
            } catch (Exception e) { ShowError(e); }
        }

        private async Task Pair()
        {
            try {
                string address = ValidateAddress(pairAddress.Text);
                string code = pairCode.Text.Trim();
                if (!Regex.IsMatch(code, "^[0-9]{6}$")) throw new InvalidOperationException("请输入手机显示的 6 位无线调试配对码。");
                SetStatus("正在配对…");
                string response = await Task.Run(() => Adb("pair", address, code));
                pairCode.Clear();
                SetStatus(response.Trim() + "。接着填入无线调试页面的连接地址。 ");
            } catch (Exception e) { ShowError(e); }
        }

        private async Task Connect()
        {
            try {
                string address = ValidateAddress(connectAddress.Text);
                SetStatus("正在连接…");
                string response = await Task.Run(() => Adb("connect", address));
                SetStatus(response.Trim());
                await RefreshDevices();
            } catch (Exception e) { ShowError(e); }
        }

        private static string ValidateAddress(string value)
        {
            value = value.Trim();
            if (!Regex.IsMatch(value, @"^[A-Za-z0-9.\-\[\]:]+:[0-9]{1,5}$"))
                throw new InvalidOperationException("地址格式应为 IP:端口，例如 192.168.1.20:37123。");
            return value;
        }

        private async Task InstallAndroidApp()
        {
            try {
                string serial = SelectedSerial();
                string apk = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PhoneGuard.apk");
                if (!File.Exists(apk)) throw new FileNotFoundException("同目录缺少 PhoneGuard.apk。", apk);
                if (MessageBox.Show("要在选中的手机上安装或更新 PhoneGuard 吗？", "确认安装",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                SetStatus("正在安装手机 App…");
                string response = await Task.Run(() => Adb("-s", serial, "install", "-r", apk));
                SetStatus(response.Trim());
            } catch (Exception e) { ShowError(e); }
        }

        private async Task Scan()
        {
            try {
                string serial = SelectedSerial();
                scanButton.Enabled = false;
                grid.Rows.Clear();
                string requestId = Guid.NewGuid().ToString("N");
                SetStatus("正在手机上启动扫描…");
                await Task.Run(() => Adb("-s", serial, "shell", "am", "start", "-n",
                        AndroidPackage + "/.MainActivity", "--ez", "scan", "true", "--es", "requestId", requestId));
                Dictionary<string, object> report = null;
                for (int attempt = 0; attempt < 30; attempt++) {
                    await Task.Delay(1000);
                    try {
                        string text = await Task.Run(() => Adb("-s", serial, "exec-out", "cat", ReportPath));
                        var parsed = json.DeserializeObject(text) as Dictionary<string, object>;
                        if (parsed != null && parsed.ContainsKey("requestId") && Convert.ToString(parsed["requestId"]) == requestId) {
                            report = parsed;
                            break;
                        }
                    } catch { /* The scan may still be writing its report. */ }
                }
                if (report == null) throw new InvalidOperationException("30 秒内没有收到新报告。请查看手机上的 PhoneGuard 是否提示扫描失败。");
                var apps = report["packages"] as object[];
                if (apps == null) throw new InvalidOperationException("手机报告格式不正确。");
                foreach (object raw in apps) {
                    var app = raw as Dictionary<string, object>;
                    if (app == null) continue;
                    var reasons = app["reasons"] as object[];
                    var notes = new List<string>();
                    if (reasons != null) foreach (object reason in reasons) notes.Add(Convert.ToString(reason));
                    grid.Rows.Add(app["name"], app["packageName"], app["riskScore"],
                            app["level"], app["installer"], string.Join("；", notes.ToArray()));
                }
                SetStatus("扫描完成：" + apps.Length + " 个用户应用。线索不是病毒判定，请结合应用用途核实。 ");
            } catch (Exception e) { ShowError(e); }
            finally { scanButton.Enabled = true; }
        }

        private async Task OpenAppInfo()
        {
            try {
                string serial = SelectedSerial();
                if (grid.SelectedRows.Count == 0) throw new InvalidOperationException("请先选中一个应用。");
                string pkg = Convert.ToString(grid.SelectedRows[0].Cells[1].Value);
                if (!Regex.IsMatch(pkg, "^[A-Za-z0-9_.]+$")) throw new InvalidOperationException("报告中的包名无效。");
                await Task.Run(() => Adb("-s", serial, "shell", "am", "start", "-a",
                        "android.settings.APPLICATION_DETAILS_SETTINGS", "-d", "package:" + pkg));
                SetStatus("已在手机上打开应用信息。若要卸载，请在手机上确认。 ");
            } catch (Exception e) { ShowError(e); }
        }

        private void SetStatus(string value) { status.Text = value; }

        private void ShowError(Exception e)
        {
            SetStatus("操作失败：" + e.Message);
            MessageBox.Show(this, e.Message, "PhoneGuard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static string Adb(params string[] args)
        {
            string bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "platform-tools", "adb.exe");
            string executable = File.Exists(bundled) ? bundled : "adb.exe";
            var info = new ProcessStartInfo(executable) {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            foreach (string arg in args) info.Arguments += (info.Arguments.Length == 0 ? "" : " ") + Quote(arg);
            using (Process process = Process.Start(info)) {
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(45000)) {
                    process.Kill();
                    throw new TimeoutException("ADB 操作超时。");
                }
                if (process.ExitCode != 0) throw new InvalidOperationException((stderr + " " + stdout).Trim());
                return stdout;
            }
        }

        private static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char character in value) {
                if (character == '\\') {
                    slashes++;
                } else if (character == '"') {
                    result.Append('\\', slashes * 2 + 1);
                    result.Append('"');
                    slashes = 0;
                } else {
                    result.Append('\\', slashes);
                    result.Append(character);
                    slashes = 0;
                }
            }
            result.Append('\\', slashes * 2);
            result.Append('"');
            return result.ToString();
        }

        private sealed class Device
        {
            public string Serial;
            public override string ToString() { return Serial; }
        }
    }
}
