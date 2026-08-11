// ===== CONFIG =====
const API_BASE = '/api';

// ===== VIEW NAVIGATION =====
function showView(viewId) {
    document.querySelectorAll('.view').forEach(v => v.classList.remove('active'));
    document.getElementById(viewId + '-view').classList.add('active');

    // Hide results when switching views
    document.getElementById('calc-result').hidden = true;
    document.getElementById('compare-result').hidden = true;
}

// ===== FILE SELECT =====
function onFileSelect(input, filenameId) {
    const file = input.files[0];
    const el = document.getElementById(filenameId);
    const dropzone = input.closest('.upload-area');

    if (file) {
        el.textContent = file.name;
        dropzone.classList.add('has-file');
    } else {
        el.textContent = 'No file selected';
        dropzone.classList.remove('has-file');
    }
}

// ===== DRAG & DROP =====
function setupDragDrop() {
    const dropzones = document.querySelectorAll('.upload-area');

    dropzones.forEach(zone => {
        zone.addEventListener('dragover', (e) => {
            e.preventDefault();
            zone.classList.add('dragover');
        });

        zone.addEventListener('dragleave', () => {
            zone.classList.remove('dragover');
        });

        zone.addEventListener('drop', (e) => {
            e.preventDefault();
            zone.classList.remove('dragover');

            const files = e.dataTransfer.files;
            if (files.length > 0) {
                const input = zone.querySelector('input[type="file"]');
                const dt = new DataTransfer();
                dt.items.add(files[0]);
                input.files = dt.files;

                const filenameId = zone.querySelector('.file-name').id;
                onFileSelect(input, filenameId);
            }
        });
    });
}

// ===== CALCULATE HASH =====
async function calculateHash() {
    const fileInput = document.getElementById('calc-file');
    const hashType = document.getElementById('calc-hash-type').value;
    const btn = document.getElementById('calc-btn');
    const resultBox = document.getElementById('calc-result');

    if (!fileInput.files[0]) {
        showToast('Please select a file first!', 'error');
        return;
    }

    setLoading(btn, true);
    resultBox.hidden = true;

    const formData = new FormData();
    formData.append('file', fileInput.files[0]);
    formData.append('hash_type', hashType);

    try {
        const res = await fetch(`${API_BASE}/hash`, {
            method: 'POST',
            body: formData
        });

        const data = await res.json();

        if (data.success) {
            document.getElementById('calc-result-filename').textContent = data.filename;
            document.getElementById('calc-result-type').textContent = data.hash_type;
            document.getElementById('calc-hash-value').textContent = data.hash;
            resultBox.hidden = false;
            showToast('Hash calculated successfully!');
        } else {
            showToast(data.error || 'Failed to calculate hash', 'error');
        }
    } catch (err) {
        showToast('Server error. Is the backend running?', 'error');
    } finally {
        setLoading(btn, false);
    }
}

// ===== COMPARE HASHES =====
async function compareHashes() {
    const fileA = document.getElementById('compare-file-a');
    const fileB = document.getElementById('compare-file-b');
    const hashType = document.getElementById('compare-hash-type').value;
    const btn = document.getElementById('compare-btn');
    const resultBox = document.getElementById('compare-result');

    if (!fileA.files[0] || !fileB.files[0]) {
        showToast('Please select both files!', 'error');
        return;
    }

    setLoading(btn, true);
    resultBox.hidden = true;

    const formData = new FormData();
    formData.append('file_a', fileA.files[0]);
    formData.append('file_b', fileB.files[0]);
    formData.append('hash_type', hashType);

    try {
        const res = await fetch(`${API_BASE}/compare`, {
            method: 'POST',
            body: formData
        });

        const data = await res.json();

        if (data.success) {
            const badge = document.getElementById('match-badge');
            badge.textContent = data.result;
            badge.className = 'match-badge ' + (data.matched ? 'matched' : 'mismatched');

            document.getElementById('compare-result-filename-a').textContent = data.file_a.filename;
            document.getElementById('compare-result-filename-b').textContent = data.file_b.filename;
            document.getElementById('compare-hash-a').textContent = data.file_a.hash;
            document.getElementById('compare-hash-b').textContent = data.file_b.hash;

            resultBox.hidden = false;
            showToast(data.matched ? 'Files match! ✅' : 'Files do not match ❌');
        } else {
            showToast(data.error || 'Comparison failed', 'error');
        }
    } catch (err) {
        showToast('Server error. Is the backend running?', 'error');
    } finally {
        setLoading(btn, false);
    }
}

// ===== UTILITIES =====
function setLoading(btn, loading) {
    const text = btn.querySelector('.btn-text');
    const spinner = btn.querySelector('.spinner');

    btn.disabled = loading;
    text.hidden = loading;
    spinner.hidden = !loading;
}

function copyToClipboard(elementId) {
    const text = document.getElementById(elementId).textContent;
    navigator.clipboard.writeText(text).then(() => {
        showToast('Hash copied to clipboard!');
    });
}

function showToast(message, type = 'success') {
    const toast = document.getElementById('toast');
    toast.textContent = message;
    toast.style.borderColor = type === 'error' ? 'var(--danger)' : 'var(--success)';
    toast.hidden = false;

    requestAnimationFrame(() => toast.classList.add('show'));

    setTimeout(() => {
        toast.classList.remove('show');
        setTimeout(() => toast.hidden = true, 300);
    }, 3000);
}

// ===== INIT =====
document.addEventListener('DOMContentLoaded', () => {
    setupDragDrop();
});
