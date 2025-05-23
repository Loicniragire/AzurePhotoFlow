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
using AzurePhotoFlow.POCO.QueueModels;


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

        var metadata = new CameraGeneratedMetadata();
        var directories = ImageMetadataReader.ReadMetadata(imageStream);

        var jpegDirectory = directories.OfType<JpegDirectory>().FirstOrDefault();
        var exifIfd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        var exifSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        var gpsDirectory = directories.OfType<GpsDirectory>().FirstOrDefault();
        var xmpDirectory = directories.OfType<XmpDirectory>().FirstOrDefault();
        var iccDirectory = directories.OfType<IccDirectory>().FirstOrDefault();
        var photoshopDirectory = directories.OfType<PhotoshopDirectory>().FirstOrDefault();
        var iptcDirectory = directories.OfType<IptcDirectory>().FirstOrDefault();
        var adobeJpegDirectory = directories.OfType<AdobeJpegDirectory>().FirstOrDefault();
        var fileTypeDirectory = directories.OfType<FileTypeDirectory>().FirstOrDefault();

        var actions = new List<Action>();

        if (jpegDirectory != null)
        {
            actions.Add(() =>
            {
                metadata.ImageWidth = MetadataHelper.SafeGetInt32(jpegDirectory, JpegDirectory.TagImageWidth);
                metadata.ImageHeight = MetadataHelper.SafeGetInt32(jpegDirectory, JpegDirectory.TagImageHeight);
                metadata.CompressionType = MetadataHelper.SafeGetDescription(jpegDirectory, JpegDirectory.TagCompressionType);
                metadata.DataPrecision = MetadataHelper.SafeGetInt32(jpegDirectory, JpegDirectory.TagDataPrecision);
                metadata.NumberOfComponents = MetadataHelper.SafeGetInt32(jpegDirectory, JpegDirectory.TagNumberOfComponents);
            });
        }

        if (exifIfd0Directory != null)
        {
            actions.Add(() =>
         {
             metadata.CameraMake = MetadataHelper.SafeGetDescription(exifIfd0Directory, ExifDirectoryBase.TagMake);
             metadata.CameraModel = MetadataHelper.SafeGetDescription(exifIfd0Directory, ExifDirectoryBase.TagModel);
             metadata.Orientation = MetadataHelper.SafeGetDescription(exifIfd0Directory, ExifDirectoryBase.TagOrientation);
             Rational? xres = MetadataHelper.SafeGetRational(exifIfd0Directory, ExifDirectoryBase.TagXResolution);
             Rational? yres = MetadataHelper.SafeGetRational(exifIfd0Directory, ExifDirectoryBase.TagYResolution);
             metadata.XResolution = xres?.ToDouble();
             metadata.YResolution = yres?.ToDouble();
             metadata.ResolutionUnit = MetadataHelper.SafeGetDescription(exifIfd0Directory, ExifDirectoryBase.TagResolutionUnit);
             metadata.Software = MetadataHelper.SafeGetDescription(exifIfd0Directory, ExifDirectoryBase.TagSoftware);
             metadata.Artist = MetadataHelper.SafeGetDescription(exifIfd0Directory, ExifDirectoryBase.TagArtist);

         });

        }

        if (exifSubIfdDirectory != null)
        {
            actions.Add(() =>
         {
             metadata.DateTimeOriginal = MetadataHelper.SafeGetDateTime(exifSubIfdDirectory, ExifDirectoryBase.TagDateTimeOriginal);
             metadata.DateTimeDigitized = MetadataHelper.SafeGetDateTime(exifSubIfdDirectory, ExifDirectoryBase.TagDateTimeDigitized);

             metadata.ShutterSpeed = MetadataHelper.SafeGetRational(exifSubIfdDirectory, ExifDirectoryBase.TagExposureTime);

             Rational? aperture = MetadataHelper.SafeGetRational(exifSubIfdDirectory, ExifDirectoryBase.TagFNumber);
             metadata.Aperture = aperture?.ToDouble();

             metadata.Iso = MetadataHelper.SafeGetInt32(exifSubIfdDirectory, ExifDirectoryBase.TagIsoEquivalent);
             metadata.ExposureProgram = MetadataHelper.SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagExposureProgram);
             metadata.SensitivityType = MetadataHelper.SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagSensitivityType);
             metadata.RecommendedExposureIndex = MetadataHelper.SafeGetInt32(exifSubIfdDirectory, ExifSubIfdDirectory.TagRecommendedExposureIndex);
             metadata.ExifVersion = MetadataHelper.SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagExifVersion);
             metadata.ColorSpace = MetadataHelper.SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagColorSpace);

             Rational? focalPlaneXResolution = MetadataHelper.SafeGetRational(exifSubIfdDirectory, ExifSubIfdDirectory.TagFocalPlaneXResolution);
             metadata.FocalPlaneXResolution = focalPlaneXResolution?.ToDouble();
             Rational? focalPlaneYResolution = MetadataHelper.SafeGetRational(exifSubIfdDirectory, ExifSubIfdDirectory.TagFocalPlaneYResolution);
             metadata.FocalPlaneYResolution = focalPlaneYResolution?.ToDouble();
             metadata.FocalPlaneResolutionUnit = MetadataHelper.SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagFocalPlaneResolutionUnit);
             metadata.CustomRendered = MetadataHelper.SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagCustomRendered);
             metadata.ExposureMode = MetadataHelper.SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagExposureMode);
             metadata.WhiteBalance = MetadataHelper.SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagWhiteBalance);
             metadata.SceneCaptureType = MetadataHelper.SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagSceneCaptureType);
             metadata.BodySerialNumber = MetadataHelper.SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagBodySerialNumber);
             metadata.LensSpecification = MetadataHelper.SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagLensSpecification);
             metadata.LensModel = MetadataHelper.SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagLensModel);
             metadata.LensSerialNumber = MetadataHelper.SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagLensSerialNumber);
             metadata.MeteringMode = MetadataHelper.SafeGetDescription(exifSubIfdDirectory, ExifSubIfdDirectory.TagMeteringMode);
             Rational? focalLength = MetadataHelper.SafeGetRational(exifSubIfdDirectory, ExifSubIfdDirectory.TagFocalLength);
             metadata.FocalLength = focalLength?.ToDouble();

         });
        }

        if (gpsDirectory != null)
        {
            actions.Add(() =>
         {
             var location = gpsDirectory.GetGeoLocation();
             if (location != null)
             {
                 metadata.GpsLatitude = location.Latitude;
                 metadata.GpsLongitude = location.Longitude;
             }
             Rational? altitude = MetadataHelper.SafeGetRational(gpsDirectory, GpsDirectory.TagAltitude);
             metadata.GpsAltitude = altitude?.ToDouble();

         });
        }

        if (xmpDirectory != null)
        {
            actions.Add(() =>
         {
             metadata.XmpValueCount = xmpDirectory.XmpMeta.Properties.Count();

         });
        }

        if (iccDirectory != null)
        {
            actions.Add(() =>
         {
             // Example: if (iccDirectory.ContainsTag(IccDirectory.TagProfileSize)) { ... }
             metadata.IccCmmType = MetadataHelper.SafeGetDescription(iccDirectory, IccDirectory.TagCmmType);
             metadata.IccColorSpace = MetadataHelper.SafeGetDescription(iccDirectory, IccDirectory.TagColorSpace);
             metadata.IccProfileConnectionSpace = MetadataHelper.SafeGetDescription(iccDirectory, IccDirectory.TagProfileConnectionSpace);
             string profileDateStr = MetadataHelper.SafeGetString(iccDirectory, IccDirectory.TagProfileDateTime);
             if (DateTime.TryParse(profileDateStr, out var profileDate))
             {
                 metadata.IccProfileDateTime = profileDate;
             }
             metadata.IccDeviceModel = MetadataHelper.SafeGetDescription(iccDirectory, IccDirectory.TagDeviceModel);
             metadata.IccTagCount = MetadataHelper.SafeGetInt32(iccDirectory, IccDirectory.TagTagCount);

         });
        }

        if (photoshopDirectory != null)
        {
            actions.Add(() =>
         {
             metadata.PhotoshopResolutionInfo = MetadataHelper.SafeGetDescription(photoshopDirectory, PhotoshopDirectory.TagResolutionInfo);
             metadata.PhotoshopCaptionDigest = MetadataHelper.SafeGetDescription(photoshopDirectory, PhotoshopDirectory.TagCaptionDigest);
         });
        }

        if (iptcDirectory != null)
        {
            actions.Add(() =>
         {
             metadata.IptcCodedCharacterSet = MetadataHelper.SafeGetDescription(iptcDirectory, IptcDirectory.TagCodedCharacterSet);
             metadata.IptcApplicationRecordVersion = MetadataHelper.SafeGetInt32(iptcDirectory, IptcDirectory.TagApplicationRecordVersion);
             metadata.IptcDateCreated = MetadataHelper.SafeGetString(iptcDirectory, IptcDirectory.TagDateCreated);
             metadata.IptcTimeCreated = MetadataHelper.SafeGetString(iptcDirectory, IptcDirectory.TagTimeCreated);
             metadata.IptcDigitalDateCreated = MetadataHelper.SafeGetString(iptcDirectory, IptcDirectory.TagDigitalDateCreated);
             metadata.IptcDigitalTimeCreated = MetadataHelper.SafeGetString(iptcDirectory, IptcDirectory.TagDigitalTimeCreated);
             metadata.IptcByLine = MetadataHelper.SafeGetDescription(iptcDirectory, IptcDirectory.TagByLine);

         });
        }

        if (adobeJpegDirectory != null)
        {
            actions.Add(() =>
         {
             metadata.AdobeDctEncodeVersion = MetadataHelper.SafeGetDescription(adobeJpegDirectory, AdobeJpegDirectory.TagDctEncodeVersion);
             metadata.AdobeFlags0 = MetadataHelper.SafeGetDescription(adobeJpegDirectory, AdobeJpegDirectory.TagApp14Flags0);
             metadata.AdobeFlags1 = MetadataHelper.SafeGetDescription(adobeJpegDirectory, AdobeJpegDirectory.TagApp14Flags1);
             metadata.AdobeColorTransform = MetadataHelper.SafeGetDescription(adobeJpegDirectory, AdobeJpegDirectory.TagColorTransform);

         });
        }

        if (fileTypeDirectory != null)
        {
            actions.Add(() =>
         {
             metadata.DetectedFileTypeName = MetadataHelper.SafeGetDescription(fileTypeDirectory, FileTypeDirectory.TagDetectedFileTypeName);
             metadata.DetectedFileTypeLongName = MetadataHelper.SafeGetDescription(fileTypeDirectory, FileTypeDirectory.TagDetectedFileTypeLongName);
             metadata.ExpectedFileNameExtension = MetadataHelper.SafeGetDescription(fileTypeDirectory, FileTypeDirectory.TagExpectedFileNameExtension);

         });
        }

        Parallel.Invoke(actions.ToArray());

        return metadata;
    }

}
