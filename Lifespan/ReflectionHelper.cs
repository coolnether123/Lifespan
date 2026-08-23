using System;
using System.Reflection;
using ModAPI.Reflection;

namespace Lifespan
{
    /// <summary>
    /// Preserves the legacy reflection API while delegating calls to ModAPI.Reflection.Safe.
    /// </summary>
    public static class ReflectionHelper
    {
        public static T GetField<T>(object obj, string name)
        {
            T val;
            if (Safe.TryGetField<T>(obj, name, out val))
                return val;
            return default(T);
        }

        public static void SetField(object obj, string name, object value)
        {
            Safe.SetField(obj, name, value);
        }

        public static object InvokeMethod(object obj, string name, params object[] args)
        {
            object result;
            if (Safe.TryCall(obj, name, out result, args))
                return result;
            return null;
        }
    }
}
