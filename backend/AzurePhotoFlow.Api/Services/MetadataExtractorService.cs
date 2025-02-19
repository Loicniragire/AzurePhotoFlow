using Api.Models;
using Api.Interfaces;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace AzurePhotoFlow.Services;

public class MetadataExtractorService : IMetadataExtractorService
{
    private readonly ILogger<MetadataExtractorService> _log;
    public MetadataExtractorService(ILogger<MetadataExtractorService> log)
    {
        _log = log;
    }

    public CameraGeneratedMetadata GetCameraGeneratedMetadata(Stream imageStream)
    {
        if (imageStream.CanSeek)
            imageStream.Position = 0;

        var directories = ImageMetadataReader.ReadMetadata(imageStream);

        var exifSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        var gpsDirectory = directories.OfType<GpsDirectory>().FirstOrDefault();
        var exifIfd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();

        var focalLength = exifSubIfdDirectory?.GetRational(ExifDirectoryBase.TagFocalLength);
        var aperture = exifSubIfdDirectory?.GetRational(ExifDirectoryBase.TagFNumber);
        var shutterSpeed = exifSubIfdDirectory?.GetRational(ExifDirectoryBase.TagExposureTime);

        var metadata = new CameraGeneratedMetadata
        {
            DateTimeOriginal = exifSubIfdDirectory?.GetDateTime(ExifDirectoryBase.TagDateTimeOriginal),
            CameraMake = exifIfd0Directory?.GetDescription(ExifDirectoryBase.TagMake),
            CameraModel = exifIfd0Directory?.GetDescription(ExifDirectoryBase.TagModel),
			FocalLength = focalLength?.ToDouble(),
			Aperture = aperture?.ToDouble(),
			ShutterSpeed = shutterSpeed?.ToDouble(),
            Iso = exifSubIfdDirectory?.GetInt32(ExifDirectoryBase.TagIsoEquivalent),
            Orientation = exifIfd0Directory?.GetDescription(ExifDirectoryBase.TagOrientation),
        };


        // GPS data extraction (if present)
        if (gpsDirectory != null)
        {
            var location = gpsDirectory.GetGeoLocation();
            if (location != null)
            {
                metadata.GpsLatitude = location.Latitude;
                metadata.GpsLongitude = location.Longitude;
            }

            var altitude = gpsDirectory.GetRational(GpsDirectory.TagAltitude);
            if (altitude != null)
            {
                metadata.GpsAltitude = altitude.ToDouble();
            }
        }

        return metadata;
    }
}
