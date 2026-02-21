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
    public class ByteSerialeze : IDisposable
    {
        private readonly MemoryStream stream;

        private readonly ApplicationData appData;

        private BinaryWriter writer;

        private bool isCompress;
         
        public ByteSerialeze( bool compress = false)
        {
            stream = new MemoryStream();

            writer = new BinaryWriter(stream);

            isCompress = compress;

            appData = ApplicationData.Get();
        }
        public void Serialize<T>(T obj)
        {
            Type type = typeof(T);
            byte[] actualTypeHash = appData.GetTypeHash(type);
            writer.Write(actualTypeHash);

            if (obj == null)
            {
                writer.Write(ApplicationData.NULL_FLAG);
            }
            byte collisionIndex = appData.GetCollisionIndexType(actualTypeHash, type);
            writer.Write(collisionIndex);
            SerializeObject(obj, type);
        }

        private void SerializeObject(object obj, Type expectedType)
        {
            if (obj == null)
            {
                writer.Write(ApplicationData.NULL_FLAG);
                return;
            }

            Type actualType = obj.GetType();

            // Если тип является интерфейсом или базовым классом
            if (expectedType != actualType)
            {
                byte[] actualTypeHash = appData.GetTypeHash(actualType);
                writer.Write(actualTypeHash);
                byte collisionIndex = appData.GetCollisionIndexType(actualTypeHash, actualType);
                writer.Write(collisionIndex);
            }

            foreach (var field in appData.GetTypeSerializableFields(actualType))
            {
                if (!appData.IsFieldSerializable(field))
                    continue;

                byte[] fieldHash = appData.GetPropertyHash(field.Name);
                writer.Write(fieldHash);

                object value = field.GetValue(obj);

                if (value == null)
                {
                    writer.Write(ApplicationData.NULL_FLAG);
                }
                else
                {
                    byte collisionIndex = appData.GetCollisionIndexField(fieldHash, field.Name);
                    byte infoIndex = (byte)(collisionIndex & ApplicationData.COLLISION_MASK);
                    writer.Write(infoIndex);
                    SerializeValue(value, field.FieldType);
                }
            }
        }

        private void SerializeValue(object value, Type type)
        {

            if (type.IsArray)
            {
                SerializeArray(value as Array, type.GetElementType());
                return;
            }

            // Целочисленные типы
            if (type == typeof(byte)) writer.Write((byte)value);
            else if (type == typeof(sbyte)) writer.Write((sbyte)value);
            else if (type == typeof(short)) writer.Write((short)value);
            else if (type == typeof(ushort)) writer.Write((ushort)value);
            else if (type == typeof(int)) writer.Write((int)value);
            else if (type == typeof(uint)) writer.Write((uint)value);
            else if (type == typeof(long)) writer.Write((long)value);
            else if (type == typeof(ulong)) writer.Write((ulong)value);

            // Числа с плавающей точкой
            else if (type == typeof(float)) writer.Write((float)value);
            else if (type == typeof(double)) writer.Write((double)value);
            else if (type == typeof(decimal)) writer.Write((decimal)value);

            // Другие примитивы
            else if (type == typeof(bool)) writer.Write((bool)value);
            else if (type == typeof(char)) writer.Write((char)value);

            // Строки и даты
            else if (type == typeof(string))
            {
                byte[] bytes = Encoding.UTF8.GetBytes((string)value);
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
            else if (type == typeof(DateTime)) writer.Write(((DateTime)value).Ticks);
            else if (type == typeof(TimeSpan)) writer.Write(((TimeSpan)value).Ticks);

            // Guid
            else if (type == typeof(Guid)) writer.Write(((Guid)value).ToByteArray());

            // Enum
            else if (type.IsEnum) writer.Write(Convert.ToInt32(value));

            // Списки
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elementType = type.GetGenericArguments()[0];
                IList list = (IList)value;

                // Записываем количество элементов
                writer.Write(list.Count);

                // Сериализуем каждый элемент
                foreach (var item in list)
                {
                    SerializeValue(item, elementType);
                }
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var keyType = type.GetGenericArguments()[0];
                var valueType = type.GetGenericArguments()[1];
                var dict = (IDictionary)value;

                writer.Write(dict.Count);
                foreach (DictionaryEntry entry in dict)
                {
                    SerializeValue(entry.Key, keyType);
                    SerializeValue(entry.Value, valueType);
                }
            }

            // Объекты
            else if (type.IsClass || type.IsValueType || type.IsInterface)
            {
                byte[] typeHash = appData.GetTypeHash(type);
                writer.Write(typeHash);
                byte collisionIndex = appData.GetCollisionIndexType(typeHash, type);
                writer.Write(collisionIndex);
                SerializeObject(value, type);
            }

            else
            {
                throw new TypeAccessException($"Unsupported type: {type.FullName}");
            }
        }

        private void SerializeArray(Array array, Type elementType)
        {
            // Записываем длину массива
            writer.Write(array.Length);

            // Сериализуем каждый элемент
            for (int i = 0; i < array.Length; i++)
            {
                SerializeValue(array.GetValue(i), elementType);
            }
        }
        public byte[] GetData()
        {
            byte[] data = stream.ToArray();
            
            if (isCompress)
                data = Compress(data);

            byte[] dataWithFlag = new byte[data.Length + 1];
            dataWithFlag[0] = (byte)(isCompress ? 1 : 0);
            Array.Copy(data, 0, dataWithFlag, 1, data.Length);

            return dataWithFlag;
        }
        public void Dispose()
        {
            stream.Dispose();
            writer.Dispose();
        }
        private static byte[] Compress(byte[] raw)
        {
            using var memory = new MemoryStream();
            using (var gzip = new GZipStream(memory, CompressionMode.Compress))
            {
                gzip.Write(raw, 0, raw.Length);
            }
            return memory.ToArray();
        }
    }
}
