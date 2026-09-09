using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Messages;
using IntelOrca.Biohazard.REEUtils.Commands;
using IntelOrca.Biohazard.REEUtils.FileTypes;

namespace IntelOrca.Biohazard.REEUtils.Tests
{
    /// <summary>
    /// Regression: `reeutils tree --json <a .msg.23>` used to write through Console.WriteLine, whose
    /// writer re-encodes text via the console codepage. Best-fit mapping turned the German closing
    /// quote U+201C (") into a bare ASCII `"` inside JSON string values, producing invalid JSON
    /// (`Expecting ',' delimiter` on the consumer). `tree --json` must emit the same raw UTF-8 bytes
    /// `export` writes.
    /// </summary>
    public sealed class TestMsgTreeJson
    {
        // Retail German text for ItemDataText_IT_EXP_29547: opening quotes are U+201E („),
        // closing quotes are U+201C (") -- the latter is exactly what the console codepage
        // best-fit mapping corrupts into a bare ASCII `"`.
        private const string GermanValue = "\u201EBrennend\u201C und \u201E\u00D6lgetr\u00E4nkt\u201C werden\r\nentfernt und diese beiden Anomalien\r\nwerden vor\u00FCbergehend verhindert.";

        private static byte[] BuildMsg()
        {
            var builder = new MsgFile.Builder
            {
                Version = 23,
                Languages = [LanguageId.English, LanguageId.German]
            };
            builder.Messages.Add(new Msg
            {
                Guid = Guid.NewGuid(),
                Crc = 0,
                Name = "ItemDataText_IT_EXP_29547",
                Values = [new MsgValue(LanguageId.English, "Cures the effects of burning."), new MsgValue(LanguageId.German, GermanValue)]
            });
            return builder.Build().Data.ToArray();
        }

        [Fact]
        public async Task TreeJson_Msg_Writes_Valid_Utf8_Json()
        {
            using var temp = new TempFolder();
            var path = temp.GetSubPath("test.msg.23");
            await File.WriteAllBytesAsync(path, BuildMsg(), TestContext.Current.CancellationToken);

            using var output = new MemoryStream();
            var cmd = new TreeCommand { JsonOutputOverride = output };
            await cmd.ExecuteAsync(null!, new TreeCommand.Settings { PathArgument = path, Json = true });

            output.Position = 0;
            using var parsed = JsonDocument.Parse(output);
            var value = parsed.RootElement.GetProperty("entries")[0].GetProperty("values")[1].GetString();
            Assert.Equal(GermanValue, value);
        }

        [Fact]
        public void TreeJson_Msg_Emits_Same_Bytes_As_Export()
        {
            var handler = new MessageFileHandler("test.msg.23", BuildMsg());
            using var json = handler.GetJson(new TreeOptions());
            using var output = new MemoryStream();
            JsonSupport.WriteJsonToOutput(json, output);

            // `tree --json` must match `export`'s body exactly (export writes UTF-8 to a file directly);
            // the only difference is the trailing newline the console emitter adds.
            var body = output.ToArray().AsSpan()[..^1].ToArray();
            Assert.Equal(handler.Export(), body);
        }

        [Fact]
        public void TreeJson_CurlyQuotes_Survive_As_Utf8()
        {
            var handler = new MessageFileHandler("test.msg.23", BuildMsg());
            using var json = handler.GetJson(new TreeOptions());
            using var output = new MemoryStream();
            JsonSupport.WriteJsonToOutput(json, output);

            // The raw UTF-8 bytes must keep the curly quotes intact (U+201E/U+201C). A bare ASCII `"`
            // here would mean the value was re-encoded through a lossy codepage (the old bug).
            var utf8 = Encoding.UTF8.GetString(output.ToArray());
            Assert.Contains("\u201EBrennend\u201C und \u201E\u00D6lgetr\u00E4nkt\u201C", utf8);
            Assert.DoesNotContain("Brennend\u0022 und", utf8.Replace("\\\"", string.Empty));
        }
    }
}