using FactoryMind.Application.Features.Knowledge;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace FactoryMind.Infrastructure.Storage;

public sealed class MinioFileStorage : IFileStorage, IDisposable {
    private readonly IMinioClient _client;
    private readonly MinioSettings _settings;
    private readonly ILogger<MinioFileStorage> _logger;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _bucketReady;

    public MinioFileStorage(
        IOptions<MinioSettings> options,
        ILogger<MinioFileStorage> logger) {
        _settings = options.Value;
        _logger = logger;
        _client = new MinioClient()
            .WithEndpoint(_settings.Endpoint)
            .WithCredentials(_settings.AccessKey, _settings.SecretKey)
            .WithSSL(_settings.UseSsl)
            .Build();
    }

    public async Task UploadAsync(
        string objectKey,
        Stream content,
        long length,
        string contentType,
        CancellationToken cancellationToken) {
        try {
            await EnsureBucketAsync(cancellationToken);
            var args = new PutObjectArgs()
                .WithBucket(_settings.Bucket)
                .WithObject(objectKey)
                .WithStreamData(content)
                .WithObjectSize(length)
                .WithContentType(contentType);
            await _client.PutObjectAsync(args, cancellationToken);
            _logger.LogInformation(
                "Uploaded object {ObjectKey} with {Length} bytes to bucket {Bucket}",
                objectKey,
                length,
                _settings.Bucket);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            _logger.LogError(exception, "Could not upload object {ObjectKey} to MinIO", objectKey);
            throw new FileStorageException("File storage is temporarily unavailable.", exception);
        }
    }

    public async Task<Stream> DownloadAsync(
        string objectKey,
        CancellationToken cancellationToken) {
        var content = new MemoryStream();
        try {
            var args = new GetObjectArgs()
                .WithBucket(_settings.Bucket)
                .WithObject(objectKey)
                .WithCallbackStream(stream => stream.CopyToAsync(content, cancellationToken));
            await _client.GetObjectAsync(args, cancellationToken);
            content.Position = 0;
            return content;
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            await content.DisposeAsync();
            _logger.LogError(exception, "Could not download object {ObjectKey} from MinIO", objectKey);
            throw new FileStorageException("File storage is temporarily unavailable.", exception);
        }
    }

    public void Dispose() {
        _client.Dispose();
        _bucketLock.Dispose();
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken) {
        if (_bucketReady) {
            return;
        }

        await _bucketLock.WaitAsync(cancellationToken);
        try {
            if (_bucketReady) {
                return;
            }

            var exists = await _client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_settings.Bucket),
                cancellationToken);
            if (!exists) {
                await _client.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(_settings.Bucket),
                    cancellationToken);
            }

            _bucketReady = true;
        } finally {
            _bucketLock.Release();
        }
    }
}
