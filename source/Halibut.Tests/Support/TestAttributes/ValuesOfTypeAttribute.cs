using System;
using System.Collections;
using Halibut.Tests.Util;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    public class ValuesOfTypeAttribute : ValuesAttribute
    {
        public ValuesOfTypeAttribute(Type sourceType) : base(CreateValues(sourceType))
        {
        }

        static object[] CreateValues(Type sourceType)
        {
            var enumerable = ((IEnumerable) Activator.CreateInstance(sourceType));
            return enumerable.ToArrayOfObjects();
        }
    }
}