using System.Reflection;

namespace PrimevalTitmouse
{
  /// <summary>
  /// Tiny helper for legacy reflection lookups on private game fields.
  /// </summary>
  public static class ReflectionExtensions
  {
    /// <summary>
    /// Returns a named instance field value (public/private) cast to reference type T.
    /// </summary>
    public static T GetField<T>(this object o, string fieldName) where T : class
    {
      FieldInfo field = o.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      object obj1;
      if (field == (FieldInfo) null)
      {
        obj1 = (object) null;
      }
      else
      {
        object obj2 = o;
        obj1 = field.GetValue(obj2);
      }
      return obj1 as T;
    }
  }
}
