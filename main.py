import os
import sys
import json
import logging
from datetime import datetime
from typing import Optional, List
from fastapi import FastAPI, HTTPException, Depends, status, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.security import HTTPBasic, HTTPBasicCredentials
from pydantic import BaseModel, Field, validator
from secrets import compare_digest
import re

# ── Logging Setup ──────────────────────────────────────────────
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s | %(levelname)s | %(message)s",
    stream=sys.stdout
)
logger = logging.getLogger("portfolio-api")

logger.info("=" * 60)
logger.info("Starting Karan Shelby Portfolio API")
logger.info("=" * 60)

# ── Firebase Init ──────────────────────────────────────────────
db = None
firebase_initialized = False
FIREBASE_CRED_PATH = os.environ.get("GOOGLE_APPLICATION_CREDENTIALS", "adminsdk.json")

logger.info(f"[FIREBASE] Credential path from env: {FIREBASE_CRED_PATH}")
logger.info(f"[FIREBASE] Absolute path: {os.path.abspath(FIREBASE_CRED_PATH)}")
logger.info(f"[FIREBASE] File exists: {os.path.exists(FIREBASE_CRED_PATH)}")

# Debug: list /etc/secrets
if os.path.exists("/etc/secrets"):
    try:
        secrets_contents = os.listdir("/etc/secrets")
        logger.info(f"[FIREBASE] /etc/secrets contents: {secrets_contents}")
    except Exception as e:
        logger.error(f"[FIREBASE] Cannot list /etc/secrets: {e}")
else:
    logger.warning("[FIREBASE] /etc/secrets directory does NOT exist")

# Debug: list current working directory
logger.info(f"[FIREBASE] CWD: {os.getcwd()}")
try:
    cwd_contents = os.listdir(".")
    logger.info(f"[FIREBASE] CWD contents: {cwd_contents}")
except Exception as e:
    logger.error(f"[FIREBASE] Cannot list CWD: {e}")

# Try to initialize Firebase
try:
    if os.path.exists(FIREBASE_CRED_PATH):
        with open(FIREBASE_CRED_PATH, "r") as f:
            cred_content = f.read()
        logger.info(f"[FIREBASE] File read successfully, size: {len(cred_content)} bytes")

        # Validate JSON
        cred_json = json.loads(cred_content)
        logger.info(f"[FIREBASE] Valid JSON. Top-level keys: {list(cred_json.keys())}")

        # Check required fields
        required_keys = ["type", "project_id", "private_key_id", "private_key", "client_email"]
        missing = [k for k in required_keys if k not in cred_json]
        if missing:
            logger.error(f"[FIREBASE] MISSING required keys: {missing}")
            raise ValueError(f"Missing keys in service account: {missing}")

        logger.info(f"[FIREBASE] All required keys present. Project: {cred_json.get('project_id')}")
        logger.info(f"[FIREBASE] Client email: {cred_json.get('client_email')}")

        # Initialize Firebase Admin
        import firebase_admin
        from firebase_admin import credentials, firestore

        cred_obj = credentials.Certificate(FIREBASE_CRED_PATH)
        firebase_admin.initialize_app(cred_obj)
        db = firestore.client()
        firebase_initialized = True
        logger.info("[FIREBASE] Firebase Admin SDK initialized SUCCESSFULLY")
    else:
        logger.error(f"[FIREBASE] CRITICAL: Service account file NOT FOUND at {FIREBASE_CRED_PATH}")
        logger.error("[FIREBASE] Please upload adminsdk.json as a secret file to /etc/secrets/adminsdk.json")
        logger.error("[FIREBASE] And set GOOGLE_APPLICATION_CREDENTIALS=/etc/secrets/adminsdk.json")

except json.JSONDecodeError as e:
    logger.error(f"[FIREBASE] CRITICAL: File is not valid JSON: {e}")
except Exception as e:
    logger.error(f"[FIREBASE] CRITICAL: Failed to initialize Firebase: {type(e).__name__}: {e}")

MESSAGES_COLLECTION = "messages"

