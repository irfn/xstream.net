using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using xstream.Converters;

namespace xstream {
    public class UnmarshallingContext {
        private readonly Dictionary<string, object> alreadyDeserialised = new Dictionary<string, object>();
        private readonly XStreamReader reader;
        private readonly ConverterLookup converterLookup;
        private readonly Aliases aliases;
        private readonly List<Assembly> assemblies;

        internal UnmarshallingContext(XStreamReader reader, ConverterLookup converterLookup, Aliases aliases, List<Assembly> assemblies) {
            this.reader = reader;
            this.converterLookup = converterLookup;
            this.aliases = aliases;
            this.assemblies = assemblies;
        }

        public object ConvertAnother() {
            string nullAttribute = reader.GetAttribute(Attributes.Null);
            if (nullAttribute != null && nullAttribute == "true") return null;
            object result = Find();
            if (result != null) return result;
            Converter converter = converterLookup.GetConverter(reader.GetNodeName());
            if (converter == null) return ConvertOriginal();
            return converter.FromXml(reader, this);
        }

        public object ConvertOriginal() {
            string nodeName = reader.GetNodeName();
            Type type = TypeToUse(nodeName);
            Converter converter = converterLookup.GetConverter(type);
            if (converter != null) return converter.FromXml(reader, this);
            return new Unmarshaller(reader, this, converterLookup).Unmarshal(type);
        }

        private Type TypeToUse(string nodeName) {
            foreach (Alias alias in aliases) {
                Type type;
                if (alias.TryGetType(nodeName, out type))
                    return type;
            }
            string typeName = reader.GetAttribute(Attributes.classType);
            return GetTypeFromOtherAssemblies(typeName);
        }

        internal Type GetTypeFromOtherAssemblies(string typeName) {
            Type type = Type.GetType(typeName);
            int indexOfComma = typeName.IndexOf(',');
            string assemblyName = typeName.Substring(indexOfComma + 2);
            string actualTypeName = typeName.Substring(0, indexOfComma);
            if (type == null) {
                foreach (Assembly assembly in assemblies) {
                    if (assemblyName.Equals(assembly.FullName)) type = assembly.GetType(actualTypeName);
                    if (type != null) break;
                }
                if (type == null) throw new ConversionException("Couldn't deserialise from " + typeName);
            }
            return type;
        }

        public void StackObject(object value) {
            try {
                alreadyDeserialised.Add(reader.CurrentPath, value);
            }
            catch (ArgumentException e) {
                throw new ConversionException("Couldn't add " + reader.CurrentPath, e);
            }
        }

        public object Find() {
            string referencesAttribute = reader.GetAttribute(Attributes.references);
            if (!string.IsNullOrEmpty(referencesAttribute)) return alreadyDeserialised[referencesAttribute];
            return null;
        }
    }
}
