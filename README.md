# Portfolio Message API

## Render Setup

### Environment Variables
- `ADMIN_USERNAME` = changeme
- `ADMIN_PASSWORD` = changeme
- `GOOGLE_APPLICATION_CREDENTIALS` = /etc/secrets/adminsdk.json

### Secret Files
Upload `adminsdk.json` as a secret file at `/etc/secrets/adminsdk.json`

### Build & Start
- Build Command: `pip install -r requirements.txt`
- Start Command: `uvicorn main:app --host 0.0.0.0 --port $PORT`

### Endpoints
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | /api/messages | No | Submit contact form |
| GET | /api/admin/messages | Basic | List all messages |
| GET | /api/admin/messages/{id} | Basic | Get single message |
| PATCH | /api/admin/messages/{id} | Basic | Toggle read/starred |
| DELETE | /api/admin/messages/{id} | Basic | Delete message |
| GET | / | No | Health + status |
| GET | /health | No | Health check |
| GET | /debug | No | Debug info (no sensitive data) |
