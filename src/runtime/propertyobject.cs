using System;
using System.Reflection;

namespace Python.Runtime
{
    using MaybeMethodInfo = MaybeMethodBase<MethodInfo>;
    /// <summary>
    /// Implements a Python descriptor type that manages CLR properties.
    /// </summary>
    [Serializable]
    internal class PropertyObject : ExtensionType
    {
        internal MaybeMemberInfo<PropertyInfo> info;
        private MaybeMethodInfo getter;
        private MaybeMethodInfo setter;

        public PropertyObject(PropertyInfo md)
        {
            getter = md.GetGetMethod(true);
            setter = md.GetSetMethod(true);
            info = md;
        }


        /// <summary>
        /// Descriptor __get__ implementation. This method returns the
        /// value of the property on the given object. The returned value
        /// is converted to an appropriately typed Python object.
        /// </summary>
        public static NewReference tp_descr_get(BorrowedReference ds, BorrowedReference ob, BorrowedReference tp)
        {
            var self = (PropertyObject)GetManagedObject(ds)!;
            if (!self.info.Valid)
            {
                return Exceptions.RaiseTypeError(self.info.DeletedMessage);
            }
            var info = self.info.Value;
            MethodInfo getter = self.getter.UnsafeValue;
            object result;


            if (getter == null)
            {
                return Exceptions.RaiseTypeError("property cannot be read");
            }

            if (ob == null || ob == Runtime.PyNone)
            {
                if (!getter.IsStatic)
                {
                    return new NewReference(ds);
                }

                try
                {
                    result = info.GetValue(null, null);
                    return Converter.ToPython(result, info.PropertyType);
                }
                catch (Exception e)
                {
                    return Exceptions.RaiseTypeError(e.Message);
                }
            }

            var co = GetManagedObject(ob) as CLRObject;
            if (co == null)
            {
                return Exceptions.RaiseTypeError("invalid target");
            }

            try
            {
                result = info.GetValue(co.inst, null);
                return Converter.ToPython(result, info.PropertyType);
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    e = e.InnerException;
                }
                Exceptions.SetError(e);
                return default;
            }
        }


        /// <summary>
        /// Descriptor __set__ implementation. This method sets the value of
        /// a property based on the given Python value. The Python value must
        /// be convertible to the type of the property.
        /// </summary>
        public static int tp_descr_set(BorrowedReference ds, BorrowedReference ob, BorrowedReference val)
        {
            var self = (PropertyObject)GetManagedObject(ds)!;
            if (!self.info.Valid)
            {
                Exceptions.RaiseTypeError(self.info.DeletedMessage);
                return -1;
            }
            var info = self.info.Value;

            MethodInfo setter = self.setter.UnsafeValue;

            if (val == null)
            {
                Exceptions.RaiseTypeError("cannot delete property");
                return -1;
            }

            if (setter == null)
            {
                Exceptions.RaiseTypeError("property is read-only");
                return -1;
            }


            if (!Converter.ToManaged(val, info.PropertyType, out var newval, true))
            {
                return -1;
            }

            bool is_static = setter.IsStatic;

            if (ob == null || ob == Runtime.PyNone)
            {
                if (!is_static)
                {
                    Exceptions.RaiseTypeError("instance property must be set on an instance");
                    return -1;
                }
            }

            try
            {
                if (!is_static)
                {
                    var co = GetManagedObject(ob) as CLRObject;
                    if (co == null)
                    {
                        Exceptions.RaiseTypeError("invalid target");
                        return -1;
                    }
                    info.SetValue(co.inst, newval, null);
                }
                else
                {
                    info.SetValue(null, newval, null);
                }
                return 0;
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    e = e.InnerException;
                }
                Exceptions.SetError(e);
                return -1;
            }
        }


        /// <summary>
        /// Descriptor __repr__ implementation.
        /// </summary>
        public static NewReference tp_repr(BorrowedReference ob)
        {
            var self = (PropertyObject)GetManagedObject(ob)!;
            return Runtime.PyString_FromString($"<property '{self.info}'>");
        }
    }
}
