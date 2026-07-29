# filebase/

Place all files to be distributed here.

The server will automatically scan this directory and serve files to connected clients.

## Supported File Types

- Executables: .exe, .msi, .bat, .sh, .bin, .run, .AppImage, etc.
- Scripts: .py, .ps1, .js, .vbs, .jar
- Documents: .pdf, .doc, .txt, .md
- Media: .mp3, .mp4, .jpg, .png
- Archives: .zip, .tar, .gz

## Security Notes

- All files are served with SHA-256 verification
- The server prevents directory traversal attacks
- Only files directly in this directory are served (no subdirectories)
