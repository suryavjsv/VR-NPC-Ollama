"""
LLM Handler using Ollama (Llama 3.2 1B)
"""

import logging
from ollama import AsyncClient

logger = logging.getLogger("LLM")


class LLMHandler:
    def __init__(self, model: str = "llama3.2:1b", host: str = "http://localhost:11434",
                 system_prompt: str = ""):
        """
        Initialize Ollama LLM client.

        Args:
            model: Ollama model name (e.g., "llama3.2:1b", "llama3.2:3b")
            host: Ollama server URL
            system_prompt: System instructions for the NPC persona
        """
        self.model = model
        self.host = host
        self.system_prompt = system_prompt
        self.client = AsyncClient(host=host)

    async def verify_connection(self):
        """Verify Ollama is running and model is available"""
        try:
            models = await self.client.list()
            model_names = [m.model for m in models.models]
            logger.info(f"Ollama models available: {model_names}")

            # Check if our target model is pulled
            base_name = self.model.split(":")[0]
            found = any(base_name in name for name in model_names)

            if not found:
                logger.warning(
                    f"Model '{self.model}' not found. "
                    f"Run: ollama pull {self.model}"
                )
                raise RuntimeError(f"Model {self.model} not available in Ollama")

            # Warm up with a test generation
            logger.info(f"Warming up model '{self.model}'...")
            response = await self.client.chat(
                model=self.model,
                messages=[
                    {"role": "user", "content": "Hello"}
                ],
                options={"num_predict": 10}
            )
            logger.info(f"Model warm-up complete: \"{response.message.content[:50]}\"")

        except Exception as e:
            logger.error(f"Failed to connect to Ollama at {self.host}: {e}")
            logger.error("Make sure Ollama is running: 'ollama serve'")
            raise

    async def generate(self, user_input: str, conversation_history: list = None) -> str:
        """
        Generate a response from the LLM.

        Args:
            user_input: The user's spoken text
            conversation_history: Previous conversation turns

        Returns:
            Generated response text
        """
        try:
            # Build messages array
            messages = []

            # System prompt
            if self.system_prompt:
                messages.append({
                    "role": "system",
                    "content": self.system_prompt
                })

            # Add conversation history
            if conversation_history:
                messages.extend(conversation_history)

            # Add current user input
            messages.append({
                "role": "user",
                "content": user_input
            })

            # Generate response
            response = await self.client.chat(
                model=self.model,
                messages=messages,
                options={
                    "num_predict": 150,       # Max tokens (~2-3 sentences)
                    "temperature": 0.7,
                    "top_p": 0.9,
                    "repeat_penalty": 1.1,
                    "num_ctx": 2048,          # Context window
                }
            )

            text = response.message.content.strip()

            # Clean up response for speech
            text = self._clean_for_speech(text)

            return text

        except Exception as e:
            logger.error(f"LLM generation error: {e}")
            return "Sorry, I had trouble processing that. Could you say it again?"

    async def generate_stream(self, user_input: str, conversation_history: list = None):
        """
        Stream response tokens (for future streaming TTS integration).

        Yields:
            Text chunks as they are generated
        """
        messages = []

        if self.system_prompt:
            messages.append({"role": "system", "content": self.system_prompt})

        if conversation_history:
            messages.extend(conversation_history)

        messages.append({"role": "user", "content": user_input})

        try:
            stream = await self.client.chat(
                model=self.model,
                messages=messages,
                stream=True,
                options={
                    "num_predict": 150,
                    "temperature": 0.7,
                    "top_p": 0.9,
                }
            )

            async for chunk in stream:
                if chunk.message.content:
                    yield chunk.message.content

        except Exception as e:
            logger.error(f"LLM streaming error: {e}")
            yield "Sorry, something went wrong."

    def _clean_for_speech(self, text: str) -> str:
        """
        Clean LLM output for natural speech synthesis.
        Remove markdown, special chars, etc.
        """
        import re

        # Remove markdown formatting
        text = re.sub(r'\*\*(.*?)\*\*', r'\1', text)   # Bold
        text = re.sub(r'\*(.*?)\*', r'\1', text)       # Italic
        text = re.sub(r'`(.*?)`', r'\1', text)         # Code
        text = re.sub(r'#{1,6}\s*', '', text)           # Headers

        # Remove bullet points and numbering
        text = re.sub(r'^\s*[-•*]\s*', '', text, flags=re.MULTILINE)
        text = re.sub(r'^\s*\d+\.\s*', '', text, flags=re.MULTILINE)

        # Clean up whitespace
        text = re.sub(r'\n+', ' ', text)
        text = re.sub(r'\s+', ' ', text)

        # Remove URLs
        text = re.sub(r'https?://\S+', '', text)

        # Remove emoji (basic pattern)
        text = re.sub(
            r'[\U0001F600-\U0001F64F\U0001F300-\U0001F5FF'
            r'\U0001F680-\U0001F6FF\U0001F1E0-\U0001F1FF]',
            '', text
        )

        return text.strip()
