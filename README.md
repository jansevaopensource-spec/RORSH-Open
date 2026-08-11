# 🔐 Hash Calculator & Comparer

A clean, modern web tool to calculate and compare file hashes using **SHA-256** and **SHA-512** algorithms.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Python](https://img.shields.io/badge/python-3.8+-green.svg)

## ✨ Features

- **Hash Calculator** — Upload any file and get its SHA-256 or SHA-512 hash
- **Hash Comparer** — Upload two files and instantly see if their hashes match
- **Drag & Drop** — Simply drag files into the upload area
- **Copy to Clipboard** — One-click copy of computed hashes
- **Responsive Design** — Works on desktop, tablet, and mobile
- **Dark Theme** — Easy on the eyes

## 🚀 Quick Start

```bash
# 1. Install dependencies
pip install flask flask-cors

# 2. Start the backend
python app.py

# 3. Open index.html in your browser
# Or use: python -m http.server 8080
```

See [Setup.md](Setup.md) for detailed instructions.

## 🖥️ Screenshots

### Home Screen
Choose between Hash Calculator or Hash Comparer.

### Hash Calculator
Upload a file, select SHA-256 or SHA-512, and get the hash instantly.

### Hash Comparer
Upload two files, compare their hashes, and see if they match with a clear ✅ / ❌ result.

## 🛠️ Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | HTML5, CSS3, Vanilla JavaScript |
| Backend | Python, Flask |
| CORS | flask-cors |

## 📡 API Reference

### Calculate Hash
```http
POST /api/hash
Content-Type: multipart/form-data

file: <binary>
hash_type: sha256 | sha512
```

**Response:**
```json
{
  "success": true,
  "filename": "example.txt",
  "hash_type": "SHA-256",
  "hash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
}
```

### Compare Hashes
```http
POST /api/compare
Content-Type: multipart/form-data

file_a: <binary>
file_b: <binary>
hash_type: sha256 | sha512
```

**Response:**
```json
{
  "success": true,
  "hash_type": "SHA-256",
  "file_a": { "filename": "file1.txt", "hash": "abc123..." },
  "file_b": { "filename": "file2.txt", "hash": "abc123..." },
  "matched": true,
  "result": "MATCHED ✅"
}
```

## 📁 File Structure

```
hash-tool/
├── app.py          # Flask backend
├── index.html      # Frontend
├── style.css       # Styling
├── script.js       # Frontend logic
├── Setup.md        # Setup instructions
├── README.md       # This file
└── LICENSE         # MIT License
```

## 🤝 Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

## 📄 License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.
