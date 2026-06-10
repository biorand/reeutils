using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public readonly struct RszTypeName : IEquatable<RszTypeName>
    {
        private readonly string _fullName;
        private readonly string _ns;
        private readonly string _nameOnly;
        private readonly string _genericDefName;
        private readonly ImmutableArray<RszTypeName> _typeArgs;
        private readonly ImmutableArray<string> _genericParamNames;

        public RszTypeName(string fullName)
        {
            if (fullName is null)
                throw new ArgumentNullException(nameof(fullName));
            _fullName = fullName;

            var genericStart = fullName.IndexOf('<');
            if (genericStart == -1 && fullName.Length > 1)
                genericStart = fullName.IndexOf('[', 1);

            int lastDot;
            if (genericStart != -1)
            {
                var basePart = fullName[..genericStart];
                lastDot = basePart.LastIndexOf('.');
            }
            else
            {
                lastDot = fullName.LastIndexOf('.');
            }

            if (lastDot == -1)
            {
                _ns = "";
                _nameOnly = fullName;
            }
            else
            {
                _ns = fullName[..lastDot];
                _nameOnly = fullName[(lastDot + 1)..];
            }

            if (_fullName.Contains('<') && !_fullName.Contains("[["))
            {
                _genericDefName = ParseGenericDefinitionName(_fullName);
                _typeArgs = ParseTypeArguments(_fullName);
                _genericParamNames = [];
            }
            else if (_fullName.Contains("[["))
            {
                _genericDefName = ParseGenericDefinitionName(_fullName);
                _typeArgs = [];
                _genericParamNames = ParseGenericParameterNames(_fullName);
            }
            else
            {
                _genericDefName = "";
                _typeArgs = [];
                _genericParamNames = [];
            }
        }

        public string FullName => _fullName;
        public string Namespace => _ns;
        public string NameWithoutNamespace => _nameOnly;

        public bool IsGeneric => _fullName.Contains('<');
        public bool IsGenericDefinition => _fullName.Contains("[[");

        public string GenericTypeDefinitionName => _genericDefName;
        public ImmutableArray<RszTypeName> TypeArguments => _typeArgs;
        public ImmutableArray<string> GenericParameterNames => _genericParamNames;

        public static RszTypeName FromClrType(Type type)
        {
            if (!type.IsGenericType)
            {
                var name = type.FullName?.Replace('+', '.') ?? type.Name;
                return new RszTypeName(name);
            }

            var genericDef = type.GetGenericTypeDefinition();
            var genericDefName = genericDef.FullName?.Replace('+', '.') ?? "";

            var typeArgs = type.GetGenericArguments();
            var argNames = new string[typeArgs.Length];
            for (var i = 0; i < typeArgs.Length; i++)
            {
                argNames[i] = typeArgs[i].FullName?.Replace('+', '.') ?? typeArgs[i].Name;
            }

            var rszName = $"{genericDefName}<{string.Join(",", argNames)}>";
            return new RszTypeName(rszName);
        }

        public Type? TryFindClrType(Assembly assembly)
        {
            var fullName = _fullName;
            if (!IsGeneric)
            {
                foreach (var t in assembly.DefinedTypes)
                {
                    if (t.FullName?.Replace('+', '.') == fullName)
                        return t;
                }
                return null;
            }

            var genericDefName = _genericDefName;
            if (string.IsNullOrEmpty(genericDefName))
                return null;

            var defType = FindGenericTypeDefinition(assembly, genericDefName);
            if (defType == null)
                return null;

            var typeArgs = _typeArgs;
            var typeArgTypes = new Type[typeArgs.Length];
            for (var i = 0; i < typeArgs.Length; i++)
            {
                var argType = typeArgs[i].TryFindClrType(assembly);
                if (argType == null)
                    return null;
                typeArgTypes[i] = argType;
            }

            try
            {
                return defType.MakeGenericType(typeArgTypes);
            }
            catch
            {
                return null;
            }
        }

        private static Type? FindGenericTypeDefinition(Assembly assembly, string genericDefName)
        {
            foreach (var t in assembly.DefinedTypes)
            {
                if (t.IsGenericTypeDefinition && t.FullName?.Replace('+', '.') == genericDefName)
                    return t;
            }
            return null;
        }

        public bool Equals(RszTypeName other) => string.Equals(_fullName, other._fullName, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is RszTypeName other && Equals(other);
        public override int GetHashCode() => _fullName?.GetHashCode() ?? 0;
        public override string ToString() => _fullName ?? "";
        public static bool operator ==(RszTypeName left, RszTypeName right) => left.Equals(right);
        public static bool operator !=(RszTypeName left, RszTypeName right) => !left.Equals(right);

        private static string ParseGenericDefinitionName(string name)
        {
            var idx = name.IndexOf('<');
            if (idx == -1)
                idx = name.IndexOf('[', 1);
            return idx == -1 ? name : name[..idx];
        }

        private static ImmutableArray<RszTypeName> ParseTypeArguments(string rszName)
        {
            var start = rszName.IndexOf('<');
            var end = rszName.LastIndexOf('>');
            if (start == -1 || end == -1 || end <= start)
                return [];

            var content = rszName[(start + 1)..end];
            var args = new List<string>();
            var depth = 0;
            var argStart = 0;
            for (var i = 0; i < content.Length; i++)
            {
                var c = content[i];
                if (c == '<')
                    depth++;
                else if (c == '>')
                    depth--;
                else if (c == ',' && depth == 0)
                {
                    args.Add(content[argStart..i].Trim());
                    argStart = i + 1;
                }
            }
            args.Add(content[argStart..].Trim());

            var builder = ImmutableArray.CreateBuilder<RszTypeName>(args.Count);
            foreach (var a in args)
            {
                builder.Add(new RszTypeName(a));
            }
            return builder.MoveToImmutable();
        }

        private static ImmutableArray<string> ParseGenericParameterNames(string name)
        {
            var argsStart = name.IndexOf("[[");
            var argsEnd = name.LastIndexOf("]]");
            if (argsStart == -1 || argsEnd == -1)
                return [];

            var argsContent = name[(argsStart + 2)..argsEnd];
            var paramNames = new List<string>();

            if (argsContent.Length > 0 && argsContent[0] == '[')
            {
                var depth = 0;
                var start = 0;
                for (var i = 0; i < argsContent.Length; i++)
                {
                    if (argsContent[i] == '[')
                    {
                        if (depth == 0)
                            start = i + 1;
                        depth++;
                    }
                    else if (argsContent[i] == ']')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            var param = argsContent[start..i];
                            paramNames.Add(ExtractSimpleTypeName(param));
                        }
                    }
                }
            }
            else if (argsContent.Length > 0)
            {
                var parts = argsContent.Split(new[] { "]," }, StringSplitOptions.None);
                for (var i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    if (i > 0 && part.Length > 0 && part[0] == '[')
                        part = part[1..];
                    paramNames.Add(ExtractSimpleTypeName(part));
                }
            }

            return [.. paramNames];
        }

        private static string ExtractSimpleTypeName(string arg)
        {
            var trimmed = arg.Trim();
            var commaIdx = trimmed.IndexOf(',');
            return commaIdx == -1 ? trimmed : trimmed[..commaIdx].Trim();
        }
    }
}
