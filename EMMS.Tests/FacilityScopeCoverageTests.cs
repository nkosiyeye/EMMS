using System.Reflection;
using EMMS.CustomAttributes;
using Microsoft.AspNetCore.Mvc;

namespace EMMS.Tests
{
    /// <summary>
    /// Every controller action must say how it keeps users to their own facility (see FacilityScopeAttributes.cs),
    /// so a new action cannot leak another facility's data by forgetting the check.
    /// </summary>
    public class FacilityScopeCoverageTests
    {
        private static readonly Type[] Markers = [typeof(FacilityScopedAttribute), typeof(FacilityCheckedAttribute), typeof(NoFacilityDataAttribute)];

        public static IEnumerable<MethodInfo> Actions() =>
            typeof(BaseController).Assembly.GetTypes()
                .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract && t.Namespace?.StartsWith("EMMS") == true)
                .Where(t => !t.Namespace!.Contains(".Areas.", StringComparison.Ordinal))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null);

        private static bool Declares(MemberInfo member) => Markers.Any(m => member.IsDefined(m, inherit: false));

        [Fact]
        public void EveryActionDeclaresItsFacilityScope()
        {
            var missing = Actions()
                .Where(m => !Declares(m) && !Declares(m.DeclaringType!))
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            Assert.True(missing.Count == 0,
                "Add [FacilityScoped], [FacilityChecked] or [NoFacilityData] to:\n  " + string.Join("\n  ", missing));
        }

        [Fact]
        public void FacilityScopedNamesARealParameter()
        {
            var broken = Actions()
                .SelectMany(m => m.GetCustomAttributes<FacilityScopedAttribute>().Select(a => (Method: m, Attr: a)))
                .Where(x => !ResolvesTo(x.Method, x.Attr.Parameter))
                .Select(x => $"{x.Method.DeclaringType!.Name}.{x.Method.Name}: \"{x.Attr.Parameter}\"")
                .ToList();

            Assert.True(broken.Count == 0, "These [FacilityScoped] paths name no Guid parameter/property:\n  " + string.Join("\n  ", broken));
        }

        // The path must lead to a Guid (or Guid?) through the action's parameters and their properties.
        private static bool ResolvesTo(MethodInfo method, string path)
        {
            var parts = path.Split('.');
            var type = method.GetParameters().FirstOrDefault(p => p.Name == parts[0])?.ParameterType;
            foreach (var name in parts.Skip(1))
                type = type?.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.PropertyType;
            return type == typeof(Guid) || type == typeof(Guid?);
        }
    }
}
