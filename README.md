# PearlTrack

PearlTrack is a starter full-stack task management system built with ASP.NET Core 10, React, Entity Framework Core, SQL Server, Serilog, and xUnit.

## Prerequisites

- **.NET 10.0** or later
- **SQL Server** (local or remote)
- **Git** (for version control)

### Check Requirements
```bash
dotnet --version  # Should show 10.0.x
```

---

## Initial Setup

### 1. Clone Repository
```bash
git clone https://github.com/Raysh454/PearlTrack.git
cd PearlTrack
```

### 2. Restore Dependencies
```bash
dotnet restore
```

### 3. Configure Database

Edit `backend/PearlTrack.API/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=PearlTrackDb;Trusted_Connection=true;TrustServerCertificate=true;"
  },
  "Jwt": {
    "SecretKey": "your-super-secret-key-at-least-32-characters-long!!!!",
    "Issuer": "PearlTrackAPI",
    "Audience": "PearlTrackClient",
    "ExpirationMinutes": 1440
  }
}
```

### 4. Build Project
```bash
dotnet build
```

### 5. Run Tests
```bash
dotnet test
```

---

## Run Application Locally

### Start API Server (Development)
```bash
cd backend/PearlTrack.API
dotnet run
```

