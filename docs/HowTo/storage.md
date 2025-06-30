# Agents SDK Storage Overview

Storage is a critical component of the Microsoft Agents SDK, enabling agents to persist conversation state, user data, and other information across sessions. It supports various storage options, including in-memory storage, Azure Cosmos DB, Azure Blob Storage, and allows for custom storage solutions like MongoDB, DynamoDB, and Redis.

## Key Storage Options

1. Memory Storage
   - Suitable for testing and development purposes.
   - Data is cleared when the agent restarts, making it unsuitable for production.
   - Data is only availble on the webapp instance, making it unsuitable when running in a cluster.

1. Azure Cosmos DB
   - A globally distributed, multi-model database ideal for production bots.
   - Supports partitioned storage for scalability and performance.

1. Azure Blob Storage
   - Optimized for storing unstructured data like text or binary files.
   - Commonly used for agent state and transcript storage.

1. Custom storage options can be provided by implementing `IStorage`

## Using different storage providers
1.  Memory
    1. All samples use `MemoryStorage`
    1. DotNet: In Program.cs, register `MemoryStorage`
       ```csharp
       builder.Services.AddSingleton<IStorage, MemoryStorage>();
       ``` 
1. Blobs
   1. DotNet
      1. Add a package dependency for `Microsoft.Agents.Storage.Blobs`
      1. In Program.cs, add (or replace existing) `IStorage` registration
         ```csharp
         builder.Services.AddSingleton<IStorage>(sp => new BlobsStorage(blobContainerUri, tokenCredential, new StorageTransferOptions()));
         ```
      1. See `StorageTransferOptions` for details.

1. CosmosDb
   1. DotNet
      1. Add a package dependency for `Microsoft.Agents.Storage.CosmosDb`
      1. In Program.cs, add (or replace existing) `IStorage` registration
         ```csharp
         builder.Services.AddSingleton<IStorage>(sp => new CosmosDbPartitionedStorage(new CosmosDbPartitionedStorageOptions());
         ```
      1. See `CosmosDbPartitionedStorageOptions` for details.