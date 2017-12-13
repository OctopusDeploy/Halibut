using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Assent;
using Assent.Namers;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class PublicSurfaceAreaFixture
    {
        static readonly string[] commonNamespaces = {
            "System",
            "System.Collections.Generic",
            "System.IO",
            "System.Net",
            "System.Net.Sockets",
            "System.Net.WebSockets",
            "System.Reflection",
            "System.Runtime.Serialization",
            "System.Runtime.InteropServices",
            "System.Security.Cryptography.X509Certificates",
            "System.Text",
            "System.Threading.Tasks"
        };

        [Test]
        public void ThePublicSurfaceAreaShouldNotRegress()
        {
            var usings = commonNamespaces.Select(ns => $"using {ns};").Concat("".InArray());

            var lines = typeof(HalibutRuntime).GetTypeInfo().Assembly
                .DefinedTypes
                .Where(t => t.IsVisible && !t.IsNested)
                .GroupBy(t => t.Namespace)
                .OrderBy(g => g.Key)
                .SelectMany(g => FormatNamespace(g.Key, g))
                .ToArray();

            var framework = string.Concat(RuntimeInformation.FrameworkDescription.Split(' ').Take(2));
            this.Assent(
                string.Join("\r\n", usings.Concat(lines)),
                new Configuration().UsingNamer(new PostfixNamer(framework.Trim('.'))).UsingExtension("cs")
            );
        }

        IEnumerable<object> FormatNamespace(string name, IEnumerable<TypeInfo> types)
        {
            return $"namespace {name}".InArray()
                .Concat("{".InArray())
                .Concat(types.OrderBy(t => t.Name).SelectMany(FormatType).Select(l => "    " + l))
                .Concat("}".InArray());
        }

        IEnumerable<string> FormatType(TypeInfo type)
        {
            if (type.IsSpecialName || type.GetCustomAttribute(typeof(CompilerGeneratedAttribute)) != null)
                return new string[0];

            if (type.IsEnum)
            {
                var values = new List<string>();
                var count = Enum.GetValues(type.AsType()).Length - 1;
                foreach (var v in Enum.GetValues(type.AsType()).Cast<Enum>())
                {
                    values.Add($"    {v} = {Convert.ChangeType(v, Convert.GetTypeCode(v), null)}{(count > 0 ? "," : "")}");
                    count--;
                }

                return $"{VisibilityString(type.GetVisibility())}enum {type.Name}".InArray()
                        .Concat("{")
                        .Concat(values)
                        .Concat("}");
            }

            var kind = type.IsInterface
                ? "interface"
                : (type.IsAbstract && type.IsSealed)
                    ? "static class"
                    : type.IsAbstract
                        ? "abstract class"
                        : type.IsSealed
                            ? "sealed class"
                            : "class";

            var interfaces = ((type.BaseType == null || type.BaseType == typeof(Object)) ? new Type[0] : type.BaseType.InArray()).Concat(type.GetInterfaces().Where(i => i.GetTypeInfo().IsVisible));
            var interfaceSeparator = interfaces.Any() ? " : " : "";
            var interfacesList = interfaces.Select(i => FormatTypeName(i)).CommaSeperate();

            var members = type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic)
                .OrderBy(t => t.Name)
                .ToArray();

            var fields = members.OfType<FieldInfo>().ToArray();
            var ctors = members.OfType<ConstructorInfo>().ToArray();
            var properties = members.OfType<PropertyInfo>().ToArray();
            var events = members.OfType<EventInfo>().ToArray();
            var methods = members.OfType<MethodInfo>().ToArray();
            var types = members.OfType<TypeInfo>().ToArray();
            var other = members.Except(methods).Except(properties).Except(fields).Except(ctors).Except(events).Except(types).ToArray();

            var body = fields.SelectMany(FormatField)
                .Concat(events.Select(e => $"event {FormatTypeName(e.EventHandlerType)} {e.Name}"))
                .Concat(ctors.SelectMany(FormatCtor))
                .Concat(properties.SelectMany(FormatProperty))
                .Concat(methods.SelectMany(FormatMethods))
                .Concat(other.Select(o => $"UNKNOWN {o.GetType().Name} {o.Name}"))
                .Concat(types.Where(t => t.IsVisible).SelectMany(FormatType));

            return
                $"{VisibilityString(type.GetVisibility())}{kind} {FormatTypeName(type, true)}{interfaceSeparator}{interfacesList}".InArray()
                    .Concat("{")
                    .Concat(body.Select(l => "    " + l))
                    .Concat("}");
        }

        string Static(bool isStatic) => isStatic ? "static " : "";

        string VisibilityString(Visibility? visibility)
        {
            if (visibility == null)
                return "";

            switch (visibility)
            {
                case Visibility.Public: return "public ";
                case Visibility.Protected: return "protected ";
                case Visibility.Internal: return "internal ";
                case Visibility.ProtectedInternal: return "protected internal ";
                case Visibility.Private: return "private ";
                default: throw new NotSupportedException();
            }
        }

        IEnumerable<string> FormatField(FieldInfo f)
        {
            if (f.IsSpecialName || !(f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly))
                return new string[0];

            return $"{VisibilityString(f.GetVisibility())}{Static(f.IsStatic)}{FormatTypeName(f.FieldType)} {f.Name};".InArray();
        }

        IEnumerable<string> FormatProperty(PropertyInfo p)
        {
            var accessors = new List<string>();
            if (p.GetMethod?.IsVisible() == true)
                accessors.Add("get;");
            if (p.SetMethod?.IsVisible() == true)
            {
                if (p.GetMethod?.GetVisibility() == p.SetMethod?.GetVisibility())
                    accessors.Add("set;");
                else
                    accessors.Add($"{VisibilityString(p.SetMethod.GetVisibility())}set;");
            }

            var isStatic = p.GetMethod?.IsStatic ?? p.SetMethod?.IsStatic ?? false;

            return $"{VisibilityString(p.GetMethod?.GetVisibility())}{Static(isStatic)}{FormatTypeName(p.PropertyType)} {p.Name} {{ {string.Join(" ", accessors)} }}".InArray();
        }

        IEnumerable<string> FormatCtor(ConstructorInfo c)
        {
            if (c.IsStatic || !(c.IsPublic || c.IsFamily || c.IsFamilyOrAssembly))
                return new string[0];

            var parameters = c.GetParameters().Select(FormatParameters);
            return $"{VisibilityString(c.GetVisibility())}{TypeNameWithoutGeneric(c.DeclaringType.Name)}({parameters.CommaSeperate()}) {{ }}".InArray();
        }

        IEnumerable<string> FormatMethods(MethodInfo m)
        {
            if (m.IsSpecialName || !(m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly))
                return new string[0];

            var genericProperties = m.IsGenericMethod ? $"<{m.GetGenericArguments().Select(t => FormatTypeName(t)).CommaSeperate()}>" : "";

            var properties = m.GetParameters().Select(FormatParameters);
            return $"{VisibilityString(m.GetVisibility())}{Static(m.IsStatic)}{FormatTypeName(m.ReturnType)} {m.Name}{genericProperties}({properties.CommaSeperate()}) {{ }}".InArray();
        }

        string FormatParameters(ParameterInfo p)
        {
            var name = p.Name;
            if (string.Equals(name, "object", StringComparison.CurrentCultureIgnoreCase))
                name = "@" + name;

            return $"{FormatTypeName(p.ParameterType)} {name}";
        }

        string FormatTypeName(TypeInfo type, bool shortName = false)
        {
            var name = (shortName || type.IsGenericParameter || commonNamespaces.Contains(type.Namespace)) ?
                type.Name :
                (type.FullName ?? $"{type.Namespace}.{type.Name}");

            if (!type.IsGenericType)
                return name;

            name = TypeNameWithoutGeneric(name);
            var args = type.GetGenericArguments().Select(a => FormatTypeName(a));
            return $"{name}<{args.CommaSeperate()}>";
        }

        string FormatTypeName(Type type, bool shortName = false)
        {
            if (type == typeof(void))
                return "void";
            if (type == typeof(int))
                return "int";
            if (type == typeof(long))
                return "long";
            if (type == typeof(bool))
                return "bool";
            if (type == typeof(string))
                return "string";

            return FormatTypeName(type.GetTypeInfo(), shortName);
        }

        string TypeNameWithoutGeneric(string name)
        {
            var index = name.IndexOf('`');
            if (index < 0)
                return name;

            return name.Substring(0, index);
        }
    }
}