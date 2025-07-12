using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using AzureCloudServicePipe.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AzureCloudServicePipe
{
    public class AzureFormRecognizerClient : IAzureCloudService
    {
        private readonly DocumentAnalysisClient _formRecognizer;
        private readonly string _apiUrl;
        private readonly string _apiKey;
        private readonly ILogger<AzureFormRecognizerClient> _logger;

        public AzureFormRecognizerClient(string apiUrl, string apiKey, ILogger<AzureFormRecognizerClient> logger)
        {
            _apiUrl = apiUrl;
            _apiKey = apiKey;
            _logger = logger;

            _formRecognizer = new DocumentAnalysisClient(new Uri(_apiUrl), new AzureKeyCredential(_apiKey));
        }

        public async Task<string> AnalyzeDocumentAsync(string filePath, string model = "prebuilt-document")
        {
            if (!File.Exists(filePath))
            {
                _logger.LogError("File not found: {FilePath}", filePath);
                throw new FileNotFoundException("The specified file does not exist.", filePath);
            }

            try
            {
                var fileName = Path.GetFileName(filePath);
                _logger.LogInformation("Starting document analysis for file: {FileName} using model: {Model}", fileName, model);

                using var fileStream = File.OpenRead(filePath);

                var operation = await _formRecognizer.AnalyzeDocumentAsync(WaitUntil.Completed, model, fileStream);
                var result = operation.Value;

                string formJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });

                _logger.LogInformation("Document analysis completed successfully for file: {FileName}", fileName);
                return formJson;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure request failed while analyzing document: {FilePath}", filePath);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while analyzing document: {FilePath}", filePath);
                throw;
            }
        }
    }
}