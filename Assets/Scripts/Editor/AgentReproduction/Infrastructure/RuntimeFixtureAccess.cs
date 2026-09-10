using System;
using System.Reflection;

namespace AgentReproduction.Infrastructure
{
    public static class RuntimeFixtureAccess
    {
        private static FieldInfo Field(object instance, string name)
        {
            for (Type type = instance.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(instance.GetType().FullName, name);
        }

        // Only use on fixture objects/configuration clones, never production assets or result caches.
        public static void Configure(object instance, string name, object value) => Field(instance, name).SetValue(instance, value);
        public static T Read<T>(object instance, string name) => (T)Field(instance, name).GetValue(instance);
    }
}
