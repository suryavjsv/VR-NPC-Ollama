"""
Text-to-Speech Handler using Piper TTS (Python API)
Works on Windows, Mac, and Linux without CLI dependency.
"""

import io
import wave
import logging
import struct
from pathlib import Path

logger = logging.getLogger("TTS")


class TTSHandler:
    def __init__(self, voice: str = "en_US-lessac-medium",
                 voice_path: str = None, sample_rate: int = 22050):
        """
        Initialize Piper TTS using Python API.

        Args:
            voice: Piper voice name (auto-downloaded if not found)
            voice_path: Explicit path to voice .onnx file (optional)
            sample_rate: Output sample rate
        """
        self.voice = voice
        self.sample_rate = sample_rate
        self.voice_path = voice_path
        self.piper_voice = None

        self._initialize_piper()

    def _initialize_piper(self):
        """Initialize Piper TTS using Python API"""
        try:
            from piper import PiperVoice

            if self.voice_path and Path(self.voice_path).exists():
                # Use explicit voice path
                logger.info(f"Loading Piper voice from: {self.voice_path}")
                self.piper_voice = PiperVoice.load(self.voice_path)
            else:
                # Auto-download voice model
                model_path = self._download_voice(self.voice)
                if model_path:
                    logger.info(f"Loading Piper voice: {self.voice}")
                    self.piper_voice = PiperVoice.load(str(model_path))
                else:
                    raise FileNotFoundError(f"Could not find or download voice: {self.voice}")

            self.sample_rate = self.piper_voice.config.sample_rate
            logger.info(f"Piper TTS ready (voice: {self.voice}, sample_rate: {self.sample_rate})")

        except ImportError:
            logger.error("piper-tts not installed. Run: pip install piper-tts")
            raise
        except Exception as e:
            logger.error(f"Failed to initialize Piper TTS: {e}")
            raise

    def _download_voice(self, voice_name: str) -> Path:
        """Download a Piper voice model if not already cached."""
        try:
            from piper.download import ensure_voice_exists, get_voices

            # Determine data directory
            data_dir = Path.home() / ".piper-voices"
            data_dir.mkdir(parents=True, exist_ok=True)

            # Check if voice already exists
            model_path = data_dir / voice_name / f"{voice_name}.onnx"
            if model_path.exists():
                logger.info(f"Voice already downloaded: {model_path}")
                return model_path

            # Download voice
            logger.info(f"Downloading Piper voice '{voice_name}' (first time only)...")

            # Get available voices
            voices_info = get_voices(data_dir, update_voices=True)

            if voice_name in voices_info:
                ensure_voice_exists(
                    voice_name,
                    data_dirs=[data_dir],
                    download_dir=data_dir / voice_name,
                    voices_info=voices_info
                )

                # Find the downloaded .onnx file
                voice_dir = data_dir / voice_name
                onnx_files = list(voice_dir.glob("*.onnx"))
                if onnx_files:
                    logger.info(f"Voice downloaded: {onnx_files[0]}")
                    return onnx_files[0]

            # Fallback: search in data_dir recursively
            for onnx_file in data_dir.rglob("*.onnx"):
                if voice_name.replace("-", "_") in str(onnx_file).replace("-", "_"):
                    return onnx_file

            logger.error(f"Voice '{voice_name}' not found in available voices")
            return None

        except ImportError:
            logger.warning("piper.download not available, trying manual path...")
            # Try common locations
            common_paths = [
                Path.home() / ".piper-voices" / voice_name / f"{voice_name}.onnx",
                Path.home() / "piper-voices" / f"{voice_name}.onnx",
                Path(f"./{voice_name}.onnx"),
            ]
            for p in common_paths:
                if p.exists():
                    return p
            return None

        except Exception as e:
            logger.error(f"Error downloading voice: {e}")
            return None

    def synthesize(self, text: str) -> bytes:
        if not text or not text.strip():
            return self._generate_silence(0.5)

        if self.piper_voice is None:
            logger.error("Piper voice not loaded")
            return self._generate_silence(1.0)

        try:
            # Clean text for TTS
            clean_text = text.strip().strip('"').strip("'")
            clean_text = clean_text.replace('"', '').replace("'", "")
            clean_text = clean_text.replace('\n', ' ').replace('\r', ' ')
            clean_text = ' '.join(clean_text.split())

            if not clean_text:
                return self._generate_silence(0.5)

            logger.info(f"TTS synthesizing: \"{clean_text[:80]}...\"")

            wav_buffer = io.BytesIO()
            wav_file = wave.open(wav_buffer, 'wb')
            self.piper_voice.synthesize_wav(clean_text, wav_file)
            wav_file.close()

            wav_bytes = wav_buffer.getvalue()
            logger.info(f"TTS output: {len(wav_bytes)} bytes")

            if len(wav_bytes) <= 44:
                logger.warning("Piper returned empty audio")
                return self._generate_silence(1.0)

            return wav_bytes

        except Exception as e:
            logger.error(f"TTS synthesis error: {e}")
            return self._generate_silence(1.0)
        
    def synthesize_to_file(self, text: str, output_path: str) -> str:
        """
        Synthesize text and save to a WAV file.

        Args:
            text: Text to synthesize
            output_path: Path to save WAV file

        Returns:
            Path to the saved file
        """
        wav_bytes = self.synthesize(text)
        with open(output_path, "wb") as f:
            f.write(wav_bytes)
        return output_path

    def _generate_silence(self, duration: float) -> bytes:
        """Generate silent WAV audio of given duration"""
        num_samples = int(self.sample_rate * duration)
        silence = struct.pack(f'<{num_samples}h', *([0] * num_samples))
        return self._raw_to_wav(silence, self.sample_rate)

    def _raw_to_wav(self, raw_data: bytes, sample_rate: int = 22050,
                    channels: int = 1, sample_width: int = 2) -> bytes:
        """Convert raw PCM bytes to WAV format"""
        wav_buffer = io.BytesIO()

        with wave.open(wav_buffer, 'wb') as wav_file:
            wav_file.setnchannels(channels)
            wav_file.setsampwidth(sample_width)
            wav_file.setframerate(sample_rate)
            wav_file.writeframes(raw_data)

        wav_buffer.seek(0)
        return wav_buffer.read()