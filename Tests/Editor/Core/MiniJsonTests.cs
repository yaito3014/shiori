using System.Collections.Generic;
using NUnit.Framework;

namespace Shiori.Tests
{
    public class MiniJsonTests
    {
        [Test]
        public void Parse_Object_WithAllValueTypes()
        {
            var parsed = MiniJson.Parse("{\"s\":\"日本語 \\\"q\\\" \\n \\u3042\",\"i\":42,\"neg\":-7,\"d\":1.5,\"e\":1e3,\"t\":true,\"f\":false,\"n\":null,\"a\":[1,\"x\",{}],\"o\":{\"k\":[]}}");
            var obj = (Dictionary<string, object>)parsed;

            Assert.That(obj["s"], Is.EqualTo("日本語 \"q\" \n あ"));
            Assert.That(obj["i"], Is.EqualTo(42L));
            Assert.That(obj["neg"], Is.EqualTo(-7L));
            Assert.That(obj["d"], Is.EqualTo(1.5));
            Assert.That(obj["e"], Is.EqualTo(1000.0));
            Assert.That(obj["t"], Is.True);
            Assert.That(obj["f"], Is.False);
            Assert.That(obj["n"], Is.Null);
            var list = (List<object>)obj["a"];
            Assert.That(list.Count, Is.EqualTo(3));
            Assert.That(list[2], Is.InstanceOf<Dictionary<string, object>>());
            Assert.That(((Dictionary<string, object>)obj["o"])["k"], Is.Empty);
        }

        [Test]
        public void Parse_ToleratesWhitespace()
        {
            var obj = (Dictionary<string, object>)MiniJson.Parse(" \n{ \"a\" : 1 ,\r\n \"b\" : [ ] }\n");
            Assert.That(obj.Count, Is.EqualTo(2));
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("{\"a\":}")]
        [TestCase("{\"a\":1,}")]
        [TestCase("[1 2]")]
        [TestCase("\"unterminated")]
        [TestCase("{\"a\":1} x")]
        [TestCase("tru")]
        [TestCase("{a:1}")]
        public void Parse_RejectsMalformedInput(string json)
        {
            Assert.Throws<System.FormatException>(() => MiniJson.Parse(json));
        }

        [Test]
        public void Serialize_ProducesStableIndentedOutput()
        {
            var obj = new Dictionary<string, object>
            {
                ["schemaVersion"] = 1,
                ["name"] = "a\"b\\c\n",
                ["flag"] = true,
                ["none"] = null,
                ["list"] = new List<object> { 1L, "x" },
                ["empty"] = new Dictionary<string, object>(),
            };

            var text = MiniJson.Serialize(obj);

            Assert.That(text, Is.EqualTo(
                "{\n" +
                "  \"schemaVersion\": 1,\n" +
                "  \"name\": \"a\\\"b\\\\c\\n\",\n" +
                "  \"flag\": true,\n" +
                "  \"none\": null,\n" +
                "  \"list\": [\n    1,\n    \"x\"\n  ],\n" +
                "  \"empty\": {}\n" +
                "}\n"));
        }

        [Test]
        public void Serialize_Compact()
        {
            var obj = new Dictionary<string, object> { ["a"] = 1L, ["b"] = new List<object> { true } };
            Assert.That(MiniJson.Serialize(obj, indented: false), Is.EqualTo("{\"a\":1,\"b\":[true]}"));
        }

        [Test]
        public void RoundTrip()
        {
            var text = "{\"a\":\"\\u0001 tab\\t\",\"b\":[1,2.5,null,{\"c\":false}]}";
            var again = MiniJson.Serialize(MiniJson.Parse(text), indented: false);
            Assert.That(again, Is.EqualTo("{\"a\":\"\\u0001 tab\\t\",\"b\":[1,2.5,null,{\"c\":false}]}"));
        }
    }
}
