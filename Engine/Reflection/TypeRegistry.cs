using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OssianForge.Engine.Core
{

    public static class TypeRegistry<TBase> where TBase : class
    {
        private static readonly Dictionary<string, Dictionary<string, Type>> _namespaceCache = new();

        public static IReadOnlyDictionary<string, Type> FromNamespace(string namespaceName)
        {
            if (_namespaceCache.TryGetValue(namespaceName, out var cached))
                return cached;

            var found = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t =>
                    t.IsClass &&
                    !t.IsAbstract &&
                    t.Namespace == namespaceName &&
                    typeof(TBase).IsAssignableFrom(t))
                .ToDictionary(t => t.Name, t => t);

            _namespaceCache[namespaceName] = found;
            return found;
        }

        public static IEnumerable<string> NamesIn(string namespaceName) =>
            FromNamespace(namespaceName).Keys;

        public static IEnumerable<Type> TypesIn(string namespaceName) =>
            FromNamespace(namespaceName).Values;

        public static Type Get(string namespaceName, string typeName) =>
            FromNamespace(namespaceName).TryGetValue(typeName, out var t) ? t : null;

        public static TBase Create(string namespaceName, string typeName) =>
            Get(namespaceName, typeName) is { } type
                ? (TBase)Activator.CreateInstance(type)
                : null;
    }

}
