using System;
using System.Drawing;
using System.Windows.Forms;

namespace TraySensor
{
    public class FloatWindow : Form
    {
        private readonly Label _lblTemp;
        private readonly Label _lblFan;
        private readonly Label _lblMemory;
        private readonly Label _lblNet;
        private readonly Label _lblTempValue;
        private readonly Label _lblFanValue;
        private readonly Label _lblMemoryValue;
        private readonly Label _lblNetValue;
        private readonly Label _lblPin;
        private readonly Panel _titleBar;
        private readonly ToolTip _toolTip;

        private bool _alwaysOnTop = true;
        public bool AlwaysOnTop
        {
            get => _alwaysOnTop;
            set
            {
                _alwaysOnTop = value;
                TopMost = value;
                UpdatePinIcon();
            }
        }

        public FloatWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(32, 32, 32);
            Opacity = 0.95;
            MinimumSize = new Size(235, 142);
            Size = new Size(235, 142);
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;

            var screen = Screen.PrimaryScreen;
            if (screen != null)
            {
                var workingArea = screen.WorkingArea;
                Location = new Point(workingArea.Right - Width - 10, workingArea.Bottom - Height - 10);
            }

            _toolTip = new ToolTip
            {
                AutoPopDelay = 3000,
                InitialDelay = 200,
                ReshowDelay = 200,
                ShowAlways = true
            };

            _titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 22,
                BackColor = Color.FromArgb(45, 45, 48)
            };
            Controls.Add(_titleBar);

            var lblTitle = new Label
            {
                Text = "  硬件监控",
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(0, 3),
                BackColor = Color.Transparent
            };
            _titleBar.Controls.Add(lblTitle);

            _lblPin = new Label
            {
                Text = "📌",
                Font = new Font("Segoe UI Emoji", 10f),
                ForeColor = Color.FromArgb(100, 200, 255),
                Size = new Size(26, 18),
                Location = new Point(_titleBar.Width - 30, 2),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            _lblPin.Click += (s, e) =>
            {
                AlwaysOnTop = !AlwaysOnTop;
                string msg = AlwaysOnTop ? "已置顶（显示在所有窗口上方）" : "已取消置顶（仍高于桌面）";
                try { _toolTip.Show(msg, _lblPin, 1500); } catch { }
            };
            _titleBar.Controls.Add(_lblPin);
            _titleBar.Resize += (s, e) =>
            {
                _lblPin.Left = _titleBar.Width - _lblPin.Width - 4;
            };
            UpdatePinIcon();

            int bodyTop = _titleBar.Bottom + 4;
            int paddingLeft = 14;
            int rowHeight = 30;
            int labelLeft = paddingLeft;
            int valueLeft = paddingLeft + 82;

            _lblTemp = new Label
            {
                Text = "CPU 温度:",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(labelLeft, bodyTop + 3)
            };
            _lblTempValue = new Label
            {
                Text = "-- °C",
                ForeColor = Color.FromArgb(255, 140, 60),
                Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(valueLeft, bodyTop)
            };

            _lblFan = new Label
            {
                Text = "风扇转速:",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(labelLeft, bodyTop + rowHeight + 3)
            };
            _lblFanValue = new Label
            {
                Text = "-- RPM",
                ForeColor = Color.FromArgb(100, 200, 255),
                Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(valueLeft, bodyTop + rowHeight)
            };

            _lblMemory = new Label
            {
                Text = "内存占用:",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(labelLeft, bodyTop + rowHeight * 2 + 3)
            };
            _lblMemoryValue = new Label
            {
                Text = "-- %",
                ForeColor = Color.FromArgb(120, 230, 140),
                Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(valueLeft, bodyTop + rowHeight * 2)
            };

            _lblNet = new Label
            {
                Text = "下行 ↓ :",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(labelLeft, bodyTop + rowHeight * 3 + 3)
            };
            _lblNetValue = new Label
            {
                Text = "--",
                ForeColor = Color.FromArgb(210, 170, 255),
                Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(valueLeft, bodyTop + rowHeight * 3)
            };
            _toolTip.SetToolTip(_lblNet, "下载速度（每秒）");
            _toolTip.SetToolTip(_lblNetValue, "下载速度（每秒）");

            Controls.Add(_lblTemp);
            Controls.Add(_lblTempValue);
            Controls.Add(_lblFan);
            Controls.Add(_lblFanValue);
            Controls.Add(_lblMemory);
            Controls.Add(_lblMemoryValue);
            Controls.Add(_lblNet);
            Controls.Add(_lblNetValue);

            SetupDraggable();
        }

