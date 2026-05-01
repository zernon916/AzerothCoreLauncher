# AzerothCore Launcher

A modern WPF-based GUI launcher for AzerothCore server management with comprehensive monitoring, automation, and notification features.

## Installation

### Option 1: Download Release (Recommended)
1. Go to the [Releases](../../releases) page
2. Download the latest `AzerothCoreLauncher.exe`
3. Place it in any folder on your computer
4. Run the executable (Administrator privileges required)

### Option 2: Build from Source
```bash
git clone <repository-url>
cd Wow Console
dotnet build -c Release
```
The executable will be in `bin\Release\net6.0-windows\AzerothCoreLauncher.exe`

## Requirements

- Windows 10/11
- .NET 6.0 Runtime (included in release build)
- Administrator privileges (requested on launch)
- AzerothCore server installed on your system
- MySQL server running

## Quick Start

1. **Launch** the application (run as Administrator)
2. **Configure** server paths in the Settings tab:
   - Server directory (where authserver.exe and worldserver.exe are located)
   - MySQL connection settings
   - Config directory location
3. **Save** your settings
4. **Start** servers using the Start buttons in the Server Status tab

## Features

### Server Status & Control
- **Start/Stop Control**: Launch and stop AuthServer and WorldServer with one click
- **Real-Time Status**: Visual status lights (green=running, yellow=booting, red=stopped/error)
- **Auto-Restart**: Automatically restart crashed servers with configurable limits
- **Graceful Shutdown**: Clean server shutdown to prevent data corruption
- **Process Detection**: Automatically detects and attaches to already-running server processes

### Console Management
- **Dual Console View**: Separate Auth and World console panes
- **Command Input**: Send commands directly to either server
- **Real-Time Output**: Live console output with automatic scrolling
- **Console Clearing**: Manual or automatic console clearing

### Player Management
- **Online Players**: View all currently online players
- **Player Search**: Find specific players quickly
- **GM Actions**: Execute Kick, Ban, Mute, GM On/Off commands
- **Player Details**: View inventory, skills, and character information

### Events & Automation
- **Scheduled Events**: Create scheduled commands, restarts, and announcements
- **Event Types**:
  - Command: Send any console command
  - Restart: Restart Auth or World server
  - Announcement: Send server-wide announcements
- **Recurrence**: Daily, weekly, monthly, or custom recurrence patterns
- **Event Chaining**: Chain multiple events with delays
- **Conditional Execution**: Execute events based on player count or time windows

### Account Management
- **Account Creation**: Create new accounts
- **Account Editing**: Modify account details
- **Account Bans**: Ban/unban accounts
- **Ban History**: View ban history for accounts
- **IP Bans**: Manage IP bans

### Server Analytics
- **Performance Metrics**: CPU and memory usage graphs
- **Historical Data**: View performance over time (1 min to 24 hours)
- **Peak Hours**: Identify peak usage times
- **Health Monitoring**: Real-time server health checks with configurable thresholds

### Communication
- **Announcements**: Send server-wide announcements
- **Quick Templates**: Pre-defined announcement templates
- **Broadcast History**: Track announcement history

### Notifications
- **System Tray Icon**: Minimize to system tray with status indicator
- **Crash Alerts**: Popup alerts when servers crash
- **Alert Sounds**: Audio notification on critical events
- **Notification History**: Track all notifications
- **Event Notifications**: Get notified when scheduled events execute

### Economy Management
- **Auction House**: View and manage auction house data
- **Mail System**: Send and view player mail
- **Currency Tracking**: Monitor player currency

### Skill Database
- **Skill Line Viewer**: Browse skill lines and abilities
- **Search**: Search for specific skills
- **Data Management**: Load and manage skill data from JSON files

### Config Editor
- **Config Loading**: Load and edit worldserver.conf and authserver.conf
- **Search**: Quickly find config settings
- **Save & Apply**: Save changes and restart servers to apply

### Settings
- **Server Configuration**: Paths, executables, MySQL settings
- **Server Stability**: Auto-restart, crash analysis, health monitoring
- **Database Backup**: Automatic database backups before restarts
- **Console Settings**: Line limits, auto-scroll

### Logging
- **Centralized Logging**: All logs consolidated to Debug.log
- **Log Viewer**: View and filter logs
- **Export**: Export logs for analysis

## Feature Synopsis

**Server Management**: Start/stop servers, auto-restart on crash, graceful shutdown, process detection

**Monitoring**: Real-time status lights, CPU/memory graphs, health checks, performance metrics

**Player Tools**: Online player list, search, GM actions (kick, ban, mute), detailed player info

**Automation**: Scheduled events (commands, restarts, announcements), recurrence patterns, event chaining, conditional execution

**Account Management**: Create/edit accounts, account/IP bans, ban history

**Analytics**: Historical performance data, peak hours, customizable time ranges

**Communication**: Server announcements, quick templates, broadcast history

**Notifications**: System tray icon, crash alerts, alert sounds, notification history

**Economy**: Auction house viewer, mail system, currency tracking

**Skill Database**: Skill line browser, ability viewer, JSON data management

**Config Editor**: Built-in config file editor with search

**Logging**: Centralized debug logging, log viewer with filtering and export

## Troubleshooting

**Server won't start:**
- Ensure running as Administrator
- Verify server paths in Settings
- Check server executables exist

**Can't connect to database:**
- Verify MySQL connection settings
- Ensure MySQL server is running
- Check database credentials

**No console output:**
- Console uses log file watching for AuthServer
- Ensure log files are being written
- Check logs directory exists

**Events not executing:**
- Ensure events are saved (click Save button)
- Verify time format is HH:mm (e.g., 12:00)
- Check target server is running

## License

This project is provided as-is for AzerothCore server management.
