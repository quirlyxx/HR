using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text;

namespace HR.Services
{
    public class S3Service
    {
        private readonly AmazonS3Client _s3Client;
        private readonly string _bucketName;

        public S3Service(
            string accessKey,
            string secretKey,
            string bucketName,
            RegionEndpoint region)
        {
            _s3Client = new AmazonS3Client(accessKey, secretKey, region);
            _bucketName = bucketName;
        }

        public async Task UploadTextAsync(string key, string text)
        {
            using var stream = new MemoryStream(
                Encoding.UTF8.GetBytes(text)
            );

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = stream,
                ContentType = "text/plain; charset=utf-8"
            };

            await _s3Client.PutObjectAsync(request);
        }

        public async Task DownloadAsync(string key, string filePath)
        {
            using var response = await _s3Client.GetObjectAsync(
                _bucketName,
                key
            );

            await response.WriteResponseStreamToFileAsync(
                filePath,
                false,
                default
            );
        }

        public async Task DeleteAsync(string key)
        {
            await _s3Client.DeleteObjectAsync(
                _bucketName,
                key
            );
        }

        public async Task ListFilesAsync()
        {
            var response = await _s3Client.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = _bucketName
                }
            );

            foreach (var file in response.S3Objects)
            {
                Console.WriteLine(
                    $" - {file.Key} || Size: {file.Size} bytes"
                );
            }
        }
        private const string ResumesPrefix = "resumes/";

        private const string MetaCandidateName = "candidate-name";
        private const string MetaContactInfo = "contact-info";
        private const string MetaOriginalFileName = "original-file-name";
        public async Task<string> UploadResumeAsync(
            Stream resumeStream,
            string candidateName,
            string contactInfo,
            string originalFileName)
        {
            var key = $"{ResumesPrefix}{Guid.NewGuid():N}.pdf";

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = resumeStream,
                AutoCloseStream = false,
                ContentType = "application/pdf"
            };

            request.Metadata.Add(MetaCandidateName, Uri.EscapeDataString(candidateName));
            request.Metadata.Add(MetaContactInfo, Uri.EscapeDataString(contactInfo));
            request.Metadata.Add(MetaOriginalFileName, Uri.EscapeDataString(originalFileName));

            await _s3Client.PutObjectAsync(request);

            return key;
        }
        public async Task<List<ResumeInfo>> ListResumesAsync()
        {
            var result = new List<ResumeInfo>();

            var listResponse = await _s3Client.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = ResumesPrefix
                }
            );

            var s3Objects = listResponse?.S3Objects ?? new List<S3Object>();

            foreach (var obj in s3Objects)
            {
                if (obj.Key == ResumesPrefix)
                {
                    continue;
                }

                try
                {
                    var metadataResponse = await _s3Client.GetObjectMetadataAsync(
                        _bucketName,
                        obj.Key
                    );

                    var originalFileName = DecodeMetadata(
                        metadataResponse.Metadata[MetaOriginalFileName]
                    );

                    result.Add(new ResumeInfo
                    {
                        Key = obj.Key,
                        CandidateName = DecodeMetadata(
                            metadataResponse.Metadata[MetaCandidateName]
                        ),
                        ContactInfo = DecodeMetadata(
                            metadataResponse.Metadata[MetaContactInfo]
                        ),
                        OriginalFileName = string.IsNullOrEmpty(originalFileName)
                            ? Path.GetFileName(obj.Key)
                            : originalFileName,
                        UploadedAtUtc = obj.LastModified?.ToUniversalTime() ?? DateTime.MinValue,
                        SizeBytes = obj.Size ?? 0
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            return result
                .OrderByDescending(r => r.UploadedAtUtc)
                .ToList();
        }
        public async Task<string> GetResumePresignedUrlAsync(string key, TimeSpan validFor)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(validFor)
            };

            return await _s3Client.GetPreSignedURLAsync(request);
        }
        public Task DeleteResumeAsync(string key) => DeleteAsync(key);

        private static string DecodeMetadata(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            try
            {
                return Uri.UnescapeDataString(value);
            }
            catch
            {
                return value;
            }
        }
    }
    public class ResumeInfo
    {
        public string Key { get; set; } = string.Empty;
        public string CandidateName { get; set; } = string.Empty;
        public string ContactInfo { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public DateTime UploadedAtUtc { get; set; }
        public long SizeBytes { get; set; }
    }
}