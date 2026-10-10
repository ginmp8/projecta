using System.Collections.Generic;
using System.Linq;
using Projecta.Json;
using NUnit.Framework;
using SharedTestData = global::Projecta.TestData.TestData;

namespace Projecta.SystemTextJson.Tests
{
    public sealed class ObjectParsingTests
    {
        [Test, TestCaseSource(nameof(GetTestCases))]
        public void RegularCases(SharedTestData testData)
        {
            JToken token = ProjectaExtensions.FromObjectViaSystemTextJson(testData.SourceObject);
            string result = token.ToFlatString();
            Assert.That(result, Is.EqualTo(testData.ExpectedJsonSystemText));
        }

        public static List<TestCaseData> GetTestCases()
        {
            return SharedTestData.GetTestCasesNunit();
        }
    }
}