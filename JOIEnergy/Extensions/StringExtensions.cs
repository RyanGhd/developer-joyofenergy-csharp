// ReSharper disable once CheckNamespace

using System.Collections;

namespace System
{
    public static class StringExtensions
    {
        public static bool Equals(this string x,string y)
        {
            return x.Equals(y, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}