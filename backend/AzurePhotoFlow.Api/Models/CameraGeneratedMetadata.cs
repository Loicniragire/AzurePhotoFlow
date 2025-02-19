namespace Api.Models;

public class CameraGeneratedMetadata
{
    /// <summary>
    /// The date and time when the photo was taken, if available.
    /// Derived from ExifDirectoryBase.TagDateTimeOriginal or TagDateTime.
    /// </summary>
    public DateTime? DateTimeOriginal { get; set; }

    /// <summary>
    /// The camera manufacturer, if available.
    /// Derived from ExifDirectoryBase.TagMake.
    /// </summary>
    public string CameraMake { get; set; }

    /// <summary>
    /// The camera model, if available.
    /// Derived from ExifDirectoryBase.TagModel.
    /// </summary>
    public string CameraModel { get; set; }

    /// <summary>
    /// The focal length in millimeters, if available.
    /// Derived from ExifDirectoryBase.TagFocalLength.
    /// </summary>
    public double? FocalLength { get; set; }

    /// <summary>
    /// The aperture (f-stop) value, if available.
    /// Derived from ExifDirectoryBase.TagFNumber or TagAperture.
    /// </summary>
    public double? Aperture { get; set; }

    /// <summary>
    /// The shutter speed in seconds, if available.
    /// Derived from ExifDirectoryBase.TagExposureTime or TagShutterSpeed.
    /// </summary>
    public double? ShutterSpeed { get; set; }

    /// <summary>
    /// The ISO speed rating, if available.
    /// Derived from ExifDirectoryBase.TagIsoEquivalent.
    /// </summary>
    public int? Iso { get; set; }

    /// <summary>
    /// The latitude, if available.
    /// Derived from GpsDirectory.TagLatitude.
    /// </summary>
    public double? GpsLatitude { get; set; }

    /// <summary>
    /// The longitude, if available.
    /// Derived from GpsDirectory.TagLongitude.
    /// </summary>
    public double? GpsLongitude { get; set; }

    /// <summary>
    /// The altitude in meters, if available.
    /// Derived from GpsDirectory.TagAltitude.
    /// </summary>
    public double? GpsAltitude { get; set; }

    /// <summary>
    /// The orientation of the image, if available (e.g., Normal, Rotated 90).
    /// Derived from ExifDirectoryBase.TagOrientation.
    /// </summary>
    public string Orientation { get; set; }
}
