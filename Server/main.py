"""
VR AI NPC Server — Main Entry Point
FastAPI WebSocket server connecting STT → LLM → TTS
"""

import asyncio
import json
import logging
import time
import os
from pathlib import Path

import uvicorn
from fastapi import FastAPI, WebSocket, WebSocketDisconnect

from stt_handler import STTHandler
from llm_handler import LLMHandler
from tts_handler import TTSHandler

# ─── Configuration ───────────────────────────────────────────────
CONFIG = {
    # Whisper STT
    "whisper_model": "base",          # tiny, base, small, medium
    "whisper_device": "auto",         # auto, cpu, cuda
    "whisper_language": "en",

    # Ollama LLM
    "ollama_model": "llama3.2:1b",
    "ollama_host": "http://localhost:11434",
    "system_prompt": """You are a helpful VR assembly assistant NPC. You guide users through 
hardware assembly tasks step by step. Keep responses concise (2-3 sentences max) 
since you are speaking out loud in VR. Be friendly and encouraging. 
If the user asks about a specific part or step, give clear, actionable instructions.
Never use markdown formatting, bullet points, or special characters in your responses — 
speak naturally as if talking face to face.""",

    # Piper TTS
    "piper_voice": "en_US-ryan-medium",
    "piper_voice_path": "D:/Personal Files/Projects/VR-NPC-Ollama/Server/Sova/.piper-voices/en_US-ryan-medium/en_US-ryan-medium.onnx",
    "tts_sample_rate": 22050,

    # Server
    "host": "0.0.0.0",
    "port": 8765,
}

# ─── Logging ─────────────────────────────────────────────────────
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s"
)
logger = logging.getLogger("VR-NPC-Server")

# ─── App ─────────────────────────────────────────────────────────
app = FastAPI(title="VR AI NPC Server")

# Global handlers (initialized on startup)
stt: STTHandler = None
llm: LLMHandler = None
tts: TTSHandler = None

# Conversation history per connection
conversations: dict[str, list] = {}


@app.on_event("startup")
async def startup():
    global stt, llm, tts

    logger.info("Initializing STT (Whisper)...")
    stt = STTHandler(
        model_size=CONFIG["whisper_model"],
        device=CONFIG["whisper_device"],
        language=CONFIG["whisper_language"]
    )

    logger.info("Initializing LLM (Ollama + Llama 3.2)...")
    llm = LLMHandler(
        model=CONFIG["ollama_model"],
        host=CONFIG["ollama_host"],
        system_prompt=CONFIG["system_prompt"]
    )
    await llm.verify_connection()

    logger.info("Initializing TTS (Piper)...")
    tts = TTSHandler(
        voice=CONFIG["piper_voice"],
        voice_path=CONFIG["piper_voice_path"],
        sample_rate=CONFIG["tts_sample_rate"]
    )

    logger.info("All systems ready. Waiting for Quest 3 connection...")


@app.websocket("/ws")
async def websocket_endpoint(ws: WebSocket):
    await ws.accept()
    conn_id = str(id(ws))
    conversations[conn_id] = []
    logger.info(f"Quest 3 connected [{conn_id}]")

    try:
        # Send ready signal
        await ws.send_json({
            "type": "status",
            "message": "connected",
            "config": {
                "sample_rate": CONFIG["tts_sample_rate"]
            }
        })

        while True:
            # Receive audio data from Quest 3
            data = await ws.receive()

            if "bytes" in data:
                await handle_audio_message(ws, conn_id, data["bytes"])
            elif "text" in data:
                msg = json.loads(data["text"])
                await handle_text_message(ws, conn_id, msg)

    except WebSocketDisconnect:
        logger.info(f"Quest 3 disconnected [{conn_id}]")
    except Exception as e:
        logger.error(f"Error [{conn_id}]: {e}")
    finally:
        conversations.pop(conn_id, None)


