# DuckovTogether Server

A headless dedicated server solution for multiplayer gaming experiences.

## Overview

DuckovTogether Server is an **independently developed** dedicated server application. This project is created from scratch and is **not affiliated with, derived from, or based on any other project or codebase**.

This software is developed independently by tlkppm and contributors.

## Features

- **Headless Operation** - Deploy on cloud servers without display requirements
- **Auto Resource Detection** - Automatic game path detection via Windows Registry and Steam libraries
- **Data Validation** - Built-in validation layer for data integrity
- **State Synchronization** - Efficient synchronization for players, AI entities, and items
- **Configurable** - Flexible server configuration options

## System Requirements

| Requirement | Specification |
|-------------|---------------|
| Operating System | Windows 10/11, Windows Server 2019+ |
| Runtime | .NET 8.0 |
| Network | Open port (default: 9050) |

## Installation

1. Download the latest release from the Releases page
2. Extract to your preferred directory
3. Run `DuckovTogetherServer.exe`

The server will automatically detect the game installation path.

## Configuration

Edit `server_config.json`:

```json
{
  "Port": 9050,
  "MaxPlayers": 4,
  "ServerName": "DuckovTogether Server",
  "GamePath": "",
  "TickRate": 60
}
```

| Parameter | Description | Default |
|-----------|-------------|---------|
| Port | Server listening port | 9050 |
| MaxPlayers | Maximum concurrent players | 4 |
| ServerName | Server display name | DuckovTogether Server |
| GamePath | Game installation path (auto-detect if empty) | "" |
| TickRate | Server tick rate (Hz) | 60 |

## Command Line Arguments

```bash
DuckovTogetherServer.exe --port=9050 --max-players=4 --game-path="C:\Games\Duckov"
```

## Building from Source

```bash
git clone https://github.com/tlkppm/DuckovTogether-Server.git
cd DuckovTogether-Server
dotnet build -c Release
```

## Legal Notice

### Independence Statement

This project is an **independent work** created from scratch. It is:

- NOT a fork, derivative, or modification of any existing project
- NOT affiliated with or endorsed by any game developer or publisher
- NOT based on any other codebase or project

### License

This project is licensed under a custom non-commercial license. See [LICENSE](LICENSE) for details.

Key terms:
- Personal, non-commercial use permitted
- Modification permitted with attribution
- Free distribution permitted
- Commercial use prohibited
- Monetization prohibited

### Disclaimer

This software is provided "AS IS" without warranty of any kind. Users assume all risks associated with its use. See [DISCLAIMER.md](DISCLAIMER.md) for complete terms.

## Support

For issues and feature requests, please use GitHub Issues.

---

Copyright (c) 2025 tlkppm. All rights reserved.
