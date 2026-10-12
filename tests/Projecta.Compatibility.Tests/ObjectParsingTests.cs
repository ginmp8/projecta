using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Projecta.Json;
using NUnit.Framework;
using SharedTestData = global::Projecta.TestData.TestData;

namespace Projecta.Compatibility.Tests
{
    public sealed class ObjectParsingTests
    {
        [TestCaseSource(nameof(GetTestCases))]
        public void RegularCases(SharedTestData testData)
        {
            JToken token = JToken.FromObject(testData.SourceObject);
            string result = token.ToFlatString();
            Assert.That(result, Is.EqualTo(testData.ExpectedJson));
        }

        public static List<TestCaseData> GetTestCases()
        {
            return SharedTestData.GetTestCasesNunit();
        }
    }
}
