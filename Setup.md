# Setup Guide — Hash Calculator & Comparer

## Prerequisites

- Python 3.8+
- pip (Python package manager)

## Installation

### 1. Clone or download the project

```bash
cd hash-tool
```

### 2. Create a virtual environment (recommended)

```bash
python -m venv venv
```

### 3. Activate the virtual environment

**Windows:**
```bash
venv\Scripts\activate
```

**macOS / Linux:**
```bash
source venv/bin/activate
```

### 4. Install dependencies

```bash
pip install flask flask-cors
```

### 5. Start the backend server

```bash
python app.py
```

The server will start on `http://localhost:5000`

### 6. Open the frontend

Simply open `index.html` in your web browser, or serve it with any static file server:

```bash
# Option A: Direct open (file:// protocol)
# Just double-click index.html

# Option B: Using Python's built-in server
python -m http.server 8080
# Then visit http://localhost:8080
```

> Note: If you open `index.html` directly via `file://`, some browsers may block CORS requests. Using a local server (Option B) is recommended.

## Directory Structure

```
hash-tool/
├── app.py          # Flask backend API
├── index.html      # Frontend UI
├── style.css       # Styles
├── script.js       # Frontend logic
├── README.md       # Project docs
├── Setup.md        # This file
└── LICENSE         # MIT License
```

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/hash` | POST | Calculate hash of a single file |
| `/api/compare` | POST | Compare hashes of two files |
| `/api/health` | GET | Health check |

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `ModuleNotFoundError` | Run `pip install flask flask-cors` |
| CORS errors | Ensure backend is running on port 5000 |
| Port 5000 in use | Change port in `app.py` or kill the process using it |
