# 🔍 Detective-Shelby

**AI vs Real Image Forensic Analyzer**

Multi-dimensional forensic analysis tool deployed as a web service.

## Quick Deploy

### Render (Recommended - Free Tier)
1. Fork/connect this repo to Render
2. Create new Web Service → Python 3
3. Build Command: `pip install -r requirements.txt`
4. Start Command: `gunicorn server:app`
5. Done — auto-deploys on every push

### Railway
1. Connect GitHub repo on Railway
2. Railway auto-detects Python + Procfile
3. Click Deploy

### Heroku
```bash
git push heroku Detective-Shelby:main
```

### Local
```bash
cd Detective-Shelby
pip install -r requirements.txt
python server.py
# Open http://localhost:5000
```

## Analysis Modules
- Metadata (EXIF, camera info, software tags)
- JPEG Forensics (Q-tables, compression, double-compression)
- Noise Analysis (wavelet residuals, spatial variance)
- Frequency Analysis (2D FFT, spectral slope)
- Color Statistics (RGB correlations, saturation)
- Resampling Detection
- Local Patch Analysis (heatmap)
- Texture Analysis (LBP, GLCM)
- Edge Analysis

## API Endpoint
```
POST /api/analyze
Content-Type: multipart/form-data
Body: image=<file>

Response: JSON with scores, heatmaps, verdict
```
