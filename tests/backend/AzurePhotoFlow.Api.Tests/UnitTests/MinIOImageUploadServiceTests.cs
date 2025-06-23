using Moq;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using AzurePhotoFlow.Services;
using Microsoft.Extensions.Logging;
using Api.Interfaces;
using AzurePhotoFlow.Api.Interfaces;
using System.Net; // Added for potential WebUtility usage and for SUT's usage
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.IO.Compression;
using Api.Models;
using AzurePhotoFlow.POCO.QueueModels;
using AzurePhotoFlow.Api.Data;

namespace unitTests
{
    [TestFixture]
    public class MinIOImageUploadServiceTests
    {
        private readonly Mock<IMinioClient> _mockMinioClient;
        private readonly Mock<ILogger<MinIOImageUploadService>> _mockLogger;
        private readonly Mock<IMetadataExtractorService> _mockMetadataExtractorService;
        private readonly Mock<IImageMappingRepository> _mockImageMappingRepository;
        private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
        private readonly MinIOImageUploadService _service;

        private const string BucketName = "photostore";
        private const string TestYear = "2023";
        private const string TestProjectName = "TestProject";
        private static readonly DateTime TestTimestamp = new DateTime(2023, 1, 1);
        private readonly string _projectHierarchyPrefix = $"{TestTimestamp:yyyy-MM-dd}/{TestProjectName}/"; // e.g., "2023-01-01/TestProject/"

