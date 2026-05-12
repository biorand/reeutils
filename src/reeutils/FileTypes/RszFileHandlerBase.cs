using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal abstract class RszFileHandlerBase(string path, byte[] data, int version, RszTypeRepository? repository) : FileHandlerBase(path, data)
    {
        protected int Version { get; } = version;
        protected RszTypeRepository Repository => repository ?? throw new InvalidOperationException("Game not specified. Use -g <game>.");

        public override bool RequiresTypeRepository => true;

        protected static JsonDocument SerializeNode(IRszNode node, TreeOptions options)
        {
            using var raw = JsonDocument.Parse(RszJsonSerializer.Serialize(node, JsonSupport.CreateOptions()));
            return JsonSupport.ApplyTreeOptions(raw, options);
        }

        protected static void SearchNode(IRszNode node, string currentPath, Regex pattern, Action<string> emit)
        {
            if (node is RszStringNode stringNode)
            {
                var value = stringNode.Value ?? "";
                if (pattern.IsMatch(currentPath) || pattern.IsMatch(value))
                    emit($"{currentPath} = {value}");
            }
            else if (node is RszValueNode valueNode)
            {
                var value = valueNode.ToString() ?? "";
                if (pattern.IsMatch(currentPath) || pattern.IsMatch(value))
                    emit($"{currentPath} = {value}");
            }
            else if (node is RszResourceNode resourceNode)
            {
                var value = resourceNode.Value ?? "";
                if (pattern.IsMatch(currentPath) || pattern.IsMatch(value))
                    emit($"{currentPath} = {value}");
            }
            else if (node is RszUserDataNode userDataNode)
            {
                var value = userDataNode.Path ?? "";
                if (pattern.IsMatch(currentPath) || pattern.IsMatch(value))
                    emit($"{currentPath} = {value}");
            }
            else if (node is RszArrayNode arrayNode)
            {
                for (var i = 0; i < arrayNode.Children.Length; i++)
                {
                    SearchNode(arrayNode.Children[i], $"{currentPath}[{i}]", pattern, emit);
                }
            }
            else if (node is RszObjectNode objectNode)
            {
                for (var i = 0; i < objectNode.Children.Length; i++)
                {
                    var fieldName = objectNode.Type.Fields[i].Name;
                    var childPath = string.IsNullOrEmpty(currentPath) ? fieldName : $"{currentPath}.{fieldName}";
                    SearchNode(objectNode.Children[i], childPath, pattern, emit);
                }
            }
            else if (node is RszFolder folder)
            {
                var folderPath = string.IsNullOrEmpty(currentPath) ? folder.Name : $"{currentPath}/{folder.Name}";
                SearchNode(folder.Settings, folderPath, pattern, emit);
                foreach (var child in folder.Children)
                {
                    SearchNode(child, folderPath, pattern, emit);
                }
            }
            else if (node is RszGameObject gameObject)
            {
                var gameObjectPath = string.IsNullOrEmpty(currentPath) ? gameObject.Name : $"{currentPath}/{gameObject.Name}";
                var guidValue = gameObject.Guid.ToString();
                if (pattern.IsMatch(gameObjectPath) || pattern.IsMatch(guidValue))
                    emit($"{gameObjectPath}.guid = {guidValue}");

                SearchNode(gameObject.Settings, gameObjectPath, pattern, emit);

                foreach (var component in gameObject.Components)
                {
                    var componentPath = $"{gameObjectPath}{{{component.Type.Name}}}";
                    SearchNode(component, componentPath, pattern, emit);
                }

                foreach (var child in gameObject.Children)
                {
                    SearchNode(child, gameObjectPath, pattern, emit);
                }
            }
        }
    }
}
