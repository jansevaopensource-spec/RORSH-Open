# Installation Guide

## Server (SecureCom.js)

### Prerequisites
- Node.js 18+
- npm or yarn

### Steps
1. Clone repository:
   ```bash
   git clone https://github.com/jansevaopensource-spec/RORSH-Open.git
   cd RORSH-Open/SecureCom.js
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

3. Configure environment:
   ```bash
   cp .env.example .env
   # Edit .env with your credentials
   ```

4. Generate encryption key:
   ```bash
   node -e "console.log(require('crypto').randomBytes(32).toString('hex'))"
   ```

5. Start server:
   ```bash
   npm start
   ```

## Admin (RAS)

### From Release
1. Download latest release from GitHub
2. Extract RAS.exe (Windows) or RAS (Linux)
3. Run the binary

### From Source
```bash
cd RAS
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Client (RCS)

### Windows
```powershell
# Run PowerShell as Administrator (optional)
.\installers\install-rcs.ps1
```

### Linux
```bash
chmod +x installers/install-rcs.sh
./installers/install-rcs.sh
```

### From Source
```bash
cd RCS
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

## Verification

1. Start server
2. Start RCS on client machine
3. Start RAS on admin machine
4. Run `RAS-Start` and authenticate
5. Run `c-list` to see connected client
6. Run `get-connect @rorshkey` to connect
7. Type commands to execute on client
