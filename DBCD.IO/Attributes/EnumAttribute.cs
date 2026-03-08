using System;

namespace DBCD.IO.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public class EnumAttribute(string enumName, bool isFlags) : Attribute
    {
        public readonly string EnumName = enumName;
        public readonly bool IsFlags = isFlags;
    }
}
