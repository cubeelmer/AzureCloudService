using AzureCloudServicePipe.Interfaces;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;

namespace AzureCloudServicePipe
{
    public class AzureSpeechCognitiveClient : IAzureCloudService
    {
        private readonly SpeechConfig _speechConfig;
        private readonly string _subscriptionKey;
        private readonly string _region;
        private readonly ILogger<AzureSpeechCognitiveClient> _logger;

        public AzureSpeechCognitiveClient(string apiKey, string resourceRegion, ILogger<AzureSpeechCognitiveClient> logger)
        {
            _subscriptionKey = apiKey;
            _region = resourceRegion;
            _logger = logger;

            _speechConfig = SpeechConfig.FromSubscription(_subscriptionKey, _region);
        }

        public async Task TextToSpeechAsync(string text, string outputFilePath)
        {
            try
            {
                using var synthesizer = new SpeechSynthesizer(_speechConfig, null);
                var result = await synthesizer.SpeakTextAsync(text);

                if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                {
                    _logger.LogInformation("Speech synthesized successfully for text: {Text}", text);
                }
                else if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                    _logger.LogWarning("Speech synthesis canceled. Reason: {Reason}", cancellation.Reason);

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        _logger.LogError("Synthesis error. Code: {ErrorCode}, Details: {Details}",
                            cancellation.ErrorCode, cancellation.ErrorDetails);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during TextToSpeechAsync.");
                throw;
            }
        }

        public async Task<string> SpeechToTextAsync()
        {
            try
            {
                _logger.LogInformation("Starting speech-to-text recognition from microphone.");

                using var recognizer = new SpeechRecognizer(_speechConfig);
                var result = await recognizer.RecognizeOnceAsync();

                if (result.Reason == ResultReason.RecognizedSpeech)
                {
                    _logger.LogInformation("Speech recognized: {Text}", result.Text);
                    return result.Text;
                }
                else if (result.Reason == ResultReason.NoMatch)
                {
                    _logger.LogWarning("No speech could be recognized.");
                    return string.Empty;
                }
                else if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = CancellationDetails.FromResult(result);
                    _logger.LogWarning("Speech recognition canceled. Reason: {Reason}", cancellation.Reason);

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        _logger.LogError("Recognition error. Code: {Code}, Details: {Details}",
                            cancellation.ErrorCode, cancellation.ErrorDetails);
                    }

                    return string.Empty;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during SpeechToTextAsync.");
                throw;
            }
        }

        public async Task<string> TranscribeAudioAsync(Stream audioStream, CancellationToken cancellationToken = default)
        {
            try
            {
                if (audioStream == null || !audioStream.CanRead)
                    throw new ArgumentException("Audio stream is not readable.");

                _logger.LogInformation("Starting transcription from audio stream.");

                var config = SpeechConfig.FromSubscription(_subscriptionKey, _region);
                config.SpeechRecognitionLanguage = "en-US";

                using var pushStream = AudioInputStream.CreatePushStream();
                using var audioInput = AudioConfig.FromStreamInput(pushStream);
                using var recognizer = new SpeechRecognizer(config, audioInput);

                byte[] buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = await audioStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    pushStream.Write(buffer, bytesRead);
                }

                pushStream.Close(); // Signal end of stream

                var result = await recognizer.RecognizeOnceAsync();

                if (result.Reason == ResultReason.RecognizedSpeech)
                {
                    _logger.LogInformation("Transcription successful: {Text}", result.Text);
                    return result.Text;
                }
                else if (result.Reason == ResultReason.NoMatch)
                {
                    _logger.LogWarning("No speech recognized in the audio stream.");
                    return "No speech recognized.";
                }
                else if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = CancellationDetails.FromResult(result);
                    _logger.LogError("Transcription canceled. Reason: {Reason}, Details: {Details}",
                        cancellation.Reason, cancellation.ErrorDetails);

                    throw new Exception($"Transcription canceled: {cancellation.Reason}. Details: {cancellation.ErrorDetails}");
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during TranscribeAudioAsync.");
                throw;
            }
        }
    }
}