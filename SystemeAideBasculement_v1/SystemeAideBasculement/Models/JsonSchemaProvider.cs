using Json.Schema;
using System.IO;

namespace SystemeAideBasculement.Models
{
    public sealed class JsonSchemaProvider
    {
        private readonly Dictionary<string, JsonSchema> _cache =
                        new(StringComparer.OrdinalIgnoreCase);
        private readonly string _rootPath;

        public JsonSchemaProvider(string rootPath)
        {
            _rootPath = rootPath;
        }

        public JsonSchema Get(string schemaName)
        {
            if (_cache.TryGetValue(schemaName, out var cached))
            {
                return cached;
            }

            var filePath = Path.Combine(
                  _rootPath,
                  schemaName);

            var schema = Load(_rootPath, schemaName);
            _cache[schemaName] = schema;

            return schema;
        }

        private static JsonSchema Load(string rootPath, string schemaName)
        {

            var schemaFileName = Path.Combine(
                rootPath,
                "config",
                $"{schemaName}.schema.json");

            if (!File.Exists(schemaFileName))
            {
                throw new FileNotFoundException(
                    $"JSON schema file not found for '{schemaFileName}'.",
                    schemaFileName);
            }

            var schemaText = File.ReadAllText(schemaFileName);

            return JsonSchema.FromText(schemaText);
        }
    }
}
