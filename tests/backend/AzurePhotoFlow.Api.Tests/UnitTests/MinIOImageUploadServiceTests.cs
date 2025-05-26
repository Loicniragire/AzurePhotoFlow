using Xunit;
using Moq;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using AzurePhotoFlow.Services;
using AzurePhotoFlow.Api.Models; // Assuming ProjectInfo, ProjectDirectory are here
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System; // For DateTime
using Api.Interfaces; // For IMetadataExtractorService, IImageUploadService
using System.Globalization; // For CultureInfo in DateTime.ParseExact

// Helper class for converting IEnumerable to IAsyncEnumerable
public static class AsyncEnumerableHelper
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            // Yielding ensures that the enumeration is deferred.
            yield return item;
        }
        // The await Task.CompletedTask is not strictly necessary for yield return to work
        // but can be useful if you needed an async method signature for other reasons.
        // For this specific conversion, it's often omitted or can be a simple await Task.Yield();
        await Task.CompletedTask; 
    }
}

namespace AzurePhotoFlow.Api.Tests.UnitTests
{
    public class MinIOImageUploadServiceTests
    {
        private readonly Mock<IMinioClient> _mockMinioClient;
        private readonly Mock<ILogger<MinIOImageUploadService>> _mockLogger;
        private readonly Mock<IMetadataExtractorService> _mockMetadataExtractorService;
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

            _service = new MinIOImageUploadService(
                _mockMinioClient.Object,
                _mockLogger.Object,
                _mockMetadataExtractorService.Object
            );

