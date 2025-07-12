using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.Net;
using AzureCloudServicePipe.Interfaces;

namespace AzureCloudServicePipe
{
    public class AzureCosmoDbClient<T> where T : class, IAzureCloudService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;
        private readonly ILogger<AzureCosmoDbClient<T>> _logger;

        public AzureCosmoDbClient(
            CosmosClient cosmosClient,
            string databaseId,
            string containerId,
            ILogger<AzureCosmoDbClient<T>> logger)
        {
            _cosmosClient = cosmosClient;
            _container = _cosmosClient.GetContainer(databaseId, containerId);
            _logger = logger;
        }

        public async Task<T?> GetItemAsync(string id, string partitionKey)
        {
            try
            {
                var response = await _container.ReadItemAsync<T>(id, new PartitionKey(partitionKey));
                _logger.LogInformation("Item retrieved: {Id}", id);
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Item not found: {Id}", id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting item: {Id}", id);
                throw;
            }
        }

        public async Task<T> CreateItemAsync(T item, string partitionKey)
        {
            try
            {
                var response = await _container.CreateItemAsync(item, new PartitionKey(partitionKey));
                _logger.LogInformation("Item created with id: {Id}", response.Resource?.GetType().GetProperty("id")?.GetValue(response.Resource));
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating item");
                throw;
            }
        }

        public async Task<T?> UpdateItemAsync(string id, T item, string partitionKey)
        {
            try
            {
                var response = await _container.UpsertItemAsync(item, new PartitionKey(partitionKey));
                _logger.LogInformation("Item updated: {Id}", id);
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating item: {Id}", id);
                throw;
            }
        }

        public async Task DeleteItemAsync(string id, string partitionKey)
        {
            try
            {
                await _container.DeleteItemAsync<T>(id, new PartitionKey(partitionKey));
                _logger.LogInformation("Item deleted: {Id}", id);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Attempted to delete non-existent item: {Id}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item: {Id}", id);
                throw;
            }
        }
    }
}

