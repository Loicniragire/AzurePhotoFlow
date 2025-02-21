using System.Text.Json;
using System.Text.Json.Serialization;
using MetadataExtractor;

namespace AzurePhotoFlow.Services;
public class RationalConverter : JsonConverter<Rational>
{
    public override Rational Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Ensure we start with an object.
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token.");
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

