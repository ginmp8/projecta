using System.Collections.Generic;
using System.Linq;
using Projecta.Json;
using NUnit.Framework;
using Projecta.TestData;

namespace Projecta.SystemTextJson.Tests
{
    public sealed class ObjectParsingTests
    {
        [Test, TestCaseSource(nameof(GetTestCases))]
        public void RegularCases(TestData testData)
        {
            JToken token = ProjectaExtensions.FromObjectViaSystemTextJson(testData.SourceObject);
            string result = token.ToFlatString();
            Assert.That(result, Is.EqualTo(testData.ExpectedJsonSystemText));
        }

        public static List<TestCaseData> GetTestCases()
        {
            return TestData.GetTestCasesNunit();
        }
    }
}