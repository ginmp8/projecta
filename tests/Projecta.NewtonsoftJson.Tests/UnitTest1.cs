using System;
using System.Globalization;
using NUnit.Framework;
using System.IO;
using Projecta.Json;

namespace Projecta.Tests
{
    public class UnitTest1
    {
        public void Test_Issue14_1()
        {
            Newtonsoft.Json.Linq.JToken newtonsoftToken = Newtonsoft.Json.Linq.JToken.FromObject(new { key = DateTime.Now });
            Projecta.Json.JToken jToken = Projecta.NewtonsoftJson.ProjectaExtensions.FromNewtonsoft(newtonsoftToken);
            Assert.Pass();
        }


        [Test]
        public void Test_Issue14_2()
        {
            Newtonsoft.Json.Linq.JToken newtonsoftToken = Newtonsoft.Json.Linq.JToken.FromObject(new { key = Guid.NewGuid() });
            Projecta.Json.JToken jToken = Projecta.NewtonsoftJson.ProjectaExtensions.FromNewtonsoft(newtonsoftToken);
            Assert.Pass();
        }

        [Test]
        public void Test_Issue14_3()
        {
            Newtonsoft.Json.Linq.JToken newtonsoftToken = Newtonsoft.Json.Linq.JToken.FromObject(new { key = TimeSpan.FromSeconds(5) });
            Projecta.Json.JToken jToken = Projecta.NewtonsoftJson.ProjectaExtensions.FromNewtonsoft(newtonsoftToken);
            Assert.Pass();
        }

        [Test]
        public void Test_Issue14_4()
        {
            Newtonsoft.Json.Linq.JToken newtonsoftToken = Newtonsoft.Json.Linq.JToken.FromObject(new { key = new Uri("http://abc.xyz") });
            Projecta.Json.JToken jToken = Projecta.NewtonsoftJson.ProjectaExtensions.FromNewtonsoft(newtonsoftToken);
            Assert.Pass();
        }

        [Test]
        public void Test_Issue14_5()
        {
            Newtonsoft.Json.Linq.JToken newtonsoftToken = Newtonsoft.Json.Linq.JToken.FromObject(new { key = DateTimeOffset.Now });
            Projecta.Json.JToken jToken = Projecta.NewtonsoftJson.ProjectaExtensions.FromNewtonsoft(newtonsoftToken);
            Assert.Pass();
        }

        [Test]
        public void Test_Issue14_6()
        {
            Newtonsoft.Json.Linq.JToken newtonsoftToken = Newtonsoft.Json.Linq.JToken.FromObject(new { key = new DateTime(2023, 09, 17, 22, 28, 00) });
            Projecta.Json.JToken jToken = Projecta.NewtonsoftJson.ProjectaExtensions.FromNewtonsoft(newtonsoftToken, CultureInfo.InvariantCulture, datetimeFormat: "yyyy~MM~dd HH:mm:ss");
            string value = (string)((Projecta.Json.JObject)jToken).Properties["key"];
            Assert.That(value, Is.EqualTo("2023~09~17 22:28:00"));
        }

        [Test]
        public void Test_Issue14_7()
        {
            Newtonsoft.Json.Linq.JToken newtonsoftToken = Newtonsoft.Json.Linq.JToken.FromObject(new { key = new TimeSpan(10, 12, 13, 14, 156) });
            Projecta.Json.JToken jToken = Projecta.NewtonsoftJson.ProjectaExtensions.FromNewtonsoft(newtonsoftToken, CultureInfo.InvariantCulture, timespanFormat: @"ddd\-hh\-mm\-ss\-fff");
            string value = (string)((Projecta.Json.JObject)jToken).Properties["key"];
            Assert.That(value, Is.EqualTo("010-12-13-14-156"));
        }

        [Test]
        public void Test_Issue14_8()
        {
            Newtonsoft.Json.Linq.JToken newtonsoftToken = Newtonsoft.Json.Linq.JToken.FromObject(new { key = Guid.Empty });
            Projecta.Json.JToken jToken = Projecta.NewtonsoftJson.ProjectaExtensions.FromNewtonsoft(newtonsoftToken, CultureInfo.InvariantCulture, guidFormat: "N");
            string value = (string)((Projecta.Json.JObject)jToken).Properties["key"];
            Assert.That(value, Is.EqualTo("00000000000000000000000000000000"));
        }

    }
}
