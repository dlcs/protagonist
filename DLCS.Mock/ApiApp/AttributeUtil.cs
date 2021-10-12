using System;
using System.Collections.Generic;
using System.Linq;
using Hydra;

namespace DLCS.Mock.ApiApp
{
    public class AttributeUtil
    {
        public static Dictionary<string, object> GetAttributeMap(string assemblyName, Type attributeType)
        {
            if (!(typeof (TypeReferencingAttribute)).IsAssignableFrom(attributeType))
            {
                throw new ArgumentException("attributeType must extend TypeReferencingAttribute", "attributeType");
            }

            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .SingleOrDefault(a => a.GetName().Name == assemblyName);

            if (assembly == null)
            {
                throw new ArgumentException("Cannot find assembly " + assemblyName, "assemblyName");
            }

            var dict = new Dictionary<string, object>();
            foreach (var type in assembly.GetTypes())
            {
                var attributes = type.GetCustomAttributes(attributeType, true);
                if (attributes.Length == 1)
                {
                    var hydraAttr = (TypeReferencingAttribute) attributes[0];
                    var typeAttrIsPointingAt = hydraAttr.ReferencedType;
                    var instanceOfThatType = Activator.CreateInstance(typeAttrIsPointingAt);
                    dict.Add(type.Name, instanceOfThatType);
                }
            }

            return dict;
        }
        
    }
}