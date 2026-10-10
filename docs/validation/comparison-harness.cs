using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using Original = Jsonata.Net.Native.Json.JToken;
using NewToken = Projecta.Json.JToken;
var source = new Payload { Id = 42, Name = "sample", Values = Enumerable.Range(0, 20).Select(i => new Item { Id = i, Name = "entry", Amount = 12.34m }).ToArray() };
string expected = Original.FromObject(source).ToFlatString();
if (expected != NewToken.FromObject(source).ToFlatString()) throw new Exception("Different normalization output.");
var records = new List<object>();
void Measure(string scenario, int count, Func<object> action)
{
    for(int i=0;i<200;i++) action();
    GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
    long allocated=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();
    object? last=null;for(int i=0;i<count;i++) last=action();sw.Stop();
    records.Add(new { scenario, iterations=count, microseconds=sw.Elapsed.TotalMicroseconds/count, allocatedBytes=(GC.GetAllocatedBytesForCurrentThread()-allocated)/(double)count });GC.KeepAlive(last);
}
for(int run=0;run<3;run++)
{
    Measure("upstream-object-reader",10000,()=>Original.FromObject(source));
    Measure("projecta-object-reader",10000,()=>NewToken.FromObject(source));
    Measure("upstream-pad-20000",200,()=>Jsonata.Net.Native.Eval.BuiltinFunctions.pad("x",20000,"ab"));
    Measure("projecta-pad-20000",200,()=>Projecta.Eval.BuiltinFunctions.pad("x",20000,"ab"));
}
Console.WriteLine(JsonSerializer.Serialize(new { runtime=Environment.Version.ToString(), framework=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, note="Same .NET 10 runtime; original engine source unchanged except target framework. Synthetic microbenchmark, not end-to-end workload.", records },new JsonSerializerOptions {WriteIndented=true}));
public class Payload { public int Id {get;set;} public string Name {get;set;}=""; public Item[] Values {get;set;}=Array.Empty<Item>(); }
public class Item { public int Id {get;set;} public string Name {get;set;}=""; public decimal Amount {get;set;} }
