using Azure.Storage.Blobs;
using ZstdNet;

public class ExportStream : IDisposable, IAsyncDisposable
{
    private readonly Stream _underlyingStream;
    private readonly Stream _userStream;
    private readonly bool _isBlobStorage;
    private readonly string _blobName;
    private readonly BlobContainerClient? _blobContainer;
    private bool _disposed;

    private ExportStream(Stream underlyingStream, Stream userStream, bool isBlobStorage, string blobName, BlobContainerClient? blobContainer)
    {
        _underlyingStream = underlyingStream;
        _userStream = userStream;
        _isBlobStorage = isBlobStorage;
        _blobName = blobName;
        _blobContainer = blobContainer;
    }

    public Stream Stream => _userStream;

    public static async Task<ExportStream> CreateAsync(
        string filename,
        bool useStorageAccount,
        bool useZstd,
        BlobServiceClient blobServiceClient)
    {
        if (useStorageAccount)
        {
            var memoryStream = new MemoryStream();
            var blobContainer = blobServiceClient.GetBlobContainerClient("upload-container");
            await blobContainer.CreateIfNotExistsAsync();

            Stream userStream = useZstd
                ? new CompressionStream(memoryStream)
                : memoryStream;

            return new ExportStream(memoryStream, userStream, true, filename, blobContainer);
        }
        else
        {
            var fileStream = new FileStream(filename, FileMode.Create, FileAccess.Write);
            Stream userStream = useZstd
                ? new CompressionStream(fileStream)
                : fileStream;

            return new ExportStream(fileStream, userStream, false, filename, null);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        // First dispose the compression stream (if exists)
        if (_userStream is CompressionStream compressionStream)
        {
            await compressionStream.DisposeAsync();
        }

        // Then handle underlying stream
        if (_isBlobStorage && _underlyingStream is MemoryStream memoryStream)
        {
            memoryStream.Position = 0;
            await _blobContainer!.UploadBlobAsync(_blobName, memoryStream);
        }

        // Finally dispose the underlying stream
        await _underlyingStream.DisposeAsync();

        _disposed = true;
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_userStream is CompressionStream compressionStream)
        {
            compressionStream.Dispose();
        }

        if (_isBlobStorage && _underlyingStream is MemoryStream memoryStream)
        {
            memoryStream.Position = 0;
            _blobContainer!.UploadBlob(_blobName, memoryStream);
        }

        _underlyingStream.Dispose();
        _disposed = true;
    }
}
