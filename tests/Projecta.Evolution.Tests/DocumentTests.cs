using Projecta.Documents;
using Projecta.Json;
using Projecta.Mapping;

internal static class DocumentTests
{
    internal static void Register(Action<string, Action> test)
    {
        var cases = new (string Name, string Input, string Patch, string Expected)[]
        {
            ("add member", "{\"a\":1}", "[{\"op\":\"add\",\"path\":\"/b\",\"value\":2}]", "{\"a\":1,\"b\":2}"),
            ("add overwrites", "{\"a\":1}", "[{\"op\":\"add\",\"path\":\"/a\",\"value\":2}]", "{\"a\":2}"),
            ("array insert", "[1,3]", "[{\"op\":\"add\",\"path\":\"/1\",\"value\":2}]", "[1,2,3]"),
            ("array append", "[1]", "[{\"op\":\"add\",\"path\":\"/-\",\"value\":2}]", "[1,2]"),
            ("remove shifts", "[1,2,3]", "[{\"op\":\"remove\",\"path\":\"/1\"}]", "[1,3]"),
            ("replace", "{\"a\":1}", "[{\"op\":\"replace\",\"path\":\"/a\",\"value\":null}]", "{\"a\":null}"),
            ("move within array", "[0,1,2,3]", "[{\"op\":\"move\",\"from\":\"/1\",\"path\":\"/3\"}]", "[0,2,3,1]"),
            ("copy subtree", "{\"a\":{\"x\":1}}", "[{\"op\":\"copy\",\"from\":\"/a\",\"path\":\"/b\"},{\"op\":\"replace\",\"path\":\"/b/x\",\"value\":2}]", "{\"a\":{\"x\":1},\"b\":{\"x\":2}}"),
            ("root replace", "null", "[{\"op\":\"replace\",\"path\":\"\",\"value\":[1]}]", "[1]"),
            ("root add", "1", "[{\"op\":\"add\",\"path\":\"\",\"value\":{}}]", "{}"),
            ("numeric test equality", "{\"a\":1}", "[{\"op\":\"test\",\"path\":\"/a\",\"value\":1.0}]", "{\"a\":1}"),
            ("escaped pointer", "{\"a/b\":{\"~c\":1}}", "[{\"op\":\"replace\",\"path\":\"/a~1b/~0c\",\"value\":2}]", "{\"a/b\":{\"~c\":2}}"),
            ("empty member", "{\"\":1}", "[{\"op\":\"replace\",\"path\":\"/\",\"value\":2}]", "{\"\":2}"),
            ("ignored extension member", "{}", "[{\"op\":\"add\",\"path\":\"/a\",\"value\":1,\"comment\":\"x\"}]", "{\"a\":1}"),
            ("move same path", "{\"a\":1}", "[{\"op\":\"move\",\"from\":\"/a\",\"path\":\"/a\"}]", "{\"a\":1}")
        };
        foreach (var c in cases) test("patch: " + c.Name, () =>
        {
            var source = JToken.Parse(c.Input); var patch = JsonPatchDocument.Parse(c.Patch);
            Check.Json(c.Expected, patch.Apply(source)); Check.Json(c.Input, source);
        });
        foreach (string patch in new[] {
            "[{\"op\":\"remove\",\"path\":\"/missing\"}]", "[{\"op\":\"replace\",\"path\":\"/missing\",\"value\":1}]",
            "[{\"op\":\"add\",\"path\":\"/missing/child\",\"value\":1}]", "[{\"op\":\"test\",\"path\":\"/a\",\"value\":2}]",
            "[{\"op\":\"move\",\"from\":\"/a\",\"path\":\"/a/b\"}]", "[{\"op\":\"remove\",\"path\":\"\"}]" })
            test("patch: fails atomically " + patch, () =>
            { var source = JToken.Parse("{\"a\":1}"); Check.Throws<DocumentTransformException>(() => JsonPatchDocument.Parse(patch).Apply(source)); Check.Json("{\"a\":1}", source); });
        foreach (string path in new[] { "a", "/a~", "/~2", "#/a" }) test("pointer: invalid " + path,
            () => Check.Throws<ProjectionConfigurationException>(() => JsonPointer.Parse(path)));
        foreach (string path in new[] { "/01", "/-1", "/+1", "/-", "/2" }) test("pointer: invalid array index " + path,
            () => Check.Throws<DocumentTransformException>(() => JsonPointer.Parse(path).Resolve(JToken.Parse("[1,2]"))));
        test("pointer: null is found", () => { Check.True(JsonPointer.Parse("/a").TryResolve(JToken.Parse("{\"a\":null}"), out var value)); Check.Equal(JTokenType.Null, value!.Type); });
        test("pointer: missing is not null", () => Check.True(!JsonPointer.Parse("/a").TryResolve(JToken.Parse("{}"), out _)));
        test("patch: malformed operations", () =>
        {
            foreach (string p in new[] { "{}", "[{}]", "[{\"op\":\"add\",\"path\":\"/a\"}]", "[{\"op\":\"unknown\",\"path\":\"\"}]", "[{\"op\":\"add\",\"op\":\"remove\",\"path\":\"\",\"value\":1}]" })
                Check.Throws<ProjectionConfigurationException>(() => JsonPatchDocument.Parse(p));
        });
        test("patch: reusable across threads", () =>
        { var p = JsonPatchDocument.Parse("[{\"op\":\"add\",\"path\":\"/a\",\"value\":1}]"); Parallel.For(0, 100, _ => Check.Json("{\"a\":1}", p.Apply(JToken.Parse("{}")))); });
        test("patch: cancellation", () =>
        { using var c = new CancellationTokenSource(); c.Cancel(); Check.Throws<OperationCanceledException>(() => JsonPatchDocument.Parse("[]").Apply(JValue.CreateNull(), c.Token)); });
        test("merge: nested deletion and scalar replacement", () =>
        {
            var source = JToken.Parse("{\"a\":{\"x\":1,\"y\":2},\"list\":[1,2]}");
            Check.Json("{\"a\":{\"y\":2,\"z\":3},\"list\":[4]}", JsonMergePatch.Apply(source, JToken.Parse("{\"a\":{\"x\":null,\"z\":3},\"list\":[4]}")));
            Check.Json("{\"a\":{\"x\":1,\"y\":2},\"list\":[1,2]}", source);
            Check.Json("null", JsonMergePatch.Apply(source, JValue.CreateNull()));
        });
        test("pipeline: copy rename default omit ensure-array", () =>
        {
            var pipeline = DocumentPipeline.Parse("{\"version\":1,\"steps\":[{\"op\":\"copy\",\"from\":\"/a\",\"path\":\"/b\"},{\"op\":\"rename\",\"from\":\"/b\",\"path\":\"/c\"},{\"op\":\"default\",\"path\":\"/d\",\"value\":3},{\"op\":\"omit\",\"path\":\"/a\"},{\"op\":\"ensureArray\",\"path\":\"/c\"}]}");
            var result = pipeline.Execute(JToken.Parse("{\"a\":1,\"d\":null}"));
            Check.Json("{\"c\":[1],\"d\":null}", result.Value); Check.Equal(5, result.Trace.Count);
        });
        test("pipeline: JSONata stage", () =>
        {
            var pipeline = DocumentPipeline.Parse("{\"version\":1,\"steps\":[{\"op\":\"project\",\"expression\":\"{'n':a+1}\"}]}");
            Check.Json("{\"n\":3}", pipeline.Execute(JToken.Parse("{\"a\":2}")).Value);
        });
    }
}
