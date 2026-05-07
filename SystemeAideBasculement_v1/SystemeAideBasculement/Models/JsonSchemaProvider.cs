using Newtonsoft.Json.Schema;

namespace SystemeAideBasculement.Models
{
    public class JsonSchemaProvider
    {
        private readonly Dictionary<string, JSchema> _schemas;

        public JsonSchemaProvider(string rootPath)
        {
            _schemas = new Dictionary<string, JSchema>(StringComparer.OrdinalIgnoreCase)
            {
                ["ClientNotification"] = Load(rootPath, "ClientNotification.schema.json"),
            };
        }

        public JSchema Get(string name)
        {
            if (!_schemas.TryGetValue(name, out var schema))
                throw new KeyNotFoundException($"Schema '{name}' not found.");

            return schema;
        }

        private static JSchema Load(string rootPath, string fileName)
        {
            var path = Path.Combine(rootPath, "config", fileName);

            if (!File.Exists(path))
                throw new FileNotFoundException($"Schema file not found: {path}");

            var json = File.ReadAllText(path);

            return JSchema.Parse(json);
        }
    }
}
