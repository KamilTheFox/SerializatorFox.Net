using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SerializatorFox
{
    public class ByteDeserializer : IDisposable
    {
        private readonly Stream stream;
        private readonly ApplicationData appData;
        private BinaryReader reader;

        public ByteDeserializer(Assembly assembly, byte[] data)
        {
            if (data[0] == 1)
            {
                byte[] result = new byte[data.Length - 1];
                Array.Copy(data, 1, result, 0, data.Length - 1);
                data = Decompress(result);
            }
            stream = new MemoryStream(data);
            reader = new BinaryReader(stream);
            appData = ApplicationData.Get();
        }

        public T Deserialize<T>()
        {
            var (typeHash, collisionIndex) = ReadTypeIdentifier();
            ValidateType<T>(typeHash, collisionIndex);
            return (T)DeserializeObject(typeof(T));
        }

        private Array DeserializeArray(Type elementType)
        {
            // Читаем длину массива
            int length = reader.ReadInt32();

            // Создаём массив нужного типа и размера
            Array array = Array.CreateInstance(elementType, length);

            // Десериализуем каждый элемент
            for (int i = 0; i < length; i++)
            {
                object value = DeserializeValue(elementType);
                array.SetValue(value, i);
            }

            return array;  
        }

        private object DeserializeValue(Type type)
        {
            var collectionResult = DeserializeCollection(type);
            if (collectionResult != null) return collectionResult;

            if (appData.IsPrimitiveType(type))
                return DeserializePrimitive(type);

            if (type.IsEnum)
                return Enum.ToObject(type, reader.ReadInt32());

            if (type.IsClass || type.IsValueType || type.IsInterface)
                return DeserializeComplexType(type);

            throw new TypeAccessException($"Unsupported type: {type.FullName}");
        }
        private object DeserializeCollection(Type type)
        {
            if (type.IsArray) return DeserializeArray(type.GetElementType());
            if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() == typeof(List<>))
                    return DeserializeList(type);
                if (type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                    return DeserializeDictionary(type);
            }
            return null;
        }

        private object DeserializeDictionary(Type type)
        {
            var keyType = type.GetGenericArguments()[0];
            var valueType = type.GetGenericArguments()[1];
            int count = reader.ReadInt32();

            IDictionary dict = (IDictionary)Activator.CreateInstance(type);
            for (int i = 0; i < count; i++)
            {
                var key = DeserializeValue(keyType);
                var value = DeserializeValue(valueType);
                dict.Add(key, value);
            }
            return dict;
        }

        private object DeserializeList(Type type)
        {
            Type elementType = type.GetGenericArguments()[0];
            int count = reader.ReadInt32();

            // Создаём список нужного типа
            IList list = (IList)Activator.CreateInstance(type);

            // Десериализуем каждый элемент
            for (int i = 0; i < count; i++)
            {
                object item = DeserializeValue(elementType);
                list.Add(item);
            }

            return list;
        }

        private string DeserializeString()
        {
            int length = reader.ReadInt32();
            byte[] invertedBytes = reader.ReadBytes(length);
            byte[] restoredBytes = invertedBytes.Select(b => (byte)~b).ToArray();
            return Encoding.UTF8.GetString(restoredBytes);
        }
        
        private object DeserializePrimitive(Type type)
        {
            if (type == typeof(byte)) return reader.ReadByte();
            if (type == typeof(sbyte)) return reader.ReadSByte();
            if (type == typeof(short)) return reader.ReadInt16();
            if (type == typeof(ushort)) return reader.ReadUInt16();
            if (type == typeof(int)) return reader.ReadInt32();
            if (type == typeof(uint)) return reader.ReadUInt32();
            if (type == typeof(long)) return reader.ReadInt64();
            if (type == typeof(ulong)) return reader.ReadUInt64();
            if (type == typeof(float)) return reader.ReadSingle();
            if (type == typeof(double)) return reader.ReadDouble();
            if (type == typeof(decimal)) return reader.ReadDecimal();
            if (type == typeof(bool)) return reader.ReadBoolean();
            if (type == typeof(char)) return reader.ReadChar();
            if (type == typeof(string)) return DeserializeString();
            if (type == typeof(int)) return reader.ReadInt32();
            if (type == typeof(DateTime)) return new DateTime(reader.ReadInt64());
            if (type == typeof(TimeSpan)) return new TimeSpan(reader.ReadInt64());
            if (type == typeof(Guid)) return new Guid(reader.ReadBytes(16));
            return null;
        }
        private (byte[] hash, byte collisionIndex) ReadTypeIdentifier()
        {
            return (reader.ReadBytes(4), reader.ReadByte());
        }

        private void ValidateType<T>(byte[] typeHash, byte collisionIndex)
        {
            Type actualType = appData.GetTypeByHash(typeHash, collisionIndex);
            if (actualType != typeof(T))
                throw new TypeAccessException($"Type mismatch. Expected {typeof(T)}, got {actualType}");
        }

        private object DeserializeObject(Type type)
        {
            if (type.IsInterface)
            {
                return DeserializeInterface(type);
            }

            object instance = Activator.CreateInstance(type);
            DeserializeFields(type, instance);
            return instance;
        }

        private object DeserializeInterface(Type type)
        {
            var (hash, collisionIndex) = ReadTypeIdentifier();

            if (IsNull(collisionIndex))
                return null;

            Type actualType = GetTypeFromHash(hash, collisionIndex);
            return DeserializeObject(actualType);
        }

        private bool IsNull(byte value) => (value & ApplicationData.NULL_FLAG) != 0;

        private Type GetTypeFromHash(byte[] hash, byte collisionIndex)
        {
            var type = appData.GetTypeByHash(hash, (byte)(collisionIndex & ApplicationData.COLLISION_MASK));
            if (type == null)
                throw new TypeAccessException($"Cannot find type By Hash {hash}");
            return type;
        }

        private void DeserializeFields(Type type, object instance)
        {
            foreach (var field in type.GetRuntimeFields().Where(f => appData.IsFieldSerializable(f)))
            {
                DeserializeField(field, instance);
            }
        }

        private void DeserializeField(FieldInfo field, object instance)
        {
            var (fieldHash, contextByte) = ReadFieldIdentifier();
            ValidateFieldName(field, fieldHash, contextByte);

            object value = IsNull(contextByte) ? null : DeserializeValue(field.FieldType);
            field.SetValue(instance, value);
        }

        private (byte[] hash, byte contextByte) ReadFieldIdentifier()
        {
            return (reader.ReadBytes(4), reader.ReadByte());
        }

        private void ValidateFieldName(FieldInfo field, byte[] fieldHash, byte contextByte)
        {
            byte indexCollision = (byte)(contextByte & ApplicationData.COLLISION_MASK);
            string fieldName = appData.GetFieldNameByHash(fieldHash, indexCollision);

            if (field.Name != fieldName)
                throw new TypeAccessException($"Field mismatch. Expected {field.Name}, got {fieldName}");
        }
        private object DeserializeComplexType(Type type)
        {
            var (typeHash, collisionIndex) = ReadTypeIdentifier();
            Type actualType = appData.GetTypeByHash(typeHash, collisionIndex);
            return DeserializeObject(actualType);
        }
        public void Dispose()
        {
            stream.Dispose();
            reader?.Dispose();
        }
        private static byte[] Decompress(byte[] compressed)
        {
            using var input = new MemoryStream(compressed);
            using var memory = new MemoryStream();
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            {
                gzip.CopyTo(memory);
            }
            return memory.ToArray();
        }
    }
}
