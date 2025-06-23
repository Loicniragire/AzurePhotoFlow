using System.Text.Json;
using System.Text.Json.Serialization;
using MetadataExtractor;
using Newtonsoft.Json;

namespace AzurePhotoFlow.Services;

/// <summary>
/// System.Text.Json converter for MetadataExtractor.Rational
/// </summary>
public class RationalConverter : System.Text.Json.Serialization.JsonConverter<Rational>
{
    public override Rational Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Ensure we start with an object.
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new System.Text.Json.JsonException("Expected StartObject token.");
        }
        
        long numerator = 0;
        long denominator = 1; // Default denominator value.
        
        while (reader.Read())
        {
            // Break at the end of the object.
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }
            
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString();
                reader.Read(); // Advance to the property value.
                
                if (propertyName == "Numerator")
                {
                    numerator = reader.GetInt64();
                }
                else if (propertyName == "Denominator")
                {
                    denominator = reader.GetInt64();
                }
                else
                {
                    // Skip any unknown properties.
                    reader.Skip();
                }
            }
        }
        
        return new Rational(numerator, denominator);
    }

    public override void Write(Utf8JsonWriter writer, Rational value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("Numerator", value.Numerator);
        writer.WriteNumber("Denominator", value.Denominator);
        writer.WriteEndObject();
    }
}

/// <summary>
/// Newtonsoft.Json converter for MetadataExtractor.Rational
/// </summary>
public class NewtonsoftRationalConverter : Newtonsoft.Json.JsonConverter<Rational>
{
    public override void WriteJson(JsonWriter writer, Rational value, Newtonsoft.Json.JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("Numerator");
        writer.WriteValue(value.Numerator);
        writer.WritePropertyName("Denominator");
        writer.WriteValue(value.Denominator);
        writer.WriteEndObject();
    }

    public override Rational ReadJson(JsonReader reader, Type objectType, Rational existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.StartObject)
        {
            throw new Newtonsoft.Json.JsonException("Expected StartObject token.");
        }

        long numerator = 0;
        long denominator = 1;

        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.EndObject)
            {
                break;
            }

            if (reader.TokenType == JsonToken.PropertyName)
            {
                string propertyName = reader.Value.ToString();
                reader.Read();

                if (propertyName == "Numerator")
                {
                    numerator = Convert.ToInt64(reader.Value);
                }
                else if (propertyName == "Denominator")
                {
                    denominator = Convert.ToInt64(reader.Value);
                }
            }
        }

        return new Rational(numerator, denominator);
    }
}

