# SessionGuard Project - Development Instructions

## Project Overview
SessionGuard is a Windows Service application (.NET 10) that manages user sessions with the following features:
- Startup time tracking
- Scheduled logout based on configured time windows
- Automatic logout after specified uptime duration
- Daily reset of continuous uptime tracking

## Checklist Progress

- [x] Project scaffolded with .NET 10 Worker Service template
- [x] Customize the Project - Add session management features
- [x] Install Required Extensions (N/A - No special extensions needed)
- [x] Compile the Project - Successfully built with .NET 10
- [x] Create and Run Task (Ready for Windows Service deployment)
- [x] Ensure Documentation is Complete

## Project Structure
```
SessionGuard/
├── SessionGuard.csproj
├── Program.cs
├── Worker.cs
├── SessionConfig.cs
├── SessionInfo.cs
├── SessionManager.cs
├── LogoutHandler.cs
├── Properties/
├── appsettings.json
├── appsettings.Development.json
├── README.md
└── .github/
    └── copilot-instructions.md
```

## Implementation Summary

### Core Components

1. **SessionConfig.cs**
   - Configuration class for session management settings
   - Properties: LogoutStartTime, LogoutEndTime, MaxContinuousUptime, EnableLogout, CheckInterval

2. **SessionInfo.cs**
   - Maintains session state information
   - Tracks startup time, current uptime, day changes
   - Provides daily reset functionality

3. **SessionManager.cs**
   - Core business logic for session management
   - Accepts SessionConfig as parameter to ShouldLogout() for real-time setting updates
   - Determines logout conditions based on:
     - Configured time window (e.g., 18:00-09:00)
     - Maximum continuous uptime duration (e.g., 1 hour)
     - Daily date changes

4. **LogoutHandler.cs**
   - Executes system logout operations
   - Displays warning messages to users
   - Handles Windows logout commands

5. **Worker.cs**
   - Background service implementation using BackgroundService
   - Uses IOptionsMonitor<SessionConfig> for real-time configuration monitoring
   - Automatically picks up configuration file changes without service restart
   - Periodic checking of logout conditions
   - Integration of SessionManager and LogoutHandler

### Real-time Configuration Updates

The application uses `IOptionsMonitor<SessionConfig>` pattern:
- Configuration changes in `appsettings.json` are automatically detected
- No service restart required for setting updates
- `OnConfigChanged` callback logs configuration updates
- Each logout check uses the current configuration values

## Build Status
✅ Project builds successfully with no errors
✅ All dependencies resolved (.NET 10 SDK)
✅ Ready for deployment as Windows Service

## Windows Service Installation

### Install as Service
```powershell
sc.exe create SessionGuard binPath="C:\wk\repos\SessionGuard\SessionGuard\bin\Release\net10.0\SessionGuard.exe"
net start SessionGuard
```

### Uninstall Service
```powershell
net stop SessionGuard
sc.exe delete SessionGuard
```

## Configuration Guide

Edit `appsettings.json` to customize behavior:
- **LogoutStartTime**: Time window start (24h format, e.g., "22:00")
- **LogoutEndTime**: Time window end (e.g., "08:00")
- **MaxContinuousUptime**: Maximum hours before forced logout (e.g., 8)
- **EnableLogout**: Toggle logout functionality (true/false)
- **CheckInterval**: Status check interval in seconds (e.g., 60)

## Next Steps for Development

1. **Unit Testing**: Create unit tests for SessionManager and LogoutHandler
2. **Integration Testing**: Test Windows Service installation and lifecycle
3. **Logging Enhancement**: Consider Windows Event Log integration
4. **Configuration UI**: Create configuration utility for non-technical users
5. **Deployment**: Package as MSI installer for production use

