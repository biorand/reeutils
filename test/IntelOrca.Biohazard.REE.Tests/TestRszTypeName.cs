using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestRszTypeName
    {
        [Fact]
        public void NonGeneric_Name()
        {
            var n = new RszTypeName("app.ItemAppointSetting");
            Assert.Equal("app.ItemAppointSetting", n.FullName);
            Assert.Equal("app", n.Namespace);
            Assert.Equal("ItemAppointSetting", n.NameWithoutNamespace);
            Assert.False(n.IsGeneric);
            Assert.False(n.IsGenericDefinition);
        }

        [Fact]
        public void NonGeneric_NoNamespace()
        {
            var n = new RszTypeName("SomeType");
            Assert.Equal("SomeType", n.FullName);
            Assert.Equal("", n.Namespace);
            Assert.Equal("SomeType", n.NameWithoutNamespace);
            Assert.False(n.IsGeneric);
        }

        [Fact]
        public void NonGeneric_SystemType()
        {
            var n = new RszTypeName("System.Guid");
            Assert.Equal("System", n.Namespace);
            Assert.Equal("Guid", n.NameWithoutNamespace);
        }

        [Fact]
        public void NonGeneric_ViaType()
        {
            var n = new RszTypeName("via.vec3");
            Assert.Equal("via", n.Namespace);
            Assert.Equal("vec3", n.NameWithoutNamespace);
        }

        [Fact]
        public void ClosedGeneric_Name()
        {
            var n = new RszTypeName("app.ContextIDRef`1<app.GimmickCore>");
            Assert.Equal("app.ContextIDRef`1<app.GimmickCore>", n.FullName);
            Assert.Equal("app", n.Namespace);
            Assert.Equal("ContextIDRef`1<app.GimmickCore>", n.NameWithoutNamespace);
            Assert.True(n.IsGeneric);
            Assert.False(n.IsGenericDefinition);
            Assert.Equal("app.ContextIDRef`1", n.GenericTypeDefinitionName);
            Assert.Single(n.TypeArguments);
            Assert.Equal("app.GimmickCore", n.TypeArguments[0].FullName);
            Assert.Equal("app", n.TypeArguments[0].Namespace);
        }

        [Fact]
        public void ClosedGeneric_MultiArg()
        {
            var n = new RszTypeName("app.CompositeKey`2<app.SubtitlesMessageType,app.SubtitleSlotSelectType>");
            Assert.Equal("app", n.Namespace);
            Assert.True(n.IsGeneric);
            Assert.Equal("app.CompositeKey`2", n.GenericTypeDefinitionName);
            Assert.Equal(2, n.TypeArguments.Length);
            Assert.Equal("app.SubtitlesMessageType", n.TypeArguments[0].FullName);
            Assert.Equal("app.SubtitleSlotSelectType", n.TypeArguments[1].FullName);
        }

        [Fact]
        public void ClosedGeneric_NestedGenericArg()
        {
            var n = new RszTypeName("container.Container`1<item.Item`1<inner.InnerType>>");
            Assert.Equal("container", n.Namespace);
            Assert.True(n.IsGeneric);
            Assert.Equal("container.Container`1", n.GenericTypeDefinitionName);
            Assert.Single(n.TypeArguments);
            Assert.Equal("item.Item`1<inner.InnerType>", n.TypeArguments[0].FullName);
            Assert.True(n.TypeArguments[0].IsGeneric);
        }

        [Fact]
        public void OpenGenericDefinition_Name()
        {
            var n = new RszTypeName("app.ContextIDRef`1[[TContextIDHolder, application, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]");
            Assert.Equal("app", n.Namespace);
            Assert.Equal("ContextIDRef`1[[TContextIDHolder, application, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]", n.NameWithoutNamespace);
            Assert.False(n.IsGeneric);
            Assert.True(n.IsGenericDefinition);
            Assert.Equal("app.ContextIDRef`1", n.GenericTypeDefinitionName);
            Assert.Single(n.GenericParameterNames);
            Assert.Equal("TContextIDHolder", n.GenericParameterNames[0]);
        }

        [Fact]
        public void OpenGenericDefinition_MultiParam()
        {
            var n = new RszTypeName("app.CompositeKey`2[[TKey1, application, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null],[TKey2, application, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]");
            Assert.Equal("app", n.Namespace);
            Assert.True(n.IsGenericDefinition);
            Assert.Equal(2, n.GenericParameterNames.Length);
            Assert.Equal("TKey1", n.GenericParameterNames[0]);
            Assert.Equal("TKey2", n.GenericParameterNames[1]);
        }

        [Fact]
        public void FromClrType_NonGeneric()
        {
            var name = RszTypeName.FromClrType(typeof(System.Guid));
            Assert.Equal("System.Guid", name.FullName);
        }

        [Fact]
        public void FromClrType_Generic()
        {
            var name = RszTypeName.FromClrType(typeof(System.Collections.Generic.List<int>));
            Assert.StartsWith("System.Collections.Generic.List`1<", name.FullName);
            Assert.True(name.IsGeneric);
            Assert.Equal("System.Collections.Generic.List`1", name.GenericTypeDefinitionName);
            Assert.Single(name.TypeArguments);
            Assert.Equal("System.Int32", name.TypeArguments[0].FullName);
        }

        [Fact]
        public void FromClrType_MultiArg()
        {
            var name = RszTypeName.FromClrType(typeof(System.Collections.Generic.Dictionary<string, int>));
            Assert.StartsWith("System.Collections.Generic.Dictionary`2<", name.FullName);
            Assert.True(name.IsGeneric);
            Assert.Equal(2, name.TypeArguments.Length);
            Assert.Equal("System.String", name.TypeArguments[0].FullName);
            Assert.Equal("System.Int32", name.TypeArguments[1].FullName);
        }

        [Fact]
        public void TryFindClrType_CustomGeneric()
        {
            var assembly = typeof(TestRszTypeName).Assembly;
            var name = new RszTypeName("IntelOrca.Biohazard.REE.Tests.MyGenericType`1<IntelOrca.Biohazard.REE.Tests.MyTypeArg>");
            var found = name.TryFindClrType(assembly);
            Assert.NotNull(found);
            Assert.True(found.IsGenericType);
            Assert.Equal("IntelOrca.Biohazard.REE.Tests.MyGenericType`1", found.GetGenericTypeDefinition().FullName);
            var arg = found.GetGenericArguments()[0];
            Assert.Equal("IntelOrca.Biohazard.REE.Tests.MyTypeArg", arg.FullName);
        }

        [Fact]
        public void Equals_SameName_ReturnsTrue()
        {
            var a = new RszTypeName("app.Foo");
            var b = new RszTypeName("app.Foo");
            Assert.True(a.Equals(b));
            Assert.True(a == b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Equals_DifferentName_ReturnsFalse()
        {
            var a = new RszTypeName("app.Foo");
            var b = new RszTypeName("app.Bar");
            Assert.False(a.Equals(b));
            Assert.True(a != b);
        }

        [Fact]
        public void ToString_ReturnsFullName()
        {
            var n = new RszTypeName("app.MyType");
            Assert.Equal("app.MyType", n.ToString());
        }

        [Fact]
        public void Null_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => new RszTypeName(null!));
        }
    }
}

namespace IntelOrca.Biohazard.REE.Tests
{
    internal class MyGenericType<T>
    {
    }

    internal class MyTypeArg
    {
    }
}