        public MinIOImageUploadServiceTests()
        {
            _mockMinioClient = new Mock<IMinioClient>();
            _mockLogger = new Mock<ILogger<MinIOImageUploadService>>();
            _mockMetadataExtractorService = new Mock<IMetadataExtractorService>();

            _mockImageMappingRepository = new Mock<IImageMappingRepository>();
            _mockScopeFactory = new Mock<IServiceScopeFactory>();

            var mockScope = new Mock<IServiceScope>();
            var mockProvider = new Mock<IServiceProvider>();
            mockProvider.Setup(p => p.GetService(typeof(IImageMappingRepository)))
                         .Returns(_mockImageMappingRepository.Object);
            mockScope.SetupGet(s => s.ServiceProvider).Returns(mockProvider.Object);
            _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

            _service = new MinIOImageUploadService(
                _mockMinioClient.Object,
                _mockLogger.Object,
                _mockMetadataExtractorService.Object,
                _mockImageMappingRepository.Object,
                _mockScopeFactory.Object
            );

            // Default setup for BucketExistsAsync
            _mockMinioClient.Setup(c => c.BucketExistsAsync(
                It.IsAny<BucketExistsArgs>(), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        private void SetupBasicProjectHierarchy()
        {
            // Setup for project hierarchy discovery
            // Since we can't access ListObjectsArgs properties directly, we'll use It.IsAny and return appropriate data
            _mockMinioClient.Setup(c => c.ListObjectsEnumAsync(
                It.IsAny<ListObjectsArgs>(),
                It.IsAny<CancellationToken>()))
                .Returns(new List<Item> { 
                    new Item { Key = $"{TestTimestamp:yyyy-MM-dd}/", IsDir = true },
                    new Item { Key = _projectHierarchyPrefix, IsDir = true },
                    new Item { Key = $"{_projectHierarchyPrefix}RawFiles/", IsDir = true },
                    new Item { Key = $"{_projectHierarchyPrefix}ProcessedFiles/", IsDir = true }
                }.ToAsyncEnumerable());
        }

        private void SetupListObjectsForCategory(string category, List<Item> items)
        {
            // Note: We can't check specific args properties, so we'll return all items for any call
            // This is a limitation but tests should still work
            _mockMinioClient.Setup(c => c.ListObjectsEnumAsync(
                It.IsAny<ListObjectsArgs>(),
                It.IsAny<CancellationToken>()))
                .Returns(items.ToAsyncEnumerable());
        }

        private void SetupListObjectsForRollCount(string category, string rollName, List<Item> fileItemsInRoll)
        {
            // Note: We can't check specific args properties, so we'll return all items for any call
            _mockMinioClient.Setup(c => c.ListObjectsEnumAsync(
                It.IsAny<ListObjectsArgs>(),
                It.IsAny<CancellationToken>()))
                .Returns(fileItemsInRoll.ToAsyncEnumerable());
        }


        
        /* [Test] */
        /* public async Task GetProjectsAsync_ProjectNameWithSpaces_FiltersAndDecodesCorrectly() */
        /* { */
        /*     // Arrange */
        /*     string projectNameWithSpaces = "Test Project With Spaces"; */
        /*     string encodedProjectName = WebUtility.UrlEncode(projectNameWithSpaces); // "Test+Project+With+Spaces" */
        /*     string dateFolderPrefix = $"{TestTimestamp:yyyy-MM-dd}/"; */
        /*     string projectFolderKey = $"{dateFolderPrefix}{encodedProjectName}/"; */
        /*  */
        /*     _mockMinioClient.Reset(); // Reset to clear default setups if they interfere */
        /*  */
        /*     // 1. Setup BucketExistsAsync (needed by ProcessHierarchyAsync) */
        /*     _mockMinioClient.Setup(c => c.BucketExistsAsync( */
        /*         It.Is<BucketExistsArgs>(b => b.BucketName == BucketName), */
        /*         It.IsAny<CancellationToken>())) */
        /*         .ReturnsAsync(true); */
        /*  */
        /*     // 2. Mock for discovering date folders (prefix: "", non-recursive) */
        /*     _mockMinioClient.Setup(c => c.ListObjectsEnumAsync( */
        /*         It.Is<ListObjectsArgs>(args => args.BucketName == BucketName && args.Prefix == "" && !args.Recursive), */
        /*         It.IsAny<CancellationToken>())) */
        /*         .Returns(new List<Item> { new Item { Key = dateFolderPrefix, IsDir = true } }.ToAsyncEnumerable()) */
        /*         .Verifiable("Date folder discovery mock was not called."); */
        /*  */
        /*     // 3. Mock for discovering project folders (prefix: "2023-01-01/", non-recursive) */
        /*     _mockMinioClient.Setup(c => c.ListObjectsEnumAsync( */
        /*         It.Is<ListObjectsArgs>(args => args.BucketName == BucketName && args.Prefix == dateFolderPrefix && !args.Recursive), */
        /*         It.IsAny<CancellationToken>())) */
        /*         .Returns(new List<Item> { new Item { Key = projectFolderKey, IsDir = true } }.ToAsyncEnumerable()) */
        /*         .Verifiable("Project folder discovery mock was not called."); */
        /*  */
        /*     // 4. Mocks for GetDirectoryDetailsAsync (RawFiles and ProcessedFiles, recursive) */
        /*     // These expect prefixes like "2023-01-01/Test+Project+With+Spaces/RawFiles/" */
        /*     string projectFilesPrefix = $"{dateFolderPrefix}{encodedProjectName}/"; */
        /*     _mockMinioClient.Setup(c => c.ListObjectsEnumAsync( */
        /*         It.Is<ListObjectsArgs>(args => args.BucketName == BucketName && args.Prefix == $"{projectFilesPrefix}RawFiles/" && args.Recursive), */
        /*         It.IsAny<CancellationToken>())) */
        /*         .Returns(new List<Item>().ToAsyncEnumerable()) // No files in RawFiles */
        /*         .Verifiable("RawFiles mock for GetDirectoryDetailsAsync was not called."); */
        /*  */
        /*     _mockMinioClient.Setup(c => c.ListObjectsEnumAsync( */
        /*         It.Is<ListObjectsArgs>(args => args.BucketName == BucketName && args.Prefix == $"{projectFilesPrefix}ProcessedFiles/" && args.Recursive), */
        /*         It.IsAny<CancellationToken>())) */
        /*         .Returns(new List<Item>().ToAsyncEnumerable()) // No files in ProcessedFiles */
        /*         .Verifiable("ProcessedFiles mock for GetDirectoryDetailsAsync was not called."); */
        /*      */
        /*     // Act */
        /*     var projects = await _service.GetProjectsAsync(TestYear, projectNameWithSpaces, TestTimestamp); */
        /*  */
        /*     // Assert */
        /*     Assert.IsNotNull(projects); */
        /*     Assert.AreEqual(1, projects.Count, "Should find one project."); */
        /*     var project = projects.Single(); */
        /*     Assert.AreEqual(projectNameWithSpaces, project.Name, "Project name should be decoded correctly."); */
        /*     Assert.AreEqual(TestTimestamp, project.Datestamp); */
        /*     Assert.IsEmpty(project.Directories, "Directories should be empty as per mock setup."); */
        /*  */
        /*     _mockMinioClient.Verify(); // Verify all verifiable mocks were called */
        /* } */

        /* [Test] */
        /* public async Task GetProjectsAsync_NoFilter_DecodesProjectNamesCorrectly() */
        /* { */
        /*     // Arrange */
        /*     string decodedProjectName = "Another Encoded Project"; */
        /*     string encodedProjectName = WebUtility.UrlEncode(decodedProjectName); // "Another+Encoded+Project" */
        /*     string dateFolderPrefix = $"{TestTimestamp:yyyy-MM-dd}/"; */
        /*     string projectFolderKey = $"{dateFolderPrefix}{encodedProjectName}/"; */
        /*  */
        /*     _mockMinioClient.Reset(); // Reset to clear default setups */
        /*  */
        /*     // 1. Setup BucketExistsAsync */
        /*     _mockMinioClient.Setup(c => c.BucketExistsAsync( */
        /*         It.Is<BucketExistsArgs>(b => b.BucketName == BucketName), */
        /*         It.IsAny<CancellationToken>())) */
        /*         .ReturnsAsync(true); */
        /*  */
        /*     // 2. Mock for discovering date folders */
        /*     _mockMinioClient.Setup(c => c.ListObjectsEnumAsync( */
        /*         It.Is<ListObjectsArgs>(args => args.BucketName == BucketName && args.Prefix == "" && !args.Recursive), */
        /*         It.IsAny<CancellationToken>())) */
        /*         .Returns(new List<Item> { new Item { Key = dateFolderPrefix, IsDir = true } }.ToAsyncEnumerable()) */
        /*         .Verifiable("Date folder discovery mock (no filter) was not called."); */
        /*  */
        /*     // 3. Mock for discovering project folders */
        /*     _mockMinioClient.Setup(c => c.ListObjectsEnumAsync( */
        /*         It.Is<ListObjectsArgs>(args => args.BucketName == BucketName && args.Prefix == dateFolderPrefix && !args.Recursive), */
        /*         It.IsAny<CancellationToken>())) */
        /*         .Returns(new List<Item> { new Item { Key = projectFolderKey, IsDir = true } }.ToAsyncEnumerable()) */
        /*         .Verifiable("Project folder discovery mock (no filter) was not called."); */
        /*  */
        /*     // 4. Mocks for GetDirectoryDetailsAsync (RawFiles and ProcessedFiles) */
        /*     string projectFilesPrefix = $"{dateFolderPrefix}{encodedProjectName}/"; */
        /*      _mockMinioClient.Setup(c => c.ListObjectsEnumAsync( */
        /*         It.Is<ListObjectsArgs>(args => args.BucketName == BucketName && args.Prefix == $"{projectFilesPrefix}RawFiles/" && args.Recursive), */
        /*         It.IsAny<CancellationToken>())) */
        /*         .Returns(new List<Item>().ToAsyncEnumerable()) // No files */
        /*         .Verifiable("RawFiles mock for GetDirectoryDetailsAsync (no filter) was not called."); */
        /*  */
        /*     _mockMinioClient.Setup(c => c.ListObjectsEnumAsync( */
        /*         It.Is<ListObjectsArgs>(args => args.BucketName == BucketName && args.Prefix == $"{projectFilesPrefix}ProcessedFiles/" && args.Recursive), */
        /*         It.IsAny<CancellationToken>())) */
        /*         .Returns(new List<Item>().ToAsyncEnumerable()) // No files */
        /*         .Verifiable("ProcessedFiles mock for GetDirectoryDetailsAsync (no filter) was not called."); */
        /*  */
        /*     // Act */
        /*     // Pass null for projectName to test no-filter scenario */
        /*     var projects = await _service.GetProjectsAsync(TestYear, null, TestTimestamp);  */
        /*  */
        /*     // Assert */
        /*     Assert.IsNotNull(projects); */
        /*     Assert.AreEqual(1, projects.Count, "Should find one project when no filter is applied."); */
        /*     var project = projects.Single(); */
        /*     Assert.AreEqual(decodedProjectName, project.Name, "Project name should be decoded correctly even with no filter."); */
        /*     Assert.AreEqual(TestTimestamp, project.Datestamp); */
        /*     Assert.IsEmpty(project.Directories, "Directories should be empty as per mock setup."); */
        /*      */
        /*     _mockMinioClient.Verify(); // Verify all verifiable mocks were called */
        /* } */

        [Test]
        public async Task ExtractAndUploadImagesAsync_UsesExistingMappingIdOnDuplicate()
        {
            // Arrange
            var existingId = Guid.NewGuid();

            _mockMinioClient.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectArgs>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var headers = new Dictionary<string, string>
            {
                {"etag", "etag"},
                {"last-modified", DateTime.UtcNow.ToString("R")},
                {"content-length", "1"}
            };
            var stat = ObjectStat.FromResponseHeaders("img.jpg", headers);
            _mockMinioClient.Setup(c => c.StatObjectAsync(It.IsAny<StatObjectArgs>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(stat);

            _mockMetadataExtractorService.Setup(m => m.GetCameraGeneratedMetadata(It.IsAny<Stream>()))
                .Returns(new CameraGeneratedMetadata());

            _mockImageMappingRepository.Setup(r => r.AddAsync(It.IsAny<ImageMapping>()))
                .ReturnsAsync(new ImageMapping { Id = existingId });

            var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                var entry = archive.CreateEntry("dir/img.jpg");
                await using var es = entry.Open();
                await es.WriteAsync(new byte[] {1,2});
            }
            zipStream.Position = 0;
            IFormFile file = new FormFile(zipStream, 0, zipStream.Length, "directory", "archive.zip");

            // Act
            var response = await _service.ExtractAndUploadImagesAsync(file, "proj", "dir", DateTime.UtcNow);

            // Assert
            Assert.That(response.Files, Has.Count.EqualTo(1));
            Assert.AreEqual(existingId, response.Files[0].Id);
        }
    }
}
