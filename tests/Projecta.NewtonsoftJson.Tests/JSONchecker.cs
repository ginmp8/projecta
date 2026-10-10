using Projecta.Json;
using Projecta.NewtonsoftJson;
using NUnit.Framework;
using Projecta.TestData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Projecta.NewtonsoftJson.Tests
{
    public sealed class JSONchecker
    {
        [Test, TestCaseSource(nameof(GetTestCases))]
        public void Test(JsonCheckerData caseInfo)
        {

            Console.WriteLine($"File: '{caseInfo.fileName}'");

            Console.WriteLine($"JSON: '{caseInfo.json}'");

            try
            {
                JToken reference = JToken.Parse(caseInfo.json);

                Newtonsoft.Json.Linq.JToken ntjSource = Newtonsoft.Json.Linq.JToken.Parse(caseInfo.json);
                JToken resultToken = ProjectaExtensions.FromNewtonsoft(ntjSource);
                Console.WriteLine($"Parsed: '{resultToken.ToFlatString()}'");
               
                Assert.IsTrue(reference.DeepEquals(resultToken));

                Newtonsoft.Json.Linq.JToken convertedReference = reference.ToNewtonsoft();
                Assert.IsTrue(Newtonsoft.Json.Linq.JToken.DeepEquals(ntjSource, convertedReference));
            }
            catch (JsonParseException ex)
            {
                Console.WriteLine($"Exception: '{ex.Message}'");
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static List<TestCaseData> GetTestCases()
        {
            return JsonCheckerData.GetTestCasesNunit();
        }
    }
}