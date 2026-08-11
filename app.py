"""
Hash Calculator & Comparer - Backend API + Static File Server
"""
from flask import Flask, request, jsonify, send_from_directory
from flask_cors import CORS
import hashlib
import os

app = Flask(__name__, static_folder='.', static_url_path='')
CORS(app)

UPLOAD_FOLDER = 'uploads'
os.makedirs(UPLOAD_FOLDER, exist_ok=True)

ALLOWED_HASH_TYPES = {'sha256', 'sha512'}

def compute_file_hash(file_path, hash_type='sha256'):
    """Compute hash of a file."""
    if hash_type == 'sha256':
        hasher = hashlib.sha256()
    elif hash_type == 'sha512':
        hasher = hashlib.sha512()
    else:
        raise ValueError(f"Unsupported hash type: {hash_type}")

    with open(file_path, 'rb') as f:
        while chunk := f.read(8192):
            hasher.update(chunk)

    return hasher.hexdigest()

# ===== SERVE FRONTEND =====
@app.route('/')
def serve_index():
    return send_from_directory('.', 'index.html')

@app.route('/<path:path>')
def serve_static(path):
    return send_from_directory('.', path)

# ===== API ENDPOINTS =====
@app.route('/api/hash', methods=['POST'])
def calculate_hash():
    """Calculate hash of a single uploaded file."""
    if 'file' not in request.files:
        return jsonify({'error': 'No file uploaded'}), 400

    file = request.files['file']
    hash_type = request.form.get('hash_type', 'sha256').lower()

    if hash_type not in ALLOWED_HASH_TYPES:
        return jsonify({'error': f'Invalid hash type. Allowed: {", ".join(ALLOWED_HASH_TYPES)}'}), 400

    if file.filename == '':
        return jsonify({'error': 'No file selected'}), 400

    file_path = os.path.join(UPLOAD_FOLDER, file.filename)
    file.save(file_path)

    try:
        file_hash = compute_file_hash(file_path, hash_type)
        return jsonify({
            'success': True,
            'filename': file.filename,
            'hash_type': hash_type.upper(),
            'hash': file_hash
        })
    except Exception as e:
        return jsonify({'error': str(e)}), 500
    finally:
        if os.path.exists(file_path):
            os.remove(file_path)

@app.route('/api/compare', methods=['POST'])
def compare_hashes():
    """Compare hashes of two uploaded files."""
    if 'file_a' not in request.files or 'file_b' not in request.files:
        return jsonify({'error': 'Both files required'}), 400

    file_a = request.files['file_a']
    file_b = request.files['file_b']
    hash_type = request.form.get('hash_type', 'sha256').lower()

    if hash_type not in ALLOWED_HASH_TYPES:
        return jsonify({'error': f'Invalid hash type. Allowed: {", ".join(ALLOWED_HASH_TYPES)}'}), 400

    if file_a.filename == '' or file_b.filename == '':
        return jsonify({'error': 'Both files must be selected'}), 400

    path_a = os.path.join(UPLOAD_FOLDER, file_a.filename)
    path_b = os.path.join(UPLOAD_FOLDER, file_b.filename)

    file_a.save(path_a)
    file_b.save(path_b)

    try:
        hash_a = compute_file_hash(path_a, hash_type)
        hash_b = compute_file_hash(path_b, hash_type)

        matched = hash_a == hash_b

        return jsonify({
            'success': True,
            'hash_type': hash_type.upper(),
            'file_a': {
                'filename': file_a.filename,
                'hash': hash_a
            },
            'file_b': {
                'filename': file_b.filename,
                'hash': hash_b
            },
            'matched': matched,
            'result': 'MATCHED ✅' if matched else 'MISMATCHED ❌'
        })
    except Exception as e:
        return jsonify({'error': str(e)}), 500
    finally:
        for p in [path_a, path_b]:
            if os.path.exists(p):
                os.remove(p)

@app.route('/api/health', methods=['GET'])
def health_check():
    return jsonify({'status': 'ok'})

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=True)
