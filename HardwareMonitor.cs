using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LibreHardwareMonitor.Hardware;

namespace TraySensor
{
    public class HardwareMonitor : IDisposable
    {
        private Computer? _computer;
        private bool _disposed;
        public string? LastError { get; private set; }
        public bool IsInitialized { get; private set; }
        private readonly List<string> _fanDiag = new();
        private readonly object _lock = new();

        public HardwareMonitor()
        {
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsMemoryEnabled = true,
                    IsGpuEnabled = false,
                    IsStorageEnabled = false,
                    IsNetworkEnabled = true,
                    IsMotherboardEnabled = true,
                    IsControllerEnabled = true
                };
                _computer.Open();
                IsInitialized = true;
            }
            catch (Exception ex)
            {
                LastError = $"HardwareMonitor 初始化失败: {ex.Message}";
                IsInitialized = false;
                _computer = null;
            }
        }

        public IReadOnlyList<string> FanDiagnostics
        {
            get { lock (_lock) { return _fanDiag.AsReadOnly(); } }
        }

        public string DumpSensorsToLog(string baseDir)
        {
            try
            {
                if (!IsInitialized || _computer == null) return "";
                string path = Path.Combine(baseDir, "TraySensor_Sensors.log");
                var sb = new StringBuilder();
                sb.AppendLine("========== " + DateTime.Now + " ==========");

                var all = new List<IHardware>();
                foreach (var h in _computer.Hardware)
                {
                    all.Add(h);
                    CollectSubHardware(h, all);
                }

                int fanCount = 0;
                foreach (var hw in all)
                {
                    try { hw.Update(); } catch { }
                    sb.AppendLine($"[HW] {hw.HardwareType} | {hw.Name} | Id={hw.Identifier}");
                    foreach (var s in hw.Sensors)
                    {
                        try
                        {
                            sb.AppendLine($"   S {s.SensorType,-12} | {s.Name,-30} | Value={s.Value ?? -1:0.00}");
                            if (s.SensorType == SensorType.Fan) fanCount++;
                        }
                        catch { }
                    }
                }
                sb.AppendLine($"Total Fan sensors: {fanCount}");
                File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
                return path;
            }
            catch (Exception ex)
            {
                return "dump err: " + ex.Message;
            }
        }

        public SensorData ReadSensors()
        {
            var result = new SensorData();

            if (!IsInitialized || _computer == null)
            {
                return result;
            }

            try
            {
                var allHardware = new List<IHardware>();
                foreach (var hw in _computer.Hardware)
                {
                    allHardware.Add(hw);
                    try
                    {
                        CollectSubHardware(hw, allHardware);
                    }
                    catch
                    {
                    }
                }

                lock (_lock) { _fanDiag.Clear(); }

                double? wifiDownloadBps = null;
                string? wifiNetName = null;
                double? fallbackDownloadBps = null;
                string? fallbackNetName = null;

                foreach (var hardware in allHardware)
                {
                    try
                    {
                        hardware.Update();
                    }
                    catch
                    {
                        continue;
                    }

                    try
                    {
                        if (hardware.HardwareType == HardwareType.Cpu)
                        {
                            foreach (var sensor in hardware.Sensors)
                            {
                                try
                                {
                                    if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                                    {
                                        var name = sensor.Name ?? "";
                                        if (name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            name.IndexOf("CCD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            name.IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            name.IndexOf("Tctl", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            name.IndexOf("Tdie", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            name.IndexOf("Average", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            name.IndexOf("Max", StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            if (!result.CpuTemperature.HasValue || sensor.Value > result.CpuTemperature)
                                            {
                                                result.CpuTemperature = Math.Round(sensor.Value.Value, 1);
                                            }
                                        }
                                    }

                                    if (sensor.SensorType == SensorType.Load &&
                                        string.Equals(sensor.Name, "CPU Total", StringComparison.OrdinalIgnoreCase) &&
                                        sensor.Value.HasValue)
                                    {
                                        result.CpuLoad = Math.Round(sensor.Value.Value, 1);
                                    }
                                }
                                catch
                                {
                                }
                            }
                        }

                        if (hardware.HardwareType == HardwareType.Memory)
                        {
                            foreach (var sensor in hardware.Sensors)
                            {
                                try
                                {
                                    if (sensor.SensorType == SensorType.Load &&
                                        string.Equals(sensor.Name, "Memory", StringComparison.OrdinalIgnoreCase) &&
                                        sensor.Value.HasValue)
                                    {
                                        result.MemoryUsage = Math.Round(sensor.Value.Value, 1);
                                    }
                                }
                                catch
                                {
                                }
                            }
                        }

                        if (hardware.HardwareType == HardwareType.Network)
                        {
                            bool hardwareIsWifi = IsWifiHardware(hardware);

                            foreach (var sensor in hardware.Sensors)
                            {
                                try
                                {
                                    if (sensor.SensorType == SensorType.Throughput &&
                                        sensor.Name != null &&
                                        sensor.Name.IndexOf("Download", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                        sensor.Value.HasValue)
                                    {
                                        double bps = sensor.Value.Value;
                                        if (bps < 0) bps = 0;

                                        string hwName = hardware.Name ?? "";

                                        if (hardwareIsWifi)
                                        {
                                            if (!wifiDownloadBps.HasValue || bps > wifiDownloadBps.Value)
                                            {
                                                wifiDownloadBps = bps;
                                                wifiNetName = hwName;
                                            }
                                        }
                                        else
                                        {
                                            if (bps > 0 && (!fallbackDownloadBps.HasValue || bps > fallbackDownloadBps.Value))
                                            {
                                                fallbackDownloadBps = bps;
                                                fallbackNetName = hwName;
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        foreach (var sensor in hardware.Sensors)
                        {
                            try
                            {
                                if (sensor.SensorType == SensorType.Fan)
                                {
                                    string hwType = hardware.HardwareType.ToString();
                                    string hwName = hardware.Name ?? "";
                                    string sName = (sensor.Name ?? "").Trim();
                                    double? val = sensor.Value;
                                    lock (_lock)
                                    {
                                        _fanDiag.Add($"[{hwType}] {hwName} / {sName} = {val?.ToString("0.0") ?? "null"}");
                                    }
                                    if (!val.HasValue) continue;
                                    double rpm = val.Value;
                                    if (rpm < 0) continue;

                                    bool isCpuFan =
                                        sName.IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        sName.IndexOf("Processor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        sName.IndexOf(" pump", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        hardware.HardwareType == HardwareType.Cpu ||
                                        hardware.HardwareType == HardwareType.SuperIO ||
                                        hardware.HardwareType == HardwareType.EmbeddedController;

                                    int currentRpm = (int)Math.Round(rpm, 0);

                                    if (!result.FanRpm.HasValue)
                                    {
                                        result.FanRpm = currentRpm;
                                        result.FanName = sName;
                                    }
                                    else if (isCpuFan && currentRpm > 0)
                                    {
                                        bool prevIsCpu =
                                            (result.FanName ?? "").IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            (result.FanName ?? "").IndexOf("Processor", StringComparison.OrdinalIgnoreCase) >= 0;
                                        if (!prevIsCpu || currentRpm < result.FanRpm.Value == false)
                                        {
                                            result.FanRpm = currentRpm;
                                            result.FanName = sName;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                if (wifiDownloadBps.HasValue)
                {
                    result.NetworkDownloadSpeedBps = wifiDownloadBps.Value;
                    result.NetworkName = wifiNetName;
                    result.NetworkSource = "WiFi";
                }
                else if (fallbackDownloadBps.HasValue)
                {
                    result.NetworkDownloadSpeedBps = fallbackDownloadBps.Value;
                    result.NetworkName = fallbackNetName;
                    result.NetworkSource = "非WiFi(回退)";
                }
            }
            catch (Exception ex)
            {
                LastError = $"ReadSensors 异常: {ex.Message}";
            }

            return result;
        }

        private static bool IsWifiHardware(IHardware hardware)
        {
            string name = (hardware.Name ?? "").Trim();
            string id = hardware.Identifier?.ToString() ?? "";
            string combined = name + "|" + id;
            return
                combined.IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combined.IndexOf("WiFi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combined.IndexOf("Wireless", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combined.IndexOf("WLAN", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combined.IndexOf("802.11", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combined.IndexOf("Wi Fi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combined.IndexOf("Ax", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (combined.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     combined.IndexOf("MediaTek", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     combined.IndexOf("Realtek", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     combined.IndexOf("Qualcomm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     combined.IndexOf("Broadcom", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void CollectSubHardware(IHardware parent, List<IHardware> list)
        {
            foreach (var sub in parent.SubHardware)
            {
                list.Add(sub);
                CollectSubHardware(sub, list);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            try
            {
                _computer?.Close();
            }
            catch
            {
            }
            _computer = null;
            _disposed = true;
        }
    }

    public class SensorData
    {
        public double? CpuTemperature { get; set; }
        public double? CpuLoad { get; set; }
        public int? FanRpm { get; set; }
        public string? FanName { get; set; }
        public double? MemoryUsage { get; set; }
        public double? NetworkDownloadSpeedBps { get; set; }
        public string? NetworkName { get; set; }
        public string? NetworkSource { get; set; }

        public string FormatDownloadSpeed()
        {
            if (!NetworkDownloadSpeedBps.HasValue) return "--";
            double bps = NetworkDownloadSpeedBps.Value;
            if (bps < 0) bps = 0;
            if (bps < 1024) return $"{bps:0.0} B/s";
            double kbps = bps / 1024.0;
            if (kbps < 1024) return $"{kbps:0.0} KB/s";
            double mbps = kbps / 1024.0;
            return $"{mbps:0.00} MB/s";
        }
    }
}
