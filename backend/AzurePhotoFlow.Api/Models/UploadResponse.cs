namespace Api.Models;

public class UploadResponse
{
	public int UploadedCount { get; set; }
	public int OriginalCount { get; set; }
	public List<UploadedFileInfo> Files { get; set; } = new();
}

public class UploadedFileInfo
{
	public Guid Id { get; set; }
	public string FileName { get; set; } = string.Empty;
	public string ObjectKey { get; set; } = string.Empty;
	public bool Success { get; set; }
	public string? ErrorMessage { get; set; }
	public long FileSize { get; set; }
	public string ContentType { get; set; } = string.Empty;
	public int? Width { get; set; }
	public int? Height { get; set; }
}