            // Default setup for BucketExistsAsync
            _mockMinioClient.Setup(c => c.BucketExistsAsync(
                It.IsAny<BucketExistsArgs>(), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            
            // Default setup for ListObjectsEnumAsync for the root prefix to discover date folders (non-recursive)
            // This call originates from ProcessHierarchyAsync with prefix ""
            _mockMinioClient.Setup(c => c.ListObjectsEnumAsync(
                It.IsAny<ListObjectsArgs>(), // Changed: Was It.Is<ListObjectsArgs>(args => args.BucketName == BucketName && args.Prefix == "" && !args.Recursive)
                It.IsAny<CancellationToken>()))
                .Returns(new List<Item> { 
                    new Item { Key = $"{TestTimestamp:yyyy-MM-dd}/", IsDir = true } 
                }.ToAsyncEnumerable());

            // Default setup for ListObjectsEnumAsync to discover project folders (non-recursive)
            // This call originates from ProcessHierarchyAsync with prefix "2023-01-01/"
            // This setup needs to be more specific if other non-recursive calls are made with different prefixes.
            // For now, relying on the order of setup or more specific setups later.
            _mockMinioClient.Setup(c => c.ListObjectsEnumAsync(
                It.IsAny<ListObjectsArgs>(), // Changed: Was It.Is<ListObjectsArgs>(args => args.BucketName == BucketName && args.Prefix == $"{TestTimestamp:yyyy-MM-dd}/" && !args.Recursive)
                It.IsAny<CancellationToken>()))
                .Returns(new List<Item> { 
                    new Item { Key = _projectHierarchyPrefix, IsDir = true } 
                }.ToAsyncEnumerable());
        }

        private void SetupListObjectsForCategory(string category, List<Item> items)
        {
            var categoryPrefix = $"{_projectHierarchyPrefix}{category}/"; // e.g., "2023-01-01/TestProject/RawFiles/"
            // This setup will now be less specific. Test logic relies on this being called with the correct categoryPrefix
            // by the SUT, and the mock returning the 'items' specific to that category.
            _mockMinioClient.Setup(c => c.ListObjectsEnumAsync(
                It.IsAny<ListObjectsArgs>(), // Changed: Was It.Is<ListObjectsArgs>(args => args.BucketName == BucketName && args.Prefix == categoryPrefix && args.Recursive)
                It.IsAny<CancellationToken>()))
                .Returns(items.ToAsyncEnumerable());
        }

        private void SetupListObjectsForRollCount(string category, string rollName, List<Item> fileItemsInRoll)
        {
            var rollPrefix = $"{_projectHierarchyPrefix}{category}/{rollName}/"; // e.g., "2023-01-01/TestProject/RawFiles/Roll1/"
            // Similar to SetupListObjectsForCategory, this is now less specific.
            _mockMinioClient.Setup(c => c.ListObjectsEnumAsync(
                It.IsAny<ListObjectsArgs>(), // Changed: Was It.Is<ListObjectsArgs>(args => args.BucketName == BucketName && args.Prefix == rollPrefix && args.Recursive)
                It.IsAny<CancellationToken>()))
                .Returns(fileItemsInRoll.ToAsyncEnumerable());
        }

        [Fact]
        public async Task GetProjectsAsync_EmptyProject_ReturnsProjectWithNoDirectories()
        {
            // Arrange
            // GetDirectoryDetailsAsync will be called for RawFiles and ProcessedFiles.
            // Simulate no items (files or inferred rolls) within these categories.
            SetupListObjectsForCategory("RawFiles", new List<Item>());
            SetupListObjectsForCategory("ProcessedFiles", new List<Item>());
            
            // Act
            var projects = await _service.GetProjectsAsync(TestYear, TestProjectName, TestTimestamp);

            // Assert
            Assert.NotNull(projects);
            var project = Assert.Single(projects);
            Assert.Equal(TestProjectName, project.Name);
            Assert.Equal(TestTimestamp, project.Datestamp);
            Assert.Empty(project.Directories);
        }

        [Fact]
        public async Task GetProjectsAsync_ProjectWithEmptyCategories_ReturnsProjectWithNoDirectories()
        {
            // Arrange - Same as EmptyProject for GetDirectoryDetailsAsync behavior
            SetupListObjectsForCategory("RawFiles", new List<Item>());
            SetupListObjectsForCategory("ProcessedFiles", new List<Item>());

            // Act
            var projects = await _service.GetProjectsAsync(TestYear, TestProjectName, TestTimestamp);

            // Assert
            Assert.NotNull(projects);
            var project = Assert.Single(projects);
            Assert.Equal(TestProjectName, project.Name);
            Assert.Equal(TestTimestamp, project.Datestamp);
            Assert.Empty(project.Directories);
        }
        
        [Fact]
        public async Task GetProjectsAsync_ProjectWithFilesInferredRolls_CorrectlyCountsFiles()
        {
            // Arrange
            var rawFilesItems = new List<Item>
            {
                new Item { Key = $"{_projectHierarchyPrefix}RawFiles/Roll1/img1.jpg", IsDir = false, Size = 100 },
                new Item { Key = $"{_projectHierarchyPrefix}RawFiles/Roll1/img2.jpg", IsDir = false, Size = 100 },
                new Item { Key = $"{_projectHierarchyPrefix}RawFiles/Roll2/img3.jpg", IsDir = false, Size = 100 }
            };
            SetupListObjectsForCategory("RawFiles", rawFilesItems);

            var processedFilesItems = new List<Item>
            {
                new Item { Key = $"{_projectHierarchyPrefix}ProcessedFiles/Roll1/img1_processed.jpg", IsDir = false, Size = 100 }
            };
            SetupListObjectsForCategory("ProcessedFiles", processedFilesItems);

            // Setup for CountFilesAsync (which is called by GetDirectoryDetailsAsync)
            SetupListObjectsForRollCount("RawFiles", "Roll1", new List<Item> { rawFilesItems[0], rawFilesItems[1] });
            SetupListObjectsForRollCount("RawFiles", "Roll2", new List<Item> { rawFilesItems[2] });
            SetupListObjectsForRollCount("ProcessedFiles", "Roll1", new List<Item> { processedFilesItems[0] });
            SetupListObjectsForRollCount("ProcessedFiles", "Roll2", new List<Item>()); // No processed files for Roll2

            // Act
            var projects = await _service.GetProjectsAsync(TestYear, TestProjectName, TestTimestamp);

            // Assert
            Assert.NotNull(projects);
            var project = Assert.Single(projects);
            Assert.Equal(TestProjectName, project.Name);
            Assert.Equal(TestTimestamp, project.Datestamp);
            Assert.Equal(2, project.Directories.Count);

            var roll1 = project.Directories.FirstOrDefault(d => d.Name == "Roll1");
            Assert.NotNull(roll1);
            Assert.Equal(2, roll1.RawFilesCount);
            Assert.Equal(1, roll1.ProcessedFilesCount);

            var roll2 = project.Directories.FirstOrDefault(d => d.Name == "Roll2");
            Assert.NotNull(roll2);
            Assert.Equal(1, roll2.RawFilesCount);
            Assert.Equal(0, roll2.ProcessedFilesCount);
        }

        [Fact]
        public async Task GetProjectsAsync_ProjectWithExplicitRollDirectoryMarkersOnly_NoFiles_ReturnsEmptyDirectories()
        {
            // Arrange
            // Simulate item.IsDir = true for rolls, but no actual files within them.
            // The new logic for GetDirectoryDetailsAsync skips item.IsDir == true, so it relies on files to infer rolls.
            var rawFilesCategoryItems = new List<Item>
            {
                // These directory markers will be skipped by the new logic in GetDirectoryDetailsAsync
                new Item { Key = $"{_projectHierarchyPrefix}RawFiles/Roll1/", IsDir = true },
                new Item { Key = $"{_projectHierarchyPrefix}RawFiles/Roll2/", IsDir = true }
            };
            SetupListObjectsForCategory("RawFiles", rawFilesCategoryItems);
            SetupListObjectsForCategory("ProcessedFiles", new List<Item>());

            // CountFilesAsync for these "empty" rolls will return 0.
            // This setup is crucial: even if a roll name *could* be inferred (which it won't be from IsDir=true items),
            // the count being 0 would lead to its exclusion or a zero count.
            // However, since IsDir=true items are skipped, rollNames set will be empty.
            SetupListObjectsForRollCount("RawFiles", "Roll1", new List<Item>());
            SetupListObjectsForRollCount("RawFiles", "Roll2", new List<Item>());
            
            // Act
            var projects = await _service.GetProjectsAsync(TestYear, TestProjectName, TestTimestamp);

            // Assert
            Assert.NotNull(projects);
            var project = Assert.Single(projects);
            Assert.Empty(project.Directories); // Because rolls are inferred from *files*, not directory markers.
        }
        
        [Fact]
        public async Task GetProjectsAsync_ProjectWithExplicitRollDirectoryMarkersAndFiles_CorrectlyCountsFiles()
        {
            // Arrange
            var rawFilesCategoryItems = new List<Item>
            {
                // The IsDir = true item will be skipped by GetDirectoryDetailsAsync's main loop.
                // Rolls are inferred from file paths.
                new Item { Key = $"{_projectHierarchyPrefix}RawFiles/Roll1/", IsDir = true }, 
                new Item { Key = $"{_projectHierarchyPrefix}RawFiles/Roll1/img1.jpg", IsDir = false, Size = 100 },
                new Item { Key = $"{_projectHierarchyPrefix}RawFiles/Roll2/img2.jpg", IsDir = false, Size = 100 }
            };
            SetupListObjectsForCategory("RawFiles", rawFilesCategoryItems);
            SetupListObjectsForCategory("ProcessedFiles", new List<Item>());

            // Setup for CountFilesAsync
            SetupListObjectsForRollCount("RawFiles", "Roll1", new List<Item> { rawFilesCategoryItems[1] }); // Only img1.jpg
            SetupListObjectsForRollCount("RawFiles", "Roll2", new List<Item> { rawFilesCategoryItems[2] }); // Only img2.jpg
            SetupListObjectsForRollCount("ProcessedFiles", "Roll1", new List<Item>());
            SetupListObjectsForRollCount("ProcessedFiles", "Roll2", new List<Item>());
            
            // Act
            var projects = await _service.GetProjectsAsync(TestYear, TestProjectName, TestTimestamp);

            // Assert
            Assert.NotNull(projects);
            var project = Assert.Single(projects);
            Assert.Equal(2, project.Directories.Count);

            var roll1 = project.Directories.FirstOrDefault(d => d.Name == "Roll1");
            Assert.NotNull(roll1);
            Assert.Equal(1, roll1.RawFilesCount);
            Assert.Equal(0, roll1.ProcessedFilesCount);

            var roll2 = project.Directories.FirstOrDefault(d => d.Name == "Roll2");
            Assert.NotNull(roll2);
            Assert.Equal(1, roll2.RawFilesCount);
            Assert.Equal(0, roll2.ProcessedFilesCount);
        }

        [Fact]
        public async Task GetProjectsAsync_ProjectWithFilesDirectlyInCategory_NoRolls_ReturnsEmptyDirectories()
        {
            // Arrange
            var rawFilesCategoryItems = new List<Item>
            {
                new Item { Key = $"{_projectHierarchyPrefix}RawFiles/img1.jpg", IsDir = false, Size = 100 },
                new Item { Key = $"{_projectHierarchyPrefix}RawFiles/img2.jpg", IsDir = false, Size = 100 }
            };
            SetupListObjectsForCategory("RawFiles", rawFilesCategoryItems);
            SetupListObjectsForCategory("ProcessedFiles", new List<Item>());
            
            // No calls to SetupListObjectsForRollCount needed as no rolls should be identified.

            // Act
            var projects = await _service.GetProjectsAsync(TestYear, TestProjectName, TestTimestamp);

            // Assert
            Assert.NotNull(projects);
            var project = Assert.Single(projects);
            Assert.Empty(project.Directories); // Files directly in category are not treated as rolls.
        }

        [Fact]
        public async Task GetProjectsAsync_ProjectWithMixedContent_CorrectlyIdentifiesRollsAndCounts()
        {
            // Arrange
            var rawFilesCategoryItems = new List<Item>
            {
                new Item { Key = $"{_projectHierarchyPrefix}RawFiles/RollA/file1.jpg", IsDir = false, Size = 100 },
                new Item { Key = $"{_projectHierarchyPrefix}RawFiles/RollA/file2.jpg", IsDir = false, Size = 100 },
                new Item { Key = $"{_projectHierarchyPrefix}RawFiles/RollB/", IsDir = true }, // This will be skipped
                new Item { Key = $"{_projectHierarchyPrefix}RawFiles/img_directly_in_raw.jpg", IsDir = false, Size = 100 }
            };
            SetupListObjectsForCategory("RawFiles", rawFilesCategoryItems);

            var processedFilesCategoryItems = new List<Item>
            {
                new Item { Key = $"{_projectHierarchyPrefix}ProcessedFiles/RollA/file1_proc.jpg", IsDir = false, Size = 100 }
            };
            SetupListObjectsForCategory("ProcessedFiles", processedFilesCategoryItems);

            // Setup for CountFilesAsync
            SetupListObjectsForRollCount("RawFiles", "RollA", new List<Item> { rawFilesCategoryItems[0], rawFilesCategoryItems[1] });
             // RollB is defined by an IsDir=true item, but the new logic infers rolls from files.
             // If there were files like "ProjectPrefix/RawFiles/RollB/file.jpg", then RollB would be detected.
             // Since there are no such files in rawFilesCategoryItems, "RollB" won't be added to rollNames.
             // Thus, we don't strictly need SetupListObjectsForRollCount for "RollB" unless a file path implied it.
             // For completeness, if it *were* detected and had no files:
            SetupListObjectsForRollCount("RawFiles", "RollB", new List<Item>()); 
            SetupListObjectsForRollCount("ProcessedFiles", "RollA", new List<Item> { processedFilesCategoryItems[0] });
            SetupListObjectsForRollCount("ProcessedFiles", "RollB", new List<Item>());


            // Act
            var projects = await _service.GetProjectsAsync(TestYear, TestProjectName, TestTimestamp);

            // Assert
            Assert.NotNull(projects);
            var project = Assert.Single(projects);
            
            // Expected: Only RollA should be found. RollB (IsDir=true) is skipped, and img_directly_in_raw.jpg doesn't form a roll.
            Assert.Single(project.Directories); 

            var rollA = project.Directories.FirstOrDefault(d => d.Name == "RollA");
            Assert.NotNull(rollA);
            Assert.Equal(2, rollA.RawFilesCount);
            Assert.Equal(1, rollA.ProcessedFilesCount);

            var rollB = project.Directories.FirstOrDefault(d => d.Name == "RollB");
            Assert.Null(rollB); // RollB should not be present as it was only an IsDir=true item or had no files.
        }
    }
}
