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

        public static List<TestCaseData> GetTestCases()
        {
            return SharedTestData.GetTestCasesNunit();
        }
    }
}