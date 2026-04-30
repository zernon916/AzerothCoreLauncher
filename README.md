# AzerothCore Launcher

A WPF-based GUI launcher for AzerothCore server management.

## Features

### Server Management
- **Server Control**: Start/Stop AuthServer and WorldServer with real-time status lights
- **Status Monitoring**: Memory usage, CPU usage, uptime tracking, and status indicators (green/yellow/red)
- **Boot Status Detection**: WorldServer status changes from yellow (booting) to green (running) based on process start and initialization
- **Graceful Shutdown**: WorldServer uses `server shutdown 1` for graceful shutdown to prevent data loss
- **Auto-Clear Console**: Console automatically clears when servers are stopped
- **Auto-Restart on Crash**: Automatically restart crashed servers (configurable max attempts and delay)
- **Crash Log Analysis**: Analyzes crash logs for error indicators and reports findings

### Console
- **Split Console View**: Separate Auth and World console panes with resizable splitter
- **Real-Time Output**: AuthServer uses log file watching, WorldServer uses direct process output streams
- **Console Line Limiting**: Configurable line limit to prevent performance issues
- **Command Input**: Send commands to Auth or World server directly from the console
- **Clear Console Button**: Manually clear console output
- **Clean Output**: No prefix spam, improved empty line filtering

### Player Management
- **Online Players**: View all currently online players
- **Player Search**: Search for specific players by name
- **GM Actions**: Execute GM commands (Kick, Ban, Mute, GM On, GM Off)

### Events
- **Scheduled Events**: Create scheduled commands, restarts, and announcements
- **Event Types**: 
  - Command: Send any console command to Auth or World server
  - Restart: Restart Auth or World server
  - Announcement: Send server announcement to World server
- **Event Persistence**: Events are saved and persist across launcher restarts
- **Daily Recurring**: Set events to repeat daily at the same time
- **Event Management**: Add, delete, and save events with a clean grid interface

### Server Health
- **Health Monitoring**: Real-time monitoring of server memory and CPU usage
- **Graphical Display**: Visual graphs showing historical health data
- **Time Range Selection**: View data for 1 min, 5 min, 30 min, 1h, or 24h
- **Fast Updates**: Health data updates every 5 seconds for real-time monitoring
- **Memory Alerts**: Configurable memory threshold for alerts
- **CPU Tracking**: World Server CPU usage graph for performance monitoring

### Config Editor
- **Config Loading**: Load worldserver.conf or authserver.conf
- **Config Editing**: Edit configuration files in the built-in text editor
- **Save & Apply**: Save changes and restart servers to apply new config

### Settings
- **Server Paths**: Configure AuthServer and WorldServer executable paths
- **Database Configuration**: Set MySQL connection settings
- **Config Directory**: Configure config file location
- **Console Line Limit**: Adjust console output line limit
- **Server Stability**: Configure auto-restart on crash, max restart attempts, restart delay, and crash log analysis
- **Health Monitoring**: Enable/disable health monitoring, set memory alert threshold, configure health check interval

## Requirements

- .NET 6.0 SDK
- Windows 10/11
- AzerothCore server installed at configured server directory
- Administrator privileges (automatically requested via UAC on launch)

## Configuration

Before running the launcher, configure the settings in the Settings tab:
- Server directory path
- AuthServer and WorldServer executable names
- MySQL connection settings (host, port, database, user, password)
- Config directory path
- Console line limit

Settings are automatically saved to `%AppData%\AzerothCoreLauncher\settings.json`

## Building

```bash
cd "C:\Users\benko\OneDrive\Desktop\Wow Console"
dotnet build -c Release
```

## Running

```bash
dotnet run
```

Or run the compiled executable:
```bash
cd "C:\Users\benko\OneDrive\Desktop\Wow Console\bin\Release\net6.0-windows"
AzerothCoreLauncher.exe
```

**Important**: Run the launcher as Administrator to start server processes.

## Usage

### Starting the Server
1. Click "Start AuthServer" to start the authentication server
2. Click "Start WorldServer" to start the game server
3. Monitor status lights (yellow = booting, green = running, red = stopped/error)
4. View console output in the Console tab (Auth and World panes)

### Console Commands
1. Select target server (Auth or World) from the dropdown
2. Type command in the command input box
3. Press Enter or click "Send"
4. Commands are sent directly to the selected server process

### Player Management
1. Go to the Players tab
2. Click "Refresh" to load online players
3. Use the search box to find specific players
4. Select a player from the list
5. Select a GM action from the dropdown (Kick, Ban, Mute, GM On, GM Off)
6. Click "Execute" to perform the action

### Events
1. Go to the Events tab
2. Fill in event details:
   - Event Name: Descriptive name for the event
   - Type: Command, Restart, or Announcement
   - Target: World or Auth server
   - Time: Execution time in HH:mm format
   - Command: Console command or announcement text
   - Repeat Daily: Check to repeat event daily
3. Click "Add" to add the event to the list
4. Click "Save" to persist all events
5. Events execute automatically at the scheduled time

### Config Editing
1. Go to the Config tab
2. Select worldserver.conf or authserver.conf from the dropdown
3. Click "Load" to load the config file
4. Edit the config in the text editor
5. Click "Save" to save changes
6. Click "Apply" to save and restart the server

### Settings
1. Go to the Settings tab
2. Configure server paths, database settings, and other options
3. Click "Save" to persist settings
4. Restart the launcher to apply new paths

## Troubleshooting

**Server won't start:**
- Ensure the launcher is running as Administrator
- Check server paths in Settings tab
- Verify server executables exist at the specified locations

**Can't connect to database:**
- Verify database connection settings in Settings tab
- Ensure MySQL server is running
- Check database credentials

**Config file not found:**
- Verify config directory path in Settings tab
- Ensure config files exist in the configs directory

**No console output:**
- Console uses log file watching - ensure log files are being written
- Check logs directory exists: `ServerDirectory\logs\`
- Verify Auth.log and World.log files are present

**Events not executing:**
- Ensure events are saved (click "Save" button)
- Check event time format is HH:mm (e.g., 12:00, 00:00)
- Verify target server is running when event executes
- Check console for event execution messages

## License

This project is provided as-is for AzerothCore server management.
