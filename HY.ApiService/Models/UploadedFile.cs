namespace HY.ApiService.Models
{
    public class UploadedFile
    {
        public required string TempPath { get; init; }
        public required string FileName { get; init; }
        public required string ContentType { get; init; }
        public required long Length { get; init; }
        public required string Sha256 { get; init; }
    }
}
