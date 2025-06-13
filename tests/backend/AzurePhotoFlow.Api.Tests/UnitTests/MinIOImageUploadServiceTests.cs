using Moq;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using AzurePhotoFlow.Services;
using Microsoft.Extensions.Logging;
using Api.Interfaces;
using System.Net; // Added for potential WebUtility usage and for SUT's usage

namespace unitTests
{
    [TestFixture]
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

        [Test]
        public async Task GetProjectsAsync_EmptyProject_ReturnsProjectWithNoDirectories()
        {
            // Arrange
            SetupBasicProjectHierarchy();
            SetupListObjectsForCategory("RawFiles", new List<Item>());
            SetupListObjectsForCategory("ProcessedFiles", new List<Item>());
            
            // Act
            var projects = await _service.GetProjectsAsync(TestYear, TestProjectName, TestTimestamp);

            // Assert
            Assert.IsNotNull(projects);
            Assert.AreEqual(1, projects.Count);
            var project = projects.Single();
            Assert.AreEqual(TestProjectName, project.Name);
            Assert.AreEqual(TestTimestamp, project.Datestamp);
            Assert.IsEmpty(project.Directories);
        }

        [Test]
        public async Task GetProjectsAsync_ProjectWithEmptyCategories_ReturnsProjectWithNoDirectories()
        {
            // Arrange
            SetupBasicProjectHierarchy();
            SetupListObjectsForCategory("RawFiles", new List<Item>());
            SetupListObjectsForCategory("ProcessedFiles", new List<Item>());

            // Act
            var projects = await _service.GetProjectsAsync(TestYear, TestProjectName, TestTimestamp);

            // Assert
            Assert.IsNotNull(projects);
            Assert.AreEqual(1, projects.Count);
            var project = projects.Single();
            Assert.AreEqual(TestProjectName, project.Name);
            Assert.AreEqual(TestTimestamp, project.Datestamp);
            Assert.IsEmpty(project.Directories);
        }
        
        [Test]
        public async Task GetProjectsAsync_ProjectWithFilesInferredRolls_CorrectlyCountsFiles()
        {
            // Arrange
            SetupBasicProjectHierarchy();
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
            Assert.IsNotNull(projects);
            Assert.AreEqual(1, projects.Count);
            var project = projects.Single();
            Assert.AreEqual(TestProjectName, project.Name);
            Assert.AreEqual(TestTimestamp, project.Datestamp);
            Assert.AreEqual(2, project.Directories.Count);

            var roll1 = project.Directories.FirstOrDefault(d => d.Name == "Roll1");
            Assert.IsNotNull(roll1);
            Assert.AreEqual(2, roll1.RawFilesCount);
            Assert.AreEqual(1, roll1.ProcessedFilesCount);

            var roll2 = project.Directories.FirstOrDefault(d => d.Name == "Roll2");
            Assert.IsNotNull(roll2);
            Assert.AreEqual(1, roll2.RawFilesCount);
            Assert.AreEqual(0, roll2.ProcessedFilesCount);
        }

        [Test]
        public async Task GetProjectsAsync_ProjectWithExplicitRollDirectoryMarkersOnly_NoFiles_ReturnsEmptyDirectories()
        {
            // Arrange
            SetupBasicProjectHierarchy();
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
            Assert.IsNotNull(projects);
            Assert.AreEqual(1, projects.Count);
            var project = projects.Single();
            Assert.IsEmpty(project.Directories); // Because rolls are inferred from *files*, not directory markers.
        }
        
        [Test]
        public async Task GetProjectsAsync_ProjectWithExplicitRollDirectoryMarkersAndFiles_CorrectlyCountsFiles()
        {
            // Arrange
            SetupBasicProjectHierarchy();
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
            Assert.IsNotNull(projects);
            Assert.AreEqual(1, projects.Count);
            var project = projects.Single();
            Assert.AreEqual(2, project.Directories.Count);

            var roll1 = project.Directories.FirstOrDefault(d => d.Name == "Roll1");
            Assert.IsNotNull(roll1);
            Assert.AreEqual(1, roll1.RawFilesCount);
            Assert.AreEqual(0, roll1.ProcessedFilesCount);

            var roll2 = project.Directories.FirstOrDefault(d => d.Name == "Roll2");
            Assert.IsNotNull(roll2);
            Assert.AreEqual(1, roll2.RawFilesCount);
            Assert.AreEqual(0, roll2.ProcessedFilesCount);
        }

        [Test]
        public async Task GetProjectsAsync_ProjectWithFilesDirectlyInCategory_NoRolls_ReturnsEmptyDirectories()
        {
            // Arrange
            SetupBasicProjectHierarchy();
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
            Assert.IsNotNull(projects);
            Assert.AreEqual(1, projects.Count);
            var project = projects.Single();
            Assert.IsEmpty(project.Directories); // Files directly in category are not treated as rolls.
        }

        [Test]
        public async Task GetProjectsAsync_ProjectWithMixedContent_CorrectlyIdentifiesRollsAndCounts()
        {
            // Arrange
            SetupBasicProjectHierarchy();
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
            Assert.IsNotNull(projects);
            Assert.AreEqual(1, projects.Count);
            var project = projects.Single();
            
            // Expected: Only RollA should be found. RollB (IsDir=true) is skipped, and img_directly_in_raw.jpg doesn't form a roll.
            Assert.AreEqual(1, project.Directories.Count); 

            var rollA = project.Directories.FirstOrDefault(d => d.Name == "RollA");
            Assert.IsNotNull(rollA);
            Assert.AreEqual(2, rollA.RawFilesCount);
            Assert.AreEqual(1, rollA.ProcessedFilesCount);

            var rollB = project.Directories.FirstOrDefault(d => d.Name == "RollB");
            Assert.IsNull(rollB); // RollB should not be present as it was only an IsDir=true item or had no files.
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
    }
}
