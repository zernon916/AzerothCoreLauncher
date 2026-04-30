using System;
using System.Collections.Generic;
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
        
        private ConfigManager? _configManager;
        private AppSettings _settings = new AppSettings();
        private DatabaseManager? _dbManager;
        private ItemCache? _itemCache;
        
        private DispatcherTimer? _restartTimer;
        
        private DateTime _authStartTime;
        private DateTime _worldStartTime;
        private System.IO.FileSystemWatcher? _authLogWatcher;
        private long _authLogLastPosition;
        private System.IO.FileSystemWatcher? _worldLogWatcher;
        private long _worldLogLastPosition;
        private bool _authWasRunning;
        private bool _worldWasRunning;
        private int _authCrashCount;
        private int _worldCrashCount;
        
        // Analytics storage
        private System.Collections.Generic.List<PlayerCountHistory> _playerCountHistory = new();
        private System.Collections.Generic.List<PeakHour> _peakHours = new();
        private System.Collections.Generic.List<PerformanceMetric> _performanceMetrics = new();
        
        // Communication storage
        private System.Collections.Generic.List<BroadcastMessage> _broadcastHistory = new();
        private DispatcherTimer? _analyticsRefreshTimer;
        
        public MainWindow()
        {
            try
            {
                LogDebug("MainWindow constructor started");
                
                InitializeComponent();
                LogDebug("InitializeComponent completed");
                
                PlayerList.ItemsSource = new ObservableCollection<PlayerInfo>();
                EventList.ItemsSource = new ObservableCollection<ScheduledEvent>();
                
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
                
                InitializeItemCache();
                LogDebug("Item cache initialized");
                
                InitializeTimers();
                LogDebug("Timers initialized");
                
                // Kill existing server processes if setting is enabled
                if (_settings.KillExistingServersOnStartup)
                {
                    KillExistingServers();
                }
                
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
            _restartTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _restartTimer.Tick += RestartTimer_Tick;
            
            // Memory update timer
            var memoryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            memoryTimer.Tick += UpdateMemoryUsage;
            memoryTimer.Start();
            
            // Analytics refresh timer
            _analyticsRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _analyticsRefreshTimer.Tick += (s, e) => BtnRefreshAnalytics_Click(s!, new RoutedEventArgs());
            _analyticsRefreshTimer.Start();
        }
        
        private void InitializeItemCache()
        {
            var cachePath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                "item_template.csv"
            );
            
            _itemCache = new ItemCache(cachePath);
            
            AppendToConsole($"Item cache path: {cachePath}", "SYSTEM");
            
            if (!_itemCache.Load())
            {
                AppendToConsole("Item cache not found. Please refresh item cache from Settings.", "SYSTEM", true);
            }
            else
            {
                AppendToConsole($"Item cache loaded: {_itemCache.Count} items", "SYSTEM");
            }
        }
        
        private void KillExistingServers()
        {
            try
            {
                var processesToKill = new List<string> { "authserver", "worldserver" };
                var killedProcesses = new List<string>();
                
                foreach (var processName in processesToKill)
                {
                    try
                    {
                        var processes = System.Diagnostics.Process.GetProcessesByName(processName);
                        foreach (var process in processes)
                        {
                            process.Kill();
                            process.WaitForExit(5000); // Wait up to 5 seconds for process to terminate
                            killedProcesses.Add(processName);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"Failed to kill {processName}: {ex.Message}");
                    }
                }
                
                if (killedProcesses.Count > 0)
                {
                    AppendToConsole($"Killed existing server processes: {string.Join(", ", killedProcesses)}", "SYSTEM");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error killing existing servers: {ex.Message}");
            }
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
        
        private void CheckEvents_Tick(object? sender, EventArgs e)
        {
            var currentTime = DateTime.Now.TimeOfDay;
            var today = DateTime.Today;
            var currentDayOfWeek = DateTime.Now.DayOfWeek;
            
            foreach (var evt in _settings.ScheduledEvents)
            {
                if (!evt.IsEnabled)
                    continue;
                
                // Check if event should execute based on recurrence pattern
                bool shouldExecuteToday = false;
                
                if (!evt.IsRecurring)
                {
                    // One-time event: check if it's the right day and time
                    shouldExecuteToday = evt.NextExecution.HasValue && evt.NextExecution.Value.Date == today;
                }
                else
                {
                    // Recurring events
                    switch (evt.RecurrencePattern)
                    {
                        case "Daily":
                            shouldExecuteToday = true;
                            break;
                        case "Weekly":
                            shouldExecuteToday = evt.RecurrenceDays.Contains(currentDayOfWeek);
                            break;
                        case "Monthly":
                            shouldExecuteToday = evt.DayOfMonth.HasValue && DateTime.Now.Day == evt.DayOfMonth.Value;
                            break;
                        case "Custom":
                            // Execute every X days from last execution
                            if (evt.LastExecuted.HasValue)
                            {
                                var daysSinceLast = (today - evt.LastExecuted.Value.Date).Days;
                                shouldExecuteToday = daysSinceLast >= evt.RecurrenceInterval;
                            }
                            else
                            {
                                shouldExecuteToday = true;
                            }
                            break;
                    }
                }
                
                // Check if already executed today (for recurring events)
                if (shouldExecuteToday && evt.IsRecurring && evt.LastExecuted.HasValue && evt.LastExecuted.Value.Date == today)
                    shouldExecuteToday = false;
                
                // Check if it's the right time
                if (shouldExecuteToday && Math.Abs((currentTime - evt.ScheduledTime).TotalSeconds) < 1)
                {
                    // Check conditions if any
                    if (evt.HasConditions && !CheckEventConditions(evt))
                    {
                        AppendToConsole($"Event '{evt.Name}' skipped: conditions not met", "SYSTEM");
                        continue;
                    }
                    
                    ExecuteEvent(evt);
                }
            }
        }
        
        private bool CheckEventConditions(ScheduledEvent evt)
        {
            // Check player count conditions
            if (evt.MinPlayerCount.HasValue || evt.MaxPlayerCount.HasValue)
            {
                try
                {
                    var players = _dbManager?.GetOnlinePlayers();
                    if (players == null) return false;
                    int playerCount = players.Count;
                    
                    if (evt.MinPlayerCount.HasValue && playerCount < evt.MinPlayerCount.Value)
                        return false;
                    
                    if (evt.MaxPlayerCount.HasValue && playerCount > evt.MaxPlayerCount.Value)
                        return false;
                }
                catch
                {
                    return false; // Skip event if we can't check player count
                }
            }
            
            // Check time window conditions
            if (evt.StartTimeWindow.HasValue || evt.EndTimeWindow.HasValue)
            {
                var currentTime = DateTime.Now.TimeOfDay;
                
                if (evt.StartTimeWindow.HasValue && currentTime < evt.StartTimeWindow.Value)
                    return false;
                
                if (evt.EndTimeWindow.HasValue && currentTime > evt.EndTimeWindow.Value)
                    return false;
            }
            
            return true;
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
                            BtnStopAuth_Click(null!, new RoutedEventArgs());
                            System.Threading.Thread.Sleep(2000);
                            BtnStartAuth_Click(null!, new RoutedEventArgs());
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
                
                // Handle event chaining
                if (evt.ChainedEventIds.Count > 0 && evt.ChainDelaySeconds > 0)
                {
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(evt.ChainDelaySeconds) };
                    timer.Tick += (s, args) =>
                    {
                        timer.Stop();
                        foreach (var chainedEventId in evt.ChainedEventIds)
                        {
                            var chainedEvent = _settings.ScheduledEvents.FirstOrDefault(e => e.Id == chainedEventId);
                            if (chainedEvent != null && chainedEvent.IsEnabled)
                            {
                                AppendToConsole($"Executing chained event: {chainedEvent.Name}", "SYSTEM");
                                ExecuteEvent(chainedEvent);
                            }
                        }
                    };
                    timer.Start();
                }
            });
        }
        
        private void ExecuteScheduledRestart()
        {
            Dispatcher.Invoke(() =>
            {
                PerformBackupBeforeRestart();
                BtnStopWorld_Click(null!, new RoutedEventArgs());
                System.Threading.Thread.Sleep(2000);
                BtnStartWorld_Click(null!, new RoutedEventArgs());
                
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
                _settings.MySqlPassword,
                _settings.AuthDatabase
            );
            
            _configManager = new ConfigManager(_settings.GetWorldServerConfigPath());
        }
        
        private void LoadSettingsToUI()
        {
            TxtServerDirectory.Text = _settings.ServerDirectory;
            TxtMySqlHost.Text = _settings.MySqlHost;
            TxtMySqlPort.Text = _settings.MySqlPort;
            TxtCharacterDatabase.Text = _settings.CharacterDatabase;
            TxtAuthDatabase.Text = _settings.AuthDatabase;
            TxtMySqlUser.Text = _settings.MySqlUser;
            TxtMySqlPassword.Password = _settings.MySqlPassword;
            TxtConfigDirectory.Text = _settings.ConfigDirectory;
            TxtConsoleLineLimit.Text = _settings.ConsoleLineLimit.ToString();
            
            // Load stability settings
            ChkAutoRestartOnCrash.IsChecked = _settings.AutoRestartOnCrash;
            TxtMaxAutoRestarts.Text = _settings.MaxAutoRestarts.ToString();
            TxtAutoRestartDelay.Text = _settings.AutoRestartDelaySeconds.ToString();
            ChkEnableCrashLogAnalysis.IsChecked = _settings.EnableCrashLogAnalysis;
            ChkKillExistingServers.IsChecked = _settings.KillExistingServersOnStartup;
            
            // Load health monitoring settings
            ChkEnableHealthMonitoring.IsChecked = _settings.EnableHealthMonitoring;
            TxtMemoryAlertThreshold.Text = _settings.MemoryAlertThresholdMB.ToString();
            TxtHealthCheckInterval.Text = _settings.HealthCheckIntervalSeconds.ToString();
            
            // Load database backup settings
            ChkBackupBeforeRestart.IsChecked = _settings.BackupDatabaseBeforeRestart;
            TxtBackupDirectory.Text = _settings.BackupDirectory;
            TxtMySqlDumpPath.Text = _settings.MySqlDumpPath;
            TxtMySqlPath.Text = _settings.MySqlPath;
        }
        
        private void SaveSettingsFromUI()
        {
            _settings.ServerDirectory = TxtServerDirectory.Text;
            _settings.ConfigDirectory = TxtConfigDirectory.Text;
            _settings.MySqlHost = TxtMySqlHost.Text;
            _settings.MySqlPort = TxtMySqlPort.Text;
            _settings.CharacterDatabase = TxtCharacterDatabase.Text;
            _settings.AuthDatabase = TxtAuthDatabase.Text;
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
            _settings.KillExistingServersOnStartup = ChkKillExistingServers.IsChecked ?? false;
            
            // Save health monitoring settings
            _settings.EnableHealthMonitoring = ChkEnableHealthMonitoring.IsChecked ?? false;
            if (int.TryParse(TxtMemoryAlertThreshold.Text, out int memoryThreshold))
                _settings.MemoryAlertThresholdMB = memoryThreshold;
            if (int.TryParse(TxtHealthCheckInterval.Text, out int healthInterval))
                _settings.HealthCheckIntervalSeconds = healthInterval;
            
            // Save database backup settings
            _settings.BackupDatabaseBeforeRestart = ChkBackupBeforeRestart.IsChecked ?? false;
            _settings.BackupDirectory = TxtBackupDirectory.Text;
            _settings.MySqlDumpPath = TxtMySqlDumpPath.Text;
            _settings.MySqlPath = TxtMySqlPath.Text;
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
                        BtnStartAuth_Click(null!, new RoutedEventArgs());
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
                        BtnStartWorld_Click(null!, new RoutedEventArgs());
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
                
                AppendToConsole($"Found {players.Count} online players", "SYSTEM");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to search players: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void PlayerList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PlayerList.SelectedItem is PlayerInfo player && _dbManager != null)
            {
                try
                {
                    // Query database to get character GUID
                    var query = $"SELECT guid, account FROM characters WHERE name = '{player.Name}'";
                    var dataTable = _dbManager.ExecuteQuery(query);
                    
                    if (dataTable.Rows.Count > 0)
                    {
                        var row = dataTable.Rows[0];
                        int characterGuid = Convert.ToInt32(row["guid"]);
                        int accountId = Convert.ToInt32(row["account"]);
                        
                        var popup = new PlayerPopup(characterGuid, player.Name, accountId, _dbManager!, _itemCache!)
                        {
                            Owner = this
                        };
                        popup.Show();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open player popup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private void BtnSearchOfflinePlayer_Click(object sender, RoutedEventArgs e)
        {
            if (_dbManager == null) return;
            
            try
            {
                var searchTerm = PlayerSearch.Text;
                var players = string.IsNullOrWhiteSpace(searchTerm) 
                    ? _dbManager.GetOfflinePlayers() 
                    : _dbManager.SearchOfflinePlayers(searchTerm);
                
                var observable = (ObservableCollection<PlayerInfo>)PlayerList.ItemsSource;
                observable.Clear();
                
                foreach (var player in players)
                {
                    observable.Add(player);
                }
                
                AppendToConsole($"Found {players.Count} offline players", "SYSTEM");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to search offline players: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                
                AppendToConsole($"Refreshed {players.Count} online players", "SYSTEM");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh players: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnSearchAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string username = AccountSearch.Text.Trim();
                if (string.IsNullOrEmpty(username))
                {
                    MessageBox.Show("Please enter an account name", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (_dbManager == null)
                {
                    MessageBox.Show("Database manager not initialized", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var account = _dbManager.GetAccountInfo(username);
                if (account == null)
                {
                    MessageBox.Show("Account not found", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                string status = account.Locked ? "LOCKED" : "Active";
                string lastLogin = account.LastLogin == DateTime.MinValue ? "Never" : account.LastLogin.ToString("yyyy-MM-dd HH:mm:ss");
                
                MessageBox.Show($"Account found: {username}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                AppendToConsole($"Loaded account info for: {username}", "SYSTEM");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to search account: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnCreateAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dbManager == null)
                {
                    MessageBox.Show("Database manager not initialized", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var popup = new CreateAccountPopup(_dbManager, _worldProcess);
                popup.Owner = this;
                var result = popup.ShowDialog();
                
                if (result == true)
                {
                    // Refresh account list
                    BtnRefreshAccount_Click(sender, e);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open create account popup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnRefreshAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dbManager == null)
                {
                    MessageBox.Show("Database manager not initialized", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Load all accounts
                var accounts = _dbManager.GetAllAccounts();
                DgAccounts.ItemsSource = accounts;
                
                MessageBox.Show($"Loaded {accounts.Count} accounts", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                AppendToConsole($"Loaded {accounts.Count} accounts", "SYSTEM");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load accounts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnEditAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dbManager == null)
                {
                    MessageBox.Show("Database manager not initialized", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var button = sender as System.Windows.Controls.Button;
                string username = button?.Tag?.ToString() ?? string.Empty;
                
                if (string.IsNullOrEmpty(username))
                {
                    MessageBox.Show("Account name not found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var popup = new AccountEditPopup(_dbManager, username, _worldProcess);
                popup.Owner = this;
                popup.ShowDialog();
                
                // Refresh account list
                BtnRefreshAccount_Click(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open account edit popup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnDeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dbManager == null)
                {
                    MessageBox.Show("Database manager not initialized", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var button = sender as System.Windows.Controls.Button;
                string username = button?.Tag?.ToString() ?? string.Empty;
                
                if (string.IsNullOrEmpty(username))
                {
                    MessageBox.Show("Account name not found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Confirm deletion
                var result = MessageBox.Show(
                    $"Are you sure you want to delete account '{username}'?\n\nThis will also delete all characters associated with this account. This action cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    _dbManager.DeleteAccount(username);
                    MessageBox.Show($"Account '{username}' has been deleted", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    AppendToConsole($"Account deleted: {username}", "SYSTEM");
                    
                    // Refresh account list
                    BtnRefreshAccount_Click(sender, e);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete account: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnRefreshIPBans_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dbManager == null)
                {
                    MessageBox.Show("Database manager not initialized", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var bans = _dbManager.GetAllIPBans();
                DgIPBans.ItemsSource = bans;
                
                AppendToConsole($"Loaded {bans.Count} IP bans", "SYSTEM");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load IP bans: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnRefreshAccountBans_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dbManager == null)
                {
                    MessageBox.Show("Database manager not initialized", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var bans = _dbManager.GetAllAccountBans();
                DgAccountBans.ItemsSource = bans;
                
                AppendToConsole($"Loaded {bans.Count} account bans", "SYSTEM");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load account bans: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnEditBannedAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dbManager == null)
                {
                    MessageBox.Show("Database manager not initialized", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var button = sender as System.Windows.Controls.Button;
                var accountId = button?.Tag;
                
                if (accountId == null)
                {
                    MessageBox.Show("Account ID not found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Get username from account ID
                string username = _dbManager.GetUsernameByAccountId((int)accountId);
                if (string.IsNullOrEmpty(username))
                {
                    MessageBox.Show("Account not found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var popup = new AccountEditPopup(_dbManager, username, _worldProcess);
                popup.Owner = this;
                popup.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open account edit popup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void AccountManagementTab_Selected(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dbManager == null) return;
                
                // Refresh account bans
                var accountBans = _dbManager.GetAllAccountBans();
                DgAccountBans.ItemsSource = accountBans;
                
                // Refresh IP bans
                var ipBans = _dbManager.GetAllIPBans();
                DgIPBans.ItemsSource = ipBans;
                
                AppendToConsole($"Auto-refreshed {accountBans.Count} account bans and {ipBans.Count} IP bans", "SYSTEM");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to auto-refresh: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnRefreshAnalytics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dbManager == null)
                {
                    MessageBox.Show("Database manager not initialized", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Get current online player count
                var onlinePlayers = _dbManager.GetOnlinePlayers();
                int playerCount = onlinePlayers.Count;
                
                // Update current stats
                TxtOnlinePlayers.Text = playerCount.ToString();
                
                // Get CPU and memory usage
                if (_worldProcess != null && !_worldProcess.HasExited)
                {
                    try
                    {
                        _worldProcess.Refresh();
                        
                        // Memory usage
                        var memoryUsage = _worldProcess.WorkingSet64 / (1024 * 1024);
                        var currentCpuTime = _worldProcess.TotalProcessorTime;
                        
                        TxtMemoryUsage.Text = $"{memoryUsage} MB";
                        
                        // CPU usage - simplified calculation
                        try
                        {
                            if (_performanceMetrics.Count > 0)
                            {
                                var lastMetric = _performanceMetrics.Last();
                                var timeDelta = (DateTime.Now - lastMetric.Time).TotalSeconds;
                                if (timeDelta > 0)
                                {
                                    var cpuDelta = (currentCpuTime - TimeSpan.FromSeconds(lastMetric.CpuUsage)).TotalSeconds;
                                    var cpuUsage = (cpuDelta / timeDelta) * 100 / Environment.ProcessorCount;
                                    cpuUsage = Math.Max(0, Math.Min(100, cpuUsage));
                                    TxtCpuUsage.Text = $"{cpuUsage:F1}%";
                                }
                                else
                                {
                                    TxtCpuUsage.Text = "Calculating...";
                                }
                            }
                            else
                            {
                                TxtCpuUsage.Text = "Calculating...";
                            }
                        }
                        catch
                        {
                            TxtCpuUsage.Text = "N/A";
                        }
                        
                        // Add to performance history
                        _performanceMetrics.Add(new PerformanceMetric
                        {
                            Time = DateTime.Now,
                            CpuUsage = currentCpuTime.TotalSeconds, // Store raw CPU time for delta calculation
                            MemoryUsage = memoryUsage
                        });
                        
                        // Keep only last 100 entries
                        if (_performanceMetrics.Count > 100)
                            _performanceMetrics.RemoveAt(0);
                    }
                    catch (Exception ex)
                    {
                        TxtMemoryUsage.Text = "Error";
                        AppendToConsole($"Error getting process stats: {ex.Message}", "ERROR");
                    }
                }
                else
                {
                    TxtCpuUsage.Text = "Server not running";
                    TxtMemoryUsage.Text = "Server not running";
                    AppendToConsole("World process not running for analytics", "DEBUG");
                }
                
                // Add to player count history
                var currentHour = DateTime.Now.Hour;
                var hourData = _playerCountHistory.Where(h => h.Timestamp.Hour == currentHour);
                var peakThisHour = hourData.Any() ? hourData.Max(h => h.PlayerCount) : 0;
                
                _playerCountHistory.Add(new PlayerCountHistory
                {
                    Timestamp = DateTime.Now,
                    PlayerCount = playerCount,
                    PeakThisHour = peakThisHour
                });
                
                // Keep only last 24 hours of data
                _playerCountHistory.RemoveAll(h => h.Timestamp < DateTime.Now.AddHours(-24));
                
                // Calculate peak hours for last 24 hours
                CalculatePeakHours();
                
                // Update DataGrids
                DgPlayerCountHistory.ItemsSource = _playerCountHistory.OrderByDescending(h => h.Timestamp).Take(50).ToList();
                DgPeakHours.ItemsSource = _peakHours;
                DgCpuHistory.ItemsSource = _performanceMetrics.OrderByDescending(m => m.Time).Take(50).ToList();
                DgMemoryHistory.ItemsSource = _performanceMetrics.OrderByDescending(m => m.Time).Take(50).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh analytics: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void CalculatePeakHours()
        {
            _peakHours.Clear();
            
            for (int hour = 0; hour < 24; hour++)
            {
                var hourData = _playerCountHistory.Where(h => h.Timestamp.Hour == hour);
                
                if (hourData.Any())
                {
                    var average = hourData.Average(h => h.PlayerCount);
                    var peak = hourData.Max(h => h.PlayerCount);
                    var peakTime = hourData.OrderByDescending(h => h.PlayerCount).First().Timestamp;
                    
                    _peakHours.Add(new PeakHour
                    {
                        Hour = hour,
                        AveragePlayers = average,
                        PeakPlayers = peak,
                        PeakTime = peakTime
                    });
                }
            }
            
            _peakHours = _peakHours.OrderByDescending(h => h.PeakPlayers).ToList();
        }
        
        private void BtnSendAnnouncement_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string message = TxtAnnouncementMessage.Text.Trim();
                if (string.IsNullOrEmpty(message))
                {
                    MessageBox.Show("Please enter an announcement message", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (_worldProcess == null || _worldProcess.HasExited)
                {
                    MessageBox.Show("World server is not running", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                string type = "announce";
                if (CmbAnnouncementType.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag != null)
                {
                    type = item.Tag.ToString() ?? "announce";
                }
                
                string command = $"{type} {message}";
                _worldProcess.StandardInput.WriteLine(command);
                
                // Add to history
                _broadcastHistory.Add(new BroadcastMessage
                {
                    Time = DateTime.Now,
                    Type = type,
                    Message = message
                });
                
                // Update history display
                DgBroadcastHistory.ItemsSource = _broadcastHistory.OrderByDescending(b => b.Time).ToList();
                
                // Clear message
                TxtAnnouncementMessage.Clear();
                
                AppendToConsole($"Sent {type}: {message}", "SYSTEM");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send announcement: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnTemplateRestart_Click(object sender, RoutedEventArgs e)
        {
            TxtAnnouncementMessage.Text = "Server will restart in 5 minutes. Please logout to prevent data loss.";
        }
        
        private void BtnTemplateMaintenance_Click(object sender, RoutedEventArgs e)
        {
            TxtAnnouncementMessage.Text = "Server is entering maintenance mode. New connections will be disabled.";
        }
        
        private void BtnTemplateWelcome_Click(object sender, RoutedEventArgs e)
        {
            TxtAnnouncementMessage.Text = "Welcome to our server! Please follow the rules and enjoy your stay.";
        }
        
        private void BtnClearBroadcastHistory_Click(object sender, RoutedEventArgs e)
        {
            _broadcastHistory.Clear();
            DgBroadcastHistory.ItemsSource = _broadcastHistory.ToList();
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
                
                if (string.IsNullOrEmpty(command)) return;
                
                if (_worldProcess != null && !_worldProcess.HasExited)
                {
                    _worldProcess.StandardInput.WriteLine(command);
                    AppendToConsole($"Executed {action} on {selectedPlayer.Name}", "SYSTEM");
                }
                else
                {
                    MessageBox.Show("WorldServer is not running", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to execute GM action: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnPlayerAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string characterName = ((Button)sender).Tag?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(characterName))
                    return;
                
                if (_dbManager == null)
                {
                    MessageBox.Show("Database manager not initialized", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Get account username from character name
                string username = _dbManager.GetAccountUsernameByCharacterName(characterName);
                if (string.IsNullOrEmpty(username))
                {
                    MessageBox.Show("Could not find account for this character", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Show account popup
                var popup = new AccountPopup(_dbManager, characterName, username, _worldProcess);
                popup.Owner = this;
                popup.ShowDialog();
                
                AppendToConsole($"Opened account popup for character: {characterName}", "SYSTEM");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open account popup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void LoadAccountCharacters(int accountId)
        {
            // No longer used - removed from UI
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
                
                string backupFile = dbManager.BackupDatabase(_settings.BackupDirectory, _settings.MySqlDumpPath);
                TxtBackupStatus.Text = $"Backup created: {System.IO.Path.GetFileName(backupFile)}";
                AppendToConsole($"Database backup created: {backupFile}", "SYSTEM");
            }
            catch (Exception ex)
            {
                TxtBackupStatus.Text = $"Backup failed: {ex.Message}";
                MessageBox.Show($"Backup failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnRefreshItemCache_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtItemCacheStatus.Text = "Refreshing item cache...";
                
                // Create a new DatabaseManager for acore_world
                var worldDbManager = new DatabaseManager(
                    _settings.MySqlHost,
                    _settings.MySqlPort,
                    "acore_world",
                    _settings.MySqlUser,
                    _settings.MySqlPassword
                );
                
                AppendToConsole($"Connecting to acore_world at {_settings.MySqlHost}:{_settings.MySqlPort}", "SYSTEM");
                
                // Test connection with a simple query
                var testQuery = "SELECT COUNT(*) as count FROM item_template";
                var testResult = worldDbManager.ExecuteQuery(testQuery);
                int totalCount = Convert.ToInt32(testResult.Rows[0]["count"]);
                AppendToConsole($"Total items in acore_world.item_template: {totalCount}", "SYSTEM");
                
                var query = @"SELECT entry, name, displayid, Quality, InventoryType, stackable, ItemLevel, RequiredLevel, BuyPrice, SellPrice, armor, holy_res, fire_res, nature_res, frost_res, shadow_res, arcane_res, socketColor_1, socketContent_1, socketColor_2, socketContent_2, socketColor_3, socketContent_3, socketBonus, spellid_1, spellid_2, spellid_3, spellid_4, spellid_5, MaxDurability
FROM item_template";
                
                var dataTable = worldDbManager.ExecuteQuery(query);
                
                AppendToConsole($"Query returned {dataTable.Rows.Count} rows from item_template", "SYSTEM");
                
                if (dataTable.Rows.Count == 0)
                {
                    TxtItemCacheStatus.Text = "Query returned 0 rows - check database connection";
                    AppendToConsole("ERROR: Query returned 0 rows. Check if acore_world database exists and has item_template table.", "SYSTEM", true);
                    return;
                }
                
                var cachePath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                    "item_template.csv"
                );
                
                var lines = new List<string>();
                lines.Add("entry,name,displayid,Quality,InventoryType,stackable,ItemLevel,RequiredLevel,BuyPrice,SellPrice,armor,holy_res,fire_res,nature_res,frost_res,shadow_res,arcane_res,socketColor_1,socketContent_1,socketColor_2,socketContent_2,socketColor_3,socketContent_3,socketBonus,spellid_1,spellid_2,spellid_3,spellid_4,spellid_5,MaxDurability");
                
                foreach (System.Data.DataRow row in dataTable.Rows)
                {
                    var line = string.Join(",", dataTable.Columns.Cast<System.Data.DataColumn>().Select(c =>
                    {
                        var value = row[c];
                        if (value == DBNull.Value) return "";
                        return $"\"{value}\"";
                    }));
                    lines.Add(line);
                }
                
                System.IO.File.WriteAllLines(cachePath, lines);
                AppendToConsole($"Wrote {lines.Count - 1} items to CSV file at {cachePath}", "SYSTEM");
                
                if (_itemCache?.Load() == true)
                {
                    TxtItemCacheStatus.Text = $"Item cache refreshed: {_itemCache.Count} items";
                    AppendToConsole($"Item cache refreshed: {_itemCache.Count} items", "SYSTEM");
                }
                else
                {
                    TxtItemCacheStatus.Text = "Failed to load refreshed cache";
                    AppendToConsole("Failed to load refreshed item cache", "SYSTEM", true);
                }
            }
            catch (Exception ex)
            {
                TxtItemCacheStatus.Text = $"Refresh failed: {ex.Message}";
                AppendToConsole($"Item cache refresh failed: {ex.Message}", "SYSTEM", true);
                MessageBox.Show($"Refresh failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    dbManager.RestoreDatabase(latestBackup, _settings.MySqlPath);
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
        
        // Account Management Event Handlers
        private void BtnBanIP_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string ip = TxtBanIP.Text.Trim();
                if (string.IsNullOrEmpty(ip))
                {
                    MessageBox.Show("Please enter an IP address", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                var dbManager = new DatabaseManager(
                    _settings.MySqlHost,
                    _settings.MySqlPort,
                    _settings.CharacterDatabase,
                    _settings.MySqlUser,
                    _settings.MySqlPassword,
                    _settings.AuthDatabase
                );
                
                bool isBanned = dbManager.IsIPBanned(ip);
                
                if (isBanned)
                {
                    dbManager.UnbanIP(ip);
                    AppendToConsole($"IP unbanned: {ip}", "SYSTEM");
                    MessageBox.Show($"IP {ip} has been unbanned", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    dbManager.BanIP(ip, "Banned via Launcher", "Launcher");
                    AppendToConsole($"IP banned: {ip}", "SYSTEM");
                    MessageBox.Show($"IP {ip} has been banned", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                
                TxtBanIP.Clear();
                BtnRefreshIPBans_Click(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to ban/unban IP: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnUnbanIP_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string ip = ((Button)sender).Tag?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(ip))
                    return;
                
                var dbManager = new DatabaseManager(
                    _settings.MySqlHost,
                    _settings.MySqlPort,
                    _settings.CharacterDatabase,
                    _settings.MySqlUser,
                    _settings.MySqlPassword
                );
                
                dbManager.UnbanIP(ip);
                AppendToConsole($"IP unbanned: {ip}", "SYSTEM");
                BtnRefreshIPBans_Click(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to unban IP: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnLoadSecurityAccount_Click(object sender, RoutedEventArgs e)
        {
            // No longer used - removed from UI
        }
        
        private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            // No longer used - removed from UI
        }
        
        private void BtnLockAccount_Click(object sender, RoutedEventArgs e)
        {
            // No longer used - removed from UI
        }
        
        private void BtnUnlockAccount_Click(object sender, RoutedEventArgs e)
        {
            // No longer used - removed from UI
        }
        
        private void BtnViewLoginHistory_Click(object sender, RoutedEventArgs e)
        {
            // No longer used - removed from UI
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
                
                string backupFile = dbManager.BackupDatabase(_settings.BackupDirectory, _settings.MySqlDumpPath);
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
            
            BtnScheduleRestart.IsEnabled = false;
            BtnCancelRestart.IsEnabled = true;
            
            RestartCountdownText.Text = $"Scheduled restart at: {scheduledTime:hh\\:mm}";
            RestartTargetText.Text = "Target: World Server";
            RestartInfoBorder.Visibility = Visibility.Visible;
            
            _restartTimer?.Start();
            
            AppendToConsole($"Scheduled restart at {scheduledTime:hh\\:mm}", "SYSTEM");
        }
        
        private void BtnCancelRestart_Click(object sender, RoutedEventArgs e)
        {
            _restartTimer?.Stop();
            
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
            var recurrencePattern = ((ComboBoxItem)CmbRecurrencePattern.SelectedItem)?.Content?.ToString() ?? "Daily";
            
            // Build recurrence days list for weekly
            var recurrenceDays = new List<DayOfWeek>();
            if (recurrencePattern == "Weekly")
            {
                if (ChkMon.IsChecked == true) recurrenceDays.Add(DayOfWeek.Monday);
                if (ChkTue.IsChecked == true) recurrenceDays.Add(DayOfWeek.Tuesday);
                if (ChkWed.IsChecked == true) recurrenceDays.Add(DayOfWeek.Wednesday);
                if (ChkThu.IsChecked == true) recurrenceDays.Add(DayOfWeek.Thursday);
                if (ChkFri.IsChecked == true) recurrenceDays.Add(DayOfWeek.Friday);
                if (ChkSat.IsChecked == true) recurrenceDays.Add(DayOfWeek.Saturday);
                if (ChkSun.IsChecked == true) recurrenceDays.Add(DayOfWeek.Sunday);
            }
            
            // Parse day of month for monthly
            int? dayOfMonth = null;
            if (recurrencePattern == "Monthly" && int.TryParse(TxtDayOfMonth.Text, out int dom))
            {
                dayOfMonth = dom;
            }
            
            // Parse custom interval
            int recurrenceInterval = 1;
            if (recurrencePattern == "Custom" && int.TryParse(TxtRecurrenceInterval.Text, out int interval))
            {
                recurrenceInterval = interval;
            }
            
            // Parse chain delay
            int chainDelay = 0;
            int.TryParse(TxtChainDelay.Text, out chainDelay);
            
            // Parse conditions
            int? minPlayers = null;
            int? maxPlayers = null;
            TimeSpan? startTimeWindow = null;
            TimeSpan? endTimeWindow = null;
            
            if (ChkHasConditions.IsChecked == true)
            {
                if (int.TryParse(TxtMinPlayers.Text, out int min)) minPlayers = min;
                if (int.TryParse(TxtMaxPlayers.Text, out int max)) maxPlayers = max;
                if (TimeSpan.TryParse(TxtStartTimeWindow.Text, out TimeSpan start)) startTimeWindow = start;
                if (TimeSpan.TryParse(TxtEndTimeWindow.Text, out TimeSpan end)) endTimeWindow = end;
            }
            
            var newEvent = new ScheduledEvent
            {
                Name = TxtEventName.Text,
                Type = eventType,
                Target = eventTarget,
                Command = TxtEventCommand.Text,
                ScheduledTime = scheduledTime,
                IsEnabled = ChkEventEnabled.IsChecked ?? true,
                IsRecurring = ChkEventRecurring.IsChecked ?? false,
                RecurrencePattern = recurrencePattern,
                RecurrenceInterval = recurrenceInterval,
                RecurrenceDays = recurrenceDays,
                DayOfMonth = dayOfMonth,
                ChainDelaySeconds = chainDelay,
                HasConditions = ChkHasConditions.IsChecked ?? false,
                MinPlayerCount = minPlayers,
                MaxPlayerCount = maxPlayers,
                StartTimeWindow = startTimeWindow,
                EndTimeWindow = endTimeWindow
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
        
        private void CmbRecurrencePattern_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = ((ComboBoxItem)CmbRecurrencePattern.SelectedItem)?.Content?.ToString() ?? "Daily";
            
            // Check if UI elements are initialized
            if (WeeklyDaysPanel == null || MonthlyDayPanel == null || LblInterval == null || TxtRecurrenceInterval == null)
                return;
            
            // Hide all panels first
            WeeklyDaysPanel.Visibility = Visibility.Collapsed;
            MonthlyDayPanel.Visibility = Visibility.Collapsed;
            LblInterval.Visibility = Visibility.Collapsed;
            TxtRecurrenceInterval.Visibility = Visibility.Collapsed;
            
            // Show appropriate panel based on selection
            switch (selectedItem)
            {
                case "Weekly":
                    WeeklyDaysPanel.Visibility = Visibility.Visible;
                    break;
                case "Monthly":
                    MonthlyDayPanel.Visibility = Visibility.Visible;
                    break;
                case "Custom":
                    LblInterval.Visibility = Visibility.Visible;
                    TxtRecurrenceInterval.Visibility = Visibility.Visible;
                    break;
            }
        }
        
        private void ChkHasConditions_Checked(object sender, RoutedEventArgs e)
        {
            ConditionsPanel.Visibility = Visibility.Visible;
            TimeWindowPanel.Visibility = Visibility.Visible;
        }
        
        private void ChkHasConditions_Unchecked(object sender, RoutedEventArgs e)
        {
            ConditionsPanel.Visibility = Visibility.Collapsed;
            TimeWindowPanel.Visibility = Visibility.Collapsed;
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
        
        private void LogDebug(string message)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
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