async def handle_audio_message(ws: WebSocket, conn_id: str, audio_bytes: bytes):
    """Full pipeline: Audio → STT → LLM → TTS → Send back audio"""
    total_start = time.perf_counter()

    # ── Step 1: Speech-to-Text ──
    await ws.send_json({"type": "status", "message": "transcribing"})
    t0 = time.perf_counter()

    transcription = await asyncio.to_thread(stt.transcribe, audio_bytes)

    stt_time = time.perf_counter() - t0
    logger.info(f"STT ({stt_time:.2f}s): \"{transcription}\"")

    if not transcription or transcription.strip() == "":
        await ws.send_json({
            "type": "status",
            "message": "no_speech_detected"
        })
        return

    # Send transcription back for display
    await ws.send_json({
        "type": "transcription",
        "text": transcription
    })

    # ── Step 2: LLM Response ──
    await ws.send_json({"type": "status", "message": "thinking"})
    t0 = time.perf_counter()

    history = conversations.get(conn_id, [])
    response_text = await llm.generate(transcription, history)

    # Update conversation history (keep last 10 turns)
    history.append({"role": "user", "content": transcription})
    history.append({"role": "assistant", "content": response_text})
    conversations[conn_id] = history[-20:]  # Keep last 10 exchanges

    llm_time = time.perf_counter() - t0
    logger.info(f"LLM ({llm_time:.2f}s): \"{response_text[:80]}...\"")

    # Send text response
    await ws.send_json({
        "type": "response_text",
        "text": response_text
    })

    # ── Step 3: Text-to-Speech ──
    await ws.send_json({"type": "status", "message": "speaking"})
    t0 = time.perf_counter()

    audio_data = await asyncio.to_thread(tts.synthesize, response_text)

    tts_time = time.perf_counter() - t0
    logger.info(f"TTS ({tts_time:.2f}s): {len(audio_data)} bytes")

    # Send audio header then audio data
    await ws.send_json({
        "type": "audio_response",
        "sample_rate": CONFIG["tts_sample_rate"],
        "num_bytes": len(audio_data),
        "format": "wav"
    })
    await ws.send_bytes(audio_data)

    total_time = time.perf_counter() - total_start
    logger.info(
        f"Pipeline complete ({total_time:.2f}s) — "
        f"STT: {stt_time:.2f}s, LLM: {llm_time:.2f}s, TTS: {tts_time:.2f}s"
    )

    await ws.send_json({
        "type": "status",
        "message": "idle",
        "latency": {
            "stt": round(stt_time, 3),
            "llm": round(llm_time, 3),
            "tts": round(tts_time, 3),
            "total": round(total_time, 3)
        }
    })


async def handle_text_message(ws: WebSocket, conn_id: str, msg: dict):
    """Handle text commands from Unity client"""
    msg_type = msg.get("type", "")

    if msg_type == "ping":
        await ws.send_json({"type": "pong", "timestamp": time.time()})

    elif msg_type == "reset":
        conversations[conn_id] = []
        await ws.send_json({"type": "status", "message": "conversation_reset"})
        logger.info(f"Conversation reset [{conn_id}]")

    elif msg_type == "text_input":
        # Allow direct text input (skip STT)
        text = msg.get("text", "")
        if text:
            await ws.send_json({"type": "status", "message": "thinking"})

            history = conversations.get(conn_id, [])
            response_text = await llm.generate(text, history)

            history.append({"role": "user", "content": text})
            history.append({"role": "assistant", "content": response_text})
            conversations[conn_id] = history[-20:]

            await ws.send_json({"type": "response_text", "text": response_text})

            # Generate and send TTS
            audio_data = await asyncio.to_thread(tts.synthesize, response_text)
            await ws.send_json({
                "type": "audio_response",
                "sample_rate": CONFIG["tts_sample_rate"],
                "num_bytes": len(audio_data),
                "format": "wav"
            })
            await ws.send_bytes(audio_data)
            await ws.send_json({"type": "status", "message": "idle"})

    elif msg_type == "update_system_prompt":
        new_prompt = msg.get("prompt", "")
        if new_prompt:
            llm.system_prompt = new_prompt
            logger.info(f"System prompt updated [{conn_id}]")


# ─── Health Check ────────────────────────────────────────────────
@app.get("/health")
async def health():
    return {
        "status": "ok",
        "stt": "whisper" if stt else "not_loaded",
        "llm": CONFIG["ollama_model"],
        "tts": CONFIG["piper_voice"],
    }


if __name__ == "__main__":
    logger.info(f"Starting VR NPC Server on {CONFIG['host']}:{CONFIG['port']}")
    uvicorn.run(
        "main:app",
        host=CONFIG["host"],
        port=CONFIG["port"],
        ws_max_size=16 * 1024 * 1024,  # 16MB max for audio
        log_level="info"
    )
