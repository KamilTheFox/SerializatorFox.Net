using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SerializatorFox
{
    public class ByteSerialeze : IDisposable
    {
        private readonly Stream stream;

        private readonly ApplicationData appData;

        private BinaryWriter writer;

        public ByteSerialeze(Stream stream)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));

            writer = new BinaryWriter(stream);

            appData = ApplicationData.Get();
        }
        public void Serialize<T>(T obj)
        {
            byte[] typeHash = appData.GetTypeHash(typeof(T));

            writer.Write(typeHash);

            SerializeObject(obj);
        }

        private void SerializeObject<T>(T obj)
        {
            foreach (var field in typeof(T).GetRuntimeFields())
            {
                if (!appData.IsFieldSerializable(field))
                    continue;

                byte[] fieldHash = appData.GetPropertyHash(field.Name);

                writer.Write(fieldHash);

                SerializeValue(field.GetValue(obj), field.FieldType);
            }
        }
        private void SerializeObject(object obj)
        {
            Type actualType = obj.GetType(); // Получаем реальный тип

            // Если тип реализует интерфейс, сохраняем информацию о реальном типе
            if (actualType.GetInterfaces().Any())
            {
                writer.Write(appData.GetTypeHash(actualType));
            }

            foreach (var field in actualType.GetRuntimeFields())
            {
                if (!appData.IsFieldSerializable(field))
                    continue;

                byte[] fieldHash = appData.GetPropertyHash(field.Name);

                writer.Write(fieldHash);

                SerializeValue(field.GetValue(obj), field.FieldType);
            }
        }

        private void SerializeValue(object value, Type type)
        {
            if (value == null)
            {
                writer.Write(false); // isNull flag
                return;
            }

            writer.Write(true); // not null

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
            else if (type == typeof(string)) writer.Write((string)value);
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

            // Объекты
            else if (type.IsClass || type.IsValueType || type.IsInterface)
            {
                byte[] typeHash = appData.GetTypeHash(type);
                writer.Write(typeHash);
                SerializeObject(value);
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

        public void Dispose()
        {
            writer.Dispose();
        }
    }
}
