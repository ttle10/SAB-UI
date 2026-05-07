using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemeAideBasculement.Models
{
    public class JsonHelper
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters =
                {
                    new JsonStringEnumConverter()
                }
            };


        public static async Task<List<T>> LoadListAsync<T>(string path)
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
        }

        public static bool Validate(string rawJson, JSchema schema, out string jsonValidationError)
        {
            jsonValidationError = string.Empty;

            JToken token;
            try
            {
                token = JToken.Parse(rawJson);
            }
            catch (Newtonsoft.Json.JsonReaderException ex)
            {
                throw new JsonException("Malformed JSON payload.", ex);
            }

            IList<ValidationError> errors;
            bool isValid = token.IsValid(schema, out errors);

            if (!isValid)
            {
                var errorPayload = new
                {
                    error = "Invalid notification schema.",
                    details = errors.Select(e => new
                    {
                        path = e.Path,
                        message = e.Message,
                        errorType = e.ErrorType.ToString()
                    })
                };

                jsonValidationError = System.Text.Json.JsonSerializer.Serialize(errorPayload);
            }

            return isValid;
        }

        public static List<T> DeserializeList<T>(string rawJson)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rawJson))
                    throw new JsonException("Empty JSON payload.");

                   return JsonSerializer.Deserialize<List<T>>(rawJson, JsonOptions) ?? new List<T>();
            }
            catch (JsonException ex)
            {
                throw new JsonException("Failed to deserialize JSON Array payload.", ex);
            }
        }

        public static T Deserialize<T>(string rawJson)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rawJson))
                    throw new JsonException("Empty JSON payload.");

                var single = System.Text.Json.JsonSerializer.Deserialize<T>(rawJson, JsonOptions)
                                                ?? throw new JsonException("Invalid notification object.");
                return single;

            }
            catch (JsonException ex)
            {
                throw new JsonException("Failed to deserialize JSON payload.", ex);
            }
        }
    }
}
