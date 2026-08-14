"""
AI vs Real Image Forensic Analyzer - Production Server
Deploys to Render, Railway, Heroku, or any WSGI-compatible platform
"""

import os
from flask import Flask, request, jsonify, send_from_directory
from flask_cors import CORS
import uuid
from analyzer import ForensicAnalyzer

app = Flask(__name__)
CORS(app)

# Upload folder (temp, cleaned after analysis)
UPLOAD_FOLDER = os.path.join(os.path.dirname(__file__), 'uploads')
os.makedirs(UPLOAD_FOLDER, exist_ok=True)

analyzer = ForensicAnalyzer()

@app.route('/')
def serve_frontend():
    return send_from_directory('static', 'index.html')

@app.route('/<path:path>')
def serve_static(path):
    return send_from_directory('static', path)

@app.route('/api/analyze', methods=['POST'])
def analyze_image():
    if 'image' not in request.files:
        return jsonify({'error': 'No image provided'}), 400

    file = request.files['image']
    if file.filename == '':
        return jsonify({'error': 'No file selected'}), 400

    ext = os.path.splitext(file.filename)[1].lower()
    if ext not in ['.jpg', '.jpeg', '.png', '.bmp', '.tiff', '.webp']:
        return jsonify({'error': 'Unsupported file format'}), 400

    file_id = str(uuid.uuid4())
    filepath = os.path.join(UPLOAD_FOLDER, f"{file_id}{ext}")
    file.save(filepath)

    try:
        results = analyzer.analyze(filepath)
        try:
            os.remove(filepath)
        except:
            pass
        return jsonify(results)
    except Exception as e:
        try:
            os.remove(filepath)
        except:
            pass
        return jsonify({'error': str(e)}), 500

@app.route('/api/health', methods=['GET'])
def health():
    return jsonify({'status': 'ok', 'service': 'Detective-Shelby Forensic Analyzer'})

# For local development
if __name__ == '__main__':
    port = int(os.environ.get('PORT', 5000))
    app.run(host='0.0.0.0', port=port, debug=False)
