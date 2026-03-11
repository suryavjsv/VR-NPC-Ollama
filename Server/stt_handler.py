"""
Speech-to-Text Handler using faster-whisper
"""

import io
import logging
import numpy as np
import soundfile as sf
from faster_whisper import WhisperModel

logger = logging.getLogger("STT")


class STTHandler:
    def __init__(self, model_size: str = "base", device: str = "auto", language: str = "en"):
        """
        Initialize Whisper STT.

        Args:
            model_size: tiny, base, small, medium, large-v3
                        - tiny:  ~1GB RAM, fastest, lower accuracy
                        - base:  ~1.5GB RAM, good balance for VR
                        - small: ~2.5GB RAM, better accuracy
            device: "auto", "cpu", or "cuda"
            language: Language code (e.g., "en")
        """
        self.language = language

        # Determine compute type based on device
        if device == "auto":
            try:
                import torch
                if torch.cuda.is_available():
                    device = "cuda"
                    compute_type = "float16"
                else:
                    device = "cpu"
                    compute_type = "int8"
            except ImportError:
                device = "cpu"
                compute_type = "int8"
        elif device == "cuda":
            compute_type = "float16"
        else:
            compute_type = "int8"

        logger.info(f"Loading Whisper model '{model_size}' on {device} ({compute_type})")

        self.model = WhisperModel(
            model_size,
            device=device,
            compute_type=compute_type
        )

        logger.info("Whisper model loaded successfully")

    def transcribe(self, audio_bytes: bytes) -> str:
        """
        Transcribe audio bytes to text.

        Args:
            audio_bytes: Raw WAV audio data from Quest 3 microphone

        Returns:
            Transcribed text string
        """
        try:
            # Parse WAV bytes into numpy array
            audio_buffer = io.BytesIO(audio_bytes)

            try:
                audio_data, sample_rate = sf.read(audio_buffer, dtype='float32')
            except Exception:
                # If not valid WAV, try to interpret as raw PCM (16-bit, 16kHz mono)
                audio_buffer.seek(0)
                raw = audio_buffer.read()
                audio_data = np.frombuffer(raw, dtype=np.int16).astype(np.float32) / 32768.0
                sample_rate = 16000

            # Resample to 16kHz if needed (Whisper expects 16kHz)
            if sample_rate != 16000:
                audio_data = self._resample(audio_data, sample_rate, 16000)

            # Ensure mono
            if len(audio_data.shape) > 1:
                audio_data = audio_data.mean(axis=1)

            # Skip very short audio (< 0.3 seconds)
            if len(audio_data) < 16000 * 0.3:
                logger.debug("Audio too short, skipping")
                return ""

            # Transcribe
            segments, info = self.model.transcribe(
                audio_data,
                language=self.language,
                beam_size=3,                # Lower = faster
                best_of=1,
                vad_filter=True,            # Filter out non-speech
                vad_parameters=dict(
                    min_silence_duration_ms=500,
                    speech_pad_ms=200
                ),
                condition_on_previous_text=False,  # Better for short utterances
                no_speech_threshold=0.6,
            )

            # Collect all segments
            text_parts = []
            for segment in segments:
                text_parts.append(segment.text.strip())

            result = " ".join(text_parts).strip()

            # Filter out common Whisper hallucinations on silence
            hallucinations = [
                "thank you", "thanks for watching", "you", "the end",
                "thanks", "bye", ".", "...", "thank you for watching"
            ]
            if result.lower() in hallucinations:
                return ""

            return result

        except Exception as e:
            logger.error(f"Transcription error: {e}")
            return ""

    def _resample(self, audio: np.ndarray, orig_sr: int, target_sr: int) -> np.ndarray:
        """Simple resampling using scipy"""
        try:
            from scipy.signal import resample
            num_samples = int(len(audio) * target_sr / orig_sr)
            return resample(audio, num_samples).astype(np.float32)
        except ImportError:
            # Fallback: linear interpolation
            ratio = target_sr / orig_sr
            indices = np.arange(0, len(audio), 1 / ratio)
            indices = indices[indices < len(audio)].astype(int)
            return audio[indices]
