using System.Collections.Generic;
using System.Linq;
using Projecta.Json;
using NUnit.Framework;
using SharedTestData = global::Projecta.TestData.TestData;

namespace Projecta.NewtonsoftJson.Tests
{
    public sealed class ObjectParsingTests
    {
        [Test, TestCaseSource(nameof(GetTestCases))]
        public void RegularCases(SharedTestData testData)
        {
            JToken token = ProjectaExtensions.FromObjectViaNewtonsoft(testData.SourceObject);
            string result = token.ToFlatString();
            Assert.That(result, Is.EqualTo(testData.ExpectedJsonNewtonsoft));
        }

        [Test]
        public void ExplicitDateTimeFormatIsSupported()
        {
            var token = Newtonsoft.Json.Linq.JToken.FromObject(new { dt = new System.DateTime(2024, 1, 2, 3, 4, 5) });
            JToken native = global::Projecta.NewtonsoftJson.ProjectaExtensions.FromNewtonsoft(token,
                System.Globalization.CultureInfo.InvariantCulture, datetimeFormat: "dd.MM.yyyy H:mm:ss");
            Assert.That(native.ToFlatString(), Is.EqualTo("{\"dt\":\"02.01.2024 3:04:05\"}"));
        }

        public static List<TestCaseData> GetTestCases()
        {
            return SharedTestData.GetTestCasesNunit();
        }
    }
}