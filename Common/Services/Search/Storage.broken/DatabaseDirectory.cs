// <copyright file="DatabaseDirectory.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.Search.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Lucene.Net.Store;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Lucene Directory implementation that stores index files in a database.
    /// Provides multi-tenant support and works with containerized deployments.
    /// </summary>
    public class DatabaseDirectory : Directory
    {
        private readonly IServiceProvider serviceProvider;
        private readonly string tenantDomain;
        private readonly string indexName;
        private readonly ILogger<DatabaseDirectory> logger;
        private readonly object lockObject = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseDirectory"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider for database access.</param>
        /// <param name="tenantDomain">The tenant domain for isolation.</param>
        /// <param name="indexName">The name of the index.</param>
        /// <param name="logger">Logger instance.</param>
        public DatabaseDirectory(
            IServiceProvider serviceProvider,
            string tenantDomain,
            string indexName,
            ILogger<DatabaseDirectory> logger)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            this.tenantDomain = tenantDomain ?? throw new ArgumentNullException(nameof(tenantDomain));
            this.indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            this.lockFactory = new DatabaseLockFactory(this);
            this.EnsureIndexExists().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Creates an <see cref="IndexOutput"/> for writing to the specified file.
        /// </summary>
        /// <param name="name">The file name.</param>
        /// <param name="context">The IO context.</param>
        /// <returns>An IndexOutput for writing.</returns>
        public override IndexOutput CreateOutput(string name, IOContext context)
        {
            this.logger.LogDebug("Creating output for file {FileName} in index {IndexName}", name, this.indexName);
            return new DatabaseIndexOutput(this, name, this.serviceProvider, this.tenantDomain, this.indexName, this.logger);
        }

        /// <summary>
        /// Creates an <see cref="IndexInput"/> for reading from the specified file.
        /// </summary>
        /// <param name="name">The file name.</param>
        /// <param name="context">The IO context.</param>
        /// <returns>An IndexInput for reading.</returns>
        public override IndexInput OpenInput(string name, IOContext context)
        {
            this.logger.LogDebug("Opening input for file {FileName} in index {IndexName}", name, this.indexName);
            
            var fileData = this.GetFileDataAsync(name).GetAwaiter().GetResult();
            if (fileData == null)
            {
                throw new FileNotFoundException($"File {name} not found in index {this.indexName}");
            }

            return new DatabaseIndexInput(name, fileData, this.logger);
        }

        /// <summary>
        /// Deletes the specified file.
        /// </summary>
        /// <param name="name">The file name to delete.</param>
        public override void DeleteFile(string name)
        {
            this.logger.LogDebug("Deleting file {FileName} from index {IndexName}", name, this.indexName);
            this.DeleteFileAsync(name).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Lists all files in the directory.
        /// </summary>
        /// <returns>Array of file names.</returns>
        public override string[] ListAll()
        {
            var files = this.ListAllAsync().GetAwaiter().GetResult();
            this.logger.LogDebug("Listed {FileCount} files in index {IndexName}", files.Length, this.indexName);
            return files;
        }

        /// <summary>
        /// Checks if the specified file exists.
        /// </summary>
        /// <param name="name">The file name.</param>
        /// <returns>True if the file exists.</returns>
        public override bool FileExists(string name)
        {
            var exists = this.FileExistsAsync(name).GetAwaiter().GetResult();
            this.logger.LogDebug("File {FileName} exists: {Exists} in index {IndexName}", name, exists, this.indexName);
            return exists;
        }

        /// <summary>
        /// Gets the length of the specified file.
        /// </summary>
        /// <param name="name">The file name.</param>
        /// <returns>The file length in bytes.</returns>
        public override long FileLength(string name)
        {
            var length = this.FileLengthAsync(name).GetAwaiter().GetResult();
            this.logger.LogDebug("File {FileName} length: {Length} bytes in index {IndexName}", name, length, this.indexName);
            return length;
        }

        /// <summary>
        /// Synchronizes the directory (ensures all writes are committed).
        /// </summary>
        /// <param name="names">The file names to sync (ignored - all files are synced).</param>
        public override void Sync(ICollection<string> names)
        {
            this.logger.LogDebug("Syncing {FileCount} files in index {IndexName}", names?.Count ?? 0, this.indexName);
            // Database writes are immediately committed, so no action needed
        }

        /// <summary>
        /// Disposes the directory resources.
        /// </summary>
        /// <param name="disposing">Whether disposing is in progress.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.logger.LogDebug("Disposing DatabaseDirectory for index {IndexName}", this.indexName);
                // No resources to dispose in this implementation
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Ensures the index metadata exists in the database.
        /// </summary>
        private async Task EnsureIndexExists()
        {
            using var scope = this.serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var exists = await context.LuceneIndexMetadata
                .AnyAsync(x => x.TenantDomain == this.tenantDomain && x.IndexName == this.indexName);

            if (!exists)
            {
                var metadata = new LuceneIndexMetadata
                {
                    TenantDomain = this.tenantDomain,
                    IndexName = this.indexName,
                    DocumentCount = 0,
                    IndexSizeBytes = 0,
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastModified = DateTimeOffset.UtcNow,
                    IsActive = true,
                    IndexVersion = "4.8.0"
                };

                context.LuceneIndexMetadata.Add(metadata);
                await context.SaveChangesAsync();
                this.logger.LogInformation("Created index metadata for {IndexName} in tenant {TenantDomain}", this.indexName, this.tenantDomain);
            }
        }

        /// <summary>
        /// Gets file data from the database.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <returns>File content as byte array, or null if not found.</returns>
        private async Task<byte[]?> GetFileDataAsync(string fileName)
        {
            using var scope = this.serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var file = await context.LuceneIndexFiles
                .FirstOrDefaultAsync(x => x.TenantDomain == this.tenantDomain 
                                       && x.IndexName == this.indexName 
                                       && x.FileName == fileName 
                                       && !x.IsDeleted);

            return file?.FileContent;
        }

        /// <summary>
        /// Saves file data to the database.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <param name="data">The file data.</param>
        internal async Task SaveFileDataAsync(string fileName, byte[] data)
        {
            using var scope = this.serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var checksum = this.CalculateChecksum(data);
            var now = DateTimeOffset.UtcNow;

            var existingFile = await context.LuceneIndexFiles
                .FirstOrDefaultAsync(x => x.TenantDomain == this.tenantDomain 
                                       && x.IndexName == this.indexName 
                                       && x.FileName == fileName);

            if (existingFile != null)
            {
                existingFile.FileContent = data;
                existingFile.FileSize = data.Length;
                existingFile.Checksum = checksum;
                existingFile.LastModified = now;
                existingFile.IsDeleted = false;
            }
            else
            {
                var newFile = new LuceneIndexFile
                {
                    TenantDomain = this.tenantDomain,
                    IndexName = this.indexName,
                    FileName = fileName,
                    FileContent = data,
                    FileSize = data.Length,
                    Checksum = checksum,
                    CreatedAt = now,
                    LastModified = now,
                    IsDeleted = false
                };

                context.LuceneIndexFiles.Add(newFile);
            }

            await context.SaveChangesAsync();

            // Update index metadata
            await this.UpdateIndexMetadataAsync();
        }

        /// <summary>
        /// Deletes a file from the database.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        private async Task DeleteFileAsync(string fileName)
        {
            using var scope = this.serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var file = await context.LuceneIndexFiles
                .FirstOrDefaultAsync(x => x.TenantDomain == this.tenantDomain 
                                       && x.IndexName == this.indexName 
                                       && x.FileName == fileName);

            if (file != null)
            {
                file.IsDeleted = true;
                file.LastModified = DateTimeOffset.UtcNow;
                await context.SaveChangesAsync();

                await this.UpdateIndexMetadataAsync();
            }
        }

        /// <summary>
        /// Lists all files in the index.
        /// </summary>
        /// <returns>Array of file names.</returns>
        private async Task<string[]> ListAllAsync()
        {
            using var scope = this.serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var fileNames = await context.LuceneIndexFiles
                .Where(x => x.TenantDomain == this.tenantDomain 
                         && x.IndexName == this.indexName 
                         && !x.IsDeleted)
                .Select(x => x.FileName)
                .ToArrayAsync();

            return fileNames;
        }

        /// <summary>
        /// Checks if a file exists in the database.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <returns>True if the file exists.</returns>
        private async Task<bool> FileExistsAsync(string fileName)
        {
            using var scope = this.serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await context.LuceneIndexFiles
                .AnyAsync(x => x.TenantDomain == this.tenantDomain 
                            && x.IndexName == this.indexName 
                            && x.FileName == fileName 
                            && !x.IsDeleted);
        }

        /// <summary>
        /// Gets the length of a file in bytes.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <returns>File length in bytes.</returns>
        private async Task<long> FileLengthAsync(string fileName)
        {
            using var scope = this.serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var file = await context.LuceneIndexFiles
                .FirstOrDefaultAsync(x => x.TenantDomain == this.tenantDomain 
                                       && x.IndexName == this.indexName 
                                       && x.FileName == fileName 
                                       && !x.IsDeleted);

            return file?.FileSize ?? 0;
        }

        /// <summary>
        /// Updates the index metadata with current statistics.
        /// </summary>
        private async Task UpdateIndexMetadataAsync()
        {
            using var scope = this.serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var metadata = await context.LuceneIndexMetadata
                .FirstOrDefaultAsync(x => x.TenantDomain == this.tenantDomain && x.IndexName == this.indexName);

            if (metadata != null)
            {
                var totalSize = await context.LuceneIndexFiles
                    .Where(x => x.TenantDomain == this.tenantDomain 
                             && x.IndexName == this.indexName 
                             && !x.IsDeleted)
                    .SumAsync(x => (long?)x.FileSize) ?? 0;

                metadata.IndexSizeBytes = totalSize;
                metadata.LastModified = DateTimeOffset.UtcNow;
                await context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Calculates SHA256 checksum for file integrity verification.
        /// </summary>
        /// <param name="data">The file data.</param>
        /// <returns>Hexadecimal checksum string.</returns>
        private string CalculateChecksum(byte[] data)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(data);
            return Convert.ToHexString(hash);
        }
    }

    /// <summary>
    /// Custom lock factory for database directory.
    /// </summary>
    internal class DatabaseLockFactory : LockFactory
    {
        private readonly DatabaseDirectory directory;

        public DatabaseLockFactory(DatabaseDirectory directory)
        {
            this.directory = directory;
        }

        public override Lock MakeLock(string lockName)
        {
            return new DatabaseLock(lockName);
        }

        public override void ClearLock(string lockName)
        {
            // Locks are in-memory only for this implementation
        }
    }

    /// <summary>
    /// Simple in-memory lock implementation for database directory.
    /// In a production environment, you might want to implement distributed locking.
    /// </summary>
    internal class DatabaseLock : Lock
    {
        private static readonly Dictionary<string, object> GlobalLocks = new Dictionary<string, object>();
        private static readonly object GlobalLockObject = new object();
        private readonly string lockName;
        private object? lockObject;

        public DatabaseLock(string lockName)
        {
            this.lockName = lockName;
        }

        public override bool Obtain()
        {
            lock (GlobalLockObject)
            {
                if (!GlobalLocks.ContainsKey(this.lockName))
                {
                    GlobalLocks[this.lockName] = new object();
                    this.lockObject = GlobalLocks[this.lockName];
                    return true;
                }
                return false;
            }
        }

        public override void Dispose()
        {
            if (this.lockObject != null)
            {
                lock (GlobalLockObject)
                {
                    GlobalLocks.Remove(this.lockName);
                    this.lockObject = null;
                }
            }
        }

        public override bool IsLocked()
        {
            lock (GlobalLockObject)
            {
                return GlobalLocks.ContainsKey(this.lockName);
            }
        }
    }

    /// <summary>
    /// IndexOutput implementation for writing to database.
    /// </summary>
    internal class DatabaseIndexOutput : IndexOutput
    {
        private readonly DatabaseDirectory directory;
        private readonly string fileName;
        private readonly MemoryStream buffer;
        private readonly ILogger logger;
        private bool disposed;

        public DatabaseIndexOutput(
            DatabaseDirectory directory,
            string fileName,
            IServiceProvider serviceProvider,
            string tenantDomain,
            string indexName,
            ILogger logger)
            : base(fileName, fileName)
        {
            this.directory = directory;
            this.fileName = fileName;
            this.buffer = new MemoryStream();
            this.logger = logger;
        }

        public override long Length => this.buffer.Length;

        public override long GetFilePointer() => this.buffer.Position;

        public override void WriteByte(byte b)
        {
            this.buffer.WriteByte(b);
        }

        public override void WriteBytes(byte[] b, int offset, int length)
        {
            this.buffer.Write(b, offset, length);
        }

        public override void Flush()
        {
            // Data is in memory buffer until Close() is called
        }

        public override void Dispose()
        {
            if (!this.disposed)
            {
                try
                {
                    // Save the buffer to database
                    var data = this.buffer.ToArray();
                    this.directory.SaveFileDataAsync(this.fileName, data).GetAwaiter().GetResult();
                    this.logger.LogDebug("Saved file {FileName} with {Size} bytes to database", this.fileName, data.Length);
                }
                finally
                {
                    this.buffer.Dispose();
                    this.disposed = true;
                }
            }
        }

        public override long Checksum => 0; // Not implemented for this example

        [Obsolete]
        public override void Seek(long pos)
        {
            this.buffer.Seek(pos, SeekOrigin.Begin);
        }
    }

    /// <summary>
    /// IndexInput implementation for reading from database.
    /// </summary>
    internal class DatabaseIndexInput : IndexInput
    {
        private readonly byte[] data;
        private readonly MemoryStream stream;
        private readonly ILogger logger;

        public DatabaseIndexInput(string description, byte[] data, ILogger logger)
            : base(description)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.stream = new MemoryStream(data, false);
            this.logger = logger;
        }

        public override long Length => this.data.Length;

        public override byte ReadByte()
        {
            var result = this.stream.ReadByte();
            if (result == -1)
            {
                throw new EndOfStreamException();
            }
            return (byte)result;
        }

        public override void ReadBytes(byte[] b, int offset, int len)
        {
            var bytesRead = this.stream.Read(b, offset, len);
            if (bytesRead < len)
            {
                throw new EndOfStreamException();
            }
        }

        public override long GetFilePointer() => this.stream.Position;

        public override void Seek(long pos)
        {
            this.stream.Seek(pos, SeekOrigin.Begin);
        }

        public override IndexInput Clone()
        {
            var clone = new DatabaseIndexInput(this.ToString(), this.data, this.logger);
            clone.Seek(this.GetFilePointer());
            return clone;
        }

        public override IndexInput Slice(string sliceDescription, long offset, long length)
        {
            var sliceData = new byte[length];
            Array.Copy(this.data, offset, sliceData, 0, length);
            return new DatabaseIndexInput(sliceDescription, sliceData, this.logger);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.stream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}