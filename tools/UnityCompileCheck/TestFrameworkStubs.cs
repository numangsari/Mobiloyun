// Compile-check only: minimal stand-ins for com.unity.test-framework attributes, which are not
// published on nuget.org. Unity supplies the real types when the project is opened in the editor.
using System;

namespace UnityEngine.TestTools
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class UnityTestAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class UnitySetUpAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class UnityTearDownAttribute : Attribute { }
}
