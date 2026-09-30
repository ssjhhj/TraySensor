using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TraySensor
{
    public class TrayApplicationContext : ApplicationContext
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private readonly NotifyIcon _notifyIcon;
        private readonly FloatWindow _floatWindow;
        private readonly HardwareMonitor _hardwareMonitor;
        private readonly System.Windows.Forms.Timer _refreshTimer;
        private readonly ToolStripMenuItem _alwaysOnTopItem;
        private readonly ToolStripMenuItem _showWindowItem;
        private SensorData _lastData;
        private Icon? _currentIcon;

        public TrayApplicationContext()
        {
            _lastData = new SensorData();
            _hardwareMonitor = new HardwareMonitor();
            _floatWindow = new FloatWindow();

            var menu = new ContextMenuStrip();

            _showWindowItem = new ToolStripMenuItem("显示监控窗口");
            _showWindowItem.CheckOnClick = true;
            _showWindowItem.Checked = true;
            _showWindowItem.Click += (s, e) =>
            {
                try
                {
                    if (_showWindowItem.Checked)
                    {
                        _floatWindow.UpdateData(_lastData);
                        _floatWindow.Show();
                    }
                    else
                    {
                        _floatWindow.Hide();
                    }
                }
                catch { }
            };
            menu.Items.Add(_showWindowItem);

            _alwaysOnTopItem = new ToolStripMenuItem("置顶显示")
            {
                CheckOnClick = true,
                Checked = true
            };
            _alwaysOnTopItem.Click += (s, e) =>
            {
                try
                {
                    _floatWindow.AlwaysOnTop = _alwaysOnTopItem.Checked;
                }
                catch { }
            };
            menu.Items.Add(_alwaysOnTopItem);

            menu.Items.Add(new ToolStripSeparator());

            var refreshItem = new ToolStripMenuItem("立即刷新");
            refreshItem.Click += (s, e) => RefreshData();
            menu.Items.Add(refreshItem);

            var dumpItem = new ToolStripMenuItem("导出传感器诊断日志");
            dumpItem.Click += (s, e) =>
            {
                try
                {
                    string dir = AppContext.BaseDirectory;
                    string logPath = _hardwareMonitor.DumpSensorsToLog(dir);
                    if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + logPath + "\"");
                        }
                        catch { }
                        MessageBox.Show("诊断日志已导出：\n" + logPath +
                            "\n\n如风扇数据仍为 --，请将该日志内容发送给开发者分析。",
                            "TraySensor 诊断", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("导出失败：" + logPath, "TraySensor 诊断",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("导出异常：" + ex.Message, "TraySensor 诊断",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            menu.Items.Add(dumpItem);

            string status = _hardwareMonitor.IsInitialized
                ? "成功"
                : "失败 - 请以管理员运行";
            var statusItem = new ToolStripMenuItem($"硬件初始化: {status}")
            {
                Enabled = false
            };
            menu.Items.Add(statusItem);

            menu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (s, e) => ExitApp();
            menu.Items.Add(exitItem);

            menu.Opening += (s, e) =>
            {
                try
                {
                    _showWindowItem.Checked = _floatWindow.Visible;
                    _alwaysOnTopItem.Checked = _floatWindow.AlwaysOnTop;
                }
                catch { }
            };

            _notifyIcon = new NotifyIcon
            {
                Icon = SafeCreateFallbackIcon(),
                Text = "TraySensor 硬件监控",
                ContextMenuStrip = menu,
                Visible = true
            };

            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
            _notifyIcon.MouseDoubleClick += (s, e) => ToggleFloatWindow();
            _notifyIcon.BalloonTipTitle = "TraySensor 硬件监控";
            _notifyIcon.BalloonTipText = _hardwareMonitor.IsInitialized
                ? "已启动。监控窗口默认显示在右下角，可点击📌切换置顶。"
                : "警告：未以管理员身份运行，温度/风扇数据可能无法读取。";
            _notifyIcon.BalloonTipIcon = _hardwareMonitor.IsInitialized ? ToolTipIcon.Info : ToolTipIcon.Warning;
            try { _notifyIcon.ShowBalloonTip(3500); } catch { }

            _refreshTimer = new System.Windows.Forms.Timer
            {
                Interval = 1000
            };
            _refreshTimer.Tick += (s, e) => RefreshData();
            _refreshTimer.Start();

            Application.ApplicationExit += (s, e) => Cleanup();

            RefreshData();

            try
            {
                _floatWindow.UpdateData(_lastData);
                _floatWindow.AlwaysOnTop = true;
                _floatWindow.Show();
            }
            catch { }
        }

        private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ToggleFloatWindow();
            }
        }

        private void ToggleFloatWindow()
        {
            try
            {
                if (_floatWindow.Visible)
                {
                    _floatWindow.Hide();
                    _showWindowItem.Checked = false;
                }
                else
                {
                    _floatWindow.UpdateData(_lastData);
                    _floatWindow.Show();
                    _showWindowItem.Checked = true;
                }
            }
            catch
            {
            }
        }

        private void RefreshData()
        {
            try
            {
                var data = _hardwareMonitor.ReadSensors();
                _lastData = data;

                string tempText = data.CpuTemperature.HasValue
                    ? $"{data.CpuTemperature.Value:0}°C"
                    : "--°C";
                string memText = data.MemoryUsage.HasValue
                    ? $"{data.MemoryUsage.Value:0}%"
                    : "--%";

                string tip1 = $"CPU {tempText} | 内存 {memText}";
                string tip2 = "TraySensor 硬件监控";
                string fullTip = tip1.Length + tip2.Length + 2 <= 63
                    ? $"{tip1}\n{tip2}"
                    : tip1;

                try
                {
                    _notifyIcon.Text = fullTip;
                }
                catch
                {
                    try { _notifyIcon.Text = "TraySensor"; } catch { }
                }

                var newIcon = SafeCreateDynamicIcon(data);
                if (newIcon != null)
                {
                    var old = _currentIcon;
                    _currentIcon = newIcon;
                    try { _notifyIcon.Icon = newIcon; } catch { }
                    if (old != null && old.Handle != newIcon.Handle)
                    {
                        try { DestroyIcon(old.Handle); } catch { }
                        try { old.Dispose(); } catch { }
                    }
                }

                if (_floatWindow.Visible)
                {
                    _floatWindow.UpdateData(data);
                }
            }
            catch
            {
            }
        }

        private Icon SafeCreateFallbackIcon()
        {
            try
            {
                return CreateTrayIcon();
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private Icon CreateTrayIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                var rect = new Rectangle(2, 2, 28, 28);
                using (var brush = new SolidBrush(Color.FromArgb(40, 120, 200)))
                {
                    g.FillEllipse(brush, rect);
                }

                using var textBrush = new SolidBrush(Color.White);
                using var font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString("T", font, textBrush, rect, sf);
            }

            IntPtr hIcon = bmp.GetHicon();
            try
            {
                return Icon.FromHandle(hIcon);
            }
            catch
            {
                try { DestroyIcon(hIcon); } catch { }
                throw;
            }
        }

        private Icon? SafeCreateDynamicIcon(SensorData data)
        {
            try
            {
                return CreateDynamicIcon(data);
            }
            catch
            {
                return null;
            }
        }

        private Icon CreateDynamicIcon(SensorData data)
        {
            Color circleColor;
            if (data.CpuTemperature.HasValue && data.CpuTemperature.Value >= 80)
                circleColor = Color.FromArgb(220, 60, 60);
            else if (data.CpuTemperature.HasValue && data.CpuTemperature.Value >= 65)
                circleColor = Color.FromArgb(230, 160, 50);
            else
                circleColor = Color.FromArgb(40, 170, 100);

            string dispText;
            if (data.CpuTemperature.HasValue)
                dispText = $"{data.CpuTemperature.Value:0}°";
            else if (data.MemoryUsage.HasValue)
                dispText = $"{data.MemoryUsage.Value:0}";
            else
                dispText = "T";

            if (dispText.Length > 3) dispText = dispText.Substring(0, 3);
            float fontSize = dispText.Length <= 2 ? 11f : 9f;

            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                try { g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; } catch { }
                g.Clear(Color.Transparent);

                var rect = new Rectangle(1, 1, 30, 30);
                using (var brush = new SolidBrush(circleColor))
                {
                    g.FillEllipse(brush, rect);
                }

                using var textBrush = new SolidBrush(Color.White);
                Font? font = null;
                try
                {
                    font = new Font("Microsoft YaHei UI", fontSize, FontStyle.Bold);
                }
                catch
                {
                    try { font = new Font(FontFamily.GenericSansSerif, fontSize, FontStyle.Bold); }
                    catch { font = Control.DefaultFont; }
                }

                using (font)
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString(dispText, font, textBrush, rect, sf);
                }
            }

            IntPtr hIcon = bmp.GetHicon();
            try
            {
                return Icon.FromHandle(hIcon);
            }
            catch
            {
                try { DestroyIcon(hIcon); } catch { }
                throw;
            }
        }

        private void ExitApp()
        {
            Cleanup();
            ExitThread();
        }

        private void Cleanup()
        {
            try
            {
                try { _refreshTimer?.Stop(); } catch { }
                try { _floatWindow?.Hide(); } catch { }
                try { _floatWindow?.Close(); } catch { }
                try { _floatWindow?.Dispose(); } catch { }
                try { _hardwareMonitor?.Dispose(); } catch { }

                try
                {
                    if (_currentIcon != null)
                    {
                        try { DestroyIcon(_currentIcon.Handle); } catch { }
                        try { _currentIcon.Dispose(); } catch { }
                    }
                }
                catch { }

                try { _notifyIcon?.Dispose(); } catch { }
            }
            catch
            {
            }
        }
    }
}
