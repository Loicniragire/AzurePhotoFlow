using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Icc;
using MetadataExtractor.Formats.Iptc;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Photoshop;
using MetadataExtractor.Formats.Xmp;
using MetadataExtractor.Formats.Adobe;
using MetadataExtractor.Formats.FileType;
using Api.Interfaces;
using Api.Models;
using MetadataDirectory = MetadataExtractor.Directory;


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

        // (Optional) Logging has been commented out.
        /*
        foreach (var directory in directories)
        {
            _log.LogInformation($"Directory: {directory.Name}");
            _log.LogInformation($"  Directory Type: {directory.GetType().Name}");
            foreach (var tag in directory.Tags)
            {
                _log.LogInformation($"  {tag.Name} = {tag.Description}");
            }
        }
        */

        var metadata = new CameraGeneratedMetadata();

        // JPEG Directory
        var jpegDirectory = directories.OfType<JpegDirectory>().FirstOrDefault();
        if (jpegDirectory != null)
        {
            metadata.ImageWidth = SafeGetInt32(jpegDirectory, JpegDirectory.TagImageWidth);
            metadata.ImageHeight = SafeGetInt32(jpegDirectory, JpegDirectory.TagImageHeight);
            metadata.CompressionType = SafeGetDescription(jpegDirectory, JpegDirectory.TagCompressionType);
            metadata.DataPrecision = SafeGetInt32(jpegDirectory, JpegDirectory.TagDataPrecision);
            metadata.NumberOfComponents = SafeGetInt32(jpegDirectory, JpegDirectory.TagNumberOfComponents);
        }

        // Exif IFD0 Directory
        var exifIfd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        if (exifIfd0Directory != null)
        {
            metadata.CameraMake = SafeGetDescription(exifIfd0Directory, ExifDirectoryBase.TagMake);
            metadata.CameraModel = SafeGetDescription(exifIfd0Directory, ExifDirectoryBase.TagModel);
            metadata.Orientation = SafeGetDescription(exifIfd0Directory, ExifDirectoryBase.TagOrientation);
            Rational? xres = SafeGetRational(exifIfd0Directory, ExifDirectoryBase.TagXResolution);
            Rational? yres = SafeGetRational(exifIfd0Directory, ExifDirectoryBase.TagYResolution);
            metadata.XResolution = xres?.ToDouble();
            metadata.YResolution = yres?.ToDouble();
            metadata.ResolutionUnit = SafeGetDescription(exifIfd0Directory, ExifDirectoryBase.TagResolutionUnit);
            metadata.Software = SafeGetDescription(exifIfd0Directory, ExifDirectoryBase.TagSoftware);
            metadata.Artist = SafeGetDescription(exifIfd0Directory, ExifDirectoryBase.TagArtist);
        }

        // Exif SubIFD Directory
        var exifSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        if (exifSubIfdDirectory != null)
        {
            metadata.DateTimeOriginal = SafeGetDateTime(exifSubIfdDirectory, ExifDirectoryBase.TagDateTimeOriginal);
            metadata.DateTimeDigitized = SafeGetDateTime(exifSubIfdDirectory, ExifDirectoryBase.TagDateTimeDigitized);

            metadata.ShutterSpeed = SafeGetRational(exifSubIfdDirectory, ExifDirectoryBase.TagExposureTime);

            Rational? aperture = SafeGetRational(exifSubIfdDirectory, ExifDirectoryBase.TagFNumber);
            metadata.Aperture = aperture?.ToDouble();

            metadata.Iso = SafeGetInt32(exifSubIfdDirectory, ExifDirectoryBase.TagIsoEquivalent);
            metadata.ExposureProgram = SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagExposureProgram);
            metadata.SensitivityType = SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagSensitivityType);
            metadata.RecommendedExposureIndex = SafeGetInt32(exifSubIfdDirectory, ExifSubIfdDirectory.TagRecommendedExposureIndex);
            metadata.ExifVersion = SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagExifVersion);
            metadata.ColorSpace = SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagColorSpace);

            Rational? focalPlaneXResolution = SafeGetRational(exifSubIfdDirectory, ExifSubIfdDirectory.TagFocalPlaneXResolution);
            metadata.FocalPlaneXResolution = focalPlaneXResolution?.ToDouble();
            Rational? focalPlaneYResolution = SafeGetRational(exifSubIfdDirectory, ExifSubIfdDirectory.TagFocalPlaneYResolution);
            metadata.FocalPlaneYResolution = focalPlaneYResolution?.ToDouble();
            metadata.FocalPlaneResolutionUnit = SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagFocalPlaneResolutionUnit);
            metadata.CustomRendered = SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagCustomRendered);
            metadata.ExposureMode = SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagExposureMode);
            metadata.WhiteBalance = SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagWhiteBalance);
            metadata.SceneCaptureType = SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagSceneCaptureType);
            metadata.BodySerialNumber = SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagBodySerialNumber);
            metadata.LensSpecification = SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagLensSpecification);
            metadata.LensModel = SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagLensModel);
            metadata.LensSerialNumber = SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagLensSerialNumber);
            metadata.MeteringMode = SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagMeteringMode);
            Rational? focalLength = SafeGetRational(exifSubIfdDirectory, ExifSubIfdDirectory.TagFocalLength);
            metadata.FocalLength = focalLength?.ToDouble();
        }

        // GPS Directory
        var gpsDirectory = directories.OfType<GpsDirectory>().FirstOrDefault();
        if (gpsDirectory != null)
        {
            var location = gpsDirectory.GetGeoLocation();
            if (location != null)
            {
                metadata.GpsLatitude = location.Latitude;
                metadata.GpsLongitude = location.Longitude;
            }
            Rational? altitude = SafeGetRational(gpsDirectory, GpsDirectory.TagAltitude);
            metadata.GpsAltitude = altitude?.ToDouble();
        }

        // XMP Directory
        var xmpDirectory = directories.OfType<XmpDirectory>().FirstOrDefault();
        if (xmpDirectory != null && xmpDirectory.XmpMeta != null)
        {
			 metadata.XmpValueCount = xmpDirectory.XmpMeta.Properties.Count();
        }

        // ICC Profile Directory
        var iccDirectory = directories.OfType<IccDirectory>().FirstOrDefault();
        if (iccDirectory != null)
        {
            // Example: if (iccDirectory.ContainsTag(IccDirectory.TagProfileSize)) { ... }
            metadata.IccCmmType = SafeGetDescription(iccDirectory, IccDirectory.TagCmmType);
            metadata.IccColorSpace = SafeGetDescription(iccDirectory, IccDirectory.TagColorSpace);
            metadata.IccProfileConnectionSpace = SafeGetDescription(iccDirectory, IccDirectory.TagProfileConnectionSpace);
            string profileDateStr = SafeGetString(iccDirectory, IccDirectory.TagProfileDateTime);
            if (DateTime.TryParse(profileDateStr, out var profileDate))
            {
                metadata.IccProfileDateTime = profileDate;
            }
            metadata.IccDeviceModel = SafeGetDescription(iccDirectory, IccDirectory.TagDeviceModel);
            metadata.IccTagCount = SafeGetInt32(iccDirectory, IccDirectory.TagTagCount);
        }

        // Photoshop Directory
        var photoshopDirectory = directories.OfType<PhotoshopDirectory>().FirstOrDefault();
        if (photoshopDirectory != null)
        {
            metadata.PhotoshopResolutionInfo = SafeGetDescription(photoshopDirectory, PhotoshopDirectory.TagResolutionInfo);
            metadata.PhotoshopCaptionDigest = SafeGetDescription(photoshopDirectory, PhotoshopDirectory.TagCaptionDigest);
        }

        // IPTC Directory
        var iptcDirectory = directories.OfType<IptcDirectory>().FirstOrDefault();
        if (iptcDirectory != null)
        {
            metadata.IptcCodedCharacterSet = SafeGetDescription(iptcDirectory, IptcDirectory.TagCodedCharacterSet);
            metadata.IptcApplicationRecordVersion = SafeGetInt32(iptcDirectory, IptcDirectory.TagApplicationRecordVersion);
            metadata.IptcDateCreated = SafeGetString(iptcDirectory, IptcDirectory.TagDateCreated);
            metadata.IptcTimeCreated = SafeGetString(iptcDirectory, IptcDirectory.TagTimeCreated);
            metadata.IptcDigitalDateCreated = SafeGetString(iptcDirectory, IptcDirectory.TagDigitalDateCreated);
            metadata.IptcDigitalTimeCreated = SafeGetString(iptcDirectory, IptcDirectory.TagDigitalTimeCreated);
            metadata.IptcByLine = SafeGetDescription(iptcDirectory, IptcDirectory.TagByLine);
        }

        // Adobe JPEG Directory
        var adobeJpegDirectory = directories.OfType<AdobeJpegDirectory>().FirstOrDefault();
        if (adobeJpegDirectory != null)
        {
            metadata.AdobeDctEncodeVersion = SafeGetDescription(adobeJpegDirectory, AdobeJpegDirectory.TagDctEncodeVersion);
            metadata.AdobeFlags0 = SafeGetDescription(adobeJpegDirectory, AdobeJpegDirectory.TagApp14Flags0);
            metadata.AdobeFlags1 = SafeGetDescription(adobeJpegDirectory, AdobeJpegDirectory.TagApp14Flags1);
            metadata.AdobeColorTransform = SafeGetDescription(adobeJpegDirectory, AdobeJpegDirectory.TagColorTransform);
        }

        // File Type Directory
        var fileTypeDirectory = directories.OfType<FileTypeDirectory>().FirstOrDefault();
        if (fileTypeDirectory != null)
        {
            metadata.DetectedFileTypeName = SafeGetDescription(fileTypeDirectory, FileTypeDirectory.TagDetectedFileTypeName);
            metadata.DetectedFileTypeLongName = SafeGetDescription(fileTypeDirectory, FileTypeDirectory.TagDetectedFileTypeLongName);
            metadata.ExpectedFileNameExtension = SafeGetDescription(fileTypeDirectory, FileTypeDirectory.TagExpectedFileNameExtension);
        }

        return metadata;
    }

    // Helper methods to safely extract values without throwing exceptions.

    private static Rational? SafeGetRational(MetadataDirectory directory, int tag)
    {
        try
        {
            return directory.ContainsTag(tag) ? directory.GetRational(tag) : null;
        }
        catch
        {
            return null;
        }
    }

    private static int? SafeGetInt32(MetadataDirectory directory, int tag)
    {
        try
        {
            return directory.ContainsTag(tag) ? directory.GetInt32(tag) : null;
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? SafeGetDateTime(MetadataDirectory directory, int tag)
    {
        try
        {
            return directory.ContainsTag(tag) ? directory.GetDateTime(tag) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string SafeGetDescription(MetadataDirectory directory, int tag)
    {
        try
        {
            return directory.ContainsTag(tag) ? directory.GetDescription(tag) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string SafeGetString(MetadataDirectory directory, int tag)
    {
        try
        {
            return directory.ContainsTag(tag) ? directory.GetString(tag) : null;
        }
        catch
        {
            return null;
        }
    }
}
