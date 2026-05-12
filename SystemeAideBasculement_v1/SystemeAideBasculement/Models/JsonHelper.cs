using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using Json.Schema;

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

        public static bool Validate(string rawJson, JsonSchema schema, out string jsonValidationError)
        {
            jsonValidationError = string.Empty;

            try
            {
                using var document = JsonDocument.Parse(rawJson);
                var result = schema.Evaluate(document.RootElement);

                if (result.IsValid)
                {
                    return true;
                }

                jsonValidationError = string.Join(
                    Environment.NewLine,
                    result.Errors?.Select(e => $"{e.Key}: {e.Value}") ?? Enumerable.Empty<string>());

                return false;
            }
            catch (JsonException ex)
            {
                jsonValidationError = ex.Message;
                return false;
            }
        }

        public static List<T> DeserializeList<T>(string rawJson)
        {
            try
            {
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
                var single = JsonSerializer.Deserialize<T>(rawJson, JsonOptions)
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
