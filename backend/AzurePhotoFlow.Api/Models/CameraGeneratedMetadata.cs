using System.Reflection;
using MetadataExtractor;

namespace Api.Models;

public class CameraGeneratedMetadata
{
    // Common Exif properties
    public DateTime? DateTimeOriginal { get; set; }
    public DateTime? DateTimeDigitized { get; set; }
    public string CameraMake { get; set; }
    public string CameraModel { get; set; }
    public double? FocalLength { get; set; }
    public double? Aperture { get; set; }
    public Rational? ShutterSpeed { get; set; }
    public int? Iso { get; set; }
    public string Orientation { get; set; }

    // JPEG Directory properties
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public string CompressionType { get; set; }
    public int? DataPrecision { get; set; }
    public int? NumberOfComponents { get; set; }

    // Exif IFD0 additional properties
    public double? XResolution { get; set; }
    public double? YResolution { get; set; }
    public string ResolutionUnit { get; set; }
    public string Software { get; set; }
    public string Artist { get; set; }

    // Exif SubIFD additional properties
    public Rational? ExposureTime { get; set; }
    public string ExposureProgram { get; set; }
    public string SensitivityType { get; set; }
    public int? RecommendedExposureIndex { get; set; }
    public string ExifVersion { get; set; }
    public string SubSecTimeOriginal { get; set; }
    public string SubSecTimeDigitized { get; set; }
    public string ColorSpace { get; set; }
    public double? FocalPlaneXResolution { get; set; }
    public double? FocalPlaneYResolution { get; set; }
    public string FocalPlaneResolutionUnit { get; set; }
    public string CustomRendered { get; set; }
    public string ExposureMode { get; set; }
    public string WhiteBalance { get; set; }
    public string SceneCaptureType { get; set; }
    public string BodySerialNumber { get; set; }
    public string LensSpecification { get; set; }
    public string LensModel { get; set; }
    public string LensSerialNumber { get; set; }
    public double? ExposureBiasValue { get; set; }
    public double? MaxApertureValue { get; set; }
    public string MeteringMode { get; set; }

    // GPS properties
    public double? GpsLatitude { get; set; }
    public double? GpsLongitude { get; set; }
    public double? GpsAltitude { get; set; }

    // XMP properties
    public int? XmpValueCount { get; set; }

    // ICC Profile properties
    public int? IccProfileSize { get; set; }
    public string IccCmmType { get; set; }
    public string IccVersion { get; set; }
    public string IccClass { get; set; }
    public string IccColorSpace { get; set; }
    public string IccProfileConnectionSpace { get; set; }
    public DateTime? IccProfileDateTime { get; set; }
    public string IccPrimaryPlatform { get; set; }
    public string IccDeviceManufacturer { get; set; }
    public string IccDeviceModel { get; set; }
    public int? IccTagCount { get; set; }
    public string IccProfileCopyright { get; set; }
    public string IccProfileDescription { get; set; }

    // Photoshop properties
    public string PhotoshopResolutionInfo { get; set; }
    public string PhotoshopThumbnailData { get; set; }
    public string PhotoshopCaptionDigest { get; set; }

    // IPTC properties
    public string IptcCodedCharacterSet { get; set; }
    public int? IptcApplicationRecordVersion { get; set; }
    public string IptcDateCreated { get; set; }
    public string IptcTimeCreated { get; set; }
    public string IptcDigitalDateCreated { get; set; }
    public string IptcDigitalTimeCreated { get; set; }
    public string IptcByLine { get; set; }

    // Adobe JPEG properties
    public string AdobeDctEncodeVersion { get; set; }
    public string AdobeFlags0 { get; set; }
    public string AdobeFlags1 { get; set; }
    public string AdobeColorTransform { get; set; }

    // File Type properties
    public string DetectedFileTypeName { get; set; }
    public string DetectedFileTypeLongName { get; set; }
    public string DetectedMimeType { get; set; }
    public string ExpectedFileNameExtension { get; set; }

	/// <summary>
	/// Returns a string representation of the camera generated metadata.
	/// Removes properties with null values to reduce size.
	/// </summary>
    public override string ToString()
    {
        var properties = GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var nonNullProperties = properties.Where(p => p.GetValue(this) != null);
        return string.Join(Environment.NewLine, nonNullProperties.Select(p => $"{p.Name}: {p.GetValue(this)}"));
    }
}
