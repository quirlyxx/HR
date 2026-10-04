using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text;
using System.Text.Json;

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
        // Сюди Lambda кладе результат аналізу Gemini: analysis/<id резюме>.txt
        private const string AnalysisPrefix = "analysis/";

        private const string MetaCandidateName = "candidate-name";
        private const string MetaContactInfo = "contact-info";
        private const string MetaOriginalFileName = "original-file-name";
        public async Task<string> UploadResumeAsync(
            Stream resumeStream,
            string candidateName,
            string contactInfo,
            string originalFileName)
        {
            var isTxt = Path.GetExtension(originalFileName).Equals(".txt", StringComparison.OrdinalIgnoreCase);
            var key = $"{ResumesPrefix}{Guid.NewGuid():N}{(isTxt ? ".txt" : ".pdf")}";

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = resumeStream,
                AutoCloseStream = false,
                ContentType = isTxt ? "text/plain; charset=utf-8" : "application/pdf"
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
            var analysisKeys = await ListAnalysisKeysAsync();

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

                    var analysisKey = AnalysisKeyFor(obj.Key);

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
                        SizeBytes = obj.Size ?? 0,
                        Analysis = analysisKeys.Contains(analysisKey)
                            ? await GetAnalysisAsync(analysisKey)
                            : null
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
            EnsureResumeKey(key);

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(validFor)
            };

            return await _s3Client.GetPreSignedURLAsync(request);
        }
        public async Task DeleteResumeAsync(string key)
        {
            EnsureResumeKey(key);

            await DeleteAsync(key);
            // S3 не повертає помилку, якщо аналізу ще немає
            await DeleteAsync(AnalysisKeyFor(key));
        }

        public async Task<ResumeInfo?> GetResumeAsync(string key)
        {
            EnsureResumeKey(key);
            return (await ListResumesAsync()).FirstOrDefault(r => r.Key == key);
        }

        private static void EnsureResumeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(ResumesPrefix))
            {
                throw new ArgumentException("Некоректний ключ резюме.");
            }
        }

        // resumes/abc.pdf -> analysis/abc.txt
        private static string AnalysisKeyFor(string resumeKey) =>
    $"{AnalysisPrefix}{Path.GetFileNameWithoutExtension(resumeKey)}.json";

        private async Task<HashSet<string>> ListAnalysisKeysAsync()
        {
            var response = await _s3Client.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = AnalysisPrefix
                }
            );

            return (response?.S3Objects ?? new List<S3Object>())
                .Select(o => o.Key)
                .ToHashSet();
        }

        private async Task<ResumeAnalysis?> GetAnalysisAsync(string analysisKey)
        {
            try
            {
                using var response = await _s3Client.GetObjectAsync(_bucketName, analysisKey);
                using var reader = new StreamReader(response.ResponseStream, Encoding.UTF8);
                var text = await reader.ReadToEndAsync();

                try
                {
                    using var doc = JsonDocument.Parse(text);
                    var root = doc.RootElement;

                    var skills = new List<string>();
                    if (root.TryGetProperty("skills", out var skillsEl) &&
                        skillsEl.ValueKind == JsonValueKind.Array)
                    {
                        skills = skillsEl.EnumerateArray()
                            .Select(x => x.ToString())
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .ToList();
                    }

                    return new ResumeAnalysis
                    {
                        CandidateName = $"{ReadString(root, "candidateName")} {ReadString(root, "candidateSurname")}".Trim(),
                        BirthDate = ReadString(root, "candidateBirthDate"),
                        MatchPercentage = ReadPercent(root, "candidateMatchPercentage"),
                        Experience = ReadString(root, "candidateExperience"),
                        Description = ReadString(root, "candidateDescription"),
                        Skills = skills
                    };
                }
                catch (JsonException)
                {
                    // Gemini повернула не JSON — покажемо як є
                    return new ResumeAnalysis { Description = text };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        private static string ReadString(JsonElement root, string name) =>
            root.TryGetProperty(name, out var value) ? value.ToString() : string.Empty;

        private static int? ReadPercent(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            {
                return (int)Math.Round(number);
            }

            var text = value.ToString().Trim().TrimEnd('%');
            return double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? (int)Math.Round(parsed)
                : null;
        }

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

        // null = Lambda ще не встигла проаналізувати резюме
        public ResumeAnalysis? Analysis { get; set; }
    }

    public class ResumeAnalysis
    {
        public string CandidateName { get; set; } = string.Empty;
        public string BirthDate { get; set; } = string.Empty;
        public int? MatchPercentage { get; set; }
        public string Experience { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Skills { get; set; } = new();
    }
}
