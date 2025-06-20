using NUnit.Framework;
using AzurePhotoFlow.Services;

namespace unitTests;

[TestFixture]
public class UploadConfigHelperTests
{
    [TearDown]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("MAX_UPLOAD_SIZE_MB", null);
    }

    [Test]
    public void DefaultLimit_WhenEnvVarMissing_Returns100MB()
    {
        Environment.SetEnvironmentVariable("MAX_UPLOAD_SIZE_MB", null);
        long result = UploadConfigHelper.GetMultipartBodyLengthLimit();
        Assert.AreEqual(104_857_600, result);
    }

    [Test]
    public void CustomLimit_FromEnvVar_ReturnsValueInBytes()
    {
        Environment.SetEnvironmentVariable("MAX_UPLOAD_SIZE_MB", "512");
        long result = UploadConfigHelper.GetMultipartBodyLengthLimit();
        Assert.AreEqual(512 * 1024L * 1024L, result);
    }
}
