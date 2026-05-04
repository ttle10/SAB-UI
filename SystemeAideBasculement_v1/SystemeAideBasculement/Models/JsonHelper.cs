namespace SystemeAideBasculement.Models
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

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
    }
}
