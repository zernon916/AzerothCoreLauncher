using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AzerothCoreLauncher
{
    public partial class MainWindow : Window
    {
        private Process? _authProcess;
        private Process? _worldProcess;
        private DatabaseManager? _dbManager;
        private ConfigManager? _configManager;
        private AppSettings _settings;
        
        private DispatcherTimer _memoryTimer;
        private DispatcherTimer _uptimeTimer;
        private DispatcherTimer _restartTimer;
        private DispatcherTimer _healthCheckTimer;
        
        private DateTime _authStartTime;
        private DateTime _worldStartTime;
        private bool _restartScheduled;
        private System.IO.FileSystemWatcher? _authLogWatcher;
        private long _authLogLastPosition;
        private System.IO.FileSystemWatcher? _worldLogWatcher;
        private long _worldLogLastPosition;
        private bool _authWasRunning;
        private bool _worldWasRunning;
        private int _authCrashCount;
        private int _worldCrashCount;
        private System.Collections.Generic.List<HealthDataPoint> _healthDataHistory;
        private int _selectedTimeRangeMinutes;
        
        public MainWindow()
        {
            try
            {
                LogDebug("MainWindow constructor started");
                
                InitializeComponent();
                LogDebug("InitializeComponent completed");
                
                PlayerList.ItemsSource = new ObservableCollection<PlayerInfo>();
                EventList.ItemsSource = new ObservableCollection<ScheduledEvent>();
                
                _healthDataHistory = new System.Collections.Generic.List<HealthDataPoint>();
                _selectedTimeRangeMinutes = 5;
                
                LogDebug("Collections initialized");
                
                CheckAdminPrivileges();
                LogDebug("Admin privileges checked");
                
                _settings = AppSettings.Load();
                LogDebug("Settings loaded");
                
                LoadSettingsToUI();
                LogDebug("Settings loaded to UI");
                
                LoadEvents();
                LogDebug("Events loaded");
                
                InitializeManagers();
                LogDebug("Managers initialized");
                
                InitializeTimers();
                LogDebug("Timers initialized");
                
                // Select default time range after initialization is complete
                CmbTimeRange.SelectedIndex = 1; // Select "5 Min"
                LogDebug("Default time range selected");
                
                LogDebug("MainWindow constructor completed successfully");
            }
            catch (Exception ex)
            {
                LogDebug($"ERROR in MainWindow constructor: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Error initializing launcher: {ex.Message}\n\nCheck debug log for details.", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void InitializeTimers()
        {
            _memoryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _memoryTimer.Tick += UpdateMemoryUsage;
            _memoryTimer.Start();
            
            _uptimeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _uptimeTimer.Tick += UpdateUptime;
            _uptimeTimer.Start();
            
            _restartTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _restartTimer.Tick += RestartTimer_Tick;
            
            _healthCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(_settings.HealthCheckIntervalSeconds)
            };
            _healthCheckTimer.Tick += HealthCheck_Tick;
            if (_settings.EnableHealthMonitoring)
                _healthCheckTimer.Start();
        }
        
        private void CheckAdminPrivileges()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                MessageBox.Show(
                    "This launcher requires administrator privileges to start the AzerothCore server processes.\n\nPlease right-click the launcher and select 'Run as administrator'.",
                    "Administrator Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }
        
        private void UpdateStatusLight(Ellipse light, string status)
        {
            Dispatcher.Invoke(() =>
            {
                light.Fill = status switch
                {
                    "running" => new SolidColorBrush(Colors.LimeGreen),
                    "starting" => new SolidColorBrush(Colors.Yellow),
                    "stopped" => new SolidColorBrush(Colors.Red),
                    "error" => new SolidColorBrush(Colors.Orange),
                    _ => new SolidColorBrush(Colors.Red)
                };
            });
        }
        
        private void UpdateMemoryUsage(object? sender, EventArgs e)
        {
            if (_authProcess != null && !_authProcess.HasExited)
            {
                try
                {
                    _authProcess.Refresh();
                    var memoryMB = _authProcess.WorkingSet64 / 1024 / 1024;
                    Dispatcher.Invoke(() =>
                    {
                        AuthMemory.Text = $"{memoryMB} MB";
                        AuthMemoryLarge.Text = $"Memory: {memoryMB} MB";
                    });
                }
                catch { }
            }
            
            if (_worldProcess != null && !_worldProcess.HasExited)
            {
                try
                {
                    _worldProcess.Refresh();
                    var memoryMB = _worldProcess.WorkingSet64 / 1024 / 1024;
                    Dispatcher.Invoke(() =>
                    {
                        WorldMemory.Text = $"{memoryMB} MB";
                        WorldMemoryLarge.Text = $"Memory: {memoryMB} MB";
                    });
                }
                catch { }
            }
        }
        
        private void UpdateUptime(object? sender, EventArgs e)
        {
            if (_authProcess != null && !_authProcess.HasExited)
            {
                var uptime = DateTime.Now - _authStartTime;
                Dispatcher.Invoke(() =>
                {
                    AuthUptime.Text = $"Uptime: {uptime:hh\\:mm\\:ss}";
                });
            }
            
            if (_worldProcess != null && !_worldProcess.HasExited)
            {
                var uptime = DateTime.Now - _worldStartTime;
                Dispatcher.Invoke(() =>
                {
                    WorldUptime.Text = $"Uptime: {uptime:hh\\:mm\\:ss}";
                });
            }
        }
        
        private void RestartTimer_Tick(object? sender, EventArgs e)
        {
            var currentTime = DateTime.Now.TimeOfDay;
            var observable = (ObservableCollection<ScheduledEvent>)EventList.ItemsSource;
            
            foreach (var evt in observable)
            {
                if (!evt.IsEnabled)
                    continue;
                
                // Check if current time matches scheduled time (within 1 second)
                if (Math.Abs((currentTime - evt.ScheduledTime).TotalSeconds) < 1)
                {
                    // Check if already executed today (for recurring events)
                    if (evt.IsRecurring && evt.LastExecuted.HasValue && evt.LastExecuted.Value.Date == DateTime.Now.Date)
                        continue;
                    
                    ExecuteEvent(evt);
                }
            }
        }
        
        private void ExecuteEvent(ScheduledEvent evt)
        {
            Dispatcher.Invoke(() =>
            {
                AppendToConsole($"Executing event: {evt.Name}", "SYSTEM");
                
                switch (evt.Type)
                {
                    case "Restart":
                        if (evt.Target == "World")
                        {
                            ExecuteScheduledRestart();
                        }
                        else if (evt.Target == "Auth")
                        {
                            BtnStopAuth_Click(null, new RoutedEventArgs());
                            System.Threading.Thread.Sleep(2000);
                            BtnStartAuth_Click(null, new RoutedEventArgs());
                        }
                        break;
                        
                    case "Command":
                        var targetProcess = evt.Target switch
                        {
                            "Auth" => _authProcess,
                            "World" => _worldProcess,
                            _ => _worldProcess
                        };
                        
                        if (targetProcess != null && !targetProcess.HasExited)
                        {
                            targetProcess.StandardInput.WriteLine(evt.Command);
                            AppendToConsole($"> {evt.Command}", evt.Target.ToUpper());
                        }
                        else
                        {
                            AppendToConsole($"{evt.Target}Server is not running", "SYSTEM", true);
                        }
                        break;
                        
                    case "Announcement":
                        // Send announcement command to world server
                        if (_worldProcess != null && !_worldProcess.HasExited)
                        {
                            _worldProcess.StandardInput.WriteLine($"announce {evt.Command}");
                            AppendToConsole($"> announce {evt.Command}", "WORLD");
                        }
                        break;
                }
                
                // Update last executed time
                evt.LastExecuted = DateTime.Now;
                if (evt.IsRecurring)
                {
                    // Calculate next execution for recurring events
                    evt.NextExecution = DateTime.Today.AddDays(1).Add(evt.ScheduledTime);
                }
            });
        }
        
        private void HealthCheck_Tick(object? sender, EventArgs e)
        {
            if (!_settings.EnableHealthMonitoring)
                return;
            
            try
            {
                double authMemoryMB = 0;
                double authCpuPercent = 0;
                double worldMemoryMB = 0;
                double worldCpuPercent = 0;
                
                // Check AuthServer memory usage
                if (_authProcess != null && !_authProcess.HasExited)
                {
                    _authProcess.Refresh();
                    authMemoryMB = _authProcess.WorkingSet64 / (1024 * 1024);
                    
                    try
                    {
                        authCpuPercent = _authProcess.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount;
                    }
                    catch
                    {
                        authCpuPercent = 0;
                    }
                    
                    if (authMemoryMB > _settings.MemoryAlertThresholdMB)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            AppendToConsole($"[ALERT] AuthServer memory usage high: {authMemoryMB} MB (threshold: {_settings.MemoryAlertThresholdMB} MB)", "SYSTEM", true);
                            UpdateStatusLight(AuthStatusLight, "error");
                            UpdateStatusLight(AuthStatusLightLarge, "error");
                        });
                    }
                }
                
                // Check WorldServer memory usage
                if (_worldProcess != null && !_worldProcess.HasExited)
                {
                    _worldProcess.Refresh();
                    worldMemoryMB = _worldProcess.WorkingSet64 / (1024 * 1024);
                    
                    try
                    {
                        worldCpuPercent = _worldProcess.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount;
                    }
                    catch
                    {
                        worldCpuPercent = 0;
                    }
                    
                    if (worldMemoryMB > _settings.MemoryAlertThresholdMB)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            AppendToConsole($"[ALERT] WorldServer memory usage high: {worldMemoryMB} MB (threshold: {_settings.MemoryAlertThresholdMB} MB)", "SYSTEM", true);
                            UpdateStatusLight(WorldStatusLight, "error");
                            UpdateStatusLight(WorldStatusLightLarge, "error");
                        });
                    }
                }
                
                // Collect health data point
                var dataPoint = new HealthDataPoint
                {
                    Timestamp = DateTime.Now,
                    AuthMemoryMB = authMemoryMB,
                    AuthCpuPercent = authCpuPercent,
                    WorldMemoryMB = worldMemoryMB,
                    WorldCpuPercent = worldCpuPercent
                };
                
                _healthDataHistory.Add(dataPoint);
                
                // Keep only last 24 hours of data
                var cutoffTime = DateTime.Now.AddHours(-24);
                _healthDataHistory.RemoveAll(dp => dp.Timestamp < cutoffTime);
                
                // Update UI and graphs
                Dispatcher.Invoke(() =>
                {
                    AuthCurrentMemory.Text = $"{authMemoryMB:F0} MB";
                    AuthCurrentCpu.Text = $"{authCpuPercent:F1}%";
                    WorldCurrentMemory.Text = $"{worldMemoryMB:F0} MB";
                    WorldCurrentCpu.Text = $"{worldCpuPercent:F1}%";
                    
                    UpdateHealthGraphs();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => AppendToConsole($"Health check error: {ex.Message}", "SYSTEM", true));
            }
        }
        
        private void ExecuteScheduledRestart()
        {
            Dispatcher.Invoke(() =>
            {
                PerformBackupBeforeRestart();
                BtnStopWorld_Click(null, new RoutedEventArgs());
                System.Threading.Thread.Sleep(2000);
                BtnStartWorld_Click(null, new RoutedEventArgs());
                
                _restartScheduled = false;
                BtnCancelRestart.IsEnabled = false;
                BtnScheduleRestart.IsEnabled = true;
            });
        }
        
        private void InitializeManagers()
        {
            _dbManager = new DatabaseManager(
                _settings.MySqlHost,
                _settings.MySqlPort,
                _settings.CharacterDatabase,
                _settings.MySqlUser,
                _settings.MySqlPassword
            );
            
            _configManager = new ConfigManager(_settings.GetWorldServerConfigPath());
        }
        
        private void LoadSettingsToUI()
        {
            TxtServerDirectory.Text = _settings.ServerDirectory;
            TxtMySqlHost.Text = _settings.MySqlHost;
            TxtMySqlPort.Text = _settings.MySqlPort;
            TxtCharacterDatabase.Text = _settings.CharacterDatabase;
            TxtMySqlUser.Text = _settings.MySqlUser;
            TxtMySqlPassword.Password = _settings.MySqlPassword;
            TxtConfigDirectory.Text = _settings.ConfigDirectory;
            TxtConsoleLineLimit.Text = _settings.ConsoleLineLimit.ToString();
            
            // Load stability settings
            ChkAutoRestartOnCrash.IsChecked = _settings.AutoRestartOnCrash;
            TxtMaxAutoRestarts.Text = _settings.MaxAutoRestarts.ToString();
            TxtAutoRestartDelay.Text = _settings.AutoRestartDelaySeconds.ToString();
            ChkEnableCrashLogAnalysis.IsChecked = _settings.EnableCrashLogAnalysis;
            
            // Load health monitoring settings
            ChkEnableHealthMonitoring.IsChecked = _settings.EnableHealthMonitoring;
            TxtMemoryAlertThreshold.Text = _settings.MemoryAlertThresholdMB.ToString();
            TxtHealthCheckInterval.Text = _settings.HealthCheckIntervalSeconds.ToString();
            
            // Load database backup settings
            ChkBackupBeforeRestart.IsChecked = _settings.BackupDatabaseBeforeRestart;
            TxtBackupDirectory.Text = _settings.BackupDirectory;
        }
        
        private void SaveSettingsFromUI()
        {
            _settings.ServerDirectory = TxtServerDirectory.Text;
            _settings.ConfigDirectory = TxtConfigDirectory.Text;
            _settings.MySqlHost = TxtMySqlHost.Text;
            _settings.MySqlPort = TxtMySqlPort.Text;
            _settings.CharacterDatabase = TxtCharacterDatabase.Text;
            _settings.MySqlUser = TxtMySqlUser.Text;
            _settings.MySqlPassword = TxtMySqlPassword.Password;
            
            if (int.TryParse(TxtConsoleLineLimit.Text, out int lineLimit))
                _settings.ConsoleLineLimit = lineLimit;
            
            // Save stability settings
            _settings.AutoRestartOnCrash = ChkAutoRestartOnCrash.IsChecked ?? false;
            if (int.TryParse(TxtMaxAutoRestarts.Text, out int maxRestarts))
                _settings.MaxAutoRestarts = maxRestarts;
            if (int.TryParse(TxtAutoRestartDelay.Text, out int restartDelay))
                _settings.AutoRestartDelaySeconds = restartDelay;
            _settings.EnableCrashLogAnalysis = ChkEnableCrashLogAnalysis.IsChecked ?? false;
            
            // Save health monitoring settings
            _settings.EnableHealthMonitoring = ChkEnableHealthMonitoring.IsChecked ?? false;
            if (int.TryParse(TxtMemoryAlertThreshold.Text, out int memoryThreshold))
                _settings.MemoryAlertThresholdMB = memoryThreshold;
            if (int.TryParse(TxtHealthCheckInterval.Text, out int healthInterval))
                _settings.HealthCheckIntervalSeconds = healthInterval;
            
            // Save database backup settings
            _settings.BackupDatabaseBeforeRestart = ChkBackupBeforeRestart.IsChecked ?? false;
            _settings.BackupDirectory = TxtBackupDirectory.Text;
        }
        
        private void BtnStartAuth_Click(object sender, RoutedEventArgs e)
        {
            // Update status light immediately to yellow
            UpdateStatusLight(AuthStatusLight, "starting");
            UpdateStatusLight(AuthStatusLightLarge, "starting");
            AuthStatusText.Text = "Status: Starting...";
            
            try
            {
                _authProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _settings.GetAuthServerPath(),
                        WorkingDirectory = _settings.ServerDirectory,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                
                _authProcess.EnableRaisingEvents = true;
                _authProcess.Exited += (s, args) => OnAuthProcessExited();
                
                _authProcess.Start();
                
                _authStartTime = DateTime.Now;
                _authWasRunning = true;
                _authCrashCount = 0; // Reset crash count on successful start
                
                BtnStartAuth.IsEnabled = false;
                BtnStopAuth.IsEnabled = true;
                
                UpdateStatusLight(AuthStatusLight, "running");
                UpdateStatusLight(AuthStatusLightLarge, "running");
                AuthStatusText.Text = "Status: Running";
                
                // Start watching the log file
                StartAuthLogWatcher();
                
                AppendToConsole("AuthServer started", "SYSTEM");
            }
            catch (Exception ex)
            {
                UpdateStatusLight(AuthStatusLight, "stopped");
                UpdateStatusLight(AuthStatusLightLarge, "stopped");
                AuthStatusText.Text = "Status: Stopped";
                AppendToConsole($"ERROR: Failed to start AuthServer: {ex.Message}", "SYSTEM", true);
                MessageBox.Show($"Failed to start AuthServer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void StartAuthLogWatcher()
        {
            try
            {
                var logPath = System.IO.Path.Combine(_settings.ServerDirectory, "logs", "Auth.log");
                
                if (System.IO.File.Exists(logPath))
                {
                    // Read existing content
                    var existingLines = System.IO.File.ReadAllLines(logPath);
                    foreach (var line in existingLines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            AppendToConsole(line, "AUTH");
                    }
                    _authLogLastPosition = new System.IO.FileInfo(logPath).Length;
                }
                
                // Start watching for changes
                _authLogWatcher = new System.IO.FileSystemWatcher
                {
                    Path = System.IO.Path.GetDirectoryName(logPath) ?? _settings.ServerDirectory,
                    Filter = System.IO.Path.GetFileName(logPath),
                    NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.Size
                };
                
                _authLogWatcher.Changed += OnAuthLogChanged;
                _authLogWatcher.EnableRaisingEvents = true;
                
                AppendToConsole($"Started watching log file: {logPath}", "SYSTEM");
            }
            catch (Exception ex)
            {
                AppendToConsole($"Failed to start log watcher: {ex.Message}", "SYSTEM", true);
            }
        }
        
        private void OnAuthLogChanged(object sender, System.IO.FileSystemEventArgs e)
        {
            try
            {
                if (_authLogWatcher == null || !_authLogWatcher.EnableRaisingEvents)
                    return;
                    
                var logPath = e.FullPath;
                using (var stream = new System.IO.FileStream(logPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                {
                    stream.Seek(_authLogLastPosition, System.IO.SeekOrigin.Begin);
                    using (var reader = new System.IO.StreamReader(stream))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                Dispatcher.Invoke(() => AppendToConsole(line, "AUTH"));
                            }
                        }
                    }
                    _authLogLastPosition = stream.Length;
                }
            }
            catch (System.IO.IOException)
            {
                // File closed or in use by another process - ignore, will retry on next change
            }
            catch (System.UnauthorizedAccessException)
            {
                // Access denied - ignore
            }
            catch (System.ObjectDisposedException)
            {
                // Object disposed - ignore
            }
            catch (Exception ex)
            {
                // Only log non-file access errors
                if (!ex.Message.Contains("Cannot access a closed file") && 
                    !ex.Message.Contains("The process cannot access the file"))
                {
                    Dispatcher.Invoke(() => AppendToConsole($"Error reading log: {ex.Message}", "SYSTEM", true));
                }
            }
        }
        
        private void OnWorldLogChanged(object sender, System.IO.FileSystemEventArgs e)
        {
            try
            {
                if (_worldLogWatcher == null || !_worldLogWatcher.EnableRaisingEvents)
                    return;
                    
                var logPath = e.FullPath;
                using (var stream = new System.IO.FileStream(logPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                {
                    stream.Seek(_worldLogLastPosition, System.IO.SeekOrigin.Begin);
                    using (var reader = new System.IO.StreamReader(stream))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    AppendToConsole(line, "WORLD");
                                    
                                    // Check for boot status
                                    if (line.Contains("Loading Modules Configuration..."))
                                    {
                                        UpdateStatusLight(WorldStatusLight, "starting");
                                        UpdateStatusLight(WorldStatusLightLarge, "starting");
                                        WorldStatusText.Text = "Status: Booting...";
                                    }
                                    else if (line.Contains("WORLD: World Initialized In"))
                                    {
                                        UpdateStatusLight(WorldStatusLight, "running");
                                        UpdateStatusLight(WorldStatusLightLarge, "running");
                                        WorldStatusText.Text = "Status: Running";
                                    }
                                });
                            }
                        }
                    }
                    _worldLogLastPosition = stream.Length;
                }
            }
            catch (System.IO.IOException)
            {
                // File closed or in use by another process - ignore, will retry on next change
            }
            catch (System.UnauthorizedAccessException)
            {
                // Access denied - ignore
            }
            catch (System.ObjectDisposedException)
            {
                // Object disposed - ignore
            }
            catch (Exception ex)
            {
                // Only log non-file access errors
                if (!ex.Message.Contains("Cannot access a closed file") && 
                    !ex.Message.Contains("The process cannot access the file"))
                {
                    Dispatcher.Invoke(() => AppendToConsole($"Error reading log: {ex.Message}", "SYSTEM", true));
                }
            }
        }
        
        private void StopAuthLogWatcher()
        {
            if (_authLogWatcher != null)
            {
                _authLogWatcher.EnableRaisingEvents = false;
                _authLogWatcher.Dispose();
                _authLogWatcher = null;
            }
        }
        
        private void StartWorldLogWatcher()
        {
            try
            {
                var logPath = System.IO.Path.Combine(_settings.ServerDirectory, "logs", "World.log");
                
                if (System.IO.File.Exists(logPath))
                {
                    // Read existing content
                    var existingLines = System.IO.File.ReadAllLines(logPath);
                    foreach (var line in existingLines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            AppendToConsole(line, "WORLD");
                    }
                    _worldLogLastPosition = new System.IO.FileInfo(logPath).Length;
                }
                
                // Start watching for changes
                _worldLogWatcher = new System.IO.FileSystemWatcher
                {
                    Path = System.IO.Path.GetDirectoryName(logPath) ?? _settings.ServerDirectory,
                    Filter = System.IO.Path.GetFileName(logPath),
                    NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.Size
                };
                
                _worldLogWatcher.Changed += OnWorldLogChanged;
                _worldLogWatcher.EnableRaisingEvents = true;
                
                AppendToConsole($"Started watching log file: {logPath}", "SYSTEM");
            }
            catch (Exception ex)
            {
                AppendToConsole($"Failed to start log watcher: {ex.Message}", "SYSTEM", true);
            }
        }
        
        private void StopWorldLogWatcher()
        {
            if (_worldLogWatcher != null)
            {
                _worldLogWatcher.EnableRaisingEvents = false;
                _worldLogWatcher.Dispose();
                _worldLogWatcher = null;
            }
        }
        
        private void OnAuthProcessExited()
        {
            Dispatcher.Invoke(() =>
            {
                StopAuthLogWatcher();
                
                AuthConsole.Document.Blocks.Clear();
                
                bool wasRunning = _authWasRunning;
                _authWasRunning = false;
                
                BtnStartAuth.IsEnabled = true;
                BtnStopAuth.IsEnabled = false;
                
                UpdateStatusLight(AuthStatusLight, "stopped");
                UpdateStatusLight(AuthStatusLightLarge, "stopped");
                AuthStatusText.Text = "Status: Stopped";
                AuthMemory.Text = "0 MB";
                AuthMemoryLarge.Text = "Memory: 0 MB";
                AuthUptime.Text = "Uptime: 00:00:00";
                
                // Check if this was a crash (unexpected exit while running)
                if (wasRunning && _settings.AutoRestartOnCrash && _authCrashCount < _settings.MaxAutoRestarts)
                {
                    _authCrashCount++;
                    AppendToConsole($"AuthServer crashed (crash #{_authCrashCount}/{_settings.MaxAutoRestarts}). Auto-restarting in {_settings.AutoRestartDelaySeconds} seconds...", "SYSTEM", true);
                    
                    // Schedule auto-restart
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_settings.AutoRestartDelaySeconds) };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        AppendToConsole("Auto-restarting AuthServer...", "SYSTEM");
                        BtnStartAuth_Click(null, new RoutedEventArgs());
                    };
                    timer.Start();
                    
                    if (_settings.EnableCrashLogAnalysis)
                    {
                        AnalyzeCrashLog("Auth");
                    }
                }
                else if (wasRunning)
                {
                    AppendToConsole($"AuthServer exited (max restart limit reached or auto-restart disabled)", "SYSTEM", true);
                }
                else
                {
                    AppendToConsole("AuthServer exited", "SYSTEM");
                }
            });
        }
        
        private void BtnStopAuth_Click(object sender, RoutedEventArgs e)
        {
            if (_authProcess != null && !_authProcess.HasExited)
            {
                _authWasRunning = false; // Mark as intentionally stopped
                _authCrashCount = 0; // Reset crash count on manual stop
                
                _authProcess.Kill();
                _authProcess.WaitForExit(5000);
                
                AuthConsole.Document.Blocks.Clear();
                
                BtnStartAuth.IsEnabled = true;
                BtnStopAuth.IsEnabled = false;
                
                UpdateStatusLight(AuthStatusLight, "stopped");
                UpdateStatusLight(AuthStatusLightLarge, "stopped");
                AuthStatusText.Text = "Status: Stopped";
                AuthMemory.Text = "0 MB";
                AuthMemoryLarge.Text = "Memory: 0 MB";
                AuthUptime.Text = "Uptime: 00:00:00";
                
                AppendToConsole("AuthServer stopped", "SYSTEM");
            }
        }
        
        private void BtnStartWorld_Click(object sender, RoutedEventArgs e)
        {
            // Update status light immediately to yellow
            UpdateStatusLight(WorldStatusLight, "starting");
            UpdateStatusLight(WorldStatusLightLarge, "starting");
            WorldStatusText.Text = "Status: Starting...";
            
            try
            {
                _worldProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _settings.GetWorldServerPath(),
                        WorkingDirectory = _settings.ServerDirectory,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                
                _worldProcess.EnableRaisingEvents = true;
                _worldProcess.Exited += (s, args) => OnWorldProcessExited();
                
                _worldProcess.Start();
                
                _worldStartTime = DateTime.Now;
                _worldWasRunning = true;
                _worldCrashCount = 0; // Reset crash count on successful start
                
                BtnStartWorld.IsEnabled = false;
                BtnStopWorld.IsEnabled = true;
                
                // Attach output streams
                _worldProcess.OutputDataReceived += (s, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data) && args.Data.Trim().Length > 0)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            AppendToConsole(args.Data, "WORLD");
                            
                            // Detect server shutdown command (intended shutdown, not a crash)
                            if (args.Data.Contains("server shutdown"))
                            {
                                _worldWasRunning = false; // Mark as intentionally stopped
                                AppendToConsole("Server shutdown command detected, disabling auto-restart", "SYSTEM");
                            }
                            
                            // Boot status detection - only change to green when fully initialized
                            if (args.Data.Contains("WORLD: World Initialized In"))
                            {
                                UpdateStatusLight(WorldStatusLight, "running");
                                UpdateStatusLight(WorldStatusLightLarge, "running");
                                WorldStatusText.Text = "Status: Running";
                            }
                        });
                    }
                };
                _worldProcess.ErrorDataReceived += (s, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data) && args.Data.Trim().Length > 0)
                        Dispatcher.Invoke(() => AppendToConsole(args.Data, "WORLD", true));
                };
                _worldProcess.BeginOutputReadLine();
                _worldProcess.BeginErrorReadLine();
                
                AppendToConsole("WorldServer started", "SYSTEM");
            }
            catch (Exception ex)
            {
                UpdateStatusLight(WorldStatusLight, "stopped");
                UpdateStatusLight(WorldStatusLightLarge, "stopped");
                WorldStatusText.Text = "Status: Stopped";
                MessageBox.Show($"Failed to start WorldServer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void OnWorldProcessExited()
        {
            Dispatcher.Invoke(() =>
            {
                WorldConsole.Document.Blocks.Clear();
                
                bool wasRunning = _worldWasRunning;
                _worldWasRunning = false;
                
                BtnStartWorld.IsEnabled = true;
                BtnStopWorld.IsEnabled = false;
                
                // Red light only when process doesn't exist
                UpdateStatusLight(WorldStatusLight, "stopped");
                UpdateStatusLight(WorldStatusLightLarge, "stopped");
                WorldStatusText.Text = "Status: Stopped";
                
                WorldMemory.Text = "0 MB";
                WorldMemoryLarge.Text = "Memory: 0 MB";
                WorldUptime.Text = "Uptime: 00:00:00";
                
                // Check if this was a crash (unexpected exit while running)
                if (wasRunning && _settings.AutoRestartOnCrash && _worldCrashCount < _settings.MaxAutoRestarts)
                {
                    _worldCrashCount++;
                    AppendToConsole($"WorldServer crashed (crash #{_worldCrashCount}/{_settings.MaxAutoRestarts}). Auto-restarting in {_settings.AutoRestartDelaySeconds} seconds...", "SYSTEM", true);
                    
                    // Schedule auto-restart
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_settings.AutoRestartDelaySeconds) };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        AppendToConsole("Auto-restarting WorldServer...", "SYSTEM");
                        BtnStartWorld_Click(null, new RoutedEventArgs());
                    };
                    timer.Start();
                    
                    if (_settings.EnableCrashLogAnalysis)
                    {
                        AnalyzeCrashLog("World");
                    }
                }
                else if (wasRunning)
                {
                    AppendToConsole($"WorldServer exited (max restart limit reached or auto-restart disabled)", "SYSTEM", true);
                }
                else
                {
                    AppendToConsole("WorldServer exited", "SYSTEM");
                }
            });
        }
        
        private void BtnStopWorld_Click(object sender, RoutedEventArgs e)
        {
            if (_worldProcess != null && !_worldProcess.HasExited)
            {
                _worldWasRunning = false; // Mark as intentionally stopped
                _worldCrashCount = 0; // Reset crash count on manual stop
                
                // Send graceful shutdown command
                _worldProcess.StandardInput.WriteLine("server shutdown 1");
                AppendToConsole("Sending shutdown command to WorldServer...", "SYSTEM");
                
                // Disable stop button while shutting down
                BtnStopWorld.IsEnabled = false;
                
                // Wait for process to exit gracefully (up to 30 seconds)
                var shutdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                int shutdownWaitTime = 0;
                shutdownTimer.Tick += (s, args) =>
                {
                    shutdownWaitTime++;
                    if (_worldProcess == null || _worldProcess.HasExited || shutdownWaitTime >= 30)
                    {
                        shutdownTimer.Stop();
                        
                        Dispatcher.Invoke(() =>
                        {
                            WorldConsole.Document.Blocks.Clear();
                            
                            BtnStartWorld.IsEnabled = true;
                            BtnStopWorld.IsEnabled = false;
                            
                            UpdateStatusLight(WorldStatusLight, "stopped");
                            UpdateStatusLight(WorldStatusLightLarge, "stopped");
                            WorldStatusText.Text = "Status: Stopped";
                            WorldMemory.Text = "0 MB";
                            WorldMemoryLarge.Text = "Memory: 0 MB";
                            WorldUptime.Text = "Uptime: 00:00:00";
                            
                            AppendToConsole("WorldServer stopped", "SYSTEM");
                            
                            // Force kill if still running after timeout
                            if (_worldProcess != null && !_worldProcess.HasExited)
                            {
                                _worldProcess.Kill();
                                AppendToConsole("WorldServer did not shut down gracefully, forced termination", "SYSTEM", true);
                            }
                        });
                    }
                };
                shutdownTimer.Start();
            }
        }
        
        private void AppendToConsole(string? text, string source, bool isError = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            
            Dispatcher.Invoke(() =>
            {
                var targetConsole = source switch
                {
                    "AUTH" => AuthConsole,
                    "WORLD" => WorldConsole,
                    _ => WorldConsole
                };
                
                if (targetConsole == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Target console is null for source: {source}");
                    return;
                }
                
                var color = source switch
                {
                    "AUTH" => new SolidColorBrush(Colors.Cyan),
                    "WORLD" => new SolidColorBrush(Colors.Lime),
                    "SYSTEM" => new SolidColorBrush(Colors.Yellow),
                    _ => new SolidColorBrush(Colors.White)
                };
                
                if (isError)
                    color = new SolidColorBrush(Colors.Red);
                
                var paragraph = new System.Windows.Documents.Paragraph();
                paragraph.Inlines.Add(new System.Windows.Documents.Run(text) { Foreground = color });
                
                targetConsole.Document.Blocks.Add(paragraph);
                
                // Limit console lines to prevent lag
                while (targetConsole.Document.Blocks.Count > _settings.ConsoleLineLimit)
                {
                    targetConsole.Document.Blocks.Remove(targetConsole.Document.Blocks.FirstBlock);
                }
                
                targetConsole.ScrollToEnd();
                
                // Check for errors and update status light to yellow
                CheckForErrors(text, source);
            });
        }
        
        private void CheckForErrors(string text, string source)
        {
            // Error patterns to detect
            var errorPatterns = new[] { "failed", "error", "Failed", "Error", "Failed to", "cannot", "Cannot" };
            
            if (errorPatterns.Any(pattern => text.Contains(pattern)))
            {
                if (source == "AUTH")
                {
                    UpdateStatusLight(AuthStatusLight, "error");
                    UpdateStatusLight(AuthStatusLightLarge, "error");
                    AuthStatusText.Text = "Status: Error";
                }
                else if (source == "WORLD")
                {
                    UpdateStatusLight(WorldStatusLight, "error");
                    UpdateStatusLight(WorldStatusLightLarge, "error");
                    WorldStatusText.Text = "Status: Error";
                }
            }
        }
        
        private void CommandInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnSendCommand_Click(sender, new RoutedEventArgs());
            }
        }
        
        private void BtnSendCommand_Click(object sender, RoutedEventArgs e)
        {
            var target = ((ComboBoxItem)CommandTarget.SelectedItem)?.Content?.ToString() ?? "World";
            var targetProcess = target switch
            {
                "Auth" => _authProcess,
                "World" => _worldProcess,
                _ => _worldProcess
            };
            
            if (targetProcess != null && !targetProcess.HasExited)
            {
                targetProcess.StandardInput.WriteLine(CommandInput.Text);
                AppendToConsole($"> {CommandInput.Text}", target.ToUpper());
                CommandInput.Clear();
            }
            else
            {
                MessageBox.Show($"{target}Server is not running", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void BtnClearConsole_Click(object sender, RoutedEventArgs e)
        {
            AuthConsole.Document.Blocks.Clear();
            WorldConsole.Document.Blocks.Clear();
        }
        
        private void BtnSearchPlayer_Click(object sender, RoutedEventArgs e)
        {
            if (_dbManager == null) return;
            
            try
            {
                var searchTerm = PlayerSearch.Text;
                var players = string.IsNullOrWhiteSpace(searchTerm) 
                    ? _dbManager.GetOnlinePlayers() 
                    : _dbManager.SearchPlayers(searchTerm);
                
                var observable = (ObservableCollection<PlayerInfo>)PlayerList.ItemsSource;
                observable.Clear();
                
                foreach (var player in players)
                {
                    observable.Add(player);
                }
                
                AppendToConsole($"Found {players.Count} players", "PLAYERS");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to search players: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnRefreshPlayers_Click(object sender, RoutedEventArgs e)
        {
            if (_dbManager == null) return;
            
            try
            {
                var players = _dbManager.GetOnlinePlayers();
                
                var observable = (ObservableCollection<PlayerInfo>)PlayerList.ItemsSource;
                observable.Clear();
                
                foreach (var player in players)
                {
                    observable.Add(player);
                }
                
                AppendToConsole($"Refreshed {players.Count} online players", "PLAYERS");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh players: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnExecuteGM_Click(object sender, RoutedEventArgs e)
        {
            if (PlayerList.SelectedItem is not PlayerInfo selectedPlayer) return;
            
            var action = CmbGMAction.Text;
            if (string.IsNullOrWhiteSpace(action)) return;
            
            try
            {
                string command = action switch
                {
                    "Kick" => $".kick {selectedPlayer.Name}",
                    "Ban" => $".ban account {selectedPlayer.Name}",
                    "Mute" => $".mute {selectedPlayer.Name}",
                    "GM On" => $".gm on {selectedPlayer.Name}",
                    "GM Off" => $".gm off {selectedPlayer.Name}",
                    _ => ""
                };
                
                if (_worldProcess != null && !_worldProcess.HasExited)
                {
                    _worldProcess.StandardInput.WriteLine(command);
                    AppendToConsole($"GM command sent: {command}", "GM");
                }
                else
                {
                    MessageBox.Show("WorldServer is not running", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to execute GM action: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnLoadConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_configManager == null) return;
            
            try
            {
                var configFile = CmbConfigFile.Text;
                var configPath = configFile switch
                {
                    "worldserver.conf" => _settings.GetWorldServerConfigPath(),
                    "authserver.conf" => _settings.GetAuthServerConfigPath(),
                    _ => ""
                };
                
                if (string.IsNullOrEmpty(configPath)) return;
                
                _configManager = new ConfigManager(configPath);
                _configManager.LoadConfig();
                
                ConfigEditor.Document.Blocks.Clear();
                ConfigEditor.AppendText(_configManager.GetFullConfigText());
                
                AppendToConsole($"Loaded config: {configFile}", "CONFIG");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load config: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_configManager == null) return;
            
            try
            {
                var text = new TextRange(ConfigEditor.Document.ContentStart, ConfigEditor.Document.ContentEnd).Text;
                _configManager.SetFullConfigText(text);
                _configManager.SaveConfig();
                
                AppendToConsole("Config saved successfully", "CONFIG");
                MessageBox.Show("Config saved successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save config: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnApplyConfig_Click(object sender, RoutedEventArgs e)
        {
            // Save first, then restart server to apply changes
            BtnSaveConfig_Click(sender, e);
            
            if (_worldProcess != null && !_worldProcess.HasExited)
            {
                var result = MessageBox.Show("Restart WorldServer to apply config changes?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    PerformBackupBeforeRestart();
                    BtnStopWorld_Click(sender, e);
                    System.Threading.Thread.Sleep(2000);
                    BtnStartWorld_Click(sender, e);
                }
            }
            else
            {
                MessageBox.Show("WorldServer is not running. Start it to apply config changes.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private void BtnFindConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_configManager == null) return;
            
            try
            {
                var searchTerm = ConfigSearch.Text;
                if (string.IsNullOrWhiteSpace(searchTerm)) return;
                
                var results = _configManager.SearchConfig(searchTerm);
                
                var message = $"Found {results.Count} results:\n\n";
                foreach (var result in results.Take(10))
                {
                    message += $"[{result.Section}] {result.Key} = {result.Value}\n";
                }
                
                if (results.Count > 10)
                    message += $"\n... and {results.Count - 10} more results";
                
                MessageBox.Show(message, "Search Results", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to search config: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnBrowseServerDir_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.SelectedPath = TxtServerDirectory.Text;
            
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtServerDirectory.Text = dialog.SelectedPath;
            }
        }
        
        private void BtnBrowseConfigDir_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.SelectedPath = TxtConfigDirectory.Text;
            
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtConfigDirectory.Text = dialog.SelectedPath;
            }
        }
        
        private void BtnBrowseBackupDir_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.SelectedPath = TxtBackupDirectory.Text;
            
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtBackupDirectory.Text = dialog.SelectedPath;
            }
        }
        
        private void BtnBackupNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dbManager = new DatabaseManager(
                    _settings.MySqlHost,
                    _settings.MySqlPort,
                    _settings.CharacterDatabase,
                    _settings.MySqlUser,
                    _settings.MySqlPassword
                );
                
                string backupFile = dbManager.BackupDatabase(TxtBackupDirectory.Text);
                TxtBackupStatus.Text = $"Backup created: {System.IO.Path.GetFileName(backupFile)}";
                AppendToConsole($"Database backup created: {backupFile}", "SYSTEM");
            }
            catch (Exception ex)
            {
                TxtBackupStatus.Text = $"Backup failed: {ex.Message}";
                MessageBox.Show($"Backup failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnRestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dbManager = new DatabaseManager(
                    _settings.MySqlHost,
                    _settings.MySqlPort,
                    _settings.CharacterDatabase,
                    _settings.MySqlUser,
                    _settings.MySqlPassword
                );
                
                var backups = dbManager.GetBackupFiles(TxtBackupDirectory.Text);
                
                if (backups.Count == 0)
                {
                    MessageBox.Show("No backup files found", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // Simple dialog to select backup (for now, just restore the latest)
                string latestBackup = backups[0];
                
                var result = MessageBox.Show($"Restore latest backup?\n\n{System.IO.Path.GetFileName(latestBackup)}", 
                    "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    dbManager.RestoreDatabase(latestBackup);
                    TxtBackupStatus.Text = $"Database restored from: {System.IO.Path.GetFileName(latestBackup)}";
                    AppendToConsole($"Database restored from: {latestBackup}", "SYSTEM");
                }
            }
            catch (Exception ex)
            {
                TxtBackupStatus.Text = $"Restore failed: {ex.Message}";
                MessageBox.Show($"Restore failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void PerformBackupBeforeRestart()
        {
            if (!_settings.BackupDatabaseBeforeRestart)
                return;
            
            try
            {
                var dbManager = new DatabaseManager(
                    _settings.MySqlHost,
                    _settings.MySqlPort,
                    _settings.CharacterDatabase,
                    _settings.MySqlUser,
                    _settings.MySqlPassword
                );
                
                string backupFile = dbManager.BackupDatabase(_settings.BackupDirectory);
                AppendToConsole($"Database backup created before restart: {backupFile}", "SYSTEM");
            }
            catch (Exception ex)
            {
                AppendToConsole($"Backup before restart failed: {ex.Message}", "SYSTEM", true);
            }
        }
        
        private void BtnLoadSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _settings = AppSettings.Load();
                LoadSettingsToUI();
                MessageBox.Show("Settings loaded successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettingsFromUI();
                _settings.Save();
                MessageBox.Show("Settings saved successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnApplySettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettingsFromUI();
                _settings.Save();
                InitializeManagers();
                MessageBox.Show("Settings applied. Restart servers to use new paths.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnScheduleRestart_Click(object sender, RoutedEventArgs e)
        {
            if (!TimeSpan.TryParse(TxtRestartTime.Text, out TimeSpan scheduledTime))
            {
                MessageBox.Show("Please enter a valid time in HH:mm format (e.g., 12:00, 00:00).", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            _restartScheduled = true;
            
            BtnScheduleRestart.IsEnabled = false;
            BtnCancelRestart.IsEnabled = true;
            
            RestartCountdownText.Text = $"Scheduled restart at: {scheduledTime:hh\\:mm}";
            RestartTargetText.Text = "Target: World Server";
            RestartInfoBorder.Visibility = Visibility.Visible;
            
            _restartTimer.Start();
            
            AppendToConsole($"Scheduled restart at {scheduledTime:hh\\:mm}", "SYSTEM");
        }
        
        private void BtnCancelRestart_Click(object sender, RoutedEventArgs e)
        {
            _restartTimer.Stop();
            _restartScheduled = false;
            
            BtnScheduleRestart.IsEnabled = true;
            BtnCancelRestart.IsEnabled = false;
            RestartInfoBorder.Visibility = Visibility.Collapsed;
            
            AppendToConsole("Scheduled restart cancelled", "SYSTEM");
        }
        
        private void BtnAddEvent_Click(object sender, RoutedEventArgs e)
        {
            if (!TimeSpan.TryParse(TxtEventTime.Text, out TimeSpan scheduledTime))
            {
                MessageBox.Show("Please enter a valid time in HH:mm format (e.g., 12:00, 00:00).", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var eventType = ((ComboBoxItem)CmbEventType.SelectedItem)?.Content?.ToString() ?? "Command";
            var eventTarget = ((ComboBoxItem)CmbEventTarget.SelectedItem)?.Content?.ToString() ?? "World";
            
            var newEvent = new ScheduledEvent
            {
                Name = TxtEventName.Text,
                Type = eventType,
                Target = eventTarget,
                Command = TxtEventCommand.Text,
                ScheduledTime = scheduledTime,
                IsRecurring = ChkEventRecurring.IsChecked ?? false,
                RecurrencePattern = ChkEventRecurring.IsChecked ?? false ? "Daily" : "Once",
                IsEnabled = true
            };
            
            var observable = (ObservableCollection<ScheduledEvent>)EventList.ItemsSource;
            observable.Add(newEvent);
            
            // Clear inputs
            TxtEventName.Clear();
            TxtEventCommand.Clear();
            
            AppendToConsole($"Added event: {newEvent.Name} at {scheduledTime:hh\\:mm}", "SYSTEM");
        }
        
        private void BtnDeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (EventList.SelectedItem is ScheduledEvent selectedEvent)
            {
                var observable = (ObservableCollection<ScheduledEvent>)EventList.ItemsSource;
                observable.Remove(selectedEvent);
                AppendToConsole($"Deleted event: {selectedEvent.Name}", "SYSTEM");
            }
        }
        
        private void BtnSaveEvents_Click(object sender, RoutedEventArgs e)
        {
            var observable = (ObservableCollection<ScheduledEvent>)EventList.ItemsSource;
            _settings.ScheduledEvents = observable.ToList();
            _settings.Save();
            AppendToConsole($"Saved {observable.Count} events", "SYSTEM");
            MessageBox.Show("Events saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void LoadEvents()
        {
            var observable = (ObservableCollection<ScheduledEvent>)EventList.ItemsSource;
            observable.Clear();
            
            foreach (var evt in _settings.ScheduledEvents)
            {
                observable.Add(evt);
            }
        }
        
        private void AnalyzeCrashLog(string server)
        {
            try
            {
                var logPath = server == "Auth" 
                    ? System.IO.Path.Combine(_settings.ServerDirectory, "logs", "Auth.log")
                    : System.IO.Path.Combine(_settings.ServerDirectory, "logs", "World.log");
                
                if (!System.IO.File.Exists(logPath))
                {
                    AppendToConsole($"Crash log not found: {logPath}", "SYSTEM", true);
                    return;
                }
                
                // Read last 100 lines of log
                var allLines = System.IO.File.ReadAllLines(logPath);
                var recentLines = allLines.Skip(Math.Max(0, allLines.Length - 100)).ToArray();
                
                // Analyze for crash indicators
                var crashIndicators = new[] { "crash", "fatal", "exception", "error", "assertion", "segfault", "aborted" };
                var foundIssues = new System.Collections.Generic.List<string>();
                
                foreach (var line in recentLines)
                {
                    if (crashIndicators.Any(indicator => line.ToLower().Contains(indicator)))
                    {
                        foundIssues.Add(line);
                    }
                }
                
                if (foundIssues.Any())
                {
                    AppendToConsole($"=== {server} Crash Log Analysis ===", "SYSTEM", true);
                    AppendToConsole($"Found {foundIssues.Count} potential crash indicators:", "SYSTEM", true);
                    foreach (var issue in foundIssues.Take(5)) // Show first 5 issues
                    {
                        AppendToConsole(issue, "SYSTEM", true);
                    }
                    if (foundIssues.Count > 5)
                    {
                        AppendToConsole($"... and {foundIssues.Count - 5} more", "SYSTEM", true);
                    }
                    AppendToConsole($"=== End Analysis ===", "SYSTEM", true);
                }
                else
                {
                    AppendToConsole($"No obvious crash indicators found in {server} log", "SYSTEM");
                }
            }
            catch (Exception ex)
            {
                AppendToConsole($"Error analyzing crash log: {ex.Message}", "SYSTEM", true);
            }
        }
        
        private void UpdateHealthGraphs()
        {
            try
            {
                // Filter data based on selected time range
                var cutoffTime = DateTime.Now.AddMinutes(-_selectedTimeRangeMinutes);
                var filteredData = _healthDataHistory.Where(dp => dp.Timestamp >= cutoffTime).ToList();
                
                if (filteredData.Count == 0)
                    return;
                
                // Render World CPU Graph (first graph, previously Auth Memory)
                RenderGraph(AuthMemoryCanvas, AuthMemoryPolyline, filteredData.Select(dp => dp.WorldCpuPercent).ToList());
                
                // Render World Memory Graph
                RenderGraph(WorldMemoryCanvas, WorldMemoryPolyline, filteredData.Select(dp => dp.WorldMemoryMB).ToList());
            }
            catch (Exception ex)
            {
                AppendToConsole($"Error updating health graphs: {ex.Message}", "SYSTEM", true);
            }
        }
        
        private void RenderGraph(Canvas canvas, Polyline polyline, System.Collections.Generic.List<double> values)
        {
            if (values.Count == 0)
                return;
            
            var width = canvas.ActualWidth;
            var height = canvas.ActualHeight;
            
            if (width <= 0 || height <= 0)
                return;
            
            var maxValue = values.Max();
            if (maxValue == 0)
                maxValue = 1;
            
            var points = new System.Collections.Generic.List<System.Windows.Point>();
            
            for (int i = 0; i < values.Count; i++)
            {
                var x = (i / (double)(values.Count - 1)) * width;
                var y = height - (values[i] / maxValue) * height;
                points.Add(new System.Windows.Point(x, y));
            }
            
            polyline.Points = new System.Windows.Media.PointCollection(points);
        }
        
        private void CmbTimeRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbTimeRange.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                if (int.TryParse(item.Tag.ToString(), out int minutes))
                {
                    _selectedTimeRangeMinutes = minutes;
                    UpdateHealthGraphs();
                }
            }
        }
        
        private void LogDebug(string message)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AzerothCoreLauncher",
                    "debug.log"
                );
                
                var logDir = System.IO.Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDir) && !System.IO.Directory.Exists(logDir))
                {
                    System.IO.Directory.CreateDirectory(logDir);
                }
                
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                System.IO.File.AppendAllText(logPath, logMessage + System.Environment.NewLine);
            }
            catch
            {
                // If logging fails, don't crash the app
            }
        }
    }
}
