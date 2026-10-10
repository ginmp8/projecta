using System.Text.Json;
using Projecta.Json;
using Projecta.Mapping;
using Projecta.SystemTextJson;

internal static class NumericTests
{
    internal static void Register(Action<string, Action> test)
    {
        foreach (double number in new[] { 1e-100, -1e-100, double.Epsilon, 1.2345678901234567, 1e100 })
            test("writer: exact double roundtrip " + number, () =>
            {
                using var doc = JsonDocument.Parse(NativeJsonWriter.ToUtf8Bytes(new JValue(number)));
                Check.Equal(number, doc.RootElement.GetDouble());
            });
        foreach (double number in new[] { 1e-100, -1e-100, double.Epsilon, 1.2345678901234567 })
        {
            test("numbers: native parser " + number, () => Check.Equal(number, (double)JToken.Parse(number.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
            test("numbers: JsonElement reader " + number, () =>
            { using var json = JsonDocument.Parse(number.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); Check.Equal(number, (double)new Projecta.Objects.ObjectReader().Read(json.RootElement)); });
            test("numbers: STJ bridge " + number, () =>
            { using var json = JsonDocument.Parse(number.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); Check.Equal(number, (double)Projecta.SystemTextJson.ProjectaExtensions.FromSystemTextJson(json.RootElement)); });
            test("numbers: native-to-node " + number, () => Check.Equal(number, new JValue(number).ToSystemTextJsonNode()!.GetValue<double>()));
        }
        test("numbers: decimals serialize without truncation", () =>
        { foreach (decimal d in new[] { -0.1234567890123456789m, 0.000000001234567890123456789m, 12345678901234567890123.45m })
          { using var j = JsonDocument.Parse(new JValue(d).ToFlatString()); Check.Equal(d,j.RootElement.GetDecimal()); } });
        test("numbers: distinct tiny floating values are not equal", () => Check.True(!new JValue(1e-100).DeepEquals(new JValue(2e-100))));
        test("definition: schema names are case sensitive", () =>
        {
            var d = ProjectionDefinition.Parse("{\"expression\":\"$\",\"inputSchema\":{\"properties\":{\"a\":true,\"A\":false}}}");
            Check.True(d.InputSchema.HasValue);
        });
        test("definition: duplicate root names are rejected", () =>
            Check.Throws<ProjectionConfigurationException>(() => ProjectionDefinition.Parse("{\"expression\":\"1\",\"Expression\":\"2\"}")));
    }
}