        private void UpdatePinIcon()
        {
            if (AlwaysOnTop)
            {
                _lblPin.Text = "📌";
                _lblPin.ForeColor = Color.FromArgb(100, 200, 255);
            }
            else
            {
                _lblPin.Text = "📌";
                _lblPin.ForeColor = Color.FromArgb(120, 120, 120);
            }
        }

        private void SetupDraggable()
        {
            bool dragging = false;
            Point dragOffset = Point.Empty;

            Action<Control> bind = (control) =>
            {
                control.MouseDown += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        dragging = true;
                        var sp = control.PointToScreen(e.Location);
                        dragOffset = new Point(sp.X - Left, sp.Y - Top);
                    }
                };
                control.MouseMove += (s, e) =>
                {
                    if (dragging)
                    {
                        var ctrl = (Control)s!;
                        var sp = ctrl.PointToScreen(e.Location);
                        Location = new Point(sp.X - dragOffset.X, sp.Y - dragOffset.Y);
                    }
                };
                control.MouseUp += (s, e) => { dragging = false; };
            };

            bind(this);
            bind(_titleBar);
            foreach (Control c in Controls)
            {
                if (c == _titleBar) continue;
                bind(c);
            }
            foreach (Control c in _titleBar.Controls)
            {
                if (c == _lblPin) continue;
                bind(c);
            }
        }

        public void UpdateData(SensorData data)
        {
            if (IsDisposed) return;

            if (InvokeRequired)
            {
                Invoke(new Action<SensorData>(UpdateData), data);
                return;
            }

            string tempText = data.CpuTemperature.HasValue
                ? $"{data.CpuTemperature.Value:0.0} °C"
                : "-- °C";
            _lblTempValue.Text = tempText;

            if (data.CpuTemperature.HasValue && data.CpuTemperature.Value >= 85)
                _lblTempValue.ForeColor = Color.FromArgb(255, 80, 80);
            else if (data.CpuTemperature.HasValue && data.CpuTemperature.Value >= 70)
                _lblTempValue.ForeColor = Color.FromArgb(255, 180, 60);
            else
                _lblTempValue.ForeColor = Color.FromArgb(255, 140, 60);

            string fanText = data.FanRpm.HasValue
                ? $"{data.FanRpm.Value} RPM"
                : "-- RPM";
            _lblFanValue.Text = fanText;

            string memText = data.MemoryUsage.HasValue
                ? $"{data.MemoryUsage.Value:0.0} %"
                : "-- %";
            _lblMemoryValue.Text = memText;

            if (data.MemoryUsage.HasValue && data.MemoryUsage.Value >= 85)
                _lblMemoryValue.ForeColor = Color.FromArgb(255, 120, 120);
            else if (data.MemoryUsage.HasValue && data.MemoryUsage.Value >= 70)
                _lblMemoryValue.ForeColor = Color.FromArgb(255, 220, 120);
            else
                _lblMemoryValue.ForeColor = Color.FromArgb(120, 230, 140);

            _lblNetValue.Text = data.FormatDownloadSpeed();
            if (data.NetworkDownloadSpeedBps.HasValue &&
                data.NetworkDownloadSpeedBps.Value / 1024.0 / 1024.0 >= 5.0)
                _lblNetValue.ForeColor = Color.FromArgb(255, 140, 220);
            else
                _lblNetValue.ForeColor = Color.FromArgb(210, 170, 255);

            string netTip;
            if (!string.IsNullOrEmpty(data.NetworkName))
            {
                string src = string.IsNullOrEmpty(data.NetworkSource) ? "" : $"[{data.NetworkSource}] ";
                netTip = src + "当前网卡: " + data.NetworkName;
            }
            else
            {
                netTip = "未检测到可用网卡数据（请确认已选 WiFi）";
            }
            try
            {
                _toolTip.SetToolTip(_lblNet, netTip);
                _toolTip.SetToolTip(_lblNetValue, netTip);
            }
            catch { }
        }

        public new void Show()
        {
            if (!IsHandleCreated || IsDisposed)
            {
                try
                {
                    CreateHandle();
                }
                catch { }
            }
            base.Show();
            if (Visible)
            {
                var screen = Screen.FromControl(this);
                var workingArea = screen.WorkingArea;
                if (Right > workingArea.Right)
                {
                    Left = workingArea.Right - Width - 10;
                }
                if (Bottom > workingArea.Bottom)
                {
                    Top = workingArea.Bottom - Height - 10;
                }
            }
        }
    }
}
