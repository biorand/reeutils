using System.IO;
using System.Linq;
using System.Text.Json;
using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Messages;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal sealed class MessageFileHandler(string path, byte[] data) : FileHandlerBase(path, data)
    {
        public override JsonDocument GetJson(TreeOptions options)
        {
            var msg = new MsgFile(Data).ToBuilder();
            var output = new SerializableMsg
            {
                Version = msg.Version,
                Languages = [.. msg.Languages.Cast<int>()],
                Entries = [.. msg.Messages.Select(x => new SerializableMsg.Entry
                {
                    Guid = x.Guid,
                    Name = x.Name,
                    Values = [.. x.Values.Select(v => v.Text)]
                })]
            };
            using var document = JsonSupport.ToDocument(output, JsonSupport.CreateOptions(camelCase: true));
            return JsonSupport.ApplyTreeOptions(document, options);
        }

        public override byte[] Import(JsonDocument json)
        {
            var data = JsonSerializer.Deserialize<SerializableMsg>(json.RootElement.GetRawText(), JsonSupport.CreateOptions(camelCase: true))
                ?? throw new InvalidDataException("Failed to deserialize MSG JSON.");
            var builder = new MsgFile.Builder
            {
                Version = data.Version,
                Languages = data.Languages.Cast<LanguageId>().ToList(),
                Messages = data.Entries.Select(x => new Msg
                {
                    Guid = x.Guid,
                    Crc = MurMur3.HashData(x.Name),
                    Name = x.Name,
                    Values = x.Values.Select((value, index) => new MsgValue((LanguageId)data.Languages[index], value)).ToList()
                }).ToList()
            };
            return builder.Build().Data.ToArray();
        }
    }
}