# ── FastAPI App ────────────────────────────────────────────────
app = FastAPI(
    title="Karan Shelby Portfolio API",
    version="1.0.0",
    docs_url="/docs" if os.environ.get("ENV") != "production" else None,
    redoc_url="/redoc" if os.environ.get("ENV") != "production" else None,
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# ── Auth ───────────────────────────────────────────────────────
security = HTTPBasic()

ADMIN_USERNAME = os.environ.get("ADMIN_USERNAME", "changeme")
ADMIN_PASSWORD = os.environ.get("ADMIN_PASSWORD", "changeme")

def verify_admin(credentials: HTTPBasicCredentials = Depends(security)):
    is_user = compare_digest(credentials.username, ADMIN_USERNAME)
    is_pass = compare_digest(credentials.password, ADMIN_PASSWORD)
    if not (is_user and is_pass):
        logger.warning(f"[AUTH] Failed login attempt for user: {credentials.username}")
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid credentials",
            headers={"WWW-Authenticate": "Basic"},
        )
    logger.info(f"[AUTH] Successful login for user: {credentials.username}")
    return credentials.username

# ── Models ─────────────────────────────────────────────────────
class MessageIn(BaseModel):
    name: str = Field(..., min_length=1, max_length=100)
    email: str = Field(..., min_length=1, max_length=200)
    subject: str = Field(..., min_length=1, max_length=200)
    message: str = Field(..., min_length=1, max_length=5000)

    @validator("email")
    def validate_email(cls, v):
        pattern = r"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"
        if not re.match(pattern, v):
            raise ValueError("Invalid email format")
        return v

class MessageUpdate(BaseModel):
    read: Optional[bool] = None
    starred: Optional[bool] = None

class MessageOut(BaseModel):
    id: str
    name: str
    email: str
    subject: str
    message: str
    read: bool
    starred: bool
    created_at: str

# ── Middleware: Log all requests ───────────────────────────────
@app.middleware("http")
async def log_requests(request: Request, call_next):
    logger.info(f"[REQUEST] {request.method} {request.url.path} from {request.client.host if request.client else 'unknown'}")
    response = await call_next(request)
    logger.info(f"[RESPONSE] {request.method} {request.url.path} -> {response.status_code}")
    return response

# ── Public: Submit Message ─────────────────────────────────────
@app.post("/api/messages", status_code=status.HTTP_201_CREATED)
async def create_message(msg: MessageIn):
    if not firebase_initialized or db is None:
        logger.error("[API] POST /api/messages failed: Database not initialized")
        raise HTTPException(
            status_code=503,
            detail="Service temporarily unavailable. Database not initialized. Please check server logs."
        )

    try:
        doc_ref = db.collection(MESSAGES_COLLECTION).document()
        data = {
            "name": msg.name,
            "email": msg.email,
            "subject": msg.subject,
            "message": msg.message,
            "read": False,
            "starred": False,
            "created_at": datetime.utcnow().isoformat(),
        }
        doc_ref.set(data)
        logger.info(f"[API] Message created: id={doc_ref.id}, from={msg.email}")
        return {"success": True, "id": doc_ref.id, "message": "Message received!"}
    except Exception as e:
        logger.error(f"[API] Failed to create message: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to save message: {str(e)}")

# ── Admin: List Messages ───────────────────────────────────────
@app.get("/api/admin/messages")
async def list_messages(_: str = Depends(verify_admin)):
    if not firebase_initialized or db is None:
        raise HTTPException(status_code=503, detail="Database not initialized")

    try:
        docs = db.collection(MESSAGES_COLLECTION).order_by("created_at", direction=firestore.Query.DESCENDING).stream()
        messages = []
        for doc in docs:
            d = doc.to_dict()
            messages.append(MessageOut(
                id=doc.id,
                name=d.get("name", ""),
                email=d.get("email", ""),
                subject=d.get("subject", ""),
                message=d.get("message", ""),
                read=d.get("read", False),
                starred=d.get("starred", False),
                created_at=d.get("created_at", ""),
            ))
        logger.info(f"[API] Listed {len(messages)} messages")
        return messages
    except Exception as e:
        logger.error(f"[API] Failed to list messages: {e}")
        raise HTTPException(status_code=500, detail=str(e))

