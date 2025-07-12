namespace AzureCloudServicePipe.Models
{
    public class AzureAiChatMessage
    {
        public string role { get; set; }
        public string? name { get; set; }
        public string content { get; set; }
    }
}
