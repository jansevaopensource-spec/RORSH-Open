#!/bin/bash

echo "╔══════════════════════════════════════════════════════╗"
echo "║                                                      ║"
echo "║           🐾 OPEN-SHARE DEPLOYMENT SCRIPT 🐾         ║"
echo "║                                                      ║"
echo "╚══════════════════════════════════════════════════════╝"
echo ""

# Check if Node.js is installed
if ! command -v node &> /dev/null; then
    echo "❌ Node.js is not installed. Please install Node.js 18+ first."
    exit 1
fi

NODE_VERSION=$(node -v | cut -d'v' -f2 | cut -d'.' -f1)
if [ "$NODE_VERSION" -lt 18 ]; then
    echo "❌ Node.js version 18+ required. Current: $(node -v)"
    exit 1
fi

echo "✅ Node.js $(node -v) detected"
echo ""

# Install dependencies
echo "📦 Installing dependencies..."
npm install

# Check if .env exists
if [ ! -f .env ]; then
    echo ""
    echo "⚠️  .env file not found!"
    echo "📝 Creating from .env.example..."
    cp .env.example .env
    echo ""
    echo "🔧 IMPORTANT: Please edit .env file and add your:"
    echo "   - TELEGRAM_BOT_TOKEN"
    echo "   - TELEGRAM_CHAT_ID"
    echo "   - SESSION_SECRET"
    echo ""
    echo "❌ Please configure .env before starting the server."
    exit 1
fi

echo ""
echo "🚀 Starting Open-Share server..."
echo ""

npm start