# ── Admin: Get Single Message ──────────────────────────────────
@app.get("/api/admin/messages/{msg_id}")
async def get_message(msg_id: str, _: str = Depends(verify_admin)):
    if not firebase_initialized or db is None:
        raise HTTPException(status_code=503, detail="Database not initialized")

    doc = db.collection(MESSAGES_COLLECTION).document(msg_id).get()
    if not doc.exists:
        raise HTTPException(status_code=404, detail="Message not found")
    d = doc.to_dict()
    return MessageOut(
        id=doc.id,
        name=d.get("name", ""),
        email=d.get("email", ""),
        subject=d.get("subject", ""),
        message=d.get("message", ""),
        read=d.get("read", False),
        starred=d.get("starred", False),
        created_at=d.get("created_at", ""),
    )

# ── Admin: Update Message ──────────────────────────────────────
@app.patch("/api/admin/messages/{msg_id}")
async def update_message(msg_id: str, update: MessageUpdate, _: str = Depends(verify_admin)):
    if not firebase_initialized or db is None:
        raise HTTPException(status_code=503, detail="Database not initialized")

    doc_ref = db.collection(MESSAGES_COLLECTION).document(msg_id)
    doc = doc_ref.get()
    if not doc.exists:
        raise HTTPException(status_code=404, detail="Message not found")

    patch = {}
    if update.read is not None:
        patch["read"] = update.read
    if update.starred is not None:
        patch["starred"] = update.starred

    if patch:
        doc_ref.update(patch)
        logger.info(f"[API] Updated message {msg_id}: {patch}")

    return {"success": True, "message": "Updated"}

# ── Admin: Delete Message ──────────────────────────────────────
@app.delete("/api/admin/messages/{msg_id}")
async def delete_message(msg_id: str, _: str = Depends(verify_admin)):
    if not firebase_initialized or db is None:
        raise HTTPException(status_code=503, detail="Database not initialized")

    doc_ref = db.collection(MESSAGES_COLLECTION).document(msg_id)
    doc = doc_ref.get()
    if not doc.exists:
        raise HTTPException(status_code=404, detail="Message not found")
    doc_ref.delete()
    logger.info(f"[API] Deleted message {msg_id}")
    return {"success": True, "message": "Deleted"}

# ── Health Checks ──────────────────────────────────────────────
@app.get("/")
async def root():
    return {
        "status": "ok",
        "service": "Karan Shelby Portfolio API",
        "version": "1.0.0",
        "firebase_initialized": firebase_initialized,
        "db_ready": db is not None,
        "timestamp": datetime.utcnow().isoformat(),
    }

@app.get("/health")
async def health():
    checks = {
        "status": "healthy" if firebase_initialized else "degraded",
        "firebase_initialized": firebase_initialized,
        "db_ready": db is not None,
        "timestamp": datetime.utcnow().isoformat(),
    }
    if not firebase_initialized:
        checks["warning"] = "Firebase not initialized. Check GOOGLE_APPLICATION_CREDENTIALS and adminsdk.json secret file."
    return checks

@app.get("/debug")
async def debug():
    """Debug endpoint - shows environment info (no sensitive data)"""
    env_info = {
        "cwd": os.getcwd(),
        "cwd_contents": os.listdir(".") if os.path.exists(".") else [],
        "firebase_cred_path": FIREBASE_CRED_PATH,
        "firebase_cred_exists": os.path.exists(FIREBASE_CRED_PATH),
        "firebase_initialized": firebase_initialized,
        "db_ready": db is not None,
        "secrets_dir_exists": os.path.exists("/etc/secrets"),
    }
    if os.path.exists("/etc/secrets"):
        try:
            env_info["secrets_contents"] = os.listdir("/etc/secrets")
        except:
            env_info["secrets_contents"] = "cannot_list"
    return env_info

logger.info("[INIT] FastAPI app configured and ready")
