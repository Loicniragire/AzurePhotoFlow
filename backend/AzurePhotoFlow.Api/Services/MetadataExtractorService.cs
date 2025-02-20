using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Icc;
using MetadataExtractor.Formats.Iptc;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Photoshop;
using MetadataExtractor.Formats.Xmp;
using Api.Interfaces;
using Api.Models;
using MetadataExtractor.Formats.Adobe;
using MetadataExtractor.Formats.FileType;

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

        // Log all directories and their tags.
        foreach (var directory in directories)
        {
            /* _log.LogInformation($"Directory: {directory.Name}"); */
            /* _log.LogInformation($"  Directory Type: {directory.GetType().Name}"); */
            /* foreach (var tag in directory.Tags) */
            /* { */
            /*     _log.LogInformation($"  {tag.Name} = {tag.Description}"); */
            /* } */
        }

        var metadata = new CameraGeneratedMetadata();

        // JPEG Directory
        var jpegDirectory = directories.OfType<JpegDirectory>().FirstOrDefault();
        if (jpegDirectory != null)
        {
            metadata.ImageWidth = jpegDirectory.GetInt32(JpegDirectory.TagImageWidth);
            metadata.ImageHeight = jpegDirectory.GetInt32(JpegDirectory.TagImageHeight);
            metadata.CompressionType = jpegDirectory.GetDescription(JpegDirectory.TagCompressionType);
            metadata.DataPrecision = jpegDirectory.GetInt32(JpegDirectory.TagDataPrecision);
            metadata.NumberOfComponents = jpegDirectory.GetInt32(JpegDirectory.TagNumberOfComponents);
        }

        // Exif IFD0 Directory
        var exifIfd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        Rational? xres = exifIfd0Directory.GetRational(ExifDirectoryBase.TagXResolution);
        Rational? yres = exifIfd0Directory.GetRational(ExifDirectoryBase.TagYResolution);
        if (exifIfd0Directory != null)
        {
            metadata.CameraMake = exifIfd0Directory.GetDescription(ExifDirectoryBase.TagMake);
            metadata.CameraModel = exifIfd0Directory.GetDescription(ExifDirectoryBase.TagModel);
            metadata.Orientation = exifIfd0Directory.GetDescription(ExifDirectoryBase.TagOrientation);
            metadata.XResolution = xres?.ToDouble();
            metadata.YResolution = yres?.ToDouble();
            metadata.ResolutionUnit = exifIfd0Directory.GetDescription(ExifDirectoryBase.TagResolutionUnit);
            metadata.Software = exifIfd0Directory.GetDescription(ExifDirectoryBase.TagSoftware);
            metadata.Artist = exifIfd0Directory.GetDescription(ExifDirectoryBase.TagArtist);
        }

        // Exif SubIFD Directory
        var exifSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        Rational? aperature = exifSubIfdDirectory.GetRational(ExifDirectoryBase.TagFNumber);
        Rational? focalPlaneXResolution = exifSubIfdDirectory.GetRational(ExifSubIfdDirectory.TagFocalPlaneXResolution);
        Rational? focalPlaneYResolution = exifSubIfdDirectory.GetRational(ExifSubIfdDirectory.TagFocalPlaneYResolution);
        Rational? focalLength = exifSubIfdDirectory.GetRational(ExifSubIfdDirectory.TagFocalLength);
        if (exifSubIfdDirectory != null)
        {
            metadata.DateTimeOriginal = exifSubIfdDirectory.GetDateTime(ExifDirectoryBase.TagDateTimeOriginal);
            metadata.DateTimeDigitized = exifSubIfdDirectory.GetDateTime(ExifDirectoryBase.TagDateTimeDigitized);
            metadata.ExposureTime = exifSubIfdDirectory.GetRational(ExifDirectoryBase.TagExposureTime);
            metadata.ShutterSpeed = exifSubIfdDirectory.GetRational(ExifDirectoryBase.TagExposureTime);
            metadata.Aperture = aperature?.ToDouble();
            metadata.Iso = exifSubIfdDirectory.GetInt32(ExifDirectoryBase.TagIsoEquivalent);
            metadata.ExposureProgram = exifSubIfdDirectory.GetDescription(ExifSubIfdDirectory.TagExposureProgram);
            metadata.SensitivityType = exifSubIfdDirectory.GetDescription(ExifSubIfdDirectory.TagSensitivityType);
            metadata.RecommendedExposureIndex = exifSubIfdDirectory.GetInt32(ExifSubIfdDirectory.TagRecommendedExposureIndex);
            metadata.ExifVersion = exifSubIfdDirectory.GetDescription(ExifSubIfdDirectory.TagExifVersion);
            metadata.ColorSpace = exifSubIfdDirectory.GetDescription(ExifSubIfdDirectory.TagColorSpace);
            metadata.FocalPlaneXResolution = focalPlaneXResolution?.ToDouble();
            metadata.FocalPlaneYResolution = focalPlaneYResolution?.ToDouble();
            metadata.FocalPlaneResolutionUnit = exifSubIfdDirectory.GetDescription(ExifSubIfdDirectory.TagFocalPlaneResolutionUnit);
            metadata.CustomRendered = exifSubIfdDirectory.GetDescription(ExifSubIfdDirectory.TagCustomRendered);
            metadata.ExposureMode = exifSubIfdDirectory.GetDescription(ExifSubIfdDirectory.TagExposureMode);
            metadata.WhiteBalance = exifSubIfdDirectory.GetDescription(ExifSubIfdDirectory.TagWhiteBalance);
            metadata.SceneCaptureType = exifSubIfdDirectory.GetDescription(ExifSubIfdDirectory.TagSceneCaptureType);
            metadata.BodySerialNumber = exifSubIfdDirectory.GetDescription(ExifSubIfdDirectory.TagBodySerialNumber);
            metadata.LensSpecification = exifSubIfdDirectory.GetDescription(ExifSubIfdDirectory.TagLensSpecification);
            metadata.LensModel = exifSubIfdDirectory.GetDescription(ExifSubIfdDirectory.TagLensModel);
            metadata.LensSerialNumber = exifSubIfdDirectory.GetDescription(ExifSubIfdDirectory.TagLensSerialNumber);
            metadata.MeteringMode = exifSubIfdDirectory.GetDescription(ExifSubIfdDirectory.TagMeteringMode);
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
            var altitude = gpsDirectory.GetRational(GpsDirectory.TagAltitude);
            if (altitude != null)
            {
                metadata.GpsAltitude = altitude.ToDouble();
            }
        }

        // XMP Directory
        var xmpDirectory = directories.OfType<XmpDirectory>().FirstOrDefault();
        if (xmpDirectory != null && xmpDirectory.XmpMeta != null)
        {
            /* metadata.XmpValueCount = xmpDirectory.XmpMeta.Count; */
        }

        // ICC Profile Directory
        var iccDirectory = directories.OfType<IccDirectory>().FirstOrDefault();
        if (iccDirectory != null)
        {
            /* metadata.IccProfileSize = iccDirectory.GetInt32(IccDirectory.TagProfileSize); */
            metadata.IccCmmType = iccDirectory.GetDescription(IccDirectory.TagCmmType);
            /* metadata.IccVersion = iccDirectory.GetDescription(IccDirectory.TagVersion); */
            /* metadata.IccClass = iccDirectory.GetDescription(IccDirectory.TagClass); */
            metadata.IccColorSpace = iccDirectory.GetDescription(IccDirectory.TagColorSpace);
            metadata.IccProfileConnectionSpace = iccDirectory.GetDescription(IccDirectory.TagProfileConnectionSpace);
            var profileDateStr = iccDirectory.GetString(IccDirectory.TagProfileDateTime);
            if (DateTime.TryParse(profileDateStr, out var profileDate))
            {
                metadata.IccProfileDateTime = profileDate;
            }
            /* metadata.IccPrimaryPlatform = iccDirectory.GetDescription(IccDirectory.TagPrimaryPlatform); */
            /* metadata.IccDeviceManufacturer = iccDirectory.GetDescription(IccDirectory.TagDeviceManufacturer); */
            metadata.IccDeviceModel = iccDirectory.GetDescription(IccDirectory.TagDeviceModel);
            metadata.IccTagCount = iccDirectory.GetInt32(IccDirectory.TagTagCount);
            /* metadata.IccProfileCopyright = iccDirectory.GetDescription(IccDirectory.TagProfileCopyright); */
            /* metadata.IccProfileDescription = iccDirectory.GetDescription(IccDirectory.TagProfileDescription); */
        }

        // Photoshop Directory
        var photoshopDirectory = directories.OfType<PhotoshopDirectory>().FirstOrDefault();
        if (photoshopDirectory != null)
        {
            metadata.PhotoshopResolutionInfo = photoshopDirectory.GetDescription(PhotoshopDirectory.TagResolutionInfo);
            /* metadata.PhotoshopThumbnailData = photoshopDirectory.GetDescription(PhotoshopDirectory.TagThumbnailData); */
            metadata.PhotoshopCaptionDigest = photoshopDirectory.GetDescription(PhotoshopDirectory.TagCaptionDigest);
        }

        // IPTC Directory
        var iptcDirectory = directories.OfType<IptcDirectory>().FirstOrDefault();
        if (iptcDirectory != null)
        {
            metadata.IptcCodedCharacterSet = iptcDirectory.GetDescription(IptcDirectory.TagCodedCharacterSet);
            metadata.IptcApplicationRecordVersion = iptcDirectory.GetInt32(IptcDirectory.TagApplicationRecordVersion);
            metadata.IptcDateCreated = iptcDirectory.GetString(IptcDirectory.TagDateCreated);
            metadata.IptcTimeCreated = iptcDirectory.GetString(IptcDirectory.TagTimeCreated);
            metadata.IptcDigitalDateCreated = iptcDirectory.GetString(IptcDirectory.TagDigitalDateCreated);
            metadata.IptcDigitalTimeCreated = iptcDirectory.GetString(IptcDirectory.TagDigitalTimeCreated);
            metadata.IptcByLine = iptcDirectory.GetDescription(IptcDirectory.TagByLine);
        }

        // Adobe JPEG Directory
        var adobeJpegDirectory = directories.OfType<AdobeJpegDirectory>().FirstOrDefault();
        if (adobeJpegDirectory != null)
        {
            metadata.AdobeDctEncodeVersion = adobeJpegDirectory.GetDescription(AdobeJpegDirectory.TagDctEncodeVersion);
            metadata.AdobeFlags0 = adobeJpegDirectory.GetDescription(AdobeJpegDirectory.TagApp14Flags0);
            metadata.AdobeFlags1 = adobeJpegDirectory.GetDescription(AdobeJpegDirectory.TagApp14Flags1);
            metadata.AdobeColorTransform = adobeJpegDirectory.GetDescription(AdobeJpegDirectory.TagColorTransform);
        }

        // File Type Directory
        var fileTypeDirectory = directories.OfType<FileTypeDirectory>().FirstOrDefault();
        if (fileTypeDirectory != null)
        {
            metadata.DetectedFileTypeName = fileTypeDirectory.GetDescription(FileTypeDirectory.TagDetectedFileTypeName);
            metadata.DetectedFileTypeLongName = fileTypeDirectory.GetDescription(FileTypeDirectory.TagDetectedFileTypeLongName);
            /* metadata.DetectedMimeType = fileTypeDirectory.GetDescription(FileTypeDirectory.TagDetectedMimeType); */
            metadata.ExpectedFileNameExtension = fileTypeDirectory.GetDescription(FileTypeDirectory.TagExpectedFileNameExtension);
        }

        return metadata;
    }
}
