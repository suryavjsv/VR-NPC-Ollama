#!/bin/bash
# ─── VR AI NPC Server — Quick Start ───────────────────────
# Run this script to start the AI backend server.
# Prerequisites: Python 3.10+, Ollama installed and running

set -e

echo "================================="
echo " VR AI NPC Server - Starting Up"
echo "================================="

# Check Ollama
echo ""
echo "[1/4] Checking Ollama..."
if ! command -v ollama &> /dev/null; then
    echo "ERROR: Ollama not found. Install from https://ollama.ai"
    exit 1
fi

# Ensure Ollama is running
if ! curl -s http://localhost:11434/api/tags > /dev/null 2>&1; then
    echo "Starting Ollama server..."
    ollama serve &
    sleep 3
fi

# Check if model is pulled
if ! ollama list | grep -q "llama3.2:1b"; then
    echo "Pulling Llama 3.2 1B model (this may take a few minutes)..."
    ollama pull llama3.2:1b
fi
echo "Ollama: OK (llama3.2:1b ready)"

# Check Python dependencies
echo ""
echo "[2/4] Checking Python dependencies..."
pip install -q -r requirements.txt 2>/dev/null || {
    echo "Installing dependencies..."
    pip install -r requirements.txt
}
echo "Dependencies: OK"

# Check Piper
echo ""
echo "[3/4] Checking Piper TTS..."
if ! command -v piper &> /dev/null; then
    echo "ERROR: Piper not found. Install with: pip install piper-tts"
    exit 1
fi
echo "Piper TTS: OK"

# Get local IP for Quest connection
echo ""
echo "[4/4] Network info..."
LOCAL_IP=$(hostname -I 2>/dev/null | awk '{print $1}' || echo "unknown")
echo "═══════════════════════════════════════════"
echo " Server will start on: ws://${LOCAL_IP}:8765"
echo " Set this IP in Unity WebSocketClient!"
echo "═══════════════════════════════════════════"
echo ""

# Start server
echo "Starting server..."
python main.py
