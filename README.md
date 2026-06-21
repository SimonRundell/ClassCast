# ClassCast — Classroom Broadcast & Control System

ClassCast is a Windows-native classroom management system for school LANs. It
replaces third-party tools such as Imperio. It consists of a **Teacher Server**
and a **Student Client**, communicating over a local Ethernet subnet with no
internet access and no external servers.

* **Author:** Simon Rundell / CodeMonkey Design Ltd.
* **Version:** 1.0.0
* **Platform:** Windows 10 / 11 (x64), .NET 8
* **License:** [CC BY-NC-SA 4.0](LICENSE)

---

## Features

* **Automatic discovery** of student PCs via UDP beacon (no manual configuration).
* **Live thumbnails** (up to 1 fps) of every student screen in a control grid.
* **Screen broadcast** of the teacher's screen at up to 15 fps / 854×480, shown
  full-screen on every student machine.
* **Keyboard & mouse lockout** of individual students or the whole class.
* **Remote logoff** of individual students or the whole class.
* **Active Directory authentication** of the teacher before any control is possible.

---

## Solution layout

```
ClassCast/
├─ ClassCast.sln
├─ ClassCast.Common/      Shared protocol, networking, media, config, logging
├─ ClassCast.Teacher/     Teacher Server (WinForms) + bundled ffmpeg/
├─ ClassCast.Student/     Student Client (tray app, no main window)
└─ ClassCast.Tests/       xUnit unit tests
```

See the Agent Specification in `design/` for the full design.

### Network ports

| Port            | Purpose                                          |
|-----------------|--------------------------------------------------|
| UDP 45678       | Student discovery beacon / teacher ACK           |
| TCP 45679       | Control channel (commands, thumbnails, heartbeat)|
| TCP 45680       | Broadcast video stream (teacher → all students)  |

On first run each application attempts to create the required inbound Windows
Firewall rules (`ClassCast-UDP-In`, `ClassCast-Control-In`,
`ClassCast-Broadcast-In`) for the Domain and Private profiles. If it lacks the
privileges to do so it logs a warning and shows a one-time message listing the
ports to open manually.

---

## Prerequisites

* Windows 10 / 11 (x64)
* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (build) or the
  .NET 8 Desktop Runtime (run only)
* Visual Studio 2022+ (optional, for development)

---

## FFmpeg

The Teacher Server uses **FFmpeg** to encode its screen to MJPEG.
The Student Client does **not** require FFmpeg (it decodes JPEG frames natively).

**FFmpeg is not included in this repository** as it is a widely available binary.
A copy of `ffmpeg.exe` must be placed at:

```
ClassCast.Teacher/ffmpeg/ffmpeg.exe
```

If it is missing, download a static Windows build and extract just `ffmpeg.exe`
into that folder:

> https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip

The path is configurable via `ffmpegPath` in the Teacher `config.json`.

---

## Build

```powershell
dotnet build ClassCast.sln -c Release
```

The build should complete with **zero errors and zero warnings**.

## Test

```powershell
dotnet test ClassCast.sln -c Release
```

Unit tests cover frame read/write round-tripping, config parsing, protocol
serialisation, and FFmpeg JPEG output. The FFmpeg test is skipped automatically
if `ffmpeg.exe` is not present.

## Run

> The applications require a Windows machine joined to an Active Directory domain
> for full functionality. See **Testing** below for a VM lab setup.

**Teacher:**

```powershell
dotnet run --project ClassCast.Teacher -c Release
```

Sign in with an AD account. If the configured `adTeacherGroup` does not exist,
any valid AD account is accepted (fail-open for first deployment).

**Student (on each student PC):**

```powershell
dotnet run --project ClassCast.Student -c Release
```

The Student Client runs in the system tray with no main window. Exiting it
requires a local administrator or AD credential, to stop students closing it.

---

## Configuration

Both applications read `config.json` from their executable directory.

**Teacher `config.json`:**

```json
{
  "adDomain":          "SCHOOL",
  "adTeacherGroup":    "ClassCast-Teachers",
  "udpDiscoveryPort":  45678,
  "tcpControlPort":    45679,
  "tcpBroadcastPort":  45680,
  "broadcastWidth":    854,
  "broadcastHeight":   480,
  "broadcastFps":      15,
  "thumbnailFps":      1,
  "thumbnailWidth":    320,
  "thumbnailHeight":   180,
  "ffmpegPath":        "ffmpeg\\ffmpeg.exe"
}
```

**Student `config.json`:**

```json
{
  "udpDiscoveryPort":  45678,
  "tcpControlPort":    45679,
  "tcpBroadcastPort":  45680
}
```

Logs are written to a `logs/` folder beside each executable, one file per day.

---

## Testing (VM lab)

Full end-to-end testing requires:

* Windows Server 2022 VM as the Active Directory Domain Controller (e.g. domain
  `SCHOOL`).
* A Windows 11 VM for the teacher (ClassCast.Teacher, domain-joined).
* One or more Windows 10/11 VMs for students (ClassCast.Student, domain-joined).
* All VMs on the same virtual LAN (host-only or bridged in Hyper-V / VirtualBox).

---

## Security notes (v1.0)

* Traffic is **plaintext** JSON/JPEG over the wire. This is acceptable on a wired
  school LAN; TLS (`SslStream`) is planned for a later version.
* A student client only obeys the **first** Teacher Server IP it connects to for
  the session, and discards out-of-order (replayed) control messages.
* Logoff and lock require an authenticated teacher.

---

## License

This project is released under the **Creative Commons
Attribution-NonCommercial-ShareAlike 4.0 International** license
(CC BY-NC-SA 4.0). See [LICENSE](LICENSE).

© Simon Rundell / CodeMonkey Design Ltd.
